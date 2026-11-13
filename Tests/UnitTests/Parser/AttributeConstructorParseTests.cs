using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Applying an attribute instantiates it: "[Mark(1, Name = "x")]" runs a constructor and sets a
///     property. The syntax names only the attribute type, so the constructor had no incoming reference,
///     and the named argument was read as a getter access because it is no assignment expression.
/// </summary>
[TestFixture]
public class AttributeConstructorParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      using System;

                                      namespace Demo;

                                      [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
                                      public class MarkAttribute : Attribute
                                      {
                                          public MarkAttribute()
                                          {
                                          }

                                          public MarkAttribute(int level)
                                          {
                                          }

                                          public string Name { get; set; } = "";
                                      }

                                      /// <summary>No constructor written - there is no element to call.</summary>
                                      public class PlainAttribute : Attribute
                                      {
                                      }

                                      public class TypedAttribute<T> : Attribute
                                      {
                                          public TypedAttribute()
                                          {
                                          }
                                      }

                                      [Mark(1, Name = "class")]
                                      public class Decorated
                                      {
                                          [Mark]
                                          private int _field;

                                          [Mark(Name = "property")]
                                          public int Value { get; set; }

                                          [Mark(2, Name = "method")]
                                          public void Run()
                                          {
                                          }

                                          [Plain]
                                          public void Plain()
                                          {
                                          }

                                          [Typed<int>]
                                          public void Typed()
                                          {
                                          }
                                      }
                                      """;

    private string[] Calls => RelsOf(RelationshipType.Calls);

    [Test]
    public void TheAttributeConstructor_IsCalledFromEverySite()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Calls, Does.Contain("Decorated -> MarkAttribute..ctor"));
            Assert.That(Calls, Does.Contain("Decorated._field -> MarkAttribute..ctor"));
            Assert.That(Calls, Does.Contain("Decorated.Value -> MarkAttribute..ctor"));
            Assert.That(Calls, Does.Contain("Decorated.Run -> MarkAttribute..ctor"));
        });
    }

    [Test]
    public void TheOverloadTheArgumentsSelect_IsTheOneCalled()
    {
        // Both constructors share the path "MarkAttribute..ctor", so the callers are grouped per element:
        // "[Mark]" and "[Mark(Name = ...)]" run the parameterless one, "[Mark(1)]" and "[Mark(2)]" the other.
        var callersPerConstructor = Graph.Nodes.Values
            .Where(n => PathOf(n) == "MarkAttribute..ctor")
            .Select(constructor => Graph.Nodes.Values
                .Where(source => source.Relationships.Any(r => r.Type == RelationshipType.Calls && r.TargetId == constructor.Id))
                .Select(PathOf)
                .Order()
                .ToArray())
            .Select(callers => string.Join(", ", callers));

        Assert.That(callersPerConstructor, Is.EquivalentTo(new[]
        {
            "Decorated._field, Decorated.Value",
            "Decorated, Decorated.Run"
        }));
    }

    [Test]
    public void AnAttributeWithoutAWrittenConstructor_GetsNoCall()
    {
        Assert.That(Calls.Where(call => call.StartsWith("Decorated.Plain")), Is.Empty);
    }

    [Test]
    public void AGenericAttribute_ResolvesToTheConstructorOfTheDefinition()
    {
        Assert.That(Calls, Does.Contain("Decorated.Typed -> TypedAttribute<T>..ctor"));
    }

    [Test]
    public void ANamedArgument_CallsTheSetter()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Calls, Does.Contain("Decorated -> MarkAttribute.Name.set_Name"));
            Assert.That(Calls, Does.Contain("Decorated.Value -> MarkAttribute.Name.set_Name"));

            // A method's attribute lists are covered by its declaration walk - a different path.
            Assert.That(Calls, Does.Contain("Decorated.Run -> MarkAttribute.Name.set_Name"));
        });
    }

    [Test]
    public void ANamedArgument_DoesNotReadTheProperty()
    {
        Assert.That(Calls.Where(call => call.EndsWith("MarkAttribute.Name.get_Name")), Is.Empty);
    }
}
