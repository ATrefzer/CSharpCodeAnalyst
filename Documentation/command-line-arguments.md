# Command-line arguments

Validation of a C# solution against a rule file can be done via command line.

| Argument            | Required | Description                                                                                            |
| ------------------- | :------- | ------------------------------------------------------------------------------------------------------ |
| -validate           | yes      | Run the validation against a rule file.                                                                |
| -sln:<file>         | yes      | Path to the C# solution file to validate.                                                              |
| -rules:<file>       | yes      | Path to the text file containing the rules to check.                                                   |
| -log-console        | no       | Program output is written to the console.                                                              |
| -log-file:<file>    | no       | Program output is written to the given file.                                                           |
| -out:<file>         | no       | Validation result is written to the given file, as readable text.                                      |
| -sarif:<file>       | no       | Validation result is *additionally* written to the given file as SARIF 2.1.0. See below.               |
| -source-root:<dir>  | no       | The directory the code was checked out into. Only affects `-sarif`. Defaults to the solution directory. |

`-out`, `-sarif` and `-log-file` create the directory of the given path if it does not exist, so a
CI script can point them straight into a fresh artifacts folder without a preceding `mkdir`.

## Example

```
CSharpCodeAnalyst -validate -sln:d:\Repositories\CSharpCodeAnalyst\CSharpCodeAnalyst.sln -rules:d:\rules.txt -log-console -out:d:\analysis-result.txt
```

## Generating the command line

The Architectural Rules dialog has a **Command line** button (clipboard icon) that copies a
validation command line to the clipboard.

## Result Code

| Code | Description                       |
| ---- | --------------------------------- |
| 0    | No violation found                |
| 1    | Violation found                   |
| 2    | Validation failed, see log output |

## Running from CI / PowerShell scripts

`CSharpCodeAnalyst.exe` is a WinExe (GUI subsystem), not a console app. When there is no
console attached to the calling process — the normal situation on a CI runner — invoking it
with the plain `&` call operator does not reliably wait for it to finish, and `$LASTEXITCODE`
can come back empty instead of the real result code. This can make a CI step look green even
when validation actually failed or crashed.

Use `Start-Process -Wait -PassThru` instead, and read the exit code from the returned process
object rather than `$LASTEXITCODE`:

```powershell
$proc = Start-Process -FilePath "C:\path\to\CSharpCodeAnalyst.exe" -ArgumentList @(
  "-validate",
  "-sln:C:\path\to\MySolution.sln",
  "-rules:C:\path\to\architecture.rules.txt",
  "-log-console",
  "-out:C:\path\to\validation-result.txt"
) -NoNewWindow -Wait -PassThru `
  -RedirectStandardOutput "stdout.txt" `
  -RedirectStandardError "stderr.txt"

Get-Content "stdout.txt" | Write-Host   # surface -log-console output in the CI log
$code = $proc.ExitCode
if ($code -ne 0) { exit $code }         # propagate 1 (violations) / 2 (load error) to the CI step
```

`-Wait` blocks the script until the process actually exits, instead of returning as soon as it
is launched. `-PassThru` returns the `System.Diagnostics.Process` object so `.ExitCode` can be
read afterwards — without it `Start-Process` returns nothing.

`appsettings.json` (if present) is resolved next to `CSharpCodeAnalyst.exe`, not relative to the
process's current working directory, so this works regardless of which directory the CI step
happens to run from. The file is optional for headless validation; if it is missing, built-in
defaults are used.

## SARIF output

`-sarif:<file>` writes the same result as a [SARIF 2.1.0](https://docs.oasis-open.org/sarif/sarif/v2.1.0/sarif-v2.1.0.html)
log, the format CI systems read to turn findings into annotations on a pull request. It does not
replace `-out`: both can be used in the same run, and the text output stays the readable one for
the CI log.

What ends up in the file:

- **One result per offending place.** A dependency rule produces one result per violating
  relationship, a `MAXLINES` rule one per element above the threshold. A `NOCYCLES` violation stays
  one result — a cycle is a property of the whole group — with its participants as locations.
- **`ruleId` is the rule keyword** (`DENY`, `RESTRICT`, `ISOLATE`, `NOCYCLES`, `MAXCYCLICITY`,
  `MAXLINES`), not the individual rule line. The concrete line is in the message and in
  `properties.ruleText`, and every result points back at it through `relatedLocations`. Editing a
  pattern therefore does not orphan the alerts of that rule.
- **Paths are relative to the source root**, with `originalUriBaseIds.SRCROOT` naming the root.
  See [What to pass as `-source-root`](#what-to-pass-as--source-root) below.
- **`partialFingerprints`** identify a finding by what it says about the architecture — the two
  elements and the relationship type — never by file or line. Moving code does not turn an
  acknowledged alert into a new one.
- **Rules that match nothing, and parser failures, are `toolConfigurationNotifications`**, not
  results. They are problems with the run, not findings about the code, which keeps "no results"
  equivalent to exit code 0.

### What to pass as -source-root

**The directory the code was checked out into** — the one from which you see `src\`, `README.md`
and the rest of the repository. Nothing more clever than that.

It is not related to `-sarif`. `-sarif` is only the destination the report file is written to;
`-source-root` is applied to the source file paths *inside* the report.

| Where you run it            | What to pass                  |
| --------------------------- | ----------------------------- |
| GitHub Actions              | `${{ github.workspace }}`     |
| Azure DevOps                | `$(Build.SourcesDirectory)`   |
| GitLab CI                   | `$CI_PROJECT_DIR`             |
| Jenkins                     | `$WORKSPACE`                  |
| Locally                     | the directory you cloned into |


The default is the directory of the `.sln`, which is right only when the solution sits at the root
of the checkout. With `src\MyApp.sln`, `<root>\src\App\Foo.cs` would be written as `App/Foo.cs`,
the consumer would look for `App/Foo.cs` at the root, find nothing, and drop the finding without
saying so.

**How to tell you got it right:** open the generated file and look at any `artifactLocation.uri`.
It has to read like a path you would see when browsing the repository (`src/App/Foo.cs`) and carry
`"uriBaseId": "SRCROOT"`. A `uri` starting with `file:///` means that file lies outside the root you
passed, and no consumer will match it.

### GitHub Actions

```yaml
- name: Validate architecture
  id: validate
  continue-on-error: true
  shell: pwsh
  run: |
    $proc = Start-Process -FilePath "${{ github.workspace }}\tool\CSharpCodeAnalyst.exe" -ArgumentList @(
      "-validate",
      "-sln:${{ github.workspace }}\src\MySolution.sln",
      "-rules:${{ github.workspace }}\architecture.rules.txt",
      # Where the checkout is - decides how the source paths INSIDE the report are written.
      "-source-root:${{ github.workspace }}",
      # Where the report file goes. Unrelated to the above; put it wherever you like.
      "-sarif:${{ runner.temp }}\architecture.sarif",
      "-log-console"
    ) -NoNewWindow -Wait -PassThru
    exit $proc.ExitCode

- name: Upload SARIF
  uses: github/codeql-action/upload-sarif@v3
  with:
    sarif_file: ${{ runner.temp }}/architecture.sarif
    category: architecture

- name: Fail on validation error
  if: steps.validate.outcome == 'failure'
  run: exit 1
```

`continue-on-error` on the validation step is what lets the upload run even when violations were
found — the findings are the point of the upload. The last step still fails the job.

The report is deliberately written outside the checkout here: `-sarif` and `-source-root` have
nothing to do with each other. `-sarif` is only the destination of the file and is never read back;
`-source-root` is applied to the source file paths *inside* the report. Writing the report into
`artifacts\` or a temp directory must not change how the findings point at the code.

## Rules file syntax

See [README.md](../README.md) and [Architectural rules](architectural-rules.md).

## CI and multi-source pipelines

For GitHub Actions packaging, JSON edge export helpers, and using Code Analyst
as one evidence stage among peer auditors (without pretending the graph sees every
UI binding), see:

- [CI + multi-source evidence pipeline](ci-and-multi-source-pipeline.md)
- Sample pack: [`samples/ci-pipeline/`](../samples/ci-pipeline/README.md)
