using CSharpCodeAnalyst.CodeGraph.Algorithms.Partitioning;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     A captured primary constructor parameter is state of the type, and it has to be a code element -
///     otherwise two methods using the same parameter share nothing in the member graph and the type
///     cohesion metric splits a class that is perfectly cohesive. That false split is what this fixture
///     measures: the C# 12 form and the written-out form of the same class must agree.
/// </summary>
[TestFixture]
public class CapturedPrimaryConstructorParameterParseTests
{
    [OneTimeSetUp]
    public async Task ParseCode()
    {
        const string code = """
                            namespace Demo;

                            public interface ILogger { void Log(string m); }

                            public class Store { }

                            /// <summary>C# 12: the parameter is captured, no field is written.</summary>
                            public class Captured(ILogger logger)
                            {
                                public void Start() { logger.Log("start"); }
                                public void Stop() { logger.Log("stop"); }
                            }

                            /// <summary>The same class written out.</summary>
                            public class Explicit
                            {
                                private readonly ILogger _logger;
                                public Explicit(ILogger logger) { _logger = logger; }
                                public void Start() { _logger.Log("start"); }
                                public void Stop() { _logger.Log("stop"); }
                            }

                            /// <summary>Never used - the compiler captures nothing.</summary>
                            public class Unused(ILogger logger)
                            {
                                public void Work() { }
                            }

                            /// <summary>Used only in a field initializer - the declared field carries the state.</summary>
                            public class Initialized(ILogger logger)
                            {
                                private readonly ILogger _logger = logger;
                                public void Work() { _logger.Log("x"); }
                            }

                            /// <summary>One captured, one only initializing.</summary>
                            public class Mixed(ILogger logger, Store store)
                            {
                                private readonly Store _store = store;
                                public void Work() { logger.Log("x"); }
                            }
                            """;

        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false));
        var result = await parser.ParseSourceAsync(code);
        _graph = result.CodeGraph;
    }

    private CodeGraph _graph = null!;

    private CodeElement Type(string name)
    {
        return _graph.Nodes.Values.Single(n => n.Name == name && n.ElementType == CodeElementType.Class && !n.IsExternal);
    }

    private string[] FieldsOf(string typeName)
    {
        return Type(typeName).Children
            .Where(c => c.ElementType == CodeElementType.Field)
            .Select(c => c.Name)
            .ToArray();
    }

    private int PartitionCountOf(string typeName)
    {
        return CodeElementPartitioner.GetPartitions(_graph, Type(typeName), PartitionOptions.Cohesion).Count;
    }

    [Test]
    public void TheCapturedParameter_IsAField()
    {
        Assert.That(FieldsOf("Captured"), Is.EquivalentTo(new[] { "logger" }));
    }

    [Test]
    public void AParameterThatIsNeverUsed_IsNotAField()
    {
        // The compiler emits no storage for it, so neither do we - a member that does not exist would
        // show up in the tree and as detached state in the cohesion view.
        Assert.That(FieldsOf("Unused"), Is.Empty);
    }

    [Test]
    public void AParameterUsedOnlyInAFieldInitializer_IsNotAField()
    {
        // "_logger" already carries the state; a second element for the same thing would split the
        // members that use it from the ones that use the parameter.
        Assert.That(FieldsOf("Initialized"), Is.EquivalentTo(new[] { "_logger" }));
    }

    [Test]
    public void OnlyTheCapturedOneOfSeveralParameters_IsAField()
    {
        Assert.That(FieldsOf("Mixed"), Is.EquivalentTo(new[] { "logger", "_store" }));
    }

    [Test]
    public void MethodsUsingTheParameter_PointAtThatField()
    {
        var field = Type("Captured").Children.Single(c => c.Name == "logger");
        var users = _graph.Nodes.Values
            .Where(n => n.Relationships.Any(r => r.TargetId == field.Id && r.Type == RelationshipType.Uses))
            .Select(n => n.Name)
            .ToArray();

        // Without this half the element would be an orphan and the split below would remain.
        Assert.That(users, Is.EquivalentTo(new[] { "Start", "Stop" }));
    }

    [Test]
    public void TheCapturedFieldCarriesItsOwnType()
    {
        var field = Type("Captured").Children.Single(c => c.Name == "logger");
        var targets = field.Relationships.Select(r => _graph.Nodes[r.TargetId].Name).ToArray();

        Assert.That(targets, Does.Contain("ILogger"));
    }

    [Test]
    public void TheCSharp12FormAndTheWrittenOutForm_AgreeOnCohesion()
    {
        // The acceptance criterion: the same class, two spellings, one verdict. Before this both the
        // members and the partition count differed.
        Assert.Multiple(() =>
        {
            Assert.That(PartitionCountOf("Captured"), Is.EqualTo(1));
            Assert.That(PartitionCountOf("Explicit"), Is.EqualTo(1));
        });
    }
}
