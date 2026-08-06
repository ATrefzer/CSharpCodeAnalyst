using CSharpCodeAnalyst.CodeGraph.Declarations;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     A XAML {Binding} reads the public properties of any type implementing INotifyPropertyChanged, so
///     the parser records those types beside the graph (<see cref="ExternalContractStore.NotifyingTypes" />).
///     The case that forces the type-level set is the external base class: a view model deriving from
///     ObservableObject or BindableBase has no PropertyChanged member of its own, so neither the graph nor
///     the member-level contracts can tell it apart from an ordinary class.
/// </summary>
[TestFixture]
public class NotifyingTypeParseTests
{
    [OneTimeSetUp]
    public async Task ParseCode()
    {
        const string code = """
                            using System.Collections.ObjectModel;
                            using System.ComponentModel;

                            namespace Demo;

                            public class DirectViewModel : INotifyPropertyChanged
                            {
                                public event PropertyChangedEventHandler? PropertyChanged;
                                public string? Title { get; set; }
                            }

                            public class DerivedViewModel : DirectViewModel
                            {
                                public string? Caption { get; set; }
                            }

                            // The implementation sits in a base class outside the analyzed code.
                            public class ExternalBaseViewModel : ObservableCollection<int>
                            {
                                public string? Header { get; set; }
                            }

                            public class Ordinary
                            {
                                public string? Name { get; set; }
                            }
                            """;

        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false));
        var result = await parser.ParseSourceAsync(code);

        _graph = result.CodeGraph;
        _contracts = result.ExternalContracts;
    }

    private CodeGraph _graph = null!;
    private ExternalContractStore _contracts = null!;

    private bool IsNotifying(string typeName)
    {
        var element = _graph.Nodes.Values.Single(n =>
            n.ElementType == CodeElementType.Class && n.Name == typeName);
        return _contracts.NotifyingTypes.Contains(element.Id);
    }

    [Test]
    public void DirectImplementation_IsRecorded()
    {
        Assert.That(IsNotifying("DirectViewModel"), Is.True);
    }

    [Test]
    public void DerivedFromAnInternalImplementer_IsRecorded()
    {
        Assert.That(IsNotifying("DerivedViewModel"), Is.True);
    }

    [Test]
    public void DerivedFromAnExternalImplementer_IsRecorded()
    {
        // The reason the set exists: no PropertyChanged member in the graph, no internal base to spread
        // from - only the symbol knows.
        Assert.That(IsNotifying("ExternalBaseViewModel"), Is.True);
    }

    [Test]
    public void OrdinaryType_IsNotRecorded()
    {
        Assert.That(IsNotifying("Ordinary"), Is.False);
    }
}
