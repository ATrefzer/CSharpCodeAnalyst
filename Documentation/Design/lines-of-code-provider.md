# LinesOfCodeProvider — how the scanner works and how file types are maintained

`CSharpCodeAnalyst.History/Metrics/LinesOfCodeProvider.cs` counts lines of code and comment lines per file, for any registered file type. It is used by the history/hotspot features, where files of many languages appear and running a real compiler front-end per language is not an option.

It is deliberately **not** a lexer or parser. It is a line-oriented scanner with exactly two states, driven entirely by *data* (`FileTypeInfo` definitions in `LinesOfCodeFileTypes.cs`). Supporting a new language normally means adding a data entry, not writing code.

> For C# there is also `SourceMetricsCollector` (Roslyn-based, exact). The two counters agree closely but not perfectly; the known, accepted differences are listed at the end of this document.

## 1. The model

A `FileTypeInfo` describes one file type and consists of three parts:

| Part | Type | Meaning |
|---|---|---|
| `Name` | `string` | Display name only. |
| `LineComments` | `List<LineCommentStyle>` | Comments running from a token to the end of the line (`//`, `#`, `'`, `REM`). Empty list = the language has no line comments (XML, CSS, plain text). |
| `Regions` | `List<DelimitedRegionStyle>` | Everything that opens with a delimiter and closes with a delimiter: block comments **and** all string variants. |

### LineCommentStyle

* `Token` — the literal comment token.
* `IsKeyword` — for tokens that are language keywords rather than symbols (VB `REM`). A keyword token matches **case-insensitively** and only as a **whole word**: there must be no identifier character (letter, digit, `_`) directly before or after the token; line start and line end count as boundaries. This is what keeps `REMainder = 5` and `theorem` from being read as comments while still accepting `REM`, `rem`, and `Dim x = 5 : REM hint`.

Symbol tokens (`//`, `#`, `'`) match literally at the current position, over their full length.

### DelimitedRegionStyle

One region style describes one kind of delimited stretch of text. The key insight of the design: **strings and block comments are the same mechanism.** While inside a region, the normal scanning rules (line comments, other region openers) are suspended until the region closes. The only difference between a string and a block comment is what the lines inside count as:

* `Kind = RegionKind.Code` — a string: its lines count as code.
* `Kind = RegionKind.Comment` — a block comment: its lines count as comments.

The remaining properties describe how the region opens and closes:

* `Start` / `End` — opening and closing delimiter.
* `Escaping` — how a literal delimiter character can appear *inside* the region without closing it:
  * `None` — no escaping at all (block comments, C# raw strings).
  * `Backslash` — `\` escapes the next character (C-family strings, JS template literals).
  * `DoubledDelimiter` — writing the delimiter twice is the escape (`""` in C# verbatim strings and in VB strings); `\` has no meaning.
* `VariableLengthQuoteRun` / `MinimumQuoteRunLength` — for C# raw strings (`"""..."""`, `""""...""""`): the opening run of quotes (3 or more) determines the minimum length of the closing run. `End` is unused in this mode.
* `RequiresPrefix` — an optional look-back predicate for delimiters that are only valid in context. Example: the C# verbatim-string style only matches a `"` preceded by `@`, `$@`, or `@$`; the raw-string style requires the *absence* of that prefix (see trap 3 below).

## 2. The scanning mechanics

`AnalyzeLinesCore` processes a file line by line with a two-state machine (`Normal`, `InRegion`). Region state carries over across lines — that is how multi-line block comments and multi-line strings work.

Per line:

1. **Blank check first, regardless of state.** A whitespace-only line counts as blank even if it lexically sits inside a multi-line string or block comment. This is a deliberate convention shared with common LOC tools (e.g. cloc).

2. **Character loop.** For each position `i`, depending on state:

   * **`Normal`:**
     1. Does a **line comment** start here (`MatchesLineComment`)? If yes → mark the line as containing a comment and skip the rest of the line.
     2. Else, does a **region** open here (`TryMatchRegionOpen`)? If yes → switch to `InRegion`, remember the style and the opening length, mark the line as code or comment according to `Kind`, and jump past the opening delimiter.
     3. Else, any non-whitespace character marks the line as containing code.
   * **`InRegion`:** mark the line as code/comment according to the active region's `Kind`, then ask `EvaluateRegionChar` how far to advance and whether the region closes here. This is the single place that knows the escaping strategies: `""` advances two characters without closing, `\x` advances two characters, a sufficient quote run closes a raw string, etc.

3. **Classify the line.** A line that contains any code counts as **code**, even if it also contains a comment (`int x = 1; // note` → code). A line with only comment content counts as **comment**. This "code wins" rule is the common LOC convention.

Two ordering rules inside the scanner are worth knowing:

* **Line comments are checked before regions.** For VB this is what makes `'` a comment rather than the start of a character/string region — which is also why the VB file type must *not* contain a single-quote string region (see below).
* **Within `Regions`, the first style whose opener matches wins.** The list order in the `FileTypeInfo` is therefore semantically significant — see the maintenance section.

## 3. Maintaining file types (`LinesOfCodeFileTypes.cs`)

All definitions live in `LinesOfCodeFileTypes.GetFileTypes()`, keyed by lower-case file extension. Shared, stateless style instances (`DoubleQuoteString`, `SingleQuoteString`, `DoubleSlashComment`) are reused across languages — this is safe because all style types are immutable (`init`-only).

### Rules and traps

1. **Region order matters: specific before general.** Styles are tried in declaration order, first match wins. Every style must appear **before** any more general style that shares its opening character, or it is unreachable:
   * C# raw string (`"""`) and verbatim string (`@"`) before the plain `"` string.
   * Python `'''` / `"""` docstrings before the plain `'` / `"` strings.
   * Java text block (`"""`) before the plain `"` string.

2. **A comment token must not double as a string delimiter.** VB uses `'` for comments, so the VB entry deliberately has **no** single-quote string region. If both were present, the line-comment check would win (it runs first) and the string style would be dead — but the correct fix is to not declare the conflicting style at all, so the intent is visible.

3. **Mutually exclusive styles need `RequiresPrefix` gates in both directions.** The C# raw-string style must not match `@"""` (that is a verbatim string starting with an escaped quote), so it is gated on the *absence* of the verbatim prefix, while the verbatim style is gated on its *presence*. When adding a style that shares an opener with an existing one and ordering alone cannot disambiguate, add a look-back predicate.

4. **Markup languages get no string regions at all.** For XML/HTML, quotes only delimit strings inside tags — in element text they are plain content, and a line scanner cannot tell the difference. Declaring string regions would let an apostrophe or unbalanced quote in prose swallow all following comments. Nothing of value is lost: well-formed XML forbids `<` inside attribute values, so a comment opener cannot legally occur inside an attribute string. (For HTML the same trade-off is accepted, and `<!--` really does start a comment even between quotes in text content.)

5. **Keyword line comments** (`IsKeyword = true`) are for comment tokens that are words in the language (VB `REM`). They get case-insensitive whole-word matching for free; symbol tokens stay literal and case-sensitive.

6. **Extensions not covered by any `FileTypeInfo`** can still be handled by callers via `RegisterCustomProvider(extension, handler)` — the handler receives the file path and returns `(code, comments)` itself. A custom handler also *overrides* an existing definition for that extension.

### Checklist for adding a language

1. Add a `fileTypes[".ext"] = new FileTypeInfo { ... }` entry in `GetFileTypes()`.
2. Line comments: one `LineCommentStyle` per token; use `IsKeyword` for word tokens.
3. Regions: one `DelimitedRegionStyle` per string/comment variant. Reuse the shared instances where the semantics match exactly; order specific before general; add `RequiresPrefix` gates where opening characters collide.
4. Double-check that no line-comment token collides with a region opener (rule 2).
5. Add tests in `Tests/UnitTests/Parser/LinesOfCodeProviderTests.cs`. The valuable cases are the adversarial ones: a fake comment token *inside* a string, an escaped delimiter followed by a real comment on the next line, and multi-line regions. The existing per-language sections show the pattern.
6. Document any deliberate simplification as a comment on the entry (see the Python and HTML entries for the expected style).

## 4. Known, deliberate simplifications

* **Blank lines inside multi-line strings/comments count as blank**, not as code/comment. `SourceMetricsCollector` (Roslyn) attributes such lines to code/comment instead — this is the main source of small per-file differences between the two counters and is accepted.
* **Python triple-quoted strings are counted as comments.** This is intentional so that docstrings count as comments; the cost is that a genuine multi-line data string using triple quotes is miscounted.
* **HTML does not switch grammar inside `<script>`/`<style>` blocks** — that would need tag awareness. LOC inside embedded scripts is approximate.
* **JS template-literal interpolation holes (`${...}`) are not parsed**; the whole literal is one code region.
* **VB date literals (`#...#`) are not modelled.** `#` is not registered as an opener for `.vb`, so it cannot open a bogus region; the literal simply counts as code, which is correct.
