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

    // --- Primary constructors and positional records ------------------------------------------
    // Phase 1 only collects ConstructorDeclarationSyntax, so primary constructors and the generated
    // positional properties have no method element. A3 adds the parameter types of the primary
    // constructor as Uses relationships directly on the type element.

    [Test]
    public void Detected_RecordsAreCodeElements()
    {
        var records = GetAllNodesOfType(GetTestGraph(), CodeElementType.Record);

        Assert.That(records, Does.Contain($"{Ns}PrimaryConstructors.Order"));
        Assert.That(records, Does.Contain($"{Ns}PrimaryConstructors.OrderId"));
    }

    [Test]
    public void Detected_PositionalRecordParameterTypesCreateRelationship()
    {
        var uses = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);

        Assert.That(uses, Does.Contain($"{Ns}PrimaryConstructors.Order -> {Ns}PrimaryConstructors.OrderId"));
        Assert.That(uses, Does.Contain($"{Ns}PrimaryConstructors.Order -> {Ns}PrimaryConstructors.Customer"));
    }

    [Test]
    public void Detected_ClassPrimaryConstructorParameterTypeCreatesRelationship()
    {
        var uses = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);

        Assert.That(uses, Does.Contain($"{Ns}PrimaryConstructors.Inventory -> {Ns}PrimaryConstructors.Warehouse"));
    }

    [Test]
    public void Detected_RecordHasNoSelfReferenceFromGeneratedIEquatable()
    {
        var uses = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);

        // Records implement the synthesized IEquatable<Self>; the type argument must not create a
        // self-reference (A3b). The real parameter-type dependency stays as a sanity check.
        Assert.That(uses, Does.Not.Contain($"{Ns}PrimaryConstructors.Order -> {Ns}PrimaryConstructors.Order"));
        Assert.That(uses, Does.Contain($"{Ns}PrimaryConstructors.Order -> {Ns}PrimaryConstructors.OrderId"));
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
    public void Detected_BaseConstructorChainingIsCaptured()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        Assert.That(calls, Does.Contain($"{Ns}ConstructorChaining.DerivedService..ctor -> {Ns}ConstructorChaining.BaseService..ctor"));
    }

    [Test]
    public void Detected_ThisConstructorChainingIsCaptured()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        Assert.That(calls, Does.Contain($"{Ns}ConstructorChaining.SelfChaining..ctor -> {Ns}ConstructorChaining.SelfChaining..ctor"));
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

    // --- Method groups outside of argument lists -------------------------------------------------
    // A9: method groups in assignments, local declarations and return statements now record a
    // Uses + IsMethodGroup relationship via the IMethodSymbol branches in AnalyzeIdentifier /
    // AnalyzeMemberAccess. The invocation guard keeps real calls (incl. obj?.Method()) out.

    [Test]
    public void Detected_MethodGroupAsInvocationArgument()
    {
        var usages = GetAllMethodGroupUsages(GetTestGraph());

        Assert.That(usages, Does.Contain($"{Ns}MethodGroups.MethodGroupUser.PassAsArgument -> {Ns}MethodGroups.Worker.DoParallelWork"));
    }

    [Test]
    public void Detected_MethodGroupAssignedToLocalIsCaptured()
    {
        var usages = GetAllMethodGroupUsages(GetTestGraph());

        Assert.That(usages, Does.Contain($"{Ns}MethodGroups.MethodGroupUser.AssignToLocal -> {Ns}MethodGroups.Worker.DoWork"));
    }

    [Test]
    public void Detected_MethodGroupReturnedIsCaptured()
    {
        var usages = GetAllMethodGroupUsages(GetTestGraph());

        Assert.That(usages, Does.Contain($"{Ns}MethodGroups.MethodGroupUser.ReturnMethodGroup -> {Ns}MethodGroups.Worker.DoMoreWork"));
    }

    [Test]
    public void Detected_MethodGroupAssignedToFieldIsCaptured()
    {
        var usages = GetAllMethodGroupUsages(GetTestGraph());

        Assert.That(usages, Does.Contain($"{Ns}MethodGroups.MethodGroupUser.AssignToField -> {Ns}MethodGroups.Worker.DoExtraWork"));
    }

    [Test]
    public void Detected_ModernEventRegistrationCreatesHandles()
    {
        var handles = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Handles);

        Assert.That(handles, Does.Contain($"{Ns}MethodGroups.Worker.OnTock -> {Ns}MethodGroups.MethodGroupUser.Ticked"));
    }

    [Test]
    public void Detected_OldStyleEventRegistrationCreatesHandles()
    {
        var handles = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Handles);

        // event += new EventHandler(handler): the handler is taken from the delegate ctor argument.
        Assert.That(handles, Does.Contain($"{Ns}MethodGroups.Worker.OnTick -> {Ns}MethodGroups.MethodGroupUser.Ticked"));
    }

    // --- Generic type-parameter constraints ------------------------------------------------------
    // where T : IFoo / where T : BaseClass. AnalyzeInheritanceRelationships looks only at a type's
    // own base/interfaces, AnalyzeMethodRelationships only at parameter/return types - the
    // ITypeParameterSymbol.ConstraintTypes are never read.

    [Test]
    public void Sanity_GenericConstraintTypesAreParsed()
    {
        var graph = GetTestGraph();
        var classes = GetAllClasses(graph);
        var interfaces = GetAllNodesOfType(graph, CodeElementType.Interface);

        // Guards against the Gap_* assertions below passing trivially due to a wrong name.
        Assert.That(classes, Does.Contain($"{Ns}GenericConstraints.ConstrainedRepository"));
        Assert.That(classes, Does.Contain($"{Ns}GenericConstraints.ConstraintUser"));
        Assert.That(classes, Does.Contain($"{Ns}GenericConstraints.EntityBase"));
        Assert.That(interfaces, Does.Contain($"{Ns}GenericConstraints.IRepository"));
    }

    [Test]
    public void Gap_ClassConstraintTypeIsNotCaptured()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}GenericConstraints.ConstrainedRepository -> {Ns}GenericConstraints.IRepository"));
    }

    [Test]
    public void Gap_MethodConstraintTypeIsNotCaptured()
    {
        var all = GetAllRelationships(GetTestGraph());

        Assert.That(all, Does.Not.Contain($"{Ns}GenericConstraints.ConstraintUser.Process -> {Ns}GenericConstraints.EntityBase"));
    }
}
