namespace CSharpCodeAnalyst.CodeParser.Parser.Config;

public class ParserConfig
{
    private readonly ProjectExclusionRegExCollection _projectExclusionFilters;

    public ParserConfig(ProjectExclusionRegExCollection projectExclusionFilters, bool includeExternals,
        bool includeXamlReferences = true)
    {
        _projectExclusionFilters = projectExclusionFilters;
        IncludeExternals = includeExternals;
        IncludeXamlReferences = includeXamlReferences;
    }

    public bool IncludeExternals { get; }

    // There is no "include generated code" option. Generated code is always parsed: leaving it out
    // removes the only reference many hand-written elements have (the markup compiler's Connect is the
    // sole caller of every XAML event handler, an [ObservableProperty] the only reader of its backing
    // field), which turns them into dead code. What a tool wrote carries CodeElement.IsGenerated instead,
    // so a result can leave it out without the graph losing an edge.

    // There is no "split property accessors" option either, for the same reason: every property is
    // always split into its getter and setter as separate child elements (get_Prop / set_Prop). This
    // lets the dependency graph distinguish read access from write access and avoids false cycles that
    // arise when both directions are merged onto a single property node - there was never a good reason
    // to turn that off, so the toggle was removed rather than defaulted (see corrections-and-updates.md).

    /// <summary>
    ///     When enabled, the XAML files next to the analyzed projects are scanned for the references the
    ///     markup compiler does not turn into C# (element tags, <c>{x:Static}</c>, <c>{x:Type}</c>) and
    ///     those become relationships in the graph. Without it a control that is only instantiated from
    ///     XAML looks unreferenced.
    /// </summary>
    public bool IncludeXamlReferences { get; }

    public bool IsProjectIncluded(string projectName)
    {

        return _projectExclusionFilters.IsProjectIncluded(projectName);
    }
}