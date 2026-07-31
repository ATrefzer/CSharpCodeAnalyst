using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Xaml;

namespace CodeParserTests.UnitTests.Xaml;

[TestFixture]
public class XamlGraphLinkerTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
        _directory = Path.Combine(Path.GetTempPath(), "XamlLinkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private TestCodeGraph _graph = null!;
    private string _directory = null!;

    /// <summary>
    ///     Builds "Assembly &gt; Namespace &gt; Type" the way the parser does: the namespace element carries
    ///     the whole dotted namespace, so the CLR name of the type is "App.Views.MyControl". The linker
    ///     resolves through the element names, so the ids stay short here.
    /// </summary>
    private CodeElement CreateType(CodeElement assembly, string namespaceName, string typeName)
    {
        var ns = assembly.Children.FirstOrDefault(c => c.Name == namespaceName)
                 ?? _graph.CreateNamespace(namespaceName, assembly);
        return _graph.CreateClass(typeName, ns);
    }

    private void WriteXaml(string relativePath, string content)
    {
        var path = Path.Combine(_directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string[] EdgesFrom(CodeElement source, CodeGraph graph)
    {
        return source.Relationships
            .Where(r => r.HasAttribute(RelationshipAttribute.IsXamlReference))
            .Select(r => graph.Nodes[r.TargetId].FullName)
            .ToArray();
    }

    [Test]
    public void Link_ElementTag_ConnectsCodeBehindToTheUsedType()
    {
        var assembly = _graph.CreateAssembly("App");
        var view = CreateType(assembly, "App.Views", "MainWindow");
        var control = CreateType(assembly, "App.Controls", "MyGrid");

        WriteXaml("MainWindow.xaml", """
                                     <Window x:Class="App.Views.MainWindow"
                                             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                             xmlns:c="clr-namespace:App.Controls">
                                         <c:MyGrid />
                                     </Window>
                                     """);

        var added = XamlGraphLinker.Link(_graph, [new XamlProject(assembly, _directory)]);

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.EqualTo(1));
            Assert.That(EdgesFrom(view, _graph), Is.EqualTo(new[] { "MyGrid" }));
        });
    }

    [Test]
    public void Link_ObjectElement_AlsoConnectsToTheConstructor()
    {
        // Without this edge the constructor has no incoming reference at all, and everything only it
        // calls dies with it once the analysis cascades.
        var assembly = _graph.CreateAssembly("App");
        var view = CreateType(assembly, "App.Views", "MainWindow");
        var control = CreateType(assembly, "App.Controls", "MyGrid");
        var constructor = _graph.CreateMethod(".ctor", control);

        WriteXaml("MainWindow.xaml", """
                                     <Window x:Class="App.Views.MainWindow"
                                             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                             xmlns:c="clr-namespace:App.Controls">
                                         <c:MyGrid />
                                     </Window>
                                     """);

        XamlGraphLinker.Link(_graph, [new XamlProject(assembly, _directory)]);

        Assert.That(EdgesFrom(view, _graph), Is.EquivalentTo(new[] { "MyGrid", constructor.FullName }));
    }

    [Test]
    public void Link_XType_DoesNotConnectToTheConstructor()
    {
        // {x:Type} only names the type; nothing is created.
        var assembly = _graph.CreateAssembly("App");
        var view = CreateType(assembly, "App.Views", "MainWindow");
        var control = CreateType(assembly, "App.Controls", "MyGrid");
        _graph.CreateMethod(".ctor", control);

        WriteXaml("MainWindow.xaml", """
                                     <Window x:Class="App.Views.MainWindow"
                                             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                             xmlns:c="clr-namespace:App.Controls">
                                         <Style TargetType="{x:Type c:MyGrid}" />
                                     </Window>
                                     """);

        XamlGraphLinker.Link(_graph, [new XamlProject(assembly, _directory)]);

        Assert.That(EdgesFrom(view, _graph), Is.EquivalentTo(new[] { "MyGrid" }));
    }

    [Test]
    public void Link_XStatic_ConnectsToTheMemberNotOnlyTheType()
    {
        var assembly = _graph.CreateAssembly("App");
        var view = CreateType(assembly, "App.Views", "MainWindow");
        var strings = CreateType(assembly, "App.Resources", "Strings");
        var caption = _graph.CreateProperty("Caption", strings);

        WriteXaml("MainWindow.xaml", """
                                     <Window x:Class="App.Views.MainWindow"
                                             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                             xmlns:res="clr-namespace:App.Resources">
                                         <TextBlock Text="{x:Static res:Strings.Caption}" />
                                     </Window>
                                     """);

        XamlGraphLinker.Link(_graph, [new XamlProject(assembly, _directory)]);

        Assert.That(EdgesFrom(view, _graph), Is.EqualTo(new[] { caption.FullName }));
    }

    [Test]
    public void Link_ResourceDictionary_GetsASyntheticClassNamedAfterTheFile()
    {
        var assembly = _graph.CreateAssembly("App");
        var converter = CreateType(assembly, "App.Converters", "BoolToBrush");

        WriteXaml(Path.Combine("Styles", "ButtonStyles.xaml"), """
                                                               <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                                                                   xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                                                   xmlns:conv="clr-namespace:App.Converters">
                                                                   <conv:BoolToBrush x:Key="B" />
                                                               </ResourceDictionary>
                                                               """);

        XamlGraphLinker.Link(_graph, [new XamlProject(assembly, _directory)]);

        var synthetic = assembly.Children.Single(c => c.Name == "Styles.ButtonStyles");

        Assert.Multiple(() =>
        {
            Assert.That(_graph.Nodes.ContainsKey(synthetic.Id), Is.True);
            Assert.That(synthetic.SourceLocations.Single().File, Does.EndWith("ButtonStyles.xaml"));
            Assert.That(EdgesFrom(synthetic, _graph), Is.EqualTo(new[] { "BoolToBrush" }));
        });
    }

    [Test]
    public void Link_FileWithoutAnyClrReference_CreatesNoSyntheticClass()
    {
        var assembly = _graph.CreateAssembly("App");

        WriteXaml("Empty.xaml", """
                                <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
                                """);

        var added = XamlGraphLinker.Link(_graph, [new XamlProject(assembly, _directory)]);

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.Zero);
            Assert.That(assembly.Children, Is.Empty);
        });
    }

    [Test]
    public void Link_AssemblyQualifiedReference_ResolvesIntoTheOtherAssembly()
    {
        var app = _graph.CreateAssembly("App");
        var view = CreateType(app, "App.Views", "MainWindow");

        var library = _graph.CreateAssembly("Sdk");
        var widget = CreateType(library, "Sdk.Controls", "Widget");

        WriteXaml("MainWindow.xaml", """
                                     <Window x:Class="App.Views.MainWindow"
                                             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                             xmlns:sdk="clr-namespace:Sdk.Controls;assembly=Sdk">
                                         <sdk:Widget />
                                     </Window>
                                     """);

        XamlGraphLinker.Link(_graph, [new XamlProject(app, _directory)]);

        Assert.That(EdgesFrom(view, _graph), Is.EqualTo(new[] { "Widget" }));
    }

    [Test]
    public void Link_GeneratedOutputDirectories_AreSkipped()
    {
        var assembly = _graph.CreateAssembly("App");
        var control = CreateType(assembly, "App.Controls", "MyGrid");

        const string xaml = """
                            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                xmlns:c="clr-namespace:App.Controls">
                                <c:MyGrid />
                            </ResourceDictionary>
                            """;
        WriteXaml(Path.Combine("obj", "Copy.xaml"), xaml);
        WriteXaml(Path.Combine("bin", "Copy.xaml"), xaml);

        var added = XamlGraphLinker.Link(_graph, [new XamlProject(assembly, _directory)]);

        Assert.That(added, Is.Zero);
    }

    [Test]
    public void Link_SameTypeUsedTwice_YieldsOneRelationshipWithBothLocations()
    {
        var assembly = _graph.CreateAssembly("App");
        var view = CreateType(assembly, "App.Views", "MainWindow");
        var control = CreateType(assembly, "App.Controls", "MyGrid");

        WriteXaml("MainWindow.xaml", """
                                     <Window x:Class="App.Views.MainWindow"
                                             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                             xmlns:c="clr-namespace:App.Controls">
                                         <c:MyGrid />
                                         <c:MyGrid />
                                     </Window>
                                     """);

        XamlGraphLinker.Link(_graph, [new XamlProject(assembly, _directory)]);

        var relationship = view.Relationships.Single();

        Assert.Multiple(() =>
        {
            Assert.That(relationship.Type, Is.EqualTo(RelationshipType.Uses));
            Assert.That(relationship.SourceLocations, Has.Count.EqualTo(2));
        });
    }
}
