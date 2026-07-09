using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.History.Model;

namespace CSharpCodeAnalyst.Features.History;

internal class ChangeCouplingViewModel(Coupling coupling) : TableRow
{
    public string File1 { get; set; } = coupling.Item1;
    public string File2 { get; set; } = coupling.Item2;
    public double Degree { get; set; } = coupling.Degree;
    public int Couplings { get; set; } = coupling.Couplings;
    public string DegreeText { get; set; } = coupling.Degree.ToString("0.00");
    public string CouplingsText { get; set; } = coupling.Couplings.ToString();

    /// <summary>
    ///     Synthetic key for the table search: a coupling spans two file columns, so the search
    ///     text must be matched against both. Combining them lets a single term hit either file.
    /// </summary>
    public string SearchKey => File1 + " " + File2;
}