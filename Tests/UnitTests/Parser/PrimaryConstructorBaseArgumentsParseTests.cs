using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Arguments of a primary-constructor base call: "class Derived() : Base(Helper.DefaultSize())".
///     A primary constructor is an element of its own, and its "body" is the argument list of the base
///     clause - a type declaration has no body walk otherwise. The fixture asserts the classic form
///     next to it: the two used to differ (the primary form anchored its edges on the type), and the
///     point of this fixture is now that they no longer do.
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
        // On the constructor, exactly like the classic form above - not on the type.
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Derived..ctor -> Helper.DefaultSize"));
    }

    [Test]
    public void ThePrimaryConstructorCallsTheBaseConstructor()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Derived..ctor -> Base..ctor"));
    }

    [Test]
    public void TheTypeItself_NoLongerCarriesTheConstructorsEdges()
    {
        // The workaround that anchored them there is gone.
        Assert.That(RelsOf(RelationshipType.Calls), Does.Not.Contain("Derived -> Helper.DefaultSize"));
    }
}
