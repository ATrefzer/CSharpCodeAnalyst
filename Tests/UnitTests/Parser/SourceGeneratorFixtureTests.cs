using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     The source-generator path of the parser, against a real generator.
///     <para>
///         This is the one half of the generated-code handling the in-memory tests cannot reach:
///         <c>GetSourceGeneratedDocumentsAsync</c> needs a real MSBuild project, and the documents it
///         returns are not on disk. The fixture uses <c>[GeneratedRegex]</c>, which ships with the SDK -
///         no package reference, and no restore or build of the fixture is required, because the design
///         time build MSBuildWorkspace performs runs the generator itself.
///     </para>
///     <para>
///         Assertions stay off the generated type names (<c>NumberPattern_0</c>, <c>Utilities</c>): those
///         are SDK implementation details and change with the version. What is asserted is the shape - who
///         is marked, who is not, and that the generated code carries its relationships.
///     </para>
/// </summary>
[TestFixture]
public class SourceGeneratorFixtureTests
{
    [OneTimeSetUp]
    public async Task FixtureSetup()
    {
        // Another fixture may have registered it already, which is harmless.
        try
        {
            Initializer.InitializeMsBuildLocator();
        }
        catch
        {
            // already registered
        }

        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false));
        _graph = (await parser.ParseAsync(@"..\..\..\..\TestSuiteGenerated\TestSuiteGenerated.sln")).CodeGraph;
    }

    private CodeGraph _graph = null!;

    private CodeElement Element(string name)
    {
        return _graph.Nodes.Values.Single(n => n.FullName.EndsWith("." + name, StringComparison.Ordinal));
    }

    private static bool IsHandWritten(CodeElement element)
    {
        return element.SourceLocations.Any(l => l.File is not null && l.File.EndsWith("Validator.cs",
            StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void Parse_GeneratorOutput_ProducesElements()
    {
        // Without this the whole path is silently dead and every assertion below would pass vacuously.
        Assert.That(_graph.Nodes.Values.Count(n => n.IsGenerated), Is.GreaterThan(0));
    }

    [Test]
    public void Parse_PartialTypeCompletedByTheGenerator_IsNotMarked()
    {
        var validator = Element("Validator");

        Assert.Multiple(() =>
        {
            // One declaration here, one in the generator output - so half of it is the user's.
            Assert.That(validator.SourceLocations, Has.Count.EqualTo(2));
            Assert.That(IsHandWritten(validator), Is.True);
            Assert.That(validator.IsGenerated, Is.False);
        });
    }

    [Test]
    public void Parse_PartialMethodImplementedByTheGenerator_IsNotMarked()
    {
        // The signature is the user's, the body is the generator's - the same rule one level down.
        var method = Element("Validator.NumberPattern");

        Assert.Multiple(() =>
        {
            Assert.That(method.SourceLocations, Has.Count.EqualTo(2));
            Assert.That(method.IsGenerated, Is.False);
        });
    }

    [Test]
    public void Parse_NothingHandWritten_IsMarked()
    {
        var marked = _graph.Nodes.Values.Where(n => n.IsGenerated).Where(IsHandWritten).ToList();

        Assert.That(marked, Is.Empty,
            "An element with a declaration in Validator.cs must never be marked as generated.");
    }

    [Test]
    public void Parse_GeneratedCode_CarriesItsRelationships()
    {
        // The reason generated code is never excluded: its edges are the only ones some elements have.
        var isNumber = Element("Validator.IsNumber");
        var numberPattern = Element("Validator.NumberPattern");

        var callsIntoTheGenerator = isNumber.Relationships
            .Any(r => r.TargetId == numberPattern.Id && r.Type == RelationshipType.Calls);

        // The generated Regex subclass is instantiated somewhere in the generator output.
        var edgesInsideTheGeneratedCode = _graph.Nodes.Values
            .Where(n => n.IsGenerated)
            .SelectMany(n => n.Relationships)
            .Count();

        Assert.Multiple(() =>
        {
            Assert.That(callsIntoTheGenerator, Is.True);
            Assert.That(edgesInsideTheGeneratedCode, Is.GreaterThan(0));
        });
    }
}
