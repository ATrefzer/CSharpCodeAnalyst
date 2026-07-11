using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Type arguments of a constructed generic type used as static-member receiver:
///     "Registry&lt;Token&gt;.Instance" / "Registry&lt;Token&gt;.CountItems()". The member relationship is
///     found via normalization (Registry&lt;T&gt;), but the type argument Token is lost: the receiver is a
///     GenericNameSyntax whose type-argument identifiers resolve to plain type symbols, which
///     AnalyzeIdentifier ignores. Object creation ("new Registry&lt;Token&gt;()") and signature types do
///     capture the type arguments - only the static-member receiver position is missing.
/// </summary>
[TestFixture]
public class ConstructedGenericStaticMemberParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      namespace Demo;

                                      public class Token
                                      {
                                      }

                                      public class Registry<T>
                                      {
                                          public static Registry<T> Instance { get; } = new Registry<T>();

                                          public static int CountItems()
                                          {
                                              return 0;
                                          }
                                      }

                                      public class Locator
                                      {
                                          public void Prime()
                                          {
                                              Registry<Token>.Instance.ToString();
                                          }

                                          public int Count()
                                          {
                                              return Registry<Token>.CountItems();
                                          }
                                      }
                                      """;

    [Test]
    public void StaticMemberRelationships_AreDetected()
    {
        // Premise guard (green): the member edges themselves are found via normalization.
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Locator.Prime -> Registry.Instance"));
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Locator.Count -> Registry.CountItems"));
    }

    [Test]
    public void TypeArgumentOfStaticPropertyReceiver_IsDetectedAsUses()
    {
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Locator.Prime -> Token"));
    }

    [Test]
    public void TypeArgumentOfStaticMethodReceiver_IsDetectedAsUses()
    {
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Locator.Count -> Token"));
    }
}
