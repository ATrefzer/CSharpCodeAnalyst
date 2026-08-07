# Command-line arguments

Validation of a C# solution against a rule file can be done via command line.

| Argument         | Required | Description                                          |
| ---------------- | :------- | ---------------------------------------------------- |
| -validate        | yes      | Run the validation against a rule file.              |
| -sln:<file>      | yes      | Path to the C# solution file to validate.            |
| -rules:<file>    | yes      | Path to the text file containing the rules to check. |
| -log-console     | no       | Program output is written to the console.            |
| -log-file:<file> | no       | Program output is written to the given file.         |
| -out:<file>      | no       | Validation result is written to the given file.      |

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

## Rules file syntax

See [README.md](../README.md)
