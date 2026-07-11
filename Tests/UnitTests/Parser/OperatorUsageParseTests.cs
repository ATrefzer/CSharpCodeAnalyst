using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Usage of user-defined operators. The operator declarations are code elements (op_Addition,
///     op_UnaryNegation, op_Equality, ...) and their bodies are analyzed, but applying an operator
///     ("a + b", "-a", "a == b", "a += b") never records an edge to the operator method - the walkers
///     only look at binary expressions for is/as. Internal operators therefore always look unused.
/// </summary>
[TestFixture]
public class OperatorUsageParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      namespace Demo;

                                      public class Money
                                      {
                                          public static Money operator +(Money left, Money right)
                                          {
                                              return new Money();
                                          }

                                          public static Money operator -(Money value)
                                          {
                                              return new Money();
                                          }

                                          public static bool operator ==(Money left, Money right)
                                          {
                                              return true;
                                          }

                                          public static bool operator !=(Money left, Money right)
                                          {
                                              return false;
                                          }
                                      }

                                      public class Wallet
                                      {
                                          public Money Sum(Money a, Money b)
                                          {
                                              return a + b;
                                          }

                                          public Money Negate(Money value)
                                          {
                                              return -value;
                                          }

                                          public bool Same(Money a, Money b)
                                          {
                                              return a == b;
                                          }

                                          public Money Accumulate(Money total, Money amount)
                                          {
                                              total += amount;
                                              return total;
                                          }
                                      }
                                      """;

    [Test]
    public void OperatorElements_AreDetected()
    {
        // Premise guard (green): the declaration side works, only the usage side is missing.
        var methods = PathsOf(CodeElementType.Method);
        Assert.Multiple(() =>
        {
            Assert.That(methods, Does.Contain("Money.op_Addition"));
            Assert.That(methods, Does.Contain("Money.op_UnaryNegation"));
            Assert.That(methods, Does.Contain("Money.op_Equality"));
        });
    }

    [Test]
    public void BinaryOperatorUsage_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Wallet.Sum -> Money.op_Addition"));
    }

    [Test]
    public void UnaryOperatorUsage_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Wallet.Negate -> Money.op_UnaryNegation"));
    }

    [Test]
    public void EqualityOperatorUsage_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Wallet.Same -> Money.op_Equality"));
    }

    [Test]
    public void CompoundAssignmentOperatorUsage_IsDetected()
    {
        // "total += amount" invokes the binary op_Addition.
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Wallet.Accumulate -> Money.op_Addition"));
    }
}
