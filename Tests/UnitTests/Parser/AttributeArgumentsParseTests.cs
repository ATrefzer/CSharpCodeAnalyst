using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Types referenced in attribute arguments ("[Handler(typeof(Payload))]"). On a method the argument is
///     captured, because phase 2 walks the whole method declaration including its attribute lists. Classes,
///     properties and fields have no such declaration walk - only the UsesAttribute edge to the attribute
///     class is recorded and the typeof argument is lost. Distinct payload types per attribute site make
///     the assertions precise.
/// </summary>
[TestFixture]
public class AttributeArgumentsParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      using System;

                                      namespace Demo;

                                      public class ClassPayload
                                      {
                                      }

                                      public class MethodPayload
                                      {
                                      }

                                      public class PropertyPayload
                                      {
                                      }

                                      public class FieldPayload
                                      {
                                      }

                                      public class HandlerAttribute : Attribute
                                      {
                                          public HandlerAttribute(Type type)
                                          {
                                          }
                                      }

                                      [Handler(typeof(ClassPayload))]
                                      public class Processor
                                      {
                                          [Handler(typeof(FieldPayload))]
                                          private int _token;

                                          [Handler(typeof(PropertyPayload))]
                                          public int Value { get; set; }

                                          [Handler(typeof(MethodPayload))]
                                          public void Run()
                                          {
                                          }
                                      }
                                      """;

    [Test]
    public void AttributeUsage_IsDetectedOnAllSites()
    {
        // Premise guard (green): the UsesAttribute edge to the attribute class works everywhere.
        var usesAttribute = RelsOf(RelationshipType.UsesAttribute);
        Assert.Multiple(() =>
        {
            Assert.That(usesAttribute, Does.Contain("Processor -> HandlerAttribute"));
            Assert.That(usesAttribute, Does.Contain("Processor._token -> HandlerAttribute"));
            Assert.That(usesAttribute, Does.Contain("Processor.Value -> HandlerAttribute"));
            Assert.That(usesAttribute, Does.Contain("Processor.Run -> HandlerAttribute"));
        });
    }

    [Test]
    public void AttributeArgumentOnMethod_IsDetected()
    {
        // Premise guard (green): the method declaration walk already covers its attribute arguments.
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Processor.Run -> MethodPayload"));
    }

    [Test]
    public void AttributeArgumentOnClass_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Processor -> ClassPayload"));
    }

    [Test]
    public void AttributeArgumentOnProperty_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Processor.Value -> PropertyPayload"));
    }

    [Test]
    public void AttributeArgumentOnField_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Processor._token -> FieldPayload"));
    }
}
