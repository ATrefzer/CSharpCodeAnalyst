using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.CommandLine;

public class CommandLineProcessor
{
    public static async Task<int> ProcessCommandLine(string[] args)
    {
        try
        {
            var arguments = ParseArguments(args);

            bool validateRules = arguments.ContainsKey("validate")
                                 && arguments.ContainsKey("rules")
                                 && arguments.ContainsKey("sln");


            if (!validateRules)
            {
                string file = arguments["rules"];
                var sln = arguments["sln"];

                var cmd = new ConsoleValidationCommand();
                return await cmd.ValidateRules(sln, file);
            }

            Console.WriteLine(Strings.Cmd_InvalidCommandLineArgs);
            ShowUsage();
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine(Strings.Cmd_GenericError, ex.Message);
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string arg in args)
        {
            if (arg.StartsWith("-") && arg.Contains(":"))
            {
                int colonIndex = arg.IndexOf(":", StringComparison.Ordinal);
                string key = arg.Substring(1, colonIndex - 1); // Remove the "-" prefix
                string value = arg.Substring(colonIndex + 1);
                arguments[key] = value;
            }
        }

        return arguments;
    }

    private static void ShowUsage()
    {
        var usage = """
                    Command line usage:

                    Validate the solution against the architectural rules:

                        CSharpCodeAnalyst.exe -validate -sln:<solution_path> -rules:<file_path>
                        

                    To start the UI, run without arguments:
                        
                        CSharpCodeAnalyst.exe

                    """;
        Console.WriteLine(usage);
    }
}