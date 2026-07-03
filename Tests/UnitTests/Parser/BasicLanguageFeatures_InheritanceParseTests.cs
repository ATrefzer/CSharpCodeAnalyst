using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Basic OO plumbing across files: a base/derived pair (override + base call + protected access),
///     object creation in field initializers and a local function, lambda bodies (Uses, not Calls),
///     and typeof / is / as type references. Migrated from the former Core.BasicLanguageFeatures
///     approval fixture (BaseClass / DerivedClass / CreatableClass / FieldInitializers / Lambdas / TypeOf).
/// </summary>
[TestFixture]
public class BasicLanguageFeatures_InheritanceParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                using System;
                                using System.Collections.Generic;

                                namespace Demo;

                                public class BaseClass
                                {
                                    protected string ProtectedField = "base";

                                    public virtual string GetMessage()
                                    {
                                        return "Base message";
                                    }

                                    protected void BaseMethod()
                                    {
                                        Console.WriteLine("Base method");
                                    }

                                    public void HasLocalFunction()
                                    {
                                        void LocalFunction()
                                        {
                                            var obj = new CreatableClass();
                                        }

                                        LocalFunction();
                                    }
                                }

                                public class DerivedClass : BaseClass
                                {
                                    public override string GetMessage()
                                    {
                                        // Base call
                                        var baseMessage = base.GetMessage();
                                        return $"Derived: {baseMessage}";
                                    }

                                    public void TestBaseAccess()
                                    {
                                        // Access protected members
                                        ProtectedField = "derived";
                                        BaseMethod();
                                    }
                                }

                                internal class CreatableClass
                                {
                                    public void Nop()
                                    {
                                    }
                                }

                                public class FieldInitializers
                                {
                                    private static List<BaseClass> _baseClassList = [new()];
                                    private BaseClass _baseClass = new();
                                }

                                internal class Lambdas
                                {
                                    private void Start()
                                    {
                                        var x = () =>
                                        {
                                            // Start -> uses -> CreatableClass
                                            var creatableClass = new CreatableClass();

                                            // Not extracted
                                            creatableClass.Nop();
                                        };


                                        var y = () =>
                                        {
                                            BaseClass? baseClass;
                                        };


                                        var z = () => Foo(Method);
                                    }

                                    private void Foo(Action method)
                                    {
                                    }

                                    private void Method()
                                    {
                                    }
                                }

                                public class TypeOf
                                {
                                    public void Experiment1(object x)
                                    {
                                        if (x.GetType() == typeof(BaseClass))
                                        {
                                        }
                                    }

                                    public void Experiment2(object x)
                                    {
                                        var isOfType = x is BaseClass;
                                    }

                                    public void Experiment3(object x)
                                    {
                                        var y = x as BaseClass;
                                    }
                                }
                                """;

    [Test]
    public void Classes_AreDetected()
    {
        var expected = new[]
        {
            "BaseClass", "DerivedClass", "CreatableClass", "FieldInitializers", "Lambdas", "TypeOf"
        };

        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(expected));
    }

    [Test]
    public void ObjectCreations_AreDetectedAsCreates()
    {
        var expected = new[]
        {
            // Field initializer (attributed to the type) and a creation inside a local function.
            "FieldInitializers -> BaseClass",
            "BaseClass.HasLocalFunction -> CreatableClass"
        };

        Assert.That(RelsOf(RelationshipType.Creates), Is.EquivalentTo(expected));
    }

    [Test]
    public void TypeReferences_AreDetectedAsUses()
    {
        var expected = new[]
        {
            // Creation inside lambda / local function (lambda body is Uses, not Calls).
            "Lambdas.Start -> CreatableClass",
            "Lambdas.Start -> CreatableClass.Nop",
            "Lambdas.Start -> BaseClass",
            "Lambdas.Start -> Lambdas.Foo",
            "Lambdas.Start -> Lambdas.Method",
            "BaseClass.HasLocalFunction -> CreatableClass",

            // Protected field access from the derived class.
            "DerivedClass.TestBaseAccess -> BaseClass.ProtectedField",

            // Field declaration types.
            "FieldInitializers._baseClass -> BaseClass",
            "FieldInitializers._baseClassList -> BaseClass",

            // typeof / is / as.
            "TypeOf.Experiment1 -> BaseClass",
            "TypeOf.Experiment2 -> BaseClass",
            "TypeOf.Experiment3 -> BaseClass"
        };

        Assert.That(RelsOf(RelationshipType.Uses), Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodCalls_AreDetected()
    {
        var expected = new[]
        {
            "DerivedClass.GetMessage -> BaseClass.GetMessage",
            "DerivedClass.TestBaseAccess -> BaseClass.BaseMethod"
        };

        Assert.That(RelsOf(RelationshipType.Calls), Is.EquivalentTo(expected));
    }
}
