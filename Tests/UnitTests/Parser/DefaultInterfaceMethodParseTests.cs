using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Default interface methods (DIM): for a class that inherits the default implementation, Roslyn's
///     FindImplementationForInterfaceMember returns the interface member itself - which must not be
///     turned into an Implements self edge ("IGreeter.Greet implements IGreeter.Greet").
/// </summary>
[TestFixture]
public class DefaultInterfaceMethodParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      namespace Demo;

                                      public static class Helper
                                      {
                                          public static void Log() { }
                                      }

                                      public interface IGreeter
                                      {
                                          void Greet() { Helper.Log(); }
                                      }

                                      // Inherits the default implementation - no Implements edge expected at all.
                                      public class Greeter : IGreeter { }

                                      // Provides its own implementation - normal member Implements edge expected.
                                      public class CustomGreeter : IGreeter
                                      {
                                          public void Greet() { }
                                      }
                                      """;

    [Test]
    public void DimBody_IsAnalyzed()
    {
        // Premise guard (green): the default implementation body is walked like a method body.
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("IGreeter.Greet -> Helper.Log"));
    }

    [Test]
    public void TypeLevelAndOverridingImplements_AreDetected()
    {
        // Premise guards (green): type-level edges and the real overriding implementation.
        var implements = RelsOf(RelationshipType.Implements);
        Assert.Multiple(() =>
        {
            Assert.That(implements, Does.Contain("Greeter -> IGreeter"));
            Assert.That(implements, Does.Contain("CustomGreeter -> IGreeter"));
            Assert.That(implements, Does.Contain("CustomGreeter.Greet -> IGreeter.Greet"));
        });
    }

    [Test]
    public void DimMember_HasNoSelfImplementsEdge()
    {
        Assert.That(RelsOf(RelationshipType.Implements), Does.Not.Contain("IGreeter.Greet -> IGreeter.Greet"));
    }
}
