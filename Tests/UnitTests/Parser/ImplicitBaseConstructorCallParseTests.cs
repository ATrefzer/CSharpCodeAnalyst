using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     A constructor without ": base(...)" still runs a base constructor, and so does the constructor
///     the compiler generates for a class that declares none. Nothing in the syntax is a call, so the
///     base constructor had no incoming reference - the normal fate of a "protected Base()".
/// </summary>
[TestFixture]
public class ImplicitBaseConstructorCallParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      using System;

                                      namespace Demo;

                                      public class Base
                                      {
                                          protected Base()
                                          {
                                          }
                                      }

                                      public class WrittenConstructor : Base
                                      {
                                          public WrittenConstructor()
                                          {
                                          }
                                      }

                                      public class NoConstructor : Base
                                      {
                                      }

                                      public class Primary(int size) : Base
                                      {
                                          public int Size => size;
                                      }

                                      public record PositionalRecord(int Size) : RecordBase;

                                      public record RecordBase
                                      {
                                          protected RecordBase()
                                          {
                                          }
                                      }

                                      /// <summary>Only the constructor that does not chain to "this" reaches the base.</summary>
                                      public class Chained : Base
                                      {
                                          public Chained() : this(1)
                                          {
                                          }

                                          public Chained(int size)
                                          {
                                          }
                                      }

                                      /// <summary>A static constructor runs no base constructor.</summary>
                                      public class StaticOnly : Base
                                      {
                                          static StaticOnly()
                                          {
                                          }
                                      }

                                      public class OptionalBase
                                      {
                                          protected OptionalBase(int size = 4)
                                          {
                                          }
                                      }

                                      public class UsesOptional : OptionalBase
                                      {
                                          public UsesOptional()
                                          {
                                          }
                                      }

                                      /// <summary>Closed hierarchy: the private constructor is reachable from the nested type only.</summary>
                                      public abstract class Shape
                                      {
                                          private Shape()
                                          {
                                          }

                                          public sealed class Circle : Shape
                                          {
                                          }
                                      }

                                      public class Repository<T>
                                      {
                                          protected Repository()
                                          {
                                          }
                                      }

                                      public class UserRepository : Repository<int>
                                      {
                                      }

                                      /// <summary>The base constructor is generated - no element, no edge.</summary>
                                      public class Plain
                                      {
                                      }

                                      public class FromPlain : Plain
                                      {
                                          public FromPlain()
                                          {
                                          }
                                      }

                                      /// <summary>The base constructor is external - no element, no edge.</summary>
                                      public class Failure : Exception
                                      {
                                          public Failure()
                                          {
                                          }
                                      }
                                      """;

    private string[] Calls => RelsOf(RelationshipType.Calls);

    [Test]
    public void AWrittenConstructorWithoutInitializer_CallsTheBaseConstructor()
    {
        Assert.That(Calls, Does.Contain("WrittenConstructor..ctor -> Base..ctor"));
    }

    [Test]
    public void TheImplicitCall_IsABaseCall()
    {
        var constructor = Graph.Nodes.Values.Single(n => PathOf(n) == "WrittenConstructor..ctor");
        var call = constructor.Relationships.Single(r => r.Type == RelationshipType.Calls);

        Assert.That(call.HasAttribute(RelationshipAttribute.IsBaseCall), Is.True);
    }

    [Test]
    public void AClassWithoutConstructor_CarriesTheCallItself()
    {
        // The generated constructor is not an element, so the type is the source.
        Assert.That(Calls, Does.Contain("NoConstructor -> Base..ctor"));
    }

    [Test]
    public void AClassWithAWrittenConstructor_DoesNotCarryTheCallAsWell()
    {
        Assert.That(Calls, Does.Not.Contain("WrittenConstructor -> Base..ctor"));
    }

    [Test]
    public void APrimaryConstructorWithoutBaseArguments_CallsTheBaseConstructor()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Calls, Does.Contain("Primary..ctor -> Base..ctor"));
            Assert.That(Calls, Does.Not.Contain("Primary -> Base..ctor"));
        });
    }

    [Test]
    public void APositionalRecord_CallsTheBaseConstructor()
    {
        Assert.That(Calls, Does.Contain("PositionalRecord..ctor -> RecordBase..ctor"));
    }

    [Test]
    public void AConstructorChainingToThis_DoesNotCallTheBaseConstructor()
    {
        // Two constructors, one edge: Chained(int). Chained() goes through this(1).
        Assert.That(Calls.Count(call => call == "Chained..ctor -> Base..ctor"), Is.EqualTo(1));
    }

    [Test]
    public void AStaticConstructor_CallsNoBaseConstructor()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Calls, Does.Not.Contain("StaticOnly..cctor -> Base..ctor"));

            // No instance constructor is written, so the generated one makes the call.
            Assert.That(Calls, Does.Contain("StaticOnly -> Base..ctor"));
        });
    }

    [Test]
    public void ABaseConstructorWithOptionalParameters_IsCalled()
    {
        Assert.That(Calls, Does.Contain("UsesOptional..ctor -> OptionalBase..ctor"));
    }

    [Test]
    public void APrivateBaseConstructor_IsCalledFromANestedType()
    {
        Assert.That(Calls, Does.Contain("Shape.Circle -> Shape..ctor"));
    }

    [Test]
    public void AGenericBaseType_ResolvesToTheConstructorOfTheDefinition()
    {
        Assert.That(Calls, Does.Contain("UserRepository -> Repository<T>..ctor"));
    }

    [Test]
    public void AGeneratedOrExternalBaseConstructor_GetsNoEdge()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Calls.Where(call => call.StartsWith("FromPlain")), Is.Empty);
            Assert.That(Calls.Where(call => call.StartsWith("Failure")), Is.Empty);
        });
    }
}
