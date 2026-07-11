using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Arguments of a primary-constructor base call: "class Derived() : Base(Helper.DefaultSize())".
///     Type declarations have no body walk, and the primary constructor has no method element, so the
///     argument expressions in the base list are never analyzed. For a classic constructor the same
///     arguments ARE captured (the constructor initializer is part of the walked constructor declaration) -
///     the fixture asserts both to document the asymmetry. The missing edges are anchored on the type
///     element, consistent with how primary-constructor parameter types are handled.
/// </summary>
[TestFixture]
public class PrimaryConstructorBaseArgumentsParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      namespace Demo;

                                      public class Base
                                      {
                                          public Base(int size)
                                          {
                                          }
                                      }

                                      public static class Helper
                                      {
                                          public static int DefaultSize()
                                          {
                                              return 4;
                                          }
                                      }

                                      public class Derived() : Base(Helper.DefaultSize());

                                      public class ClassicDerived : Base
                                      {
                                          public ClassicDerived() : base(Helper.DefaultSize())
                                          {
                                          }
                                      }
                                      """;

    [Test]
    public void Inheritance_IsDetected()
    {
        // Premise guard (green): the Inherits edge itself is not affected.
        Assert.That(RelsOf(RelationshipType.Inherits), Does.Contain("Derived -> Base"));
    }

    [Test]
    public void ClassicConstructorBaseArguments_AreDetected()
    {
        // Premise guard (green): with a classic constructor the base-call arguments are captured.
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("ClassicDerived..ctor -> Helper.DefaultSize"));
    }

    [Test]
    public void PrimaryConstructorBaseArguments_AreDetected()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Derived -> Helper.DefaultSize"));
    }
}
