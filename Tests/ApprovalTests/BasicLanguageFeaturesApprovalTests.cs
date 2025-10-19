using Contracts.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class BasicLanguageFeaturesApprovalTests : ApprovalTestBase
{

    [Test]
    public void Core_BasicLanguageFeatures_Classes_ShouldBeDetected()
    {
        var classes = GetAllClasses(GetTestAssemblyGraph()).ToList();

        var expected = new[]
        {
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.DerivedClass",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.FieldInitializers",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.CreatableClass",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Lambdas",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeOf"
        };

        CollectionAssert.AreEquivalent(expected, classes);
    }

    private CodeGraph GetTestAssemblyGraph()
    {
        return GetAssemblyGraph("Core.BasicLanguageFeatures");
    }


    [Test]
    public void Core_BasicLanguageFeatures_Structs_ShouldBeDetected()
    {
        var structs = GetAllStructs(GetTestAssemblyGraph()).ToList();

        var expected = new[]
        {
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Point",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Rectangle"
        };

        CollectionAssert.AreEquivalent(expected, structs.OrderBy(x => x).ToArray());
    }

    [Test]
    public void Core_BasicLanguageFeatures_Creates_ShouldBeDetected()
    {
        var methodCalls = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Creates)
            .Select(r => r.ToString())
            .OrderBy(x => x)
            .ToList();

        var expected = new[]
        {
            // Field initializer
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.FieldInitializers -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass",

            // Local function
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass.HasLocalFunction -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.CreatableClass"
        };

        CollectionAssert.AreEquivalent(expected, methodCalls.ToArray());
    }

    [Test]
    public void Core_BasicLanguageFeatures_Uses_ShouldBeDetected()
    {
        var uses = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Uses)
            .Select(r => r.ToString())
            .OrderBy(x => x)
            .ToArray();

        var expected = new[]
        {
            // Creation inside lambda
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Lambdas.Start -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.CreatableClass",

            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass.HasLocalFunction -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.CreatableClass",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.InitializeData -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls._privateField",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.TestMethodCalls -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls._privateField",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.DerivedClass.TestBaseAccess -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass.ProtectedField",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.FieldInitializers._baseClass -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.FieldInitializers._baseClassList -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass",

            // Lambda has uses not calls relationship
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Lambdas.Start -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.CreatableClass.Nop",

            // is as and typeof
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeOf.Experiment1 -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeOf.Experiment2 -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeOf.Experiment3 -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass",

            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Lambdas.Start -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Lambdas.Start -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Lambdas.Foo",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Lambdas.Start -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Lambdas.Method"
        };

        CollectionAssert.AreEquivalent(expected, uses);
    }

    [Test]
    public void Core_BasicLanguageFeatures_Enums_ShouldBeDetected()
    {
        var enums = GetAllEnums(GetTestAssemblyGraph()).ToList();

        var expected = new[]
        {
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Color",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Priority"
        };

        CollectionAssert.AreEquivalent(expected, enums);
    }

    [Test]
    public void Core_BasicLanguageFeatures_MethodCalls_ShouldBeDetected()
    {
        var methodCalls = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Calls)
            .Select(r => r.ToString())
            .OrderBy(x => x)
            .ToList();

        var expected = new[]
        {
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls..ctor -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.InitializeData",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls..ctor -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.SetProperty",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.SetProperty -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.PublicProperty",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.TestMethodCalls -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.CalculateLength",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.TestMethodCalls -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.ProcessData",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.TestMethodCalls -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.PublicProperty",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.DerivedClass.GetMessage -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass.GetMessage",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.DerivedClass.TestBaseAccess -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass.BaseMethod"
        };

        CollectionAssert.AreEquivalent(expected, methodCalls.ToArray());
    }


    [Test]
    public void Core_BasicLanguageFeatures_Properties_ShouldBeDetected()
    {
        var properties = GetAllProperties(GetTestAssemblyGraph());

        var expected = new[]
        {
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.PublicProperty"
        };

        CollectionAssert.AreEquivalent(expected, properties.ToArray());
    }
}