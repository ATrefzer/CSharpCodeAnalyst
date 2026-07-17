# Supported projects and solutions

C# Code Analyst loads your solution through Roslyn's **`MSBuildWorkspace`**. Understanding how
that works explains which projects load cleanly, which do not, and what the messages in the
**Errors/Warnings** dialog after an import actually mean.

## How the app loads your code

- Because the application itself runs on **.NET 10**, it can only use the **MSBuild that ships
  with the .NET SDK**. It **cannot** load the desktop `MSBuild.exe` that Visual Studio uses (that
  one runs on the .NET Framework, and a .NET process cannot host it).
- It performs a **design-time build**, not a full compile (see below). This is why a project can
  build fine in Visual Studio yet still fail to load here: the two use different MSBuild engines
  *and* different build modes.

### What is a design-time build?

A **full build** (`dotnet build`, or F6 in Visual Studio) runs every MSBuild target, invokes the C#
compiler, and produces the output DLLs/EXEs. A **design-time build** is a lightweight, partial
evaluation that answers *"what would this project compile, and against what?"* without actually
compiling: it resolves the **source files**, **references** and **compiler options**, but does
**not** run the compiler and produces **no** output assembly (it sets properties such as
`DesignTimeBuild=true` and `SkipCompilerExecution=true`). This is the same mechanism Visual Studio
runs constantly in the background to power IntelliSense and error squiggles.

Roslyn's `MSBuildWorkspace` needs exactly that information — sources, references and options — to
build its in-memory model of your code; it does not need the compiled DLL. The catch: a design-time
build is a **different code path** than a full build, so custom or framework-specific targets (e.g.
WPF's markup targets) can behave differently under it — which is how a full build in Visual Studio
can succeed while the design-time evaluation here fails.

## Requirements for analyzed projects

- **The solution must load with the .NET SDK toolchain.** SDK-style project files
  (`<Project Sdk="Microsoft.NET.Sdk...">`) load reliably.
- **The matching targeting packs must be installed** for whatever frameworks the projects target
  (e.g. the *.NET Framework 4.7.2 Developer Pack* for `net472` projects).

### Litmus test: does `dotnet build` work?

Run this from a terminal:

```
dotnet build YourSolution.sln
```

`dotnet build` uses the same .NET SDK MSBuild the app uses. **If it succeeds, the app can load the
solution. If it fails while Visual Studio succeeds, the project will not load here either** —
Visual Studio built it with the .NET Framework desktop MSBuild, which this app cannot use.

## Known limitation: .NET Framework / non-SDK projects

**Legacy (non-SDK) .NET Framework projects can fail to load.** In particular, **old-style WPF
projects targeting .NET Framework** (e.g. `net472`) often fail during the design-time build with an
MSBuild `NullReferenceException` — surfaced as *"Object reference not set to an instance of an
object"* — even though they compile cleanly in Visual Studio. This is a limitation of evaluating
.NET Framework project types from a .NET-based process, **not** a defect in your solution.

### What happens on a load failure

The rest of the solution is still analyzed. But the types from the failed project are **missing**
from the graph, and any relationships crossing its boundary are incomplete. Projects that
referenced the failed one also produce *"project reference without a matching metadata reference"*
warnings (see below).

### Workarounds

- **Convert the affected project to an SDK-style `.csproj`.** It may keep targeting .NET Framework
  (`<TargetFramework>net472</TargetFramework>` with `<UseWPF>true</UseWPF>`); SDK-style projects
  have far more robust design-time build support and usually load. Make sure the matching
  targeting pack is installed.
- **Or retarget the project to a modern .NET** (e.g. `net10.0-windows`).
- **Or accept the gap** and analyze the rest of the solution — the failed project simply will not
  appear in the graph.

## Troubleshooting the import dialog

After an import, failed and partially-resolved projects are listed in the **Errors/Warnings**
dialog. Both texts below come straight from Roslyn's `MSBuildWorkspace`, not from the analyzer
itself.

### Error: "MSBuild error while processing the file ... Object reference not set to an instance of an object"

A `NullReferenceException` thrown **inside MSBuild** while evaluating that project (typically a
.NET Framework WPF project — see the limitation above). That one project could not be loaded. The
remaining projects are still analyzed.

### Warning: "Found project reference without a matching metadata reference"

A follow-on effect: because a referenced project did not load (or did not produce a build output),
Roslyn has no compiled metadata to point the `ProjectReference` at. The listed projects reference
something whose output is missing — usually the project that failed with the error above. Fixing
the underlying load failure clears these warnings too.
