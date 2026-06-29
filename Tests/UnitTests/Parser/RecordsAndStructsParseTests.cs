using CodeGraph.Graph;
using CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Records, structs with interfaces, primary constructors, extension methods and partial methods.
///     Self-contained language-feature probe - parsed in isolation via the in-memory parser instead of
///     pulling in the whole TestSuite. (Migrated from the former RecordsAndStructs approval fixture.)
/// </summary>
[TestFixture]
public class RecordsAndStructsParseTests
{
    private const string Code = """
                                using System;

                                namespace Demo;

                                public record RecordA(string Name, RecordB RecordB);

                                public record RecordB(int Value, RecordA RecordA);

                                // Non-record class with a primary constructor. The parameter type is captured as Uses.
                                public class Warehouse { }

                                public class Inventory(Warehouse warehouse) { }

                                public struct StructWithInterface : IComparable<StructWithInterface>
                                {
                                    public int Value { get; init; }

                                    public int CompareTo(StructWithInterface other)
                                    {
                                        return Value.CompareTo(other.Value);
                                    }
                                }

                                public static class Extensions
                                {
                                    public static string Slice(this ExtendedType source, int start, int length)
                                    {
                                        return source.Data.Substring(start, length);
                                    }
                                }

                                public class ExtendedType
                                {
                                    public string Data { get; set; } = string.Empty;
                                }

                                public partial class PartialClient
                                {
                                    partial void OnCreated();

                                    public PartialClient CreateInstance()
                                    {
                                        var instance = new PartialClient();
                                        instance.OnCreated();
                                        return instance;
                                    }
                                }

                                public partial class PartialClient
                                {
                                    partial void OnCreated()
                                    {
                                        // Implementation
                                    }
                                }
                                """;

    private CodeGraph.Graph.CodeGraph _graph = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        // Split off to mirror the original parser configuration of this scenario.
        var parser = new CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false));
        _graph = parser.ParseSourceAsync(Code).GetAwaiter().GetResult();
    }

    [Test]
    public void Classes_AreDetected()
    {
        Assert.That(NamesOf(CodeElementType.Class),
            Is.EquivalentTo(new[] { "ExtendedType", "Extensions", "PartialClient", "Warehouse", "Inventory" }));
    }

    [Test]
    public void Records_AreDetected()
    {
        Assert.That(NamesOf(CodeElementType.Record), Is.EquivalentTo(new[] { "RecordA", "RecordB" }));
    }

    [Test]
    public void Structs_AreDetected()
    {
        Assert.That(NamesOf(CodeElementType.Struct), Is.EquivalentTo(new[] { "StructWithInterface" }));
    }

    [Test]
    public void PrimaryConstructorParameterTypes_AreRecordedAsUses()
    {
        var recordA = Node("RecordA", CodeElementType.Record);
        var recordB = Node("RecordB", CodeElementType.Record);

        Assert.Multiple(() =>
        {
            // Positional record params and class primary-ctor params are Uses on the type.
            Assert.That(Has(recordA, recordB, RelationshipType.Uses), Is.True);
            Assert.That(Has(recordB, recordA, RelationshipType.Uses), Is.True);
            Assert.That(Has(Node("Inventory", CodeElementType.Class), Node("Warehouse", CodeElementType.Class),
                RelationshipType.Uses), Is.True);

            // The synthesized IEquatable<Self> of a record must not create a self-reference.
            Assert.That(Has(recordA, recordA, RelationshipType.Uses), Is.False);
            Assert.That(Has(recordB, recordB, RelationshipType.Uses), Is.False);
        });
    }

    [Test]
    public void MethodCalls_AreDetected()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Has(Node("CompareTo", CodeElementType.Method),
                Node("Value", CodeElementType.Property, "StructWithInterface"), RelationshipType.Calls), Is.True);
            Assert.That(Has(Node("Slice", CodeElementType.Method),
                Node("Data", CodeElementType.Property, "ExtendedType"), RelationshipType.Calls), Is.True);
            Assert.That(Has(Node("CreateInstance", CodeElementType.Method),
                Node("OnCreated", CodeElementType.Method), RelationshipType.Calls), Is.True);
        });
    }

    private string[] NamesOf(CodeElementType type)
    {
        return _graph.Nodes.Values.Where(n => n.ElementType == type).Select(n => n.Name).ToArray();
    }

    private CodeElement Node(string name, CodeElementType type, string? parentName = null)
    {
        return _graph.Nodes.Values.Single(n => n.Name == name && n.ElementType == type &&
                                               (parentName == null || n.Parent?.Name == parentName));
    }

    private static bool Has(CodeElement source, CodeElement target, RelationshipType type)
    {
        return source.Relationships.Any(r => r.TargetId == target.Id && r.Type == type);
    }
}
