using CodeGraph.Graph;

namespace CodeParserTests.ApprovalTests.KnownGaps;

/// <summary>
///     Documents known gaps of the parser: dependencies that exist in the source code of the
///     TestSuite project "ParserGaps" but are not found by the parser.
///     Each Gap_* test asserts the CURRENT (incomplete) behavior, so the test run stays green
///     while still proving that the relationship is missing.
///     If one of these tests fails, the parser has learned the construct: invert the assertion
///     and move the test to the regular approval tests.
///     The Detected_* tests are contrast cases showing the closely related constructs that DO work.
///     See also Documentation/uncovered-csharp-syntax.md.
/// </summary>
[TestFixture]
public class ParserGapsTests : ApprovalTestBase
{
    private const string Ns = "ParserGaps.global.ParserGaps.";

    private CodeGraph.Graph.CodeGraph GetTestGraph()
    {
        return GetTestGraph("ParserGaps");
    }

    [Test]
    public void Sanity_TestProjectIsParsed()
    {
        var classes = GetAllClasses(GetTestGraph());

        Assert.That(classes, Does.Contain($"{Ns}IndexersAndOperators.Catalog"));
        Assert.That(classes, Does.Contain($"{Ns}PatternMatching.PatternUser"));
        Assert.That(classes, Does.Contain($"{Ns}TypeContexts.TypeContextUser"));
        Assert.That(classes, Does.Contain($"{Ns}MethodGroups.MethodGroupUser"));
    }

    // --- Indexers, operators, conversion operators, finalizers -------------------------------
    // HierarchyAnalyzer.ProcessNodeForHierarchy does not handle IndexerDeclarationSyntax,
    // OperatorDeclarationSyntax, ConversionOperatorDeclarationSyntax and DestructorDeclarationSyntax.
    // These members are not code elements and their bodies are never analyzed in phase 2.

    [Test]
    public void Gap_IndexerIsNotACodeElement()
    {
        var catalogProperties = GetAllProperties(GetTestGraph())
            .Where(p => p.StartsWith($"{Ns}IndexersAndOperators.Catalog."));

        // Only Count is found. The indexer is missing entirely.
        Assert.That(catalogProperties, Is.EquivalentTo(new[] { $"{Ns}IndexersAndOperators.Catalog.Count" }));
    }

    [Test]
    public void Gap_CallsInsideIndexerBodyAreInvisible()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        // DataStore.Compute is only called from the indexer body.
        Assert.That(calls.Where(c => c.EndsWith($"-> {Ns}IndexersAndOperators.DataStore.Compute")), Is.Empty);
    }

    [Test]
    public void Gap_OperatorBodyIsInvisible()
    {
        var graph = GetTestGraph();
        var calls = GetRelationshipsOfType(graph, RelationshipType.Calls);
        var creates = GetRelationshipsOfType(graph, RelationshipType.Creates);

        // Absorb is only called from operator +, the Catalog instance is only created there.
        Assert.That(calls.Where(c => c.EndsWith($"-> {Ns}IndexersAndOperators.Catalog.Absorb")), Is.Empty);
        Assert.That(creates.Where(c => c.EndsWith($"-> {Ns}IndexersAndOperators.Catalog")), Is.Empty);
    }

    [Test]
    public void Gap_ConversionOperatorBodyIsInvisible()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        // ComputeTotal is only called from the implicit conversion operator.
        Assert.That(calls.Where(c => c.EndsWith($"-> {Ns}IndexersAndOperators.Catalog.ComputeTotal")), Is.Empty);
    }

    [Test]
    public void Gap_FinalizerBodyIsInvisible()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        // Cleanup is only called from the finalizer.
        Assert.That(calls.Where(c => c.EndsWith($"-> {Ns}IndexersAndOperators.Catalog.Cleanup")), Is.Empty);
    }

    [Test]
    public void Detected_FieldInitializerObjectCreation()
    {
        var creates = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Creates);

        Assert.That(creates, Does.Contain($"{Ns}IndexersAndOperators.Catalog -> {Ns}IndexersAndOperators.DataStore"));
    }

    // --- Primary constructors and positional records ------------------------------------------
    // Phase 1 only collects ConstructorDeclarationSyntax. Primary constructors and the generated
    // positional properties are not collected, so their parameter types create no relationship.

    [Test]
    public void Detected_RecordsAreCodeElements()
    {
        var records = GetAllNodesOfType(GetTestGraph(), CodeElementType.Record);

        Assert.That(records, Does.Contain($"{Ns}PrimaryConstructors.Order"));
        Assert.That(records, Does.Contain($"{Ns}PrimaryConstructors.OrderId"));
    }

    [Test]
    public void Gap_PositionalRecordParameterTypesCreateNoRelationship()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}PrimaryConstructors.Order -> {Ns}PrimaryConstructors.OrderId"));
        Assert.That(all, Does.Not.Contain($"{Ns}PrimaryConstructors.Order -> {Ns}PrimaryConstructors.Customer"));
    }

    [Test]
    public void Gap_ClassPrimaryConstructorParameterTypeCreatesNoRelationship()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}PrimaryConstructors.Inventory -> {Ns}PrimaryConstructors.Warehouse"));
    }

    // --- Pattern matching ----------------------------------------------------------------------
    // "shape is Circle circle" is an IsPatternExpressionSyntax (not the handled BinaryExpression),
    // and type identifiers inside patterns resolve to INamedTypeSymbol, which AnalyzeIdentifier drops.

    [Test]
    public void Detected_ParameterTypeOfPatternMethod()
    {
        var uses = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);

        Assert.That(uses, Does.Contain($"{Ns}PatternMatching.PatternUser.DeclarationPattern -> {Ns}PatternMatching.Shape"));
    }

    [Test]
    public void Gap_DeclarationPatternTypeIsNotCaptured()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}PatternMatching.PatternUser.DeclarationPattern -> {Ns}PatternMatching.Circle"));
    }

    [Test]
    public void Gap_SwitchExpressionTypePatternsAreNotCaptured()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}PatternMatching.PatternUser.SwitchExpression -> {Ns}PatternMatching.Square"));
        Assert.That(all, Does.Not.Contain($"{Ns}PatternMatching.PatternUser.SwitchExpression -> {Ns}PatternMatching.Triangle"));
    }

    [Test]
    public void Gap_CasePatternTypeIsNotCaptured()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}PatternMatching.PatternUser.CaseStatement -> {Ns}PatternMatching.Rectangle"));
    }

    // --- Constructor chaining --------------------------------------------------------------------
    // ConstructorInitializerSyntax (": base(...)" / ": this(...)") is not an InvocationExpression
    // and is not handled anywhere.

    [Test]
    public void Detected_InheritanceOfChainedConstructors()
    {
        var inherits = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Inherits);

        Assert.That(inherits, Does.Contain($"{Ns}ConstructorChaining.DerivedService -> {Ns}ConstructorChaining.BaseService"));
    }

    [Test]
    public void Gap_BaseConstructorChainingIsNotCaptured()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        Assert.That(calls, Does.Not.Contain($"{Ns}ConstructorChaining.DerivedService..ctor -> {Ns}ConstructorChaining.BaseService..ctor"));
    }

    [Test]
    public void Gap_ThisConstructorChainingIsNotCaptured()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        Assert.That(calls, Does.Not.Contain($"{Ns}ConstructorChaining.SelfChaining..ctor -> {Ns}ConstructorChaining.SelfChaining..ctor"));
    }

    // --- Property initializers -------------------------------------------------------------------
    // AnalyzePropertyBody only handles the expression body and the accessor list,
    // not PropertyDeclarationSyntax.Initializer.

    [Test]
    public void Detected_FieldInitializerCreates()
    {
        var creates = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Creates);

        Assert.That(creates, Does.Contain($"{Ns}Initializers.CarWithFieldInitializer -> {Ns}Initializers.Engine"));
    }

    [Test]
    public void Gap_PropertyInitializerCreatesNoRelationship()
    {
        var creates = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Creates);

        Assert.That(creates, Does.Not.Contain($"{Ns}Initializers.CarWithPropertyInitializer -> {Ns}Initializers.Engine"));
        Assert.That(creates, Does.Not.Contain($"{Ns}Initializers.CarWithPropertyInitializer.Engine -> {Ns}Initializers.Engine"));
    }

    // --- Type names in special contexts ----------------------------------------------------------
    // catch declarations, foreach variable types, using statement declarations and array creation
    // all end up as plain type identifiers, which AnalyzeIdentifier drops.

    [Test]
    public void Detected_ThrowStatementObjectCreation()
    {
        var creates = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Creates);

        Assert.That(creates, Does.Contain($"{Ns}TypeContexts.TypeContextUser.Throwing -> {Ns}TypeContexts.ParsingFailedException"));
    }

    [Test]
    public void Gap_CatchClauseTypeIsNotCaptured()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}TypeContexts.TypeContextUser.CatchClause -> {Ns}TypeContexts.ParsingFailedException"));
    }

    [Test]
    public void Gap_ForEachVariableTypeIsNotCaptured()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}TypeContexts.TypeContextUser.ForEachLoop -> {Ns}TypeContexts.InventoryItem"));
    }

    [Test]
    public void Gap_ArrayCreationElementTypeIsNotCaptured()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}TypeContexts.TypeContextUser.ArrayCreation -> {Ns}TypeContexts.InventoryItem"));
    }

    [Test]
    public void Gap_UsingStatementDeclarationTypeIsNotCaptured()
    {
        var graph = GetTestGraph();
        var calls = GetRelationshipsOfType(graph, RelationshipType.Calls);
        var all = GetAllRelationships(graph);

        // The factory call inside the using statement is found ...
        Assert.That(calls, Does.Contain($"{Ns}TypeContexts.TypeContextUser.UsingStatement -> {Ns}TypeContexts.TypeContextUser.CreateResource"));

        // ... but the declared variable type is not.
        Assert.That(all, Does.Not.Contain($"{Ns}TypeContexts.TypeContextUser.UsingStatement -> {Ns}TypeContexts.PooledResource"));
    }

    // --- Method groups outside of argument lists -------------------------------------------------
    // Method groups are only captured by AnalyzeArgument. In assignments, local declarations and
    // return statements the IMethodSymbol is dropped.

    [Test]
    public void Detected_MethodGroupAsInvocationArgument()
    {
        var usages = GetAllMethodGroupUsages(GetTestGraph());

        Assert.That(usages, Does.Contain($"{Ns}MethodGroups.MethodGroupUser.PassAsArgument -> {Ns}MethodGroups.Worker.DoParallelWork"));
    }

    [Test]
    public void Gap_MethodGroupAssignedToLocalIsNotCaptured()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}MethodGroups.MethodGroupUser.AssignToLocal -> {Ns}MethodGroups.Worker.DoWork"));
    }

    [Test]
    public void Gap_MethodGroupReturnedIsNotCaptured()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}MethodGroups.MethodGroupUser.ReturnMethodGroup -> {Ns}MethodGroups.Worker.DoMoreWork"));
    }

    [Test]
    public void Gap_MethodGroupAssignedToFieldIsNotCaptured()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}MethodGroups.MethodGroupUser.AssignToField -> {Ns}MethodGroups.Worker.DoExtraWork"));
    }

    [Test]
    public void Detected_ModernEventRegistrationCreatesHandles()
    {
        var handles = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Handles);

        Assert.That(handles, Does.Contain($"{Ns}MethodGroups.Worker.OnTock -> {Ns}MethodGroups.MethodGroupUser.Ticked"));
    }

    [Test]
    public void Gap_OldStyleEventRegistrationCreatesNoHandles()
    {
        var handles = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Handles);

        Assert.That(handles, Does.Not.Contain($"{Ns}MethodGroups.Worker.OnTick -> {Ns}MethodGroups.MethodGroupUser.Ticked"));
    }
}
