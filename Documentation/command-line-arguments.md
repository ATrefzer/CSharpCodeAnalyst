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

## Result Code

| Code | Description                       |
| ---- | --------------------------------- |
| 0    | No violation found                |
| 1    | Violation found                   |
| 2    | Validation failed, see log output |

## Rules file syntax

See [README.md](../README.md)
