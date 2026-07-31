using CSharpCodeAnalyst.CodeParser.Xaml;

namespace CodeParserTests.UnitTests.Xaml;

[TestFixture]
public class XamlReferenceExtractorTests
{
    private static string[] TypeRefs(string xaml)
    {
        return XamlReferenceExtractor.Extract(xaml).References
            .Where(r => r.MemberName is null)
            .Select(r => r.TypeFullName)
            .Distinct()
            .ToArray();
    }

    private static string[] MemberRefs(string xaml)
    {
        return XamlReferenceExtractor.Extract(xaml).References
            .Where(r => r.MemberName is not null)
            .Select(r => $"{r.TypeFullName}.{r.MemberName}")
            .Distinct()
            .ToArray();
    }

    [Test]
    public void Extract_ElementTag_IsResolvedThroughTheXmlnsPrefix()
    {
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                    xmlns:grid="clr-namespace:App.Shared.DynamicDataGrid">
                                <grid:DynamicDataGrid />
                            </Window>
                            """;

        Assert.That(TypeRefs(xaml), Is.EqualTo(new[] { "App.Shared.DynamicDataGrid.DynamicDataGrid" }));
    }

    [Test]
    public void Extract_ElementTagInsideDataTemplate_IsFoundToo()
    {
        // The case that started this: a named control inside a template gets no generated field, so the
        // element tag is the only trace of the type.
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                    xmlns:grid="clr-namespace:App.Grids">
                                <Window.Resources>
                                    <DataTemplate x:Key="T">
                                        <grid:DynamicDataGrid x:Name="_dynamicDataGrid" />
                                    </DataTemplate>
                                </Window.Resources>
                            </Window>
                            """;

        Assert.That(TypeRefs(xaml), Is.EqualTo(new[] { "App.Grids.DynamicDataGrid" }));
    }

    [Test]
    public void Extract_XStatic_YieldsTypeAndMember()
    {
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                    xmlns:res="clr-namespace:App.Resources">
                                <TextBlock Text="{x:Static res:Strings.Close_Button}" />
                            </Window>
                            """;

        Assert.That(MemberRefs(xaml), Is.EqualTo(new[] { "App.Resources.Strings.Close_Button" }));
    }

    [Test]
    public void Extract_NestedMarkupExtension_IsFound()
    {
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                    xmlns:res="clr-namespace:App.Resources">
                                <TextBlock Text="{Binding Name, FallbackValue={x:Static res:Strings.Fallback}}" />
                            </Window>
                            """;

        Assert.That(MemberRefs(xaml), Is.EqualTo(new[] { "App.Resources.Strings.Fallback" }));
    }

    [Test]
    public void Extract_ObjectElement_IsMarkedAsInstantiation()
    {
        // XAML creates the object here, so the constructor runs - the linker needs to know.
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                    xmlns:local="clr-namespace:App.Views">
                                <local:MyControl />
                            </Window>
                            """;

        Assert.That(XamlReferenceExtractor.Extract(xaml).References.Single().IsInstantiation, Is.True);
    }

    [Test]
    public void Extract_PropertyElementSyntaxAndXType_AreNoInstantiation()
    {
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                    xmlns:local="clr-namespace:App.Views">
                                <Style TargetType="{x:Type local:Other}" />
                                <local:MyControl.Items />
                            </Window>
                            """;

        Assert.That(XamlReferenceExtractor.Extract(xaml).References.Select(r => r.IsInstantiation),
            Is.All.False);
    }

    [Test]
    public void Extract_XType_YieldsTypeOnly()
    {
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                    xmlns:local="clr-namespace:App.Views">
                                <Style TargetType="{x:Type local:MyControl}" />
                            </Window>
                            """;

        Assert.That(TypeRefs(xaml), Is.EqualTo(new[] { "App.Views.MyControl" }));
    }

    [Test]
    public void Extract_PropertyElementSyntax_ReferencesTheTypeNotTheProperty()
    {
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                    xmlns:local="clr-namespace:App.Views">
                                <local:MyControl.Items>
                                    <sys:String xmlns:sys="clr-namespace:System;assembly=mscorlib">x</sys:String>
                                </local:MyControl.Items>
                            </Window>
                            """;

        Assert.That(TypeRefs(xaml), Does.Contain("App.Views.MyControl"));
    }

    [Test]
    public void Extract_AssemblyQualifiedXmlns_KeepsTheAssemblyName()
    {
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                    xmlns:other="clr-namespace:Other.Lib;assembly=Other">
                                <other:Widget />
                            </Window>
                            """;

        var reference = XamlReferenceExtractor.Extract(xaml).References.Single();

        Assert.Multiple(() =>
        {
            Assert.That(reference.TypeFullName, Is.EqualTo("Other.Lib.Widget"));
            Assert.That(reference.AssemblyName, Is.EqualTo("Other"));
        });
    }

    [Test]
    public void Extract_FrameworkNamespaces_AreIgnored()
    {
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                                <Button x:Name="Go" Click="OnGo" Content="Go" />
                            </Window>
                            """;

        Assert.That(XamlReferenceExtractor.Extract(xaml).References, Is.Empty);
    }

    [Test]
    public void Extract_BindingPath_IsNotCollected()
    {
        // Deliberate: without the DataContext this is a bare name, and matching it by name would
        // suppress far more than it explains.
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                                <TextBlock Text="{Binding BoundOnlyInXaml}" />
                            </Window>
                            """;

        Assert.That(XamlReferenceExtractor.Extract(xaml).References, Is.Empty);
    }

    [Test]
    public void Extract_CodeBehindClass_IsTakenFromXClass()
    {
        const string xaml = """
                            <Window x:Class="App.Views.MainWindow"
                                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
                            """;

        Assert.That(XamlReferenceExtractor.Extract(xaml).CodeBehindClass, Is.EqualTo("App.Views.MainWindow"));
    }

    [Test]
    public void Extract_ResourceDictionaryWithoutCodeBehind_HasNoClass()
    {
        const string xaml = """
                            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                xmlns:conv="clr-namespace:App.Converters">
                                <conv:BoolToBrush x:Key="B" />
                            </ResourceDictionary>
                            """;

        var result = XamlReferenceExtractor.Extract(xaml);

        Assert.Multiple(() =>
        {
            Assert.That(result.CodeBehindClass, Is.Null);
            Assert.That(result.References.Select(r => r.TypeFullName),
                Is.EqualTo(new[] { "App.Converters.BoolToBrush" }));
        });
    }

    [Test]
    public void Extract_MalformedXaml_ReturnsNothingInsteadOfThrowing()
    {
        Assert.That(XamlReferenceExtractor.Extract("<Window").References, Is.Empty);
    }

    [Test]
    public void Extract_AliasedXamlPrefix_IsResolvedNotAssumed()
    {
        // The "x" prefix is only a convention. Here the XAML language namespace is bound to "xaml".
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:xaml="http://schemas.microsoft.com/winfx/2006/xaml"
                                    xmlns:res="clr-namespace:App.Resources">
                                <TextBlock Text="{xaml:Static res:Strings.Caption}" />
                            </Window>
                            """;

        Assert.That(MemberRefs(xaml), Is.EqualTo(new[] { "App.Resources.Strings.Caption" }));
    }

    [Test]
    public void Extract_ForeignPrefixNamedX_IsNotMistakenForXaml()
    {
        const string xaml = """
                            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="clr-namespace:App.NotXaml"
                                    xmlns:res="clr-namespace:App.Resources">
                                <TextBlock Text="{x:Static res:Strings.Caption}" />
                            </Window>
                            """;

        Assert.That(MemberRefs(xaml), Is.Empty);
    }
}
