using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     A record's positional parameters declare public properties, and those are code elements like
///     any other. Before that, a positional record was an empty type in the tree and every use of
///     "order.Id" fell back to the type - the long form of the same record produced a different graph.
/// </summary>
[TestFixture]
public class PositionalRecordMemberParseTests
{
    private const string Code = """
                            namespace Demo;

                            public record Money(decimal Amount);

                            public record Order(int Id, Money Total)
                            {
                                public int Doubled => Id * 2;
                            }

                            /// <summary>The same record written out - both must produce the same members.</summary>
                            public record OrderLong
                            {
                                public int Id { get; init; }
                                public Money Total { get; init; }
                            }

                            /// <summary>A positional parameter whose property the record writes out itself.</summary>
                            public record Explicit(int Id)
                            {
                                public int Id { get; init; } = Id;
                            }

                            /// <summary>A class primary constructor declares no member.</summary>
                            public class Service(Money fee)
                            {
                                public decimal Fee => fee.Amount;
                            }

                            public class Consumer
                            {
                                public int Read(Order order)
                                {
                                    return order.Id;
                                }
                            }
                            """;

    [OneTimeSetUp]
    public async Task ParseCode()
    {
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false));
        var result = await parser.ParseSourceAsync(Code);
        _graph = result.CodeGraph;
    }

    private CodeGraph _graph = null!;

    private CodeElement Type(string name)
    {
        return _graph.Nodes.Values.Single(n => n.Name == name && !n.IsExternal && n.Children.Count >= 0
                                               && n.ElementType is CodeElementType.Record or CodeElementType.Class);
    }

    private HashSet<(string, CodeElementType)> MembersOf(string typeName)
    {
        return Type(typeName).Children.Select(c => (c.Name, c.ElementType)).ToHashSet();
    }

    [Test]
    public void PositionalParameters_BecomeProperties()
    {
        Assert.That(MembersOf("Order"), Is.SupersetOf(new[]
        {
            ("Id", CodeElementType.Property),
            ("Total", CodeElementType.Property)
        }));
    }

    [Test]
    public void ThePositionalFormAndTheLongForm_ProduceTheSameMembers()
    {
        // The inconsistency this fixes: the two spellings used to give different graphs.
        Assert.That(MembersOf("Order"), Is.SupersetOf(MembersOf("OrderLong")));
    }

    [Test]
    public void AWrittenOutProperty_IsNotCreatedTwice()
    {
        // "record Explicit(int Id) { public int Id { get; init; } = Id; }" - the member's declaration is
        // the property, not the parameter, so only the ordinary property path may create it.
        var ids = Type("Explicit").Children.Where(c => c.Name == "Id").ToList();

        Assert.That(ids, Has.Count.EqualTo(1));
    }

    [Test]
    public void AClassPrimaryConstructorParameter_DeclaresNoProperty()
    {
        // Only a record turns a positional parameter into a property. "fee" is used in the body, so it
        // is captured state and becomes a Field - see CapturedPrimaryConstructorParameterParseTests.
        Assert.Multiple(() =>
        {
            Assert.That(MembersOf("Service"), Does.Not.Contain(("fee", CodeElementType.Property)));
            Assert.That(MembersOf("Service"), Does.Contain(("fee", CodeElementType.Field)));
        });
    }

    [Test]
    public void APositionalPropertyGetsItsAccessorsLikeAnyOther()
    {
        // CreatePropertyAccessorElements takes the accessors from the symbol, not from syntax, so a
        // positional property is covered exactly like a written-out one.
        var id = _graph.Nodes.Values.Single(n => n.Name == "Id" && n.ElementType == CodeElementType.Property
                                                 && n.Parent?.Name == "Order");

        // An init accessor is called set_Id - that is its metadata name, init is a modreq on it.
        Assert.That(id.Children.Select(c => c.Name), Is.EquivalentTo(new[] { "get_Id", "set_Id" }));
    }

    [Test]
    public void AUseOfAPositionalProperty_ResolvesToTheMemberInsteadOfTheType()
    {
        // Phase 2 needs no change for this - the property carries a normal symbol key, so the existing
        // property path finds it. The read is then routed one level further, to the getter accessor
        // (accessors are always split), same as for any other property.
        var read = _graph.Nodes.Values.Single(n => n.Name == "Read");
        var targets = read.Relationships
            .Select(r => _graph.Nodes[r.TargetId])
            .Select(t => t.FullName)
            .ToList();

        Assert.That(targets, Has.Some.EndsWith("Demo.Order.Id.get_Id"));
    }
}
