import 'package:analyzer/dart/ast/ast.dart';
import 'package:analyzer/dart/ast/visitor.dart';
import 'package:analyzer/dart/element/element.dart';

import 'dart_extractor.dart';
import 'model.dart';

/// Walks the bodies of a resolved unit and records what each declaration refers
/// to. Which declaration is "current" follows the innermost enclosing member;
/// declarations the graph does not model (local functions) keep the enclosing
/// member as the source, so their references are not lost.
///
/// Calls inside a closure become Uses rather than Calls: a closure body is not
/// executed where it is written, and Flutter code is full of builder callbacks.
/// This mirrors how the C# parser treats lambda bodies (see ISyntaxNodeHandler).
class ReferenceVisitor extends RecursiveAstVisitor<void> {
  ReferenceVisitor(this._extractor);

  final DartExtractor _extractor;

  GraphElement? _current;
  int _closureDepth = 0;

  String get _callType => _closureDepth > 0 ? 'Uses' : 'Calls';

  // ------------------------------------------------------------- declarations

  void _withDeclaration(Fragment? fragment, void Function() visit) {
    final element = fragment?.element;
    final previous = _current;
    if (element != null) {
      _current = _extractor.ensureElement(element) ?? previous;
    }
    try {
      visit();
    } finally {
      _current = previous;
    }
  }

  @override
  void visitClassDeclaration(ClassDeclaration node) =>
      _withDeclaration(node.declaredFragment, () => super.visitClassDeclaration(node));

  @override
  void visitMixinDeclaration(MixinDeclaration node) =>
      _withDeclaration(node.declaredFragment, () => super.visitMixinDeclaration(node));

  @override
  void visitEnumDeclaration(EnumDeclaration node) =>
      _withDeclaration(node.declaredFragment, () => super.visitEnumDeclaration(node));

  @override
  void visitExtensionDeclaration(ExtensionDeclaration node) =>
      _withDeclaration(node.declaredFragment, () => super.visitExtensionDeclaration(node));

  @override
  void visitExtensionTypeDeclaration(ExtensionTypeDeclaration node) =>
      _withDeclaration(node.declaredFragment, () => super.visitExtensionTypeDeclaration(node));

  @override
  void visitMethodDeclaration(MethodDeclaration node) =>
      _withDeclaration(node.declaredFragment, () => super.visitMethodDeclaration(node));

  @override
  void visitConstructorDeclaration(ConstructorDeclaration node) =>
      _withDeclaration(node.declaredFragment, () => super.visitConstructorDeclaration(node));

  @override
  void visitFunctionDeclaration(FunctionDeclaration node) =>
      _withDeclaration(node.declaredFragment, () => super.visitFunctionDeclaration(node));

  @override
  void visitVariableDeclaration(VariableDeclaration node) =>
      _withDeclaration(node.declaredFragment, () => super.visitVariableDeclaration(node));

  // The type annotation of "int _counter = 0" sits on the VariableDeclarationList,
  // a sibling of the VariableDeclaration - without these two overrides it would be
  // attributed to the enclosing class instead of to the field.
  @override
  void visitFieldDeclaration(FieldDeclaration node) =>
      _withDeclaration(node.fields.variables.first.declaredFragment, () => super.visitFieldDeclaration(node));

  @override
  void visitTopLevelVariableDeclaration(TopLevelVariableDeclaration node) => _withDeclaration(
      node.variables.variables.first.declaredFragment, () => super.visitTopLevelVariableDeclaration(node));

  @override
  void visitFunctionExpression(FunctionExpression node) {
    // The body of a named function is not a closure - only genuine anonymous
    // functions raise the depth.
    final isClosure = node.parent is! FunctionDeclaration;
    if (isClosure) {
      _closureDepth++;
    }
    try {
      super.visitFunctionExpression(node);
    } finally {
      if (isClosure) {
        _closureDepth--;
      }
    }
  }

  // -------------------------------------------------------------- invocations

  @override
  void visitMethodInvocation(MethodInvocation node) {
    _add(node.methodName.element, _callType);
    super.visitMethodInvocation(node);
  }

  @override
  void visitFunctionExpressionInvocation(FunctionExpressionInvocation node) {
    _add(node.element, _callType);
    super.visitFunctionExpressionInvocation(node);
  }

  @override
  void visitInstanceCreationExpression(InstanceCreationExpression node) {
    // The edge points at the constructor, which lives below the created type -
    // a type-level rollup therefore still yields "creator -> created type".
    _add(node.constructorName.element, 'Creates');
    super.visitInstanceCreationExpression(node);
  }

  @override
  void visitRedirectingConstructorInvocation(RedirectingConstructorInvocation node) {
    _add(node.element, _callType);
    super.visitRedirectingConstructorInvocation(node);
  }

  @override
  void visitSuperConstructorInvocation(SuperConstructorInvocation node) {
    _add(node.element, _callType);
    super.visitSuperConstructorInvocation(node);
  }

  // ------------------------------------------------------------------ members

  @override
  void visitPropertyAccess(PropertyAccess node) {
    _addMemberReference(node.propertyName.element);
    super.visitPropertyAccess(node);
  }

  @override
  void visitPrefixedIdentifier(PrefixedIdentifier node) {
    _addMemberReference(node.identifier.element);
    super.visitPrefixedIdentifier(node);
  }

  @override
  void visitSimpleIdentifier(SimpleIdentifier node) {
    // Names that are only part of a construct handled above, and the declared
    // names themselves, must not produce a second (wrongly typed) edge.
    final parent = node.parent;
    final alreadyHandled = node.inDeclarationContext() ||
        (parent is MethodInvocation && parent.methodName == node) ||
        (parent is PropertyAccess && parent.propertyName == node) ||
        (parent is PrefixedIdentifier) ||
        parent is NamedType ||
        parent is ConstructorName ||
        parent is Label ||
        parent is Annotation ||
        parent is ImportDirective ||
        parent is ExportDirective;
    if (!alreadyHandled) {
      _addMemberReference(node.element);
    }
    super.visitSimpleIdentifier(node);
  }

  // -------------------------------------------------------------------- types

  @override
  void visitNamedType(NamedType node) {
    // extends/implements/with/on are already modelled from the element model as
    // Inherits/Implements/Uses - a second Uses edge would only add noise.
    final parent = node.parent;
    final isSupertypeClause =
        parent is ExtendsClause || parent is ImplementsClause || parent is WithClause || parent is MixinOnClause;
    if (!isSupertypeClause) {
      _add(node.element, 'Uses');
    }
    super.visitNamedType(node);
  }

  @override
  void visitAnnotation(Annotation node) {
    final element = node.element;
    // "@Deprecated('x')" resolves to a constructor, "@override" to a getter -
    // the interesting node in both cases is the annotation type.
    final target = element is ConstructorElement ? element.enclosingElement : element;
    _add(target, 'UsesAttribute');
    super.visitAnnotation(node);
  }

  // ---------------------------------------------------------------- operators

  @override
  void visitBinaryExpression(BinaryExpression node) {
    _addUserDefinedOperator(node.element);
    super.visitBinaryExpression(node);
  }

  @override
  void visitPrefixExpression(PrefixExpression node) {
    _addUserDefinedOperator(node.element);
    super.visitPrefixExpression(node);
  }

  @override
  void visitPostfixExpression(PostfixExpression node) {
    _addUserDefinedOperator(node.element);
    super.visitPostfixExpression(node);
  }

  @override
  void visitIndexExpression(IndexExpression node) {
    _addUserDefinedOperator(node.element);
    super.visitIndexExpression(node);
  }

  // ------------------------------------------------------------------ helpers

  /// Getters and setters are executable, so accessing one is a call; reading a
  /// field or a variable is not.
  void _addMemberReference(Element? element) {
    if (element == null) {
      return;
    }
    if (element is PropertyAccessorElement && !element.isSynthetic) {
      _add(element, _callType);
    } else if (element is PropertyInducingElement || element is ExecutableElement) {
      // Includes tear-offs ("onPressed: _increment"), which reference a method
      // without calling it.
      _add(element, 'Uses');
    }
  }

  void _addUserDefinedOperator(Element? element) {
    // Built-in operators bind to no element and are not interesting.
    if (element != null) {
      _add(element, _callType);
    }
  }

  void _add(Element? target, String type) {
    final source = _current;
    if (source == null || target == null) {
      return;
    }
    _extractor.addReference(source, target, type);
  }
}
