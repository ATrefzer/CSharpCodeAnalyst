using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Member-level Implements edges for generic interfaces. The type-level edge
///     ("ItemHandler -> IHandler") works, but the member edges are lost twice over:
///     (1) the interface-key -> implementing-types map is filled with the keys of the CONSTRUCTED
///     interfaces from AllInterfaces (IHandler&lt;Item&gt;) while the lookup uses the key of the
///     DEFINITION (IHandler&lt;T&gt;), so closed implementations are never found, and
///     (2) Roslyn's FindImplementationForInterfaceMember demands the member of the constructed
///     interface the type actually implements - handing it the definition member returns null,
///     which also breaks the open form (GenHandler&lt;T&gt; : IHandler&lt;T&gt;).
/// </summary>
[TestFixture]
public class GenericInterfaceImplementsParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      namespace Demo;

                                      public class Item { }
                                      public class Widget { }

                                      public interface IHandler<T>
                                      {
                                          void Handle(T item);
                                      }

                                      public interface IProvider<T>
                                      {
                                          T Current { get; }
                                      }

                                      // Closed construction.
                                      public class ItemHandler : IHandler<Item>
                                      {
                                          public void Handle(Item item) { }
                                      }

                                      // Open construction.
                                      public class GenHandler<T> : IHandler<T>
                                      {
                                          public void Handle(T item) { }
                                      }

                                      // Property member of a generic interface.
                                      public class ItemProvider : IProvider<Item>
                                      {
                                          public Item Current => new Item();
                                      }

                                      // Two constructions of the same generic interface - both overloads implement.
                                      public class DualHandler : IHandler<Item>, IHandler<Widget>
                                      {
                                          public void Handle(Item item) { }
                                          public void Handle(Widget item) { }
                                      }

                                      // Non-generic control group.
                                      public interface IPlain
                                      {
                                          void Run();
                                      }

                                      public class PlainImpl : IPlain
                                      {
                                          public void Run() { }
                                      }
                                      """;

    [Test]
    public void TypeLevelImplements_AreDetected()
    {
        // Premise guard (green): the type-level Implements edges exist; only the member level is broken.
        var implements = RelsOf(RelationshipType.Implements);
        Assert.Multiple(() =>
        {
            Assert.That(implements, Does.Contain("ItemHandler -> IHandler"));
            Assert.That(implements, Does.Contain("GenHandler -> IHandler"));
            Assert.That(implements, Does.Contain("ItemProvider -> IProvider"));
            Assert.That(implements, Does.Contain("DualHandler -> IHandler"));
        });
    }

    [Test]
    public void NonGenericInterface_MemberImplements_IsDetected()
    {
        // Control group (green): for a non-generic interface the member edge works.
        Assert.That(RelsOf(RelationshipType.Implements), Does.Contain("PlainImpl.Run -> IPlain.Run"));
    }

    [Test]
    public void ClosedConstruction_MemberImplements_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Implements), Does.Contain("ItemHandler.Handle -> IHandler.Handle"));
    }

    [Test]
    public void OpenConstruction_MemberImplements_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Implements), Does.Contain("GenHandler.Handle -> IHandler.Handle"));
    }

    [Test]
    public void PropertyMember_Implements_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Implements), Does.Contain("ItemProvider.Current -> IProvider.Current"));
    }

    [Test]
    public void TwoConstructions_BothOverloads_Implement()
    {
        // Handle(Item) and Handle(Widget) both implement IHandler<T>.Handle - two edges, one per overload.
        var count = RelsOf(RelationshipType.Implements)
            .Count(r => r == "DualHandler.Handle -> IHandler.Handle");
        Assert.That(count, Is.EqualTo(2));
    }
}
