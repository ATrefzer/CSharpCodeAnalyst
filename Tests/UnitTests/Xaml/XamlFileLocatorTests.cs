using CSharpCodeAnalyst.CodeParser.Parser;
using CSharpCodeAnalyst.CodeParser.Xaml;

namespace CodeParserTests.UnitTests.Xaml;

[TestFixture]
public class XamlFileLocatorTests
{
    [OneTimeSetUp]
    public void FixtureSetup()
    {
        // Evaluating a project file needs MSBuild. Another fixture may have registered it already, which
        // is harmless.
        try
        {
            Initializer.InitializeMsBuildLocator();
        }
        catch
        {
            // already registered
        }
    }

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "XamlLocatorTests", Guid.NewGuid().ToString("N"));
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

    private string _directory = null!;

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private string[] Locate(string? projectFileName)
    {
        using var locator = new XamlFileLocator();
        return locator.Locate(projectFileName is null ? null : Path.Combine(_directory, projectFileName), _directory)
            .Select(file => Path.GetRelativePath(_directory, file))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>The whole point of asking MSBuild: a file on disk that the project does not contain.</summary>
    [Test]
    public void Locate_FileRemovedFromTheProject_IsNotReturned()
    {
        Write("App.csproj", """
                            <Project Sdk="Microsoft.NET.Sdk">
                              <PropertyGroup>
                                <TargetFramework>net10.0-windows</TargetFramework>
                                <UseWPF>true</UseWPF>
                              </PropertyGroup>
                              <ItemGroup>
                                <Page Remove="Old\Excluded.xaml" />
                              </ItemGroup>
                            </Project>
                            """);
        Write("Included.xaml", "<ResourceDictionary />");
        Write(Path.Combine("Old", "Excluded.xaml"), "<ResourceDictionary />");

        Assert.That(Locate("App.csproj"), Is.EqualTo(new[] { "Included.xaml" }));
    }

    /// <summary>
    ///     The SDK contributes a couple of dozen PropertyPageSchema items pointing into the dotnet
    ///     installation. They are XAML files, they are evaluated items, and they are none of our business.
    /// </summary>
    [Test]
    public void Locate_SdkOwnedXamlItems_AreNotReturned()
    {
        Write("App.csproj", """
                            <Project Sdk="Microsoft.NET.Sdk">
                              <PropertyGroup>
                                <TargetFramework>net10.0-windows</TargetFramework>
                                <UseWPF>true</UseWPF>
                              </PropertyGroup>
                            </Project>
                            """);
        Write("Included.xaml", "<ResourceDictionary />");

        Assert.That(Locate("App.csproj"), Is.EqualTo(new[] { "Included.xaml" }));
    }

    /// <summary>Without a project file - and for one that cannot be evaluated - only the scan is left.</summary>
    [Test]
    public void Locate_WithoutAnEvaluableProject_FallsBackToTheDirectoryScan()
    {
        Write("Broken.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><NotClosed>");
        Write("Included.xaml", "<ResourceDictionary />");
        Write(Path.Combine("Old", "Excluded.xaml"), "<ResourceDictionary />");

        var expected = new[] { "Included.xaml", Path.Combine("Old", "Excluded.xaml") };

        Assert.Multiple(() =>
        {
            Assert.That(Locate(null), Is.EqualTo(expected));
            Assert.That(Locate("Broken.csproj"), Is.EqualTo(expected));
        });
    }

    [Test]
    public void EnumerateDirectory_SkipsGeneratedOutputDirectories()
    {
        Write("MainWindow.xaml", "<ResourceDictionary />");
        Write(Path.Combine("obj", "Copy.xaml"), "<ResourceDictionary />");
        Write(Path.Combine("bin", "Copy.xaml"), "<ResourceDictionary />");

        Assert.That(XamlFileLocator.EnumerateDirectory(_directory).Select(Path.GetFileName),
            Is.EqualTo(new[] { "MainWindow.xaml" }));
    }

    [Test]
    public void EnumerateDirectory_MissingDirectory_IsEmpty()
    {
        Assert.That(XamlFileLocator.EnumerateDirectory(Path.Combine(_directory, "does-not-exist")), Is.Empty);
    }
}
