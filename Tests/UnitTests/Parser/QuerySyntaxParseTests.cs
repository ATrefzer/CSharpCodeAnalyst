using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     LINQ query syntax ("from ... where ... select"). The compiler translates the clauses into calls to
///     the query-pattern methods (Where/Select/...), but no walker looks at query clauses, so those calls
///     are never recorded - here demonstrated with an internal query provider. In addition, the clause
///     bodies are semantically lambdas: following the documented lambda modelling (corrections-and-updates,
///     "Lambdas") the members referenced inside them should get "Uses" edges, not "Calls" - today they run
///     through the normal method-body walker and get "Calls".
/// </summary>
[TestFixture]
public class QuerySyntaxParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      using System;

                                      namespace Demo;

                                      public class Sequence
                                      {
                                          public Sequence Where(Func<int, bool> predicate)
                                          {
                                              return this;
                                          }

                                          public Sequence Select(Func<int, int> selector)
                                          {
                                              return this;
                                          }
                                      }

                                      public class Report
                                      {
                                          public Sequence Build(Sequence source)
                                          {
                                              return from value in source
                                                     where value > Threshold()
                                                     select Shift(value);
                                          }

                                          public int Threshold()
                                          {
                                              return 10;
                                          }

                                          public int Shift(int value)
                                          {
                                              return value + 1;
                                          }
                                      }
                                      """;

    [Test]
    public void QueryPatternMethods_AreDetectedAsCalls()
    {
        // The Where/Select calls run when the query is built - they are real calls of Build.
        var calls = RelsOf(RelationshipType.Calls);
        Assert.Multiple(() =>
        {
            Assert.That(calls, Does.Contain("Report.Build -> Sequence.Where"));
            Assert.That(calls, Does.Contain("Report.Build -> Sequence.Select"));
        });
    }

    [Test]
    public void QueryClauseBodies_HaveLambdaSemantics()
    {
        // The clause bodies are deferred like lambda bodies: "Uses", not "Calls" (see the lambda
        // chapter in corrections-and-updates.md). Flip these assertions if we decide otherwise.
        Assert.Multiple(() =>
        {
            Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Report.Build -> Report.Threshold"));
            Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Report.Build -> Report.Shift"));
            Assert.That(RelsOf(RelationshipType.Calls), Does.Not.Contain("Report.Build -> Report.Threshold"));
            Assert.That(RelsOf(RelationshipType.Calls), Does.Not.Contain("Report.Build -> Report.Shift"));
        });
    }
}
