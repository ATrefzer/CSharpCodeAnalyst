using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     "stackalloc Sample[n]" in expression position. StackAllocArrayCreationExpressionSyntax is not
///     handled by any walker, so the element type is lost when the stackalloc does not flow through a
///     local declaration (whose declared/inferred type would capture it). The callee's Span&lt;Sample&gt;
///     parameter captures the type on the callee side - asserted as a guard.
/// </summary>
[TestFixture]
public class StackAllocParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      using System;

                                      namespace Demo;

                                      public struct Sample
                                      {
                                          public int Value;
                                      }

                                      public class Buffer
                                      {
                                          public int Total()
                                          {
                                              return Sum(stackalloc Sample[2]);
                                          }

                                          private static int Sum(Span<Sample> span)
                                          {
                                              return span.Length;
                                          }
                                      }
                                      """;

    [Test]
    public void SpanParameterTypeArgument_IsDetected()
    {
        // Premise guard (green): the callee signature captures Sample via Span<Sample>.
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Buffer.Sum -> Sample"));
    }

    [Test]
    public void StackAllocElementType_IsDetectedAsUses()
    {
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Buffer.Total -> Sample"));
    }
}
