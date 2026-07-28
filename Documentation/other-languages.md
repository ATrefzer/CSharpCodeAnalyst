## Other languages

### C++, Python and Java

To import a directory with C++, Python or Java files, you need **[doxygen](https://www.doxygen.nl/index.html)** in your search path. We use doxygen to extract the types and dependencies. Python packages and modules as well as Java packages appear as namespaces. Directories that hold no source of your own are excluded automatically: virtual environments and caches for Python (`venv`, `.venv`, `__pycache__`, `site-packages`, `.tox`), build output for Java (`build`, `target`, `out`, `bin`, `.gradle`, `.idea`) — which also keeps generated sources such as `target/generated-sources` out of the graph.

Keep in mind (see Limitations) that C# Code Analyst ignores the folder structure and organizes the code by its namespaces.

Use the "Import C++/Python/Java project (doxygen)" menu.

![](Images/import-menu.png)

The wizard asks you for the root directory of your source code, the language, and a project name. The project name results in a single root node containing all code elements.

![](Images/import-wizard.png)

> Doxygen's Python parser is noticeably more heuristic than the C++ mode. Hierarchy, classes, and inheritance are handled reliably; however, the call references (REFERENCES_RELATION) are more incomplete with dynamic typing—Doxygen often cannot resolve self.foo() on a duck-typed object. As a result, the graph is more likely to have too few edges than incorrect ones.

#### Java specifics

A Java package becomes a namespace, so `com.example.core` results in the nested namespaces `com` → `example` → `core`. Types, fields, methods, `extends` and `implements` are reliable. A Java `enum` is a type of its own with its constants and methods below it — unlike C#, where enum members are not code elements.

The same caveat as for Python applies to the call references: doxygen resolves a call by name, without a type checker. Calls through an interface or a type parameter (`item.area()` where `item` is a `T extends Shape`) are frequently missing, so expect too few edges rather than wrong ones. Annotations produce no dependency at all.

Only your own source is in the graph. References into the JDK or into libraries are dropped, since doxygen never saw those types.

### Dart and Flutter

Dart projects are not imported via doxygen — doxygen does not support the language at all. Instead
they are analyzed with Dart's own tooling (the `analyzer` package), which gives fully resolved
types, calls and constructor invocations, including into the Flutter SDK.

You need a **Dart or Flutter SDK** in your search path, and the project must already be resolved
with `flutter pub get` (or `dart pub get`) — otherwise `package:` imports do not resolve and the
graph stays almost empty. The import dialog tells you when that is the case.

Use the "Import Dart/Flutter project" menu and select the directory containing `pubspec.yaml`.
There is nothing else to configure: assembly names come from the package names, and the hierarchy
below them follows the directory layout inside the package, with the library file as the last
level. So `package:app/features/auth/login_page.dart` becomes `app` → `features` → `auth` →
`login_page`.

The Dart/Flutter SDK and pub packages appear as separate assemblies marked as external, so you can
see that a widget derives from `StatelessWidget` without pulling all of Flutter into your view.

> The first import prepares the bundled extractor tool (a one-time `dart pub get`), which needs an
> internet connection and takes a few seconds. Note that calls inside closures are recorded as
> `Uses` rather than `Calls` — a builder callback is not executed where it is written. This matches
> how the C# parser treats lambda bodies.

### Java: source (doxygen) or bytecode (jdeps)

There are two ways to get a Java project into the tool, and they answer different questions.

**From the source code, via doxygen** (see above): the project does not have to compile, and the graph goes down to methods and fields, including calls between them. This is the way to go if you want to explore the code.

**From the compiled classes, via jdeps**: needs a built project and gives you dependencies between *types* only. What it does give you is complete for that level, because it comes from the bytecode instead of from a name-based source scan — including everything that only exists after the build (Lombok, MapStruct and other annotation processors) and everything written in another JVM language, which doxygen cannot parse at all.

Use the jdeps tool to generate a dependency file. You can import this file directly using the Import menu in the Ribbon.

```
jdeps.exe -verbose:class <bin-folder1> <bin-folder2>...  >jdeps.txt
```

> **What a jdeps import cannot show you.** Its output is a flat list of `from.Type -> to.Type` lines. It says *that* one type depends on another, never *how*: a base class looks exactly like a parameter type. An imported graph therefore contains **no inheritance** — the dependency on the base class is there, but as an ordinary `Uses` edge — and interfaces and enums are indistinguishable from classes. There are no methods, fields or calls either. Everything that builds on relationship types (inheritance views, the event and architectural analyses) stays empty; what works well is the type-level picture: DSM, cycles, partitions and the dependency metrics.
>
> If you need the structure inside your types, import the sources via doxygen. The two are complementary, not redundant: doxygen goes deep but resolves by name and misses edges, jdeps stays shallow but knows what the compiler actually emitted.

---