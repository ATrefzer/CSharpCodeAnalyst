using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Generic method groups. A standalone generic method group ("Create&lt;Widget&gt;") is a
///     GenericNameSyntax, not an IdentifierNameSyntax, so VisitIdentifierName never fires and neither the
///     method-group Uses edge nor the type-argument Uses edge is recorded. The qualified form
///     ("Producer.Produce&lt;Widget&gt;") goes through AnalyzeMemberAccess and works - asserted as a guard.
///     The method group is passed to a Delegate parameter on purpose: a Func&lt;Widget&gt; signature or
///     local would already record "Uses Widget" via the signature walk and mask the missing edge.
/// </summary>
[TestFixture]
public class MethodGroups_GenericParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      using System;

                                      namespace Demo;

                                      public class Widget
                                      {
                                      }

                                      public static class Producer
                                      {
                                          public static T Produce<T>() where T : new()
                                          {
                                              return new T();
                                          }
                                      }

                                      public class Dispatcher
                                      {
                                          public void Wire()
                                          {
                                              Register(Create<Widget>);
                                          }

                                          public Func<Widget> WireQualified()
                                          {
                                              return Producer.Produce<Widget>;
                                          }

                                          private static void Register(Delegate factory)
                                          {
                                          }

                                          private static T Create<T>() where T : new()
                                          {
                                              return new T();
                                          }
                                      }
                                      """;

    [Test]
    public void QualifiedGenericMethodGroup_IsDetected()
    {
        // Premise guard (green): the member-access form is already captured.
        Assert.That(MethodGroupUsages(), Does.Contain("Dispatcher.WireQualified -> Producer.Produce"));
    }

    [Test]
    public void StandaloneGenericMethodGroup_IsDetected()
    {
        Assert.That(MethodGroupUsages(), Does.Contain("Dispatcher.Wire -> Dispatcher.Create"));
    }

    [Test]
    public void TypeArgumentOfStandaloneGenericMethodGroup_IsDetectedAsUses()
    {
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Dispatcher.Wire -> Widget"));
    }
}
