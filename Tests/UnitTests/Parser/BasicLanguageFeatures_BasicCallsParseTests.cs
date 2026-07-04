using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Basic intra-class method calls, property access and field access. Migrated from the former
///     Core.BasicLanguageFeatures approval fixture (BasicCalls).
/// </summary>
[TestFixture]
public class BasicLanguageFeatures_BasicCallsParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                namespace Demo;

                                public class BasicCalls
                                {
                                    private string _privateField = "initial";

                                    public BasicCalls()
                                    {
                                        // Constructor calls
                                        InitializeData();
                                        SetProperty("constructor value");
                                    }

                                    public string PublicProperty { get; set; } = "default";

                                    public void TestMethodCalls()
                                    {
                                        // Direct method calls
                                        var result = ProcessData("input");

                                        // Property access
                                        var current = PublicProperty;
                                        PublicProperty = "new value";

                                        // Field access
                                        _privateField = "updated";
                                        var fieldValue = _privateField;

                                        // Static method calls
                                        var length = CalculateLength("test");

                                        // Method chaining
                                        var final = ProcessData("chain")
                                            .ToUpperInvariant()
                                            .Trim();
                                    }

                                    private void InitializeData()
                                    {
                                        _privateField = "initialized";
                                    }

                                    private void SetProperty(string value)
                                    {
                                        PublicProperty = value;
                                    }

                                    private string ProcessData(string input)
                                    {
                                        return $"Processed: {input}";
                                    }

                                    private static int CalculateLength(string input)
                                    {
                                        return input?.Length ?? 0;
                                    }
                                }
                                """;

    [Test]
    public void Classes_AreDetected()
    {
        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(new[] { "BasicCalls" }));
    }

    [Test]
    public void Properties_AreDetected()
    {
        Assert.That(PathsOf(CodeElementType.Property), Is.EquivalentTo(new[] { "BasicCalls.PublicProperty" }));
    }

    [Test]
    public void MethodAndPropertyCalls_AreDetected()
    {
        var expected = new[]
        {
            "BasicCalls..ctor -> BasicCalls.InitializeData",
            "BasicCalls..ctor -> BasicCalls.SetProperty",
            "BasicCalls.SetProperty -> BasicCalls.PublicProperty",
            "BasicCalls.TestMethodCalls -> BasicCalls.CalculateLength",
            "BasicCalls.TestMethodCalls -> BasicCalls.ProcessData",
            "BasicCalls.TestMethodCalls -> BasicCalls.PublicProperty"
        };

        Assert.That(RelsOf(RelationshipType.Calls), Is.EquivalentTo(expected));
    }

    [Test]
    public void FieldAccess_IsDetectedAsUses()
    {
        var expected = new[]
        {
            "BasicCalls.InitializeData -> BasicCalls._privateField",
            "BasicCalls.TestMethodCalls -> BasicCalls._privateField"
        };

        Assert.That(RelsOf(RelationshipType.Uses), Is.EquivalentTo(expected));
    }
}
