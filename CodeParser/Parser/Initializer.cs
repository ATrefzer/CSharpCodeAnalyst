using Microsoft.Build.Locator;

namespace CodeParser.Parser;

public class Initializer
{
    public static void InitializeMsBuildLocator()
    {
        // Without the MSBuildLocator the Project.Documents list is empty!
        // Referencing MSBuild packages directly and copy to output is not reliable and causes
        // hard to find problems
        MSBuildLocator.RegisterDefaults();
    }
}