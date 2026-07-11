using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Nested lambdas. The LambdaBodyWalker skips nested lambda expressions entirely (documented as
///     "skipped(!)" in SyntaxWalkerBase), so every dependency inside the inner lambda is lost - it is not
///     even attributed to the containing method. The documented lambda modelling ("Uses" edges for
///     everything the lambda references) applies just as well to the inner lambda.
///     Build passes its lambda to a Delegate parameter on purpose: a Func&lt;Func&lt;Widget&gt;&gt;
///     signature or local would already record "Uses Widget" via the signature walk and mask the gap.
/// </summary>
[TestFixture]
public class NestedLambdaParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      using System;

                                      namespace Demo;

                                      public class Widget
                                      {
                                      }

                                      public class Factory
                                      {
                                          public Func<Func<int>> Provide()
                                          {
                                              return () => () => Compute();
                                          }

                                          public void Build()
                                          {
                                              Run(() => () => new Widget());
                                          }

                                          public int Compute()
                                          {
                                              return 42;
                                          }

                                          private static void Run(Delegate action)
                                          {
                                          }
                                      }
                                      """;

    [Test]
    public void MethodCallInNestedLambda_IsDetectedAsUses()
    {
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Factory.Provide -> Factory.Compute"));
    }

    [Test]
    public void ObjectCreationInNestedLambda_IsDetectedAsUses()
    {
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Factory.Build -> Widget"));
    }
}
