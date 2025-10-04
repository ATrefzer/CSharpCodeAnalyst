using Contracts.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class BasicLanguageFeaturesApprovalTests : ProjectTestBase
{

    [Test]
    public void Core_BasicLanguageFeatures_Classes_ShouldBeDetected()
    {
        var classes = GetAllClasses(GetTestAssemblyGraph()).ToList();

        var expected = new[]
        {
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls",
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BaseClass",
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.DerivedClass",
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.FieldInitializers"
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
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.Point",
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.Rectangle"
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
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.FieldInitializers -> Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BaseClass"
        };

        CollectionAssert.AreEquivalent(expected, methodCalls.ToArray());
    }

    [Test]
    public void Core_BasicLanguageFeatures_Enums_ShouldBeDetected()
    {
        var enums = GetAllEnums(GetTestAssemblyGraph()).ToList();

        var expected = new[]
        {
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.Color",
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.Priority"
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
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls..ctor -> Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls.InitializeData",
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls..ctor -> Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls.SetProperty",
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls.SetProperty -> Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls.PublicProperty",
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls.TestMethodCalls -> Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls.CalculateLength",
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls.TestMethodCalls -> Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls.ProcessData",
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls.TestMethodCalls -> Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls.PublicProperty",
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.DerivedClass.GetMessage -> Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BaseClass.GetMessage",
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.DerivedClass.TestBaseAccess -> Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BaseClass.BaseMethod"
        };

        CollectionAssert.AreEquivalent(expected, methodCalls.ToArray());
    }


    [Test]
    public void Core_BasicLanguageFeatures_Properties_ShouldBeDetected()
    {
        var properties = GetAllProperties(GetTestAssemblyGraph());

        var expected = new[]
        {
            "Core.BasicLanguageFeatures.Core.BasicLanguageFeatures.BasicCalls.PublicProperty"
        };

        CollectionAssert.AreEquivalent(expected, properties.ToArray());
    }
}