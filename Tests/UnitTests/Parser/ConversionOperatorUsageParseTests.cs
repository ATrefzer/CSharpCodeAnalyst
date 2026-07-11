using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Usage of user-defined conversion operators. An implicit conversion ("Celsius c = 21.5;") and an
///     explicit cast ("(double)c") both invoke an operator method (op_Implicit / op_Explicit), but neither
///     records an edge to it: the cast only captures the target type, the implicit conversion nothing at
///     all. Like ordinary operators, internal conversions therefore always look unused.
/// </summary>
[TestFixture]
public class ConversionOperatorUsageParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      namespace Demo;

                                      public class Celsius
                                      {
                                          public static implicit operator Celsius(double value)
                                          {
                                              return new Celsius();
                                          }

                                          public static explicit operator double(Celsius value)
                                          {
                                              return 0.0;
                                          }
                                      }

                                      public class Weather
                                      {
                                          public Celsius FromNumber()
                                          {
                                              Celsius celsius = 21.5;
                                              return celsius;
                                          }

                                          public double ToNumber(Celsius celsius)
                                          {
                                              return (double)celsius;
                                          }
                                      }
                                      """;

    [Test]
    public void ConversionOperatorElements_AreDetected()
    {
        // Premise guard (green): the declaration side works, only the usage side is missing.
        var methods = PathsOf(CodeElementType.Method);
        Assert.Multiple(() =>
        {
            Assert.That(methods, Does.Contain("Celsius.op_Implicit"));
            Assert.That(methods, Does.Contain("Celsius.op_Explicit"));
        });
    }

    [Test]
    public void ImplicitConversionUsage_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Weather.FromNumber -> Celsius.op_Implicit"));
    }

    [Test]
    public void ExplicitConversionUsage_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Weather.ToNumber -> Celsius.op_Explicit"));
    }
}
