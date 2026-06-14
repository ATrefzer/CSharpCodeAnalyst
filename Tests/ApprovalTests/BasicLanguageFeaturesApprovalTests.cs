using CodeGraph.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class BasicLanguageFeaturesApprovalTests : ApprovalTestBase
{

    [Test]
    public void Core_BasicLanguageFeatures_Classes_ShouldBeDetected()
    {
        var actual = GetAllClasses(GetTestAssemblyGraph()).ToList();

        var expected = new[]
        {
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.DerivedClass",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.FieldInitializers",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.CreatableClass",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Lambdas",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeOf",

            // Indexers/operators/finalizers, pattern matching, initializers and special type contexts.
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.DataStore",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Initializers.Engine",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Initializers.CarWithPropertyInitializer",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Initializers.CarWithFieldInitializer",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.Shape",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.Circle",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.Square",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.Triangle",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.Rectangle",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.PatternUser",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.ParsingFailedException",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.InventoryItem",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.PooledResource",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    private CodeGraph.Graph.CodeGraph GetTestAssemblyGraph()
    {
        return GetTestGraph("Core.BasicLanguageFeatures");
    }


    [Test]
    public void Core_BasicLanguageFeatures_Structs_ShouldBeDetected()
    {
        var actual = GetAllStructs(GetTestAssemblyGraph()).ToList();

        var expected = new[]
        {
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Point",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Rectangle"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
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
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass.HasLocalFunction -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.CreatableClass",

            // Field/property initializers and object creation inside operator / methods
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.DataStore",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.op_Addition -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Initializers.CarWithFieldInitializer -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Initializers.Engine",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Initializers.CarWithPropertyInitializer -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Initializers.Engine",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser.CreateResource -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.PooledResource",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser.Throwing -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.ParsingFailedException"
        };

        Assert.That(methodCalls.ToArray(), Is.EquivalentTo(expected));
    }

    [Test]
    public void Core_BasicLanguageFeatures_Uses_ShouldBeDetected()
    {
        var actual = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Uses)
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
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Lambdas.Start -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Lambdas.Method",

            // Indexers/operators: field and parameter/local types
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog._store -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.DataStore",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.Absorb -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.op_Addition -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.op_Implicit -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.this[] -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog._store",

            // Initializers: field/property types
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Initializers.CarWithFieldInitializer._engine -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Initializers.Engine",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Initializers.CarWithPropertyInitializer.Engine -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Initializers.Engine",

            // Pattern matching: parameter types and pattern types
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.PatternUser.DeclarationPattern -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.Shape",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.PatternUser.DeclarationPattern -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.Circle",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.PatternUser.SwitchExpression -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.Shape",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.PatternUser.SwitchExpression -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.Square",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.PatternUser.SwitchExpression -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.Triangle",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.PatternUser.CaseStatement -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.Shape",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.PatternUser.CaseStatement -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.PatternMatching.Rectangle",

            // Type contexts: catch / foreach / using / array creation / field
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser._items -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.InventoryItem",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser.CatchClause -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.ParsingFailedException",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser.ForEachLoop -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.InventoryItem",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser.ForEachLoop -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser._items",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser.ArrayCreation -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.InventoryItem",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser.UsingStatement -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.PooledResource",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser.CreateResource -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.PooledResource"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void Core_BasicLanguageFeatures_Enums_ShouldBeDetected()
    {
        var actual = GetAllEnums(GetTestAssemblyGraph()).ToList();

        var expected = new[]
        {
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Color",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Priority"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void Core_BasicLanguageFeatures_MethodCalls_ShouldBeDetected()
    {
        var actual = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Calls)
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
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.DerivedClass.TestBaseAccess -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BaseClass.BaseMethod",

            // Calls from inside indexer / operator / conversion / finalizer bodies and special contexts
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.Absorb -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.Count",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.ComputeTotal -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.Count",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.Finalize -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.Cleanup",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.op_Addition -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.Absorb",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.op_Implicit -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.ComputeTotal",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.this[] -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.DataStore.Compute",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser.CatchClause -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser.Work",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser.UsingStatement -> Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.TypeContexts.TypeContextUser.CreateResource"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }


    [Test]
    public void Core_BasicLanguageFeatures_Properties_ShouldBeDetected()
    {
        var actual = GetAllProperties(GetTestAssemblyGraph());

        var expected = new[]
        {
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.BasicCalls.PublicProperty",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.Count",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.IndexersAndOperators.Catalog.this[]",
            "Core.BasicLanguageFeatures.global.Core.BasicLanguageFeatures.Initializers.CarWithPropertyInitializer.Engine"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }
}