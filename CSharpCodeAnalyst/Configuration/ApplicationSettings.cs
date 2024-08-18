namespace CSharpCodeAnalyst.Configuration;

public class ApplicationSettings
{
    public int WarningCodeElementLimitForCycle { get; set; } = 50;
    public string DefaultProjectExcludeFilter { get; set; } = string.Empty;
    public bool DefaultShowQuickHelp { get; set; }
}