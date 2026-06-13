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
    }

    // --- Indexers, operators, conversion operators, finalizers -------------------------------
    // HierarchyAnalyzer.ProcessNodeForHierarchy now handles IndexerDeclarationSyntax (A1) as well
    // as OperatorDeclarationSyntax, ConversionOperatorDeclarationSyntax and
    // DestructorDeclarationSyntax (A2). All become code elements and their bodies are walked in
    // phase 2. The symbol names are "this[]", "op_Addition", "op_Implicit" and "Finalize".

    [Test]
    public void Detected_IndexerIsACodeElement()
    {
        var catalogProperties = GetAllProperties(GetTestGraph())
            .Where(p => p.StartsWith($"{Ns}IndexersAndOperators.Catalog."));

        // The indexer is now found as a property named "this[]".
        Assert.That(catalogProperties, Is.EquivalentTo(new[]
        {
            $"{Ns}IndexersAndOperators.Catalog.Count",
            $"{Ns}IndexersAndOperators.Catalog.this[]"
        }));
    }

    [Test]
    public void Detected_CallsInsideIndexerBodyAreVisible()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        // DataStore.Compute is only called from the indexer body.
        Assert.That(calls, Does.Contain($"{Ns}IndexersAndOperators.Catalog.this[] -> {Ns}IndexersAndOperators.DataStore.Compute"));
    }

    [Test]
    public void Detected_OperatorBodyIsVisible()
    {
        var graph = GetTestGraph();
        var calls = GetRelationshipsOfType(graph, RelationshipType.Calls);
        var creates = GetRelationshipsOfType(graph, RelationshipType.Creates);

        // operator + is found as the method "op_Addition". Absorb is only called from there,
        // and the Catalog instance is only created there.
        Assert.That(calls, Does.Contain($"{Ns}IndexersAndOperators.Catalog.op_Addition -> {Ns}IndexersAndOperators.Catalog.Absorb"));
        Assert.That(creates, Does.Contain($"{Ns}IndexersAndOperators.Catalog.op_Addition -> {Ns}IndexersAndOperators.Catalog"));
    }

    [Test]
    public void Detected_ConversionOperatorBodyIsVisible()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        // The implicit conversion operator is found as "op_Implicit". ComputeTotal is only called from there.
        Assert.That(calls, Does.Contain($"{Ns}IndexersAndOperators.Catalog.op_Implicit -> {Ns}IndexersAndOperators.Catalog.ComputeTotal"));
    }

    [Test]
    public void Detected_FinalizerBodyIsVisible()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        // The finalizer is found as the method "Finalize". Cleanup is only called from there.
        Assert.That(calls, Does.Contain($"{Ns}IndexersAndOperators.Catalog.Finalize -> {Ns}IndexersAndOperators.Catalog.Cleanup"));
    }

    [Test]
    public void Detected_FieldInitializerObjectCreation()
    {
        var creates = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Creates);

        Assert.That(creates, Does.Contain($"{Ns}IndexersAndOperators.Catalog -> {Ns}IndexersAndOperators.DataStore"));
    }

    // --- Pattern matching ----------------------------------------------------------------------
    // A6: declaration patterns ("shape is Circle circle"), type patterns (switch arms "Square => ..")
    // and recursive patterns ("is Foo { .. }") now record their type as a Uses relationship via the
    // VisitDeclarationPattern / VisitTypePattern / VisitRecursivePattern overrides in SyntaxWalkerBase.

    [Test]
    public void Detected_ParameterTypeOfPatternMethod()
    {
        var uses = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);

        Assert.That(uses, Does.Contain($"{Ns}PatternMatching.PatternUser.DeclarationPattern -> {Ns}PatternMatching.Shape"));
    }

    [Test]
    public void Detected_DeclarationPatternTypeIsCaptured()
    {
        var uses = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);

        Assert.That(uses, Does.Contain($"{Ns}PatternMatching.PatternUser.DeclarationPattern -> {Ns}PatternMatching.Circle"));
    }

    [Test]
    public void Detected_SwitchExpressionTypePatternsAreCaptured()
    {
        var uses = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);

        Assert.That(uses, Does.Contain($"{Ns}PatternMatching.PatternUser.SwitchExpression -> {Ns}PatternMatching.Square"));
        Assert.That(uses, Does.Contain($"{Ns}PatternMatching.PatternUser.SwitchExpression -> {Ns}PatternMatching.Triangle"));
    }

    [Test]
    public void Detected_CasePatternTypeIsCaptured()
    {
        var uses = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);

        Assert.That(uses, Does.Contain($"{Ns}PatternMatching.PatternUser.CaseStatement -> {Ns}PatternMatching.Rectangle"));
    }

    // --- Property initializers -------------------------------------------------------------------
    // AnalyzePropertyBody now also handles PropertyDeclarationSyntax.Initializer (A5), treated like
    // a field initializer: the containing type "creates" the object, the property "uses" it.

    [Test]
    public void Detected_FieldInitializerCreates()
    {
        var creates = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Creates);

        Assert.That(creates, Does.Contain($"{Ns}Initializers.CarWithFieldInitializer -> {Ns}Initializers.Engine"));
    }

    [Test]
    public void Detected_PropertyInitializerCreatesRelationship()
    {
        var creates = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Creates);

        // The containing class creates the object, exactly like a field initializer.
        Assert.That(creates, Does.Contain($"{Ns}Initializers.CarWithPropertyInitializer -> {Ns}Initializers.Engine"));
        // The property itself only "uses" the type (via its type), it never "creates" it.
        Assert.That(creates, Does.Not.Contain($"{Ns}Initializers.CarWithPropertyInitializer.Engine -> {Ns}Initializers.Engine"));
    }

    // --- Type names in special contexts ----------------------------------------------------------
    // A7: catch declarations, foreach variable types and using-statement declarations now record
    // their type as Uses (VisitCatchDeclaration / VisitForEachStatement / VisitUsingStatement).
    // A8: array creation element types (new Foo[n]) via VisitArrayCreationExpression.

    [Test]
    public void Detected_ThrowStatementObjectCreation()
    {
        var creates = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Creates);

        Assert.That(creates, Does.Contain($"{Ns}TypeContexts.TypeContextUser.Throwing -> {Ns}TypeContexts.ParsingFailedException"));
    }

    [Test]
    public void Detected_CatchClauseTypeIsCaptured()
    {
        var uses = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);

        Assert.That(uses, Does.Contain($"{Ns}TypeContexts.TypeContextUser.CatchClause -> {Ns}TypeContexts.ParsingFailedException"));
    }

    [Test]
    public void Detected_ForEachVariableTypeIsCaptured()
    {
        var uses = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);

        Assert.That(uses, Does.Contain($"{Ns}TypeContexts.TypeContextUser.ForEachLoop -> {Ns}TypeContexts.InventoryItem"));
    }

    [Test]
    public void Detected_ArrayCreationElementTypeIsCaptured()
    {
        var uses = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);

        Assert.That(uses, Does.Contain($"{Ns}TypeContexts.TypeContextUser.ArrayCreation -> {Ns}TypeContexts.InventoryItem"));
    }

    [Test]
    public void Detected_UsingStatementDeclarationTypeIsCaptured()
    {
        var graph = GetTestGraph();
        var calls = GetRelationshipsOfType(graph, RelationshipType.Calls);
        var uses = GetRelationshipsOfType(graph, RelationshipType.Uses);

        // The factory call inside the using statement is found ...
        Assert.That(calls, Does.Contain($"{Ns}TypeContexts.TypeContextUser.UsingStatement -> {Ns}TypeContexts.TypeContextUser.CreateResource"));

        // ... and now the declared variable type is too.
        Assert.That(uses, Does.Contain($"{Ns}TypeContexts.TypeContextUser.UsingStatement -> {Ns}TypeContexts.PooledResource"));
    }

}
