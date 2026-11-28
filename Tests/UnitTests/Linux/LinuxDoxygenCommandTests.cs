using CSharpCodeAnalyst.Linux;
using CSharpCodeAnalyst.CodeGraph.Export;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Importers.Doxygen;

namespace CodeParserTests.UnitTests.Linux;

/// <summary>
///     The Linux command line (<c>CSharpCodeAnalyst.Linux -doxygen</c>). doxygen itself is
///     replaced by prepared XML, so these run without it; <see cref="LinuxDoxygenCommandEndToEndTests" />
///     covers the real process. The point that matters most is the last one: the file the command
///     writes is read back by the application's plain text import without losing anything.
/// </summary>
[TestFixture]
public class LinuxDoxygenCommandTests
{
    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "LinuxDoxygenCommandTests_" + Guid.NewGuid().ToString("N"));
        _sourceDirectory = Path.Combine(_root, "demo");
        Directory.CreateDirectory(_sourceDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string _root = null!;
    private string _sourceDirectory = null!;

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await CommandLine.RunAsync(args, output, error, CancellationToken.None);
        return (exitCode, output.ToString(), error.ToString());
    }

    private DoxygenExportOptions ParseOptions(params string[] args)
    {
        var parsed = DoxygenExportOptions.TryParse(Arguments.Parse(args), out var options, out var problem);
        Assert.That(parsed, Is.True, problem);
        return options!;
    }

    [Test]
    public async Task NoArguments_PrintsTheUsageAndFails()
    {
        var (exitCode, output, _) = await RunAsync();

        Assert.That(exitCode, Is.EqualTo(CommandLine.Failure));
        Assert.That(output, Does.Contain("-doxygen"));
    }

    [Test]
    public async Task Help_PrintsTheUsageAndSucceeds()
    {
        var (exitCode, output, _) = await RunAsync("-help");

        Assert.That(exitCode, Is.EqualTo(CommandLine.Success));
        Assert.That(output, Does.Contain("-source:<dir>"));
    }

    [Test]
    public async Task WithoutTheDoxygenCommand_Fails()
    {
        var (exitCode, _, error) = await RunAsync($"-source:{_sourceDirectory}", "-out:graph.txt");

        Assert.That(exitCode, Is.EqualTo(CommandLine.Failure));
        Assert.That(error, Does.Contain("No command"));
    }

    [TestCase("-source")]
    [TestCase("-out")]
    public async Task ARequiredArgumentIsMissing_Fails(string missing)
    {
        var args = new List<string> { "-doxygen", $"-source:{_sourceDirectory}", $"-out:{Path.Combine(_root, "graph.txt")}" };
        args.RemoveAll(arg => arg.StartsWith(missing + ":", StringComparison.Ordinal));

        var (exitCode, _, error) = await RunAsync(args.ToArray());

        Assert.That(exitCode, Is.EqualTo(CommandLine.Failure));
        Assert.That(error, Does.Contain(missing + ":"));
    }

    [Test]
    public async Task ASourceDirectoryThatDoesNotExist_Fails()
    {
        var (exitCode, _, error) = await RunAsync("-doxygen", $"-source:{Path.Combine(_root, "missing")}", "-out:graph.txt");

        Assert.That(exitCode, Is.EqualTo(CommandLine.Failure));
        Assert.That(error, Does.Contain("does not exist"));
    }

    /// <summary>A typo must not silently fall back to a default on a build agent.</summary>
    [TestCase("-hierachy:directories", "Unknown argument")]
    [TestCase("graph.txt", "Unexpected argument")]
    [TestCase("-language:cobol", "Unknown language")]
    [TestCase("-hierarchy:flat", "Unknown hierarchy")]
    public async Task AnUnknownArgumentOrValue_Fails(string bad, string expectedMessage)
    {
        var (exitCode, _, error) = await RunAsync("-doxygen", $"-source:{_sourceDirectory}", "-out:graph.txt", bad);

        Assert.That(exitCode, Is.EqualTo(CommandLine.Failure));
        Assert.That(error, Does.Contain(expectedMessage));
    }

    [Test]
    public void Defaults_MatchTheImportDialog()
    {
        var options = ParseOptions("-doxygen", $"-source:{_sourceDirectory}", "-out:graph.txt");

        Assert.That(options.Language, Is.EqualTo(DoxygenLanguage.Cpp));
        Assert.That(options.HierarchyMode, Is.EqualTo(DoxygenHierarchyMode.Declared));
        Assert.That(options.ProjectName, Is.EqualTo("demo"));
        Assert.That(options.WorkingDirectory, Is.Null);
    }

    [Test]
    public void ATrailingSeparatorOnTheSource_StillGivesTheDirectoryName()
    {
        var options = ParseOptions("-doxygen", $"-source:{_sourceDirectory}{Path.DirectorySeparatorChar}", "-out:graph.txt");

        Assert.That(options.ProjectName, Is.EqualTo("demo"));
    }

    [TestCase("cpp", DoxygenLanguage.Cpp)]
    [TestCase("C++", DoxygenLanguage.Cpp)]
    [TestCase("Python", DoxygenLanguage.Python)]
    [TestCase("JAVA", DoxygenLanguage.Java)]
    public void Language_IsParsedCaseInsensitively(string value, DoxygenLanguage expected)
    {
        var options = ParseOptions("-doxygen", $"-source:{_sourceDirectory}", "-out:graph.txt", $"-language:{value}");

        Assert.That(options.Language, Is.EqualTo(expected));
    }

    [Test]
    public void ExplicitOptions_AreTakenOver()
    {
        var workDir = Path.Combine(_root, "work");
        var options = ParseOptions("-DOXYGEN", $"-Source:{_sourceDirectory}", "-out:graph.txt", "-hierarchy:Directories",
            "-name:My Project", $"-work-dir:{workDir}");

        Assert.That(options.HierarchyMode, Is.EqualTo(DoxygenHierarchyMode.Directories));
        Assert.That(options.ProjectName, Is.EqualTo("My Project"));
        Assert.That(options.WorkingDirectory, Is.EqualTo(workDir));
        Assert.That(options.SourceDirectory, Is.EqualTo(_sourceDirectory));
    }

    [Test]
    public void TheValueIsSplitAtTheFirstColonOnly()
    {
        var arguments = Arguments.Parse([@"-out:C:\temp\graph.txt", "-flag"]);

        Assert.That(arguments.Get("out"), Is.EqualTo(@"C:\temp\graph.txt"));
        Assert.That(arguments.Has("flag"), Is.True);
    }

    [Test]
    public async Task TheWrittenFile_IsReadBackByThePlainTextImportWithoutLoss()
    {
        var outputFile = Path.Combine(_root, "out", "nested", "graph.txt");
        var options = ParseOptions("-doxygen", $"-source:{_sourceDirectory}", $"-out:{outputFile}", "-name:demo");
        var output = new StringWriter();

        await new DoxygenExportCommand(options, output, WriteSampleXml).ExecuteAsync(CancellationToken.None);

        // The output directory is created on demand.
        Assert.That(File.Exists(outputFile), Is.True);

        // What Import > Plain text does on Windows.
        var imported = CodeGraphSerializer.DeserializeFromFile(outputFile);
        Assert.That(CodeGraphSerializer.Serialize(imported), Is.EqualTo(File.ReadAllText(outputFile)));

        Assert.That(imported.Nodes.Values.Select(n => n.FullName), Is.SupersetOf(new[] { "demo.app.Base", "demo.app.Widget", "demo.app.Widget.draw", "demo.app.helper" }));
        var relationships = imported.GetAllRelationships()
            .Select(r => $"{imported.Nodes[r.SourceId].FullName} {r.Type} {imported.Nodes[r.TargetId].FullName}")
            .ToList();
        Assert.That(relationships, Does.Contain($"demo.app.Widget {RelationshipType.Inherits} demo.app.Base"));
        Assert.That(relationships, Does.Contain($"demo.app.Widget.draw {RelationshipType.Calls} demo.app.helper"));

        var draw = imported.Nodes.Values.Single(n => n.FullName == "demo.app.Widget.draw");
        Assert.That(draw.SourceLocations.Select(l => l.Line), Does.Contain(9));

        Assert.That(output.ToString(), Does.Contain("Wrote"));
    }

    [Test]
    public async Task DirectoryHierarchy_IsRelativeToTheSourceDirectory()
    {
        var outputFile = Path.Combine(_root, "graph.txt");
        var options = ParseOptions("-doxygen", $"-source:{_sourceDirectory}", $"-out:{outputFile}", "-hierarchy:directories");

        await new DoxygenExportCommand(options, TextWriter.Null, WriteSampleXml).ExecuteAsync(CancellationToken.None);

        var imported = CodeGraphSerializer.DeserializeFromFile(outputFile);
        Assert.That(imported.Nodes.Values.Select(n => n.FullName), Does.Contain("demo.core.widgets.Widget"));
    }

    /// <summary>
    ///     Found on Linux: the default name of a source directory "My Project" went into the file as
    ///     "name=My Project", and the plain text import - which splits at whitespace - read the root
    ///     back as "My" and every full name below it as "My". The command strips the whitespace,
    ///     the same rule the converter applies to all other names.
    /// </summary>
    [Test]
    public async Task AProjectNameWithSpaces_SurvivesThePlainTextImport()
    {
        var sourceDirectory = Path.Combine(_root, "My Project");
        Directory.CreateDirectory(sourceDirectory);
        var outputFile = Path.Combine(_root, "graph.txt");
        var options = ParseOptions("-doxygen", $"-source:{sourceDirectory}", $"-out:{outputFile}");
        var output = new StringWriter();

        await new DoxygenExportCommand(options, output, WriteSampleXml).ExecuteAsync(CancellationToken.None);

        var imported = CodeGraphSerializer.DeserializeFromFile(outputFile);
        Assert.That(CodeGraphSerializer.Serialize(imported), Is.EqualTo(File.ReadAllText(outputFile)));
        Assert.That(imported.Nodes.Values.Single(n => n.ElementType == CodeElementType.Assembly).Name, Is.EqualTo("MyProject"));
        Assert.That(imported.Nodes.Values.Select(n => n.FullName), Does.Contain("MyProject.app.Widget.draw"));
        Assert.That(output.ToString(), Does.Contain("Using 'MyProject'"));
    }

    [TestCase("My Project", "MyProject")]
    [TestCase("Tabs\tand  spaces", "Tabsandspaces")]
    [TestCase("Plain", "Plain")]
    public void ToPlainTextName_RemovesAllWhitespace(string name, string expected)
    {
        Assert.That(DoxygenExportCommand.ToPlainTextName(name), Is.EqualTo(expected));
    }

    [Test]
    public async Task WithoutWorkDir_TheTemporaryDirectoryIsDeleted()
    {
        var options = ParseOptions("-doxygen", $"-source:{_sourceDirectory}", $"-out:{Path.Combine(_root, "graph.txt")}");
        string? usedWorkingDirectory = null;

        await new DoxygenExportCommand(options, TextWriter.Null, (o, workingDirectory, ct) =>
        {
            usedWorkingDirectory = workingDirectory;
            return WriteSampleXml(o, workingDirectory, ct);
        }).ExecuteAsync(CancellationToken.None);

        Assert.That(usedWorkingDirectory, Is.Not.Null);
        Assert.That(Directory.Exists(usedWorkingDirectory), Is.False);
    }

    [Test]
    public async Task WithWorkDir_TheDoxygenOutputIsKept()
    {
        var workDir = Path.Combine(_root, "work");
        var options = ParseOptions("-doxygen", $"-source:{_sourceDirectory}", $"-out:{Path.Combine(_root, "graph.txt")}", $"-work-dir:{workDir}");

        await new DoxygenExportCommand(options, TextWriter.Null, WriteSampleXml).ExecuteAsync(CancellationToken.None);

        Assert.That(File.Exists(Path.Combine(workDir, "xml", "index.xml")), Is.True);
    }

    [Test]
    public void AFailingRun_StillDeletesTheTemporaryDirectory()
    {
        var options = ParseOptions("-doxygen", $"-source:{_sourceDirectory}", $"-out:{Path.Combine(_root, "graph.txt")}");
        string? usedWorkingDirectory = null;

        var command = new DoxygenExportCommand(options, TextWriter.Null, (_, workingDirectory, _) =>
        {
            usedWorkingDirectory = workingDirectory;
            Directory.CreateDirectory(workingDirectory);
            throw new InvalidOperationException("doxygen exited with code 1.");
        });

        Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync(CancellationToken.None));
        Assert.That(Directory.Exists(usedWorkingDirectory), Is.False);
        Assert.That(File.Exists(Path.Combine(_root, "graph.txt")), Is.False);
    }

    /// <summary>
    ///     Stands in for doxygen: a namespace function, a base class and a class whose method calls
    ///     the function. The locations are below the source directory, with forward slashes as
    ///     doxygen writes them, so the directory mode has something to derive.
    /// </summary>
    private static Task<string> WriteSampleXml(DoxygenExportOptions options, string workingDirectory, CancellationToken cancellationToken)
    {
        var xmlDirectory = Path.Combine(workingDirectory, "xml");
        Directory.CreateDirectory(xmlDirectory);
        var source = options.SourceDirectory.Replace('\\', '/');

        File.WriteAllText(Path.Combine(xmlDirectory, "index.xml"), """
            <doxygenindex version="1.10.0">
              <compound refid="namespaceapp" kind="namespace"><name>app</name></compound>
              <compound refid="classapp_1_1Base" kind="class"><name>app::Base</name></compound>
              <compound refid="classapp_1_1Widget" kind="class"><name>app::Widget</name></compound>
            </doxygenindex>
            """);

        File.WriteAllText(Path.Combine(xmlDirectory, "namespaceapp.xml"), $"""
            <doxygen>
              <compounddef id="namespaceapp" kind="namespace" language="C++">
                <compoundname>app</compoundname>
                <sectiondef kind="func">
                  <memberdef kind="function" id="namespaceapp_1ahelper">
                    <type>void</type>
                    <name>helper</name>
                    <location file="{source}/core/util/helper.cpp" line="7" column="1"/>
                  </memberdef>
                </sectiondef>
              </compounddef>
            </doxygen>
            """);

        File.WriteAllText(Path.Combine(xmlDirectory, "classapp_1_1Base.xml"), $"""
            <doxygen>
              <compounddef id="classapp_1_1Base" kind="class" language="C++">
                <compoundname>app::Base</compoundname>
                <location file="{source}/core/widgets/base.h" line="4" column="1"/>
              </compounddef>
            </doxygen>
            """);

        File.WriteAllText(Path.Combine(xmlDirectory, "classapp_1_1Widget.xml"), $"""
            <doxygen>
              <compounddef id="classapp_1_1Widget" kind="class" language="C++">
                <compoundname>app::Widget</compoundname>
                <basecompoundref refid="classapp_1_1Base" prot="public">app::Base</basecompoundref>
                <sectiondef kind="public-func">
                  <memberdef kind="function" id="classapp_1_1Widget_1adraw">
                    <type>void</type>
                    <name>draw</name>
                    <location file="{source}/core/widgets/widget.cpp" line="9" column="3"/>
                    <references refid="namespaceapp_1ahelper">app::helper</references>
                  </memberdef>
                </sectiondef>
                <location file="{source}/core/widgets/widget.h" line="7" column="1"/>
              </compounddef>
            </doxygen>
            """);

        return Task.FromResult(xmlDirectory);
    }
}

/// <summary>
///     The command line with the real doxygen process. Explicit for the same reason as
///     <c>DoxygenRunnerTests</c>: the build agent has no doxygen.
/// </summary>
[TestFixture]
[Explicit("Needs doxygen on the PATH.")]
public class LinuxDoxygenCommandEndToEndTests
{
    [Test]
    public async Task RunsDoxygenAndWritesAGraph()
    {
        if (!DoxygenRunner.IsDoxygenAvailable())
        {
            Assert.Ignore("doxygen was not found on the PATH.");
        }

        var root = Path.Combine(Path.GetTempPath(), "Linux Doxygen Command Tests_" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "shapes.h"), """
            namespace app {
            class Shape { public: virtual void draw(); };
            class Circle : public Shape { public: void draw(); };
            }
            """);

        try
        {
            var outputFile = Path.Combine(root, "graph.txt");
            var error = new StringWriter();
            var exitCode = await CommandLine.RunAsync(["-doxygen", $"-source:{source}", $"-out:{outputFile}"], TextWriter.Null, error,
                CancellationToken.None);

            Assert.That(exitCode, Is.EqualTo(CommandLine.Success), error.ToString());
            var graph = CodeGraphSerializer.DeserializeFromFile(outputFile);
            Assert.That(graph.Nodes.Values.Select(n => n.FullName), Is.SupersetOf(new[] { "src.app.Shape", "src.app.Circle" }));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
