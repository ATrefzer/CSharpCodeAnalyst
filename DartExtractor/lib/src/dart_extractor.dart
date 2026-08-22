import 'dart:io';

import 'package:analyzer/dart/analysis/analysis_context.dart';
import 'package:analyzer/dart/analysis/analysis_context_collection.dart';
import 'package:analyzer/dart/analysis/results.dart';
import 'package:analyzer/dart/element/element.dart';
import 'package:analyzer/dart/element/type.dart';
import 'package:path/path.dart' as p;
import 'package:yaml/yaml.dart';

import 'graph_builder.dart';
import 'metrics_collector.dart';
import 'model.dart';
import 'reference_visitor.dart';

/// Turns a Dart/Flutter project into the element/relationship graph consumed by
/// CSharpCodeAnalyst.
///
/// Modelling decisions (Dart has no namespaces, so the hierarchy is derived):
///  - Assembly  = package. `package:app/...` -> "app", `dart:core` -> "dart:core".
///    Files outside `lib/` (test/, bin/, tool/) have a `file:` URI; their package
///    name comes from the pubspec of the enclosing analysis context root.
///  - Namespace = the path inside the package, the library file itself being the
///    last segment: `package:app/features/auth/login_page.dart` becomes
///    "features" -> "auth" -> "login_page". This mirrors the Python import, where
///    doxygen maps `pkg/mod.py` to the namespace `pkg::mod`. A library with no
///    path segments lands in the synthetic "global" namespace, following the
///    convention of the C# parser.
///  - `part` files have no library of their own, so their declarations are folded
///    into the namespace of the library that owns them - which is what Dart's
///    privacy model implies anyway.
///  - Everything outside the analyzed directory (SDK, pub packages) is marked
///    external and is created on demand, with its full parent chain, exactly the
///    way ExternalCodeElementCache does it on the C# side.
class DartExtractor {
  DartExtractor(this.rootPath, {this.log});

  final String rootPath;
  final void Function(String message)? log;

  final GraphBuilder builder = GraphBuilder();

  /// Package root -> package name, longest path first so the innermost package
  /// of a monorepo wins the lookup.
  final List<({String root, String name})> _packageRoots = [];

  /// URIs of libraries that live inside [rootPath]. Everything else is external.
  final Set<Uri> _projectLibraryUris = {};

  /// Guards against re-entering _ensureElement for cyclic enclosing chains.
  final Set<String> _inProgress = {};

  int skippedUnresolved = 0;

  Future<void> extract() async {
    final collection = AnalysisContextCollection(includedPaths: [rootPath]);

    for (final context in collection.contexts) {
      _readPackageName(context);
    }
    _packageRoots.sort((a, b) => b.root.length.compareTo(a.root.length));

    final units = <ResolvedUnitResult>[];
    for (final context in collection.contexts) {
      final files = context.contextRoot.analyzedFiles().where((f) => f.endsWith('.dart')).toList()..sort();
      for (final file in files) {
        final result = await context.currentSession.getResolvedUnit(file);
        if (result is! ResolvedUnitResult) {
          continue;
        }
        units.add(result);
        _projectLibraryUris.add(result.libraryElement.uri);
      }
      log?.call('Resolved ${files.length} files in ${context.contextRoot.root.path}');
    }

    // Pass 1: declare everything the project itself defines, so that a member is
    // never created as a by-product of a reference (which would lose its
    // location and, for fields, its element type).
    final declaredLibraries = <Uri>{};
    for (final unit in units) {
      final library = unit.libraryElement;
      if (declaredLibraries.add(library.uri)) {
        _declareLibrary(library);
      }
    }
    log?.call('Declared ${builder.elementCount} elements');

    // Pass 2: walk the bodies for calls, constructor invocations, type uses and member metrics.
    // Line info is per unit, so the collector is built here rather than shared.
    for (final unit in units) {
      unit.unit.accept(ReferenceVisitor(this, MetricsCollector(unit.lineInfo)));
    }
    log?.call('Collected ${builder.relationshipCount} relationships and ${builder.metricCount} member metrics');
  }

  void _readPackageName(AnalysisContext context) {
    final root = context.contextRoot.root.path;
    final pubspec = File(p.join(root, 'pubspec.yaml'));
    var name = p.basename(root);
    if (pubspec.existsSync()) {
      try {
        final parsed = loadYaml(pubspec.readAsStringSync());
        if (parsed is YamlMap && parsed['name'] is String) {
          name = parsed['name'] as String;
        }
      } on Exception {
        // Keep the directory name - a broken pubspec must not fail the import.
      }
    }
    _packageRoots.add((root: root, name: name));
  }

  // ---------------------------------------------------------------- declaring

  void _declareLibrary(LibraryElement library) {
    for (final element in <Element>[
      ...library.classes,
      ...library.mixins,
      ...library.enums,
      ...library.extensions,
      ...library.extensionTypes,
      ...library.typeAliases,
      ...library.topLevelFunctions,
      ...library.topLevelVariables,
      ...library.getters,
      ...library.setters,
    ]) {
      final declared = ensureElement(element);
      if (declared == null) {
        continue;
      }
      if (element is InterfaceElement) {
        _declareMembers(element, declared);
        _addSupertypes(element, declared);
      } else if (element is ExtensionElement) {
        _declareMembers(element, declared);
        _addTypeUse(declared, element.extendedType);
      } else if (element is TypeAliasElement) {
        _addTypeUse(declared, element.aliasedType);
      }
    }
  }

  void _declareMembers(InstanceElement owner, GraphElement ownerElement) {
    for (final member in <Element>[
      ...owner.fields,
      ...owner.getters,
      ...owner.setters,
      ...owner.methods,
      if (owner is InterfaceElement) ...owner.constructors,
    ]) {
      final memberElement = ensureElement(member);
      if (memberElement == null) {
        continue;
      }
      _addSignatureTypes(member, memberElement);
      // Constructors are executable but never override anything.
      if (owner is InterfaceElement && member is ExecutableElement && member is! ConstructorElement) {
        _addOverride(owner, member, memberElement);
      }
    }
  }

  void _addSupertypes(InterfaceElement element, GraphElement source) {
    if (element is! MixinElement) {
      final superType = element.supertype;
      // Every class implicitly extends Object - that edge carries no information.
      if (superType != null && !superType.isDartCoreObject) {
        _addTypeRelationship(source, superType, 'Inherits');
      }
    }

    // A mixin application is part of the superclass chain in Dart, so "with"
    // is modelled as Inherits rather than as a relationship of its own.
    for (final mixin in element.mixins) {
      _addTypeRelationship(source, mixin, 'Inherits');
    }
    for (final interface in element.interfaces) {
      _addTypeRelationship(source, interface, 'Implements');
    }
    if (element is MixinElement) {
      // "mixin M on Base" is a constraint on the user of the mixin, not an
      // inheritance - Uses is the honest edge here.
      for (final constraint in element.superclassConstraints) {
        _addTypeRelationship(source, constraint, 'Uses');
      }
    }
  }

  void _addSignatureTypes(Element member, GraphElement source) {
    if (member is ExecutableElement) {
      // A constructor "returns" its own class - that would be an edge from a
      // child to its own parent and says nothing.
      if (member is! ConstructorElement && member.returnType is! VoidType) {
        _addTypeUse(source, member.returnType);
      }
      for (final parameter in member.formalParameters) {
        _addTypeUse(source, parameter.type);
      }
    } else if (member is PropertyInducingElement) {
      _addTypeUse(source, member.type);
    }
  }

  /// Overriding is not limited to methods: implementing an abstract getter of an interface is an
  /// override just as much, and getters carry the bulk of a Dart interface.
  void _addOverride(InterfaceElement owner, ExecutableElement member, GraphElement source) {
    final name = member.name;
    if (name == null) {
      return;
    }
    for (final supertype in owner.allSupertypes) {
      final overridden = switch (member) {
        GetterElement() => supertype.getGetter(name),
        SetterElement() => supertype.getSetter(name),
        MethodElement() => supertype.getMethod(name),
        _ => null,
      };
      if (overridden != null) {
        addReference(source, overridden, 'Overrides');
        return;
      }
    }
  }

  // ------------------------------------------------------------- relationships

  /// Adds an edge to a resolved target, creating the target (and its parents)
  /// on demand. Unresolvable targets are counted, not reported.
  void addReference(GraphElement source, Element? target, String type) {
    if (target == null) {
      skippedUnresolved++;
      return;
    }
    final targetElement = ensureElement(target);
    if (targetElement == null) {
      skippedUnresolved++;
      return;
    }
    builder.addRelationship(source.id, targetElement.id, type);
  }

  void _addTypeRelationship(GraphElement source, DartType type, String relationship) {
    if (type is InterfaceType) {
      addReference(source, type.element, relationship);
    }
  }

  /// Records a Uses edge to the named type and to every type argument, so that
  /// `Future<List<Order>>` reaches Order and not only Future.
  void _addTypeUse(GraphElement source, DartType type) {
    if (type is InterfaceType) {
      addReference(source, type.element, 'Uses');
      for (final argument in type.typeArguments) {
        _addTypeUse(source, argument);
      }
    } else if (type is FunctionType) {
      if (type.returnType is! VoidType) {
        _addTypeUse(source, type.returnType);
      }
      for (final parameter in type.formalParameters) {
        _addTypeUse(source, parameter.type);
      }
    }
    // TypeParameterType (T), dynamic, void, record types: nothing to point at.
  }

  // ----------------------------------------------------------------- elements

  /// Returns the graph element for [element], creating it and its whole parent
  /// chain if necessary. Returns null for anything that has no place in the
  /// graph (locals, parameters, type parameters, unnamed extensions).
  GraphElement? ensureElement(Element element) {
    final canonical = _canonicalize(element);
    final library = canonical.library;
    if (library == null) {
      return null;
    }
    if (canonical is LibraryElement) {
      return _ensureLibraryNamespace(canonical);
    }

    final type = _mapElementType(canonical);
    if (type == null) {
      return null;
    }

    final name = _nameOf(canonical);
    if (name == null) {
      return null;
    }

    final id = _idFor(canonical, library);
    final existing = builder[id];
    if (existing != null) {
      return existing;
    }
    if (!_inProgress.add(id)) {
      return null;
    }
    try {
      final enclosing = canonical.enclosingElement;
      if (enclosing == null) {
        return null;
      }
      final parent = ensureElement(enclosing);
      if (parent == null) {
        return null;
      }

      return builder.add(GraphElement(
        id: id,
        type: type,
        name: name,
        parentId: parent.id,
        isExternal: !_projectLibraryUris.contains(library.uri),
        role: _memberRoleOf(canonical, type),
        location: _locationOf(canonical),
      ));
    } finally {
      _inProgress.remove(id);
    }
  }

  /// Picks the element that owns a declaration when Dart models it twice.
  ///
  /// A field and a hand-written accessor each induce the other as a synthetic counterpart, and the
  /// two carry the same name - so without a rule the outcome would depend on iteration order:
  ///  - a field access resolves to the compiler's synthetic getter; redirect it to the field, so
  ///    `obj.count` points at the declared field instead of an invisible accessor;
  ///  - a hand-written getter/setter induces a synthetic variable of the same name; redirect that
  ///    to the accessor, otherwise every property would end up in the graph as a field.
  ///
  /// A variable that is synthetic *and* has only synthetic accessors is genuinely generated
  /// (an enum's `values`) and stays a field - there is no declaration to prefer over it.
  Element _canonicalize(Element element) {
    if (element is PropertyAccessorElement && element.isSynthetic) {
      return element.variable;
    }

    if (element is PropertyInducingElement && element.isSynthetic) {
      // Getter and setter of a pair share one id, so either is a fine representative.
      final getter = element.getter;
      if (getter != null && !getter.isSynthetic) {
        return getter;
      }
      final setter = element.setter;
      if (setter != null && !setter.isSynthetic) {
        return setter;
      }
    }

    return element;
  }

  /// `package:app/features/auth/login_page.dart#LoginPage.build`.
  ///
  /// Dart has no overloading, so a name is unique inside its container; named
  /// constructors carry their own name and the unnamed one is called "new".
  String _idFor(Element element, LibraryElement library) {
    final parts = <String>[];
    Element? current = element;
    while (current != null && current is! LibraryElement) {
      parts.add(_nameOf(current) ?? '?');
      current = current.enclosingElement;
    }
    return '${library.uri}#${parts.reversed.join('.')}';
  }

  String? _nameOf(Element element) {
    if (element is ConstructorElement) {
      return element.name ?? 'new';
    }
    final name = element.name;
    if (name != null && name.isNotEmpty) {
      return name;
    }
    // An unnamed extension ("extension on String") cannot be referenced by name
    // and would produce an anonymous node - drop it and keep its members out too.
    return null;
  }

  /// What the member is there for, as the literal name of a MemberRole. Dart states
  /// it because no name test on the C# side could: a named constructor
  /// `Foo.fromJson` is a method called `fromJson`, indistinguishable from an
  /// ordinary method of that name, and the unnamed one is called `new`.
  ///
  /// Dart has neither a static constructor nor a finalizer, so only Constructor
  /// and Normal ever occur. Anything that is not a method carries no role at all.
  String? _memberRoleOf(Element element, String type) {
    if (type != 'Method') {
      return null;
    }
    return element is ConstructorElement ? 'Constructor' : 'Normal';
  }

  String? _mapElementType(Element element) {
    // Order matters: ExtensionTypeElement and EnumElement are InterfaceElements.
    if (element is EnumElement) return 'Enum';
    if (element is ExtensionTypeElement) return 'Struct';
    if (element is MixinElement) return 'Class';
    if (element is ExtensionElement) return 'Class';
    if (element is ClassElement) return element.isInterface ? 'Interface' : 'Class';
    if (element is TypeAliasElement) return 'Delegate';
    // A class without a written constructor still has one in the language, and `Foo()` binds to it -
    // but it is not in the source. The C# parser, which walks declaration syntax, does not model the
    // implicit constructor either, so dropping it keeps both graphs comparable.
    if (element is ConstructorElement) return element.isSynthetic ? null : 'Method';
    if (element is MethodElement) return 'Method';
    if (element is TopLevelFunctionElement) return 'Method';
    if (element is PropertyAccessorElement) return 'Property';
    if (element is FieldElement) return 'Field';
    if (element is TopLevelVariableElement) return 'Field';
    // Locals, parameters, type parameters, prefixes: not part of the graph.
    return null;
  }

  SourceLocation? _locationOf(Element element) {
    final fragment = element.firstFragment;
    final offset = fragment.nameOffset;
    final libraryFragment = fragment.libraryFragment;
    if (offset == null || libraryFragment == null) {
      return null;
    }
    final path = libraryFragment.source.fullName;
    final location = libraryFragment.lineInfo.getLocation(offset);
    return SourceLocation(path, location.lineNumber, location.columnNumber);
  }

  // --------------------------------------------------------- assembly & namespaces

  GraphElement _ensureLibraryNamespace(LibraryElement library) {
    final isExternal = !_projectLibraryUris.contains(library.uri);
    final (packageName, segments) = _splitLibraryUri(library);

    var parent = builder.add(GraphElement(
      id: 'pkg:$packageName',
      type: 'Assembly',
      name: packageName,
      parentId: null,
      isExternal: isExternal,
    ));

    // Anything directly at package root goes into the synthetic "global"
    // namespace, so no element ever sits directly below an assembly.
    final path = segments.isEmpty ? const ['global'] : segments;
    var id = 'pkg:$packageName';
    for (final segment in path) {
      id = '$id/$segment';
      parent = builder.add(GraphElement(
        id: 'ns:$id',
        type: 'Namespace',
        name: segment,
        parentId: parent.id,
        isExternal: isExternal,
      ));
    }
    return parent;
  }

  (String, List<String>) _splitLibraryUri(LibraryElement library) {
    final uri = library.uri;

    if (uri.scheme == 'package') {
      final segments = uri.pathSegments;
      if (segments.isEmpty) {
        return ('unknown', const []);
      }
      return (segments.first, _stripDartExtension(segments.skip(1).toList()));
    }

    if (uri.scheme == 'dart') {
      // The SDK is one assembly per library ("dart:core", "dart:ui").
      return ('dart:${uri.path}', const []);
    }

    if (uri.scheme == 'file') {
      // test/, bin/, tool/ and example/ are not package-addressable.
      final path = uri.toFilePath();
      for (final root in _packageRoots) {
        if (p.isWithin(root.root, path)) {
          final relative = p.split(p.relative(path, from: root.root));
          return (root.name, _stripDartExtension(relative));
        }
      }
    }

    return ('unknown', const []);
  }

  List<String> _stripDartExtension(List<String> segments) {
    if (segments.isEmpty) {
      return segments;
    }
    final last = segments.last;
    return [...segments.take(segments.length - 1), last.endsWith('.dart') ? last.substring(0, last.length - 5) : last];
  }
}
