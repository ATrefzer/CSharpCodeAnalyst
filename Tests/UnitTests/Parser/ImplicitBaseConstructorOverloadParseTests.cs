using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Which base constructor an unwritten <c>base()</c> means when several can be called without
///     arguments. For a written constructor Roslyn has resolved it; for a primary constructor and for a
///     class without any constructor there is nothing to ask, and the parser ranks the candidates itself.
///     <para>
///         Overloads share the path "Base..ctor", so every constructor calls a marker method of its own
///         and is identified by that.
///     </para>
/// </summary>
[TestFixture]
public class ImplicitBaseConstructorOverloadParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      namespace Demo;

                                      public static class Marker
                                      {
                                          public static void ParamsInt() { }
                                          public static void OptionalInt() { }
                                          public static void ParamsObject() { }
                                          public static void ParamsString() { }
                                          public static void Parameterless() { }
                                          public static void OptionalNextToParameterless() { }
                                      }

                                      /// <summary>Declared in the order that misleads a "first match" rule.</summary>
                                      public class OptionalBeatsParams
                                      {
                                          protected OptionalBeatsParams(params int[] values) { Marker.ParamsInt(); }
                                          protected OptionalBeatsParams(int value = 0) { Marker.OptionalInt(); }
                                      }

                                      public class WrittenA : OptionalBeatsParams { public WrittenA() { } }
                                      public class GeneratedA : OptionalBeatsParams { }
                                      public class PrimaryA(int size) : OptionalBeatsParams { public int Size => size; }

                                      public class TwoParams
                                      {
                                          protected TwoParams(params object[] values) { Marker.ParamsObject(); }
                                          protected TwoParams(params string[] values) { Marker.ParamsString(); }
                                      }

                                      public class WrittenB : TwoParams { public WrittenB() { } }
                                      public class GeneratedB : TwoParams { }

                                      public class ParameterlessBeatsOptional
                                      {
                                          protected ParameterlessBeatsOptional(int value = 0) { Marker.OptionalNextToParameterless(); }
                                          protected ParameterlessBeatsOptional() { Marker.Parameterless(); }
                                      }

                                      public class WrittenC : ParameterlessBeatsOptional { public WrittenC() { } }
                                      public class GeneratedC : ParameterlessBeatsOptional { }
                                      """;

    /// <summary>The elements calling the constructor that calls the given marker method.</summary>
    private string[] CallersOfTheConstructorMarkedWith(string marker)
    {
        var markerMethod = Graph.Nodes.Values.Single(n => PathOf(n) == $"Marker.{marker}");
        var constructor = Graph.Nodes.Values.Single(n => n.Relationships.Any(r => r.Type == RelationshipType.Calls && r.TargetId == markerMethod.Id));

        return Graph.Nodes.Values
            .Where(source => source.Relationships.Any(r => r.Type == RelationshipType.Calls && r.TargetId == constructor.Id))
            .Select(PathOf)
            .Order()
            .ToArray();
    }

    [Test]
    public void AnOptionalParameter_BeatsAParamsArray_InAllThreeForms()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CallersOfTheConstructorMarkedWith("OptionalInt"),
                Is.EqualTo(new[] { "GeneratedA", "PrimaryA..ctor", "WrittenA..ctor" }));
            Assert.That(CallersOfTheConstructorMarkedWith("ParamsInt"), Is.Empty);
        });
    }

    [Test]
    public void AWrittenConstructor_GetsTheCompilersChoiceBetweenTwoParamsArrays()
    {
        // The more specific element type wins - Roslyn's resolution, read from the operation tree.
        Assert.Multiple(() =>
        {
            Assert.That(CallersOfTheConstructorMarkedWith("ParamsString"), Does.Contain("WrittenB..ctor"));
            Assert.That(CallersOfTheConstructorMarkedWith("ParamsObject"), Does.Not.Contain("WrittenB..ctor"));
        });
    }

    [Test]
    public void AGeneratedConstructor_LinksBothParamsArrays()
    {
        // No operation to ask, and the tie-break between two params arrays is not rebuilt: both are linked.
        Assert.Multiple(() =>
        {
            Assert.That(CallersOfTheConstructorMarkedWith("ParamsString"), Does.Contain("GeneratedB"));
            Assert.That(CallersOfTheConstructorMarkedWith("ParamsObject"), Does.Contain("GeneratedB"));
        });
    }

    [Test]
    public void TheParameterlessConstructor_BeatsAnOptionalParameter()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CallersOfTheConstructorMarkedWith("Parameterless"),
                Is.EqualTo(new[] { "GeneratedC", "WrittenC..ctor" }));
            Assert.That(CallersOfTheConstructorMarkedWith("OptionalNextToParameterless"), Is.Empty);
        });
    }
}
