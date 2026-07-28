using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Importers.Doxygen;

namespace CodeParserTests.UnitTests.Import;

/// <summary>
///     Runs doxygen itself over a handful of C++ lines and converts the result. Explicit because it
///     needs doxygen on the PATH, which the build agent does not have - DoxygenXmlConverterTests
///     covers the conversion from synthetic XML instead.
///     This is the only test that exercises the process start, the generated Doxyfile and the exit
///     code handling; everything else about the doxygen import is tested against fixed XML.
/// </summary>
[TestFixture]
[Explicit("Needs doxygen on the PATH.")]
public class DoxygenRunnerTests
{
    [SetUp]
    public void SetUp()
    {
        if (!DoxygenRunner.IsDoxygenAvailable())
        {
            Assert.Ignore("doxygen was not found on the PATH.");
        }

        // A path with a space: the arguments must be quoted by the process start, not by hand.
        _root = Path.Combine(Path.GetTempPath(), "Doxygen Runner Tests_" + Guid.NewGuid().ToString("N"));
        _sourceDirectory = Path.Combine(_root, "src");
        _workingDirectory = Path.Combine(_root, "work");
        Directory.CreateDirectory(_sourceDirectory);

        File.WriteAllText(Path.Combine(_sourceDirectory, "shapes.h"), """
            namespace app {

            class Shape {
            public:
                virtual void draw();
            };

            class Circle : public Shape {
            public:
                void draw();
                double radius_;
            };

            }
            """);

        var javaPackage = Path.Combine(_sourceDirectory, "com", "example");
        Directory.CreateDirectory(javaPackage);
        File.WriteAllText(Path.Combine(javaPackage, "Kind.java"), """
            package com.example;

            public enum Kind {
                ROUND;

                public boolean isRound() {
                    return this == ROUND;
                }
            }
            """);
        File.WriteAllText(Path.Combine(javaPackage, "Box.java"), """
            package com.example;

            import java.util.ArrayList;

            public class Box {
                private Kind kind = Kind.ROUND;

                public boolean check() {
                    return this.kind.isRound();
                }
            }
            """);
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
    private string _workingDirectory = null!;

    [Test]
    public void FindsDoxygen()
    {
        Assert.That(DoxygenRunner.IsDoxygenAvailable(), Is.True);
    }

    [Test]
    public async Task ProducesAGraphFromRealSources()
    {
        var xmlDirectory = await DoxygenRunner.RunAsync(_sourceDirectory, _workingDirectory, "DemoCpp", DoxygenLanguage.Cpp);

        Assert.That(File.Exists(Path.Combine(xmlDirectory, "index.xml")), Is.True, "doxygen wrote no index.xml.");

        var graph = new DoxygenXmlConverter().Convert(xmlDirectory, "DemoCpp");
        var byFullName = graph.Nodes.Values.ToDictionary(e => e.FullName, e => e);

        Assert.Multiple(() =>
        {
            Assert.That(byFullName.ContainsKey("DemoCpp.app"), Is.True);
            Assert.That(byFullName["DemoCpp.app.Shape"].ElementType, Is.EqualTo(CodeElementType.Class));
            Assert.That(byFullName["DemoCpp.app.Circle.radius_"].ElementType, Is.EqualTo(CodeElementType.Field));

            var inherits = graph.GetAllRelationships()
                .Where(r => r.Type == RelationshipType.Inherits)
                .Select(r => (graph.Nodes[r.SourceId].FullName, graph.Nodes[r.TargetId].FullName));
            Assert.That(inherits, Does.Contain(("DemoCpp.app.Circle", "DemoCpp.app.Shape")));
        });
    }

    /// <summary>
    ///     The same for Java, where the interesting part is the enum: doxygen reports it as a
    ///     compound of its own, unlike the C++ enum, which is a member of its scope.
    ///     The C++ sources in the same directory must not appear - the language decides the file
    ///     patterns.
    /// </summary>
    [Test]
    public async Task ProducesAGraphFromRealJavaSources()
    {
        var xmlDirectory = await DoxygenRunner.RunAsync(_sourceDirectory, _workingDirectory, "DemoJava", DoxygenLanguage.Java);

        var graph = new DoxygenXmlConverter().Convert(xmlDirectory, "DemoJava");
        var byFullName = graph.Nodes.Values.ToDictionary(e => e.FullName, e => e);

        Assert.Multiple(() =>
        {
            Assert.That(byFullName.ContainsKey("DemoJava.com.example"), Is.True, "Package hierarchy is missing.");
            Assert.That(byFullName["DemoJava.com.example.Kind"].ElementType, Is.EqualTo(CodeElementType.Enum));
            Assert.That(byFullName["DemoJava.com.example.Kind.isRound"].ElementType, Is.EqualTo(CodeElementType.Method));
            Assert.That(byFullName["DemoJava.com.example.Box.kind"].ElementType, Is.EqualTo(CodeElementType.Field));

            // C++ sources in the same directory are not part of a Java import.
            Assert.That(byFullName.Keys, Has.No.Member("DemoJava.app.Shape"));

            // The package doxygen only saw in an import must not become a namespace.
            Assert.That(byFullName.Keys.Where(key => key.StartsWith("DemoJava.java")), Is.Empty);

            var uses = graph.GetAllRelationships()
                .Where(r => r.Type == RelationshipType.Uses)
                .Select(r => (graph.Nodes[r.SourceId].FullName, graph.Nodes[r.TargetId].FullName));
            Assert.That(uses, Does.Contain(("DemoJava.com.example.Box.kind", "DemoJava.com.example.Kind")));
        });
    }

    /// <summary>
    ///     Pins a behaviour that is easy to mistake for a bug: pointed at a directory with nothing to
    ///     parse, doxygen exits successfully and writes a valid but empty index.xml, so the import
    ///     yields a graph holding only the artificial assembly node - no error anywhere.
    ///     The import dialog is what keeps this out of the user's way: it refuses a directory that
    ///     does not exist. If that guard is ever relaxed, this is the failure mode to expect.
    /// </summary>
    [Test]
    public async Task YieldsAnEmptyGraphForADirectoryWithoutSources()
    {
        var empty = Path.Combine(_root, "empty");
        Directory.CreateDirectory(empty);

        var xmlDirectory = await DoxygenRunner.RunAsync(empty, _workingDirectory, "DemoCpp", DoxygenLanguage.Cpp);
        var graph = new DoxygenXmlConverter().Convert(xmlDirectory, "DemoCpp");

        Assert.That(graph.Nodes.Values.Select(e => e.FullName), Is.EquivalentTo(new[] { "DemoCpp" }));
    }
}
