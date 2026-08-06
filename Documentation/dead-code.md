# Dead Code

Finds code that nothing in your solution references anymore. 

Run it from **Analyzers → Dead Code**.

## Reading the results

You get a sortable table:

| Column | What it tells you |
|---|---|
| **Element** | The unreferenced class, method, property, etc. |
| **Kind** | Class, Interface, Method, Field, Property... |
| **Access** | Public, private, internal, etc. |
| **Confidence** | How much you can trust the finding (see below) |
| **Notes** | Anything worth knowing. |

**Tips:**
- Right-click a row for *Jump to code* or *Copy to explorer graph* to inspect a finding before acting on it.
- Use the filter box to cut down noise on large codebases, e.g. `-Strings. -Tests excludes anything matching those terms.

### Confidence levels

| Confidence       | Meaning |
|---|---|
| 🟢 **High**   | Nothing outside your code could call this (it's private/internal). Safe to trust. |
| 🟠 **Medium** | Public code with no note. Probably safe, but a caller outside the analyzed solution could exist. A finding used only by tests is capped at Medium |
| 🔴 **Low**    | The Notes column flags a reason the finding might be wrong (e.g. it could be an entry point, or reached via attributes/reflection). Check before deleting. |


### What the Notes mean

If Notes has a value, it's one of two things:

- **A warning to double-check** — there may be a caller the tool can't see (reflection, a framework, dependency injection).
- **An explanation** — the tool understands the finding fully and is telling you what else would break or disappear if you delete it.

Here's what each specific note means:

**Warnings to double-check:**

| Note | Meaning |
|---|---|
| `Entry point` | This is `Main`, or a top-level statement — the runtime calls it, so "no code references it" is expected and not a sign it's unused. |
| `Generated code` | A tool wrote this, not a person. The finding is technically correct, but it'll just be regenerated on the next build — not something you'd manually delete. |
| `Attributes: ...` | This element has attributes attached (listed in the note), which often means a framework calls it behind the scenes — e.g. a test runner or serializer. |

**Explanations:**

| Note | Meaning |
|---|---|
| `Implemented but never called: ...` | This is an interface/abstract member that's implemented somewhere, but nothing ever calls it through the interface. |
| `Implements unused contract: ...` | This implements an interface member that is itself dead — so both the interface member and this implementation can likely go together. |
| `Implements external contract: ...` | This implements something from outside your codebase (like `IDisposable.Dispose` or `object.ToString`). The framework is the real caller, so this is almost certainly still needed. |
| `Used only by tests: ...` | No production code calls this — only the listed tests do. Whether to keep it is your call, but production code doesn't need it. |

## Things worth knowing before you delete

- **One class, one row.** If a whole class is dead, you get a single row for the class — not one per method. If only some of its methods are dead, you get rows for just those methods.
- **Deletions happen in rounds.** After you delete a finding, run the analysis again — deleting code can reveal more dead code.
- **Two rows can look identical.** Generic types and overloaded methods (`Foo(int)` vs `Foo(string)`) can display with the same name. Use *Jump to code* to confirm you're looking at the right one.
- **XAML bindings aren't seen.** `{Binding}` and `{StaticResource}` in XAML are invisible to the analysis. This is why properties on view models get capped confidence.
- **Reflection, DI, and serialization are invisible.** Anything only referenced at runtime through these mechanisms can't be detected — the tool has some built-in protections for common cases (like serialized DTOs), but not all.
- **External libraries aren't in scope.** If you're building a library consumed by other solutions, its public API will look "dead" here even if it's actively used elsewhere.
- **Circular dead code isn't caught.** Two classes that only reference each other, and nothing else, will each look "used" — because they reference one another. The tool can't detect this kind of mutual dead code.

**Bottom line:** trust High-confidence, empty-note findings the most. Always check Notes and Confidence before deleting anything else — and re-run the analysis after each round of cleanup.
