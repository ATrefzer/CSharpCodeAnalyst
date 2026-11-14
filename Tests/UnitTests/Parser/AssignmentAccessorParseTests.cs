using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class AssignmentAccessorParseTests
{
    [TestCase("(P, Q) = (1, 2);", "set_P,set_Q", false)]
    [TestCase("(P, (Q, this[0])) = (1, (2, 3));", "set_P,set_Q,set_Item", false)]
    [TestCase("(P, Q) = (Q, P);", "get_P,set_P,get_Q,set_Q", false)]
    [TestCase("(Receiver.P, Q) = (1, 2);", "get_Receiver,set_P,set_Q", false)]
    [TestCase("Pair = (P, Q);", "get_P,get_Q", false)]
    [TestCase("Receiver.P = 1;", "get_Receiver,set_P", false)]
    [TestCase("Receiver?.P = 1;", "get_Receiver,set_P", false)]
    [TestCase("Receiver?.P += 1;", "get_Receiver,get_P,set_P", false)]
    [TestCase("Receiver?.Maybe ??= 1;", "get_Receiver,get_Maybe,set_Maybe", false)]
    [TestCase("Receiver?[0] = 1;", "get_Receiver,set_Item", false)]
    [TestCase("Receiver?[P] += Q;", "get_Receiver,get_Item,set_Item,get_P,get_Q", false)]
    [TestCase("Receiver?.Receiver.P = 1;", "get_Receiver,set_P", false)]
    [TestCase("Receiver?.Receiver?.P = 1;", "get_Receiver,set_P", false)]
    [TestCase("P = Receiver?.P ?? 0;", "get_Receiver,get_P,set_P", false)]
    [TestCase("(P, Q) = (1, 2);", "set_P,set_Q", true)]
    [TestCase("Receiver?.P = 1;", "get_Receiver,set_P", true)]
    [TestCase("Receiver?[P] += Q;", "get_Receiver,get_Item,set_Item,get_P,get_Q", true)]
    public async Task Assignment_UsesCorrectAccessors(string statement, string expectedAccessors, bool inLambda)
    {
        var body = inLambda ? $"System.Action action = () => {{ {statement} }};" : statement;
        var code = $$"""
            class Subject
            {
                public int P { get; set; }
                public int Q { get; set; }
                public int? Maybe { get; set; }
                public Subject Receiver { get; set; }
                public (int, int) Pair;
                public int this[int index] { get => 0; set { } }
                void Execute() { {{body}} }
            }
            """;
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false, false));
        var graph = (await parser.ParseSourceAsync(code)).CodeGraph;
        Assert.That(parser.Diagnostics.Failures, Is.Empty, parser.Diagnostics.FormatFailures());
        var caller = graph.Nodes.Values.Single(n => n.Name == "Execute");
        var relationshipType = inLambda ? RelationshipType.Uses : RelationshipType.Calls;
        var accessorEdges = caller.Relationships
            .Where(r => r.Type == relationshipType && graph.Nodes[r.TargetId].ElementType == CodeElementType.PropertyAccessor)
            .Select(r => graph.Nodes[r.TargetId].Name).ToArray();
        Assert.That(accessorEdges, Is.EquivalentTo(expectedAccessors.Split(',')));
        if (inLambda)
        {
            Assert.That(caller.Relationships.Where(r => r.Type == RelationshipType.Calls), Is.Empty,
                "Deferred lambda bodies contribute Uses, not immediate Calls.");
        }
    }
}
