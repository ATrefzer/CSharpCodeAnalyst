using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.DeadCode.Presentation;

public class DeadCodeRowViewModel : TableRow
{
    /// <summary>Beyond this many related members the hint only states the count - the cell has to stay readable.</summary>
    private const int MaxNamedRelatedMembers = 3;

    internal DeadCodeRowViewModel(DeadCodeFinding finding)
    {
        Element = finding.Element;
        Name = finding.Element.FullName;
        Kind = finding.Element.ElementType.ToString();
        // Fully qualified: WPF pulls a global "Accessibility" namespace into scope; ours is AccessLevel.
        Access = finding.Element.AccessLevel == CodeGraph.Graph.AccessLevel.Unknown
            ? string.Empty
            : finding.Element.AccessLevel.ToString();

        Confidence = finding.Confidence.ToString();

        // Bound for the colour rating and for sorting; the column displays the word.
        ConfidenceValue = (int)finding.Confidence;
        Hint = FormatHint(finding);
    }

    /// <summary>The underlying graph node, used to jump to the source and to add it to the Code Explorer.</summary>
    public CodeElement Element { get; }

    public string Name { get; }
    public string Kind { get; }

    /// <summary>The element's visibility, empty when the producer did not supply one.</summary>
    public string Access { get; }

    public string Confidence { get; }

    /// <summary>Numeric backer of <see cref="Confidence" /> for the colour rating and for sorting.</summary>
    public int ConfidenceValue { get; }

    /// <summary>
    ///     Two kinds of note, joined into one cell: why the element might be alive despite having no
    ///     visible reference (entry point, test code, attributes), and - for a contract finding - what
    ///     dies together with it. Empty means neither applies, so nothing speaks against deleting it.
    /// </summary>
    public string Hint { get; }

    private static string FormatHint(DeadCodeFinding finding)
    {
        var parts = new List<string>();

        if (finding.Hints.HasFlag(DeadCodeHint.EntryPoint))
        {
            parts.Add(Strings.DeadCode_Hint_EntryPoint);
        }

        if (finding.Hints.HasFlag(DeadCodeHint.TestCode))
        {
            parts.Add(Strings.DeadCode_Hint_TestCode);
        }

        if (finding.Hints.HasFlag(DeadCodeHint.ContractNeverCalled))
        {
            parts.Add(string.Format(Strings.DeadCode_Hint_ContractNeverCalled, FormatRelated(finding)));
        }

        if (finding.Hints.HasFlag(DeadCodeHint.ImplementsDeadContract))
        {
            parts.Add(string.Format(Strings.DeadCode_Hint_ImplementsDeadContract, FormatRelated(finding)));
        }

        if (finding.Hints.HasFlag(DeadCodeHint.ImplementsExternalContract))
        {
            parts.Add(string.Format(Strings.DeadCode_Hint_ImplementsExternalContract, finding.ExternalContract));
        }

        if (finding.Hints.HasFlag(DeadCodeHint.Attributed))
        {
            parts.Add(string.Format(Strings.DeadCode_Hint_Attributed, string.Join(", ", finding.Attributes)));
        }

        return string.Join("; ", parts);
    }

    private static string FormatRelated(DeadCodeFinding finding)
    {
        if (finding.RelatedMembers.Count > MaxNamedRelatedMembers)
        {
            return string.Format(Strings.DeadCode_Hint_RelatedCount, finding.RelatedMembers.Count);
        }

        return string.Join(", ", finding.RelatedMembers.Select(m => m.FullName));
    }
}
