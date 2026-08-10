using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Exploration;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Exploration;

/// <summary>
///     Tests for <see cref="CodeGraphExplorer.FindPathsBetween" />, the search for the missing piece
///     between two selected elements.
/// </summary>
[TestFixture]
public class FindPathsBetweenTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
        _explorer = new CodeGraphExplorer();
        _explorer.LoadCodeGraph(_graph);
    }

    private TestCodeGraph _graph = null!;
    private CodeGraphExplorer _explorer = null!;

    private Relationship Rel(CodeElement source, CodeElement target, RelationshipType type = RelationshipType.Calls)
    {
        var r = new Relationship(source.Id, target.Id, type);
        source.Relationships.Add(r);
        return r;
    }

    private static IEnumerable<string> Ids(SearchResult result)
    {
        return result.Elements.Select(e => e.Id);
    }

    [Test]
    public void FindPathsBetween_DirectRelationship_ReturnsThatRelationship()
    {
        var cls = _graph.CreateClass("C");
        var a = _graph.CreateMethod("A", cls);
        var b = _graph.CreateMethod("B", cls);
        var call = Rel(a, b);

        var result = _explorer.FindPathsBetween([a.Id, b.Id], 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.Relationships, Is.EquivalentTo([call]));
            Assert.That(Ids(result), Is.SupersetOf([a.Id, b.Id]));
        });
    }

    /// <summary>
    ///     The whole point of the feature: the connecting element is NOT part of the selection and
    ///     has to be discovered.
    /// </summary>
    [Test]
    public void FindPathsBetween_AddsIntermediateElementsOutsideTheSelection()
    {
        var cls = _graph.CreateClass("C");
        var a = _graph.CreateMethod("A", cls);
        var middle = _graph.CreateMethod("Middle", cls);
        var b = _graph.CreateMethod("B", cls);
        var first = Rel(a, middle);
        var second = Rel(middle, b);

        var result = _explorer.FindPathsBetween([a.Id, b.Id], 5);

        Assert.Multiple(() =>
        {
            Assert.That(Ids(result), Does.Contain(middle.Id));
            Assert.That(result.Relationships, Is.EquivalentTo([first, second]));
        });
    }

    /// <summary>
    ///     All paths of the shortest length are reported - the user has to see whether the connection
    ///     is a single thin wire or a bundle. Longer alternatives are not.
    /// </summary>
    [Test]
    public void FindPathsBetween_ReturnsAllShortestPaths_ButNoLongerOnes()
    {
        var cls = _graph.CreateClass("C");
        var a = _graph.CreateMethod("A", cls);
        var b = _graph.CreateMethod("B", cls);

        // Two paths of length 2.
        var x = _graph.CreateMethod("X", cls);
        var y = _graph.CreateMethod("Y", cls);
        var viaX1 = Rel(a, x);
        var viaX2 = Rel(x, b);
        var viaY1 = Rel(a, y);
        var viaY2 = Rel(y, b);

        // One path of length 3, must not show up.
        var z1 = _graph.CreateMethod("Z1", cls);
        var z2 = _graph.CreateMethod("Z2", cls);
        Rel(a, z1);
        Rel(z1, z2);
        Rel(z2, b);

        var result = _explorer.FindPathsBetween([a.Id, b.Id], 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.Relationships, Is.EquivalentTo([viaX1, viaX2, viaY1, viaY2]));
            Assert.That(Ids(result), Is.SupersetOf([x.Id, y.Id]));
            Assert.That(Ids(result), Does.Not.Contain(z1.Id));
            Assert.That(Ids(result), Does.Not.Contain(z2.Id));
        });
    }

    [Test]
    public void FindPathsBetween_PathLongerThanMaxLength_IsNotReported()
    {
        var cls = _graph.CreateClass("C");
        var a = _graph.CreateMethod("A", cls);
        var m1 = _graph.CreateMethod("M1", cls);
        var m2 = _graph.CreateMethod("M2", cls);
        var b = _graph.CreateMethod("B", cls);
        Rel(a, m1);
        Rel(m1, m2);
        Rel(m2, b);

        var tooShort = _explorer.FindPathsBetween([a.Id, b.Id], 2);
        var longEnough = _explorer.FindPathsBetween([a.Id, b.Id], 3);

        Assert.Multiple(() =>
        {
            Assert.That(tooShort.Elements, Is.Empty);
            Assert.That(tooShort.Relationships, Is.Empty);
            Assert.That(longEnough.Relationships, Has.Count.EqualTo(3));
        });
    }

    /// <summary>
    ///     Every ordered pair is searched, so it does not matter which end the user selected first.
    /// </summary>
    [Test]
    public void FindPathsBetween_SearchesBothDirections()
    {
        var cls = _graph.CreateClass("C");
        var a = _graph.CreateMethod("A", cls);
        var middle = _graph.CreateMethod("Middle", cls);
        var b = _graph.CreateMethod("B", cls);

        // Only B reaches A, not the other way round.
        var first = Rel(b, middle);
        var second = Rel(middle, a);

        var result = _explorer.FindPathsBetween([a.Id, b.Id], 5);

        Assert.That(result.Relationships, Is.EquivalentTo([first, second]));
    }

    /// <summary>
    ///     Selecting types instead of methods is the more convenient entry point: the endpoints are
    ///     expanded to their members, so the concrete call chain still comes out.
    ///     Every direct call has to be reported, not just the first one found. Three mechanisms have
    ///     to work together for that: several sources reaching the same target (the equal-distance
    ///     predecessor case), several targets settled on the same level, and several relationships
    ///     belonging to one hop.
    /// </summary>
    [Test]
    public void FindPathsBetween_ExpandsSelectedTypesToTheirMembers_AndReportsEveryDirectCall()
    {
        var source = _graph.CreateClass("Source");
        var m1 = _graph.CreateMethod("Source.M1", source);
        var m2 = _graph.CreateMethod("Source.M2", source);
        var withoutConnection = _graph.CreateMethod("Source.M3", source);

        var target = _graph.CreateClass("Target");
        var n1 = _graph.CreateMethod("Target.N1", target);
        var n2 = _graph.CreateMethod("Target.N2", target);

        // Two sources reaching the same target ...
        var m1ToN1 = Rel(m1, n1);
        var m2ToN1 = Rel(m2, n1);
        // ... a second target on the same level ...
        var m1ToN2 = Rel(m1, n2);
        // ... and a second relationship for that same hop.
        var m1UsesN2 = Rel(m1, n2, RelationshipType.Uses);

        var result = _explorer.FindPathsBetween([source.Id, target.Id], 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.Relationships, Is.EquivalentTo([m1ToN1, m2ToN1, m1ToN2, m1UsesN2]));
            Assert.That(Ids(result), Is.SupersetOf([m1.Id, m2.Id, n1.Id, n2.Id]));
            Assert.That(Ids(result), Does.Not.Contain(withoutConnection.Id),
                "A member that is on no path must not be dragged in.");
        });
    }

    /// <summary>
    ///     The flip side of reporting every direct call: as soon as one exists, the search ends on
    ///     that level. A second, longer connection between the same two types stays invisible - the
    ///     question is "what is the shortest connection", not "every connection".
    /// </summary>
    [Test]
    public void FindPathsBetween_DirectCall_HidesLongerConnectionBetweenTheSameTypes()
    {
        var source = _graph.CreateClass("Source");
        var direct = _graph.CreateMethod("Source.Direct", source);
        var indirect = _graph.CreateMethod("Source.Indirect", source);

        var target = _graph.CreateClass("Target");
        var n1 = _graph.CreateMethod("Target.N1", target);
        var n2 = _graph.CreateMethod("Target.N2", target);

        var helperClass = _graph.CreateClass("Helper");
        var helper = _graph.CreateMethod("Helper.M", helperClass);

        var directCall = Rel(direct, n1);

        // A real, but longer connection between the very same two classes.
        Rel(indirect, helper);
        Rel(helper, n2);

        var result = _explorer.FindPathsBetween([source.Id, target.Id], 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.Relationships, Is.EquivalentTo([directCall]));
            Assert.That(Ids(result), Does.Not.Contain(helper.Id));
            Assert.That(Ids(result), Does.Not.Contain(n2.Id));
        });
    }

    /// <summary>
    ///     A discovered element must not be added without the containers that connect it to something
    ///     already on the canvas.
    /// </summary>
    [Test]
    public void FindPathsBetween_FillsHierarchyGapsForDiscoveredElements()
    {
        var ns = _graph.CreateNamespace("Ns");
        var source = _graph.CreateClass("Source", ns);
        var sourceMethod = _graph.CreateMethod("Source.M", source);
        var target = _graph.CreateClass("Target", ns);
        var targetMethod = _graph.CreateMethod("Target.M", target);

        // The connecting method sits in a class nobody selected.
        var middleClass = _graph.CreateClass("Middle", ns);
        var middleMethod = _graph.CreateMethod("Middle.M", middleClass);

        Rel(sourceMethod, middleMethod);
        Rel(middleMethod, targetMethod);

        var result = _explorer.FindPathsBetween([ns.Id, sourceMethod.Id, targetMethod.Id], 5);

        Assert.That(Ids(result), Is.SupersetOf([middleMethod.Id, middleClass.Id]),
            "The container of the discovered method has to come with it.");
    }

    /// <summary>
    ///     Handles is stored as handler -> event but is the callback wiring, not a dependency. Walking
    ///     it would invent paths that do not exist in the code.
    /// </summary>
    [Test]
    public void FindPathsBetween_DoesNotFollowNonDependencyRelationships()
    {
        var cls = _graph.CreateClass("C");
        var a = _graph.CreateMethod("A", cls);
        var handler = _graph.CreateMethod("Handler", cls);
        var b = _graph.CreateMethod("B", cls);
        Rel(a, handler, RelationshipType.Handles);
        Rel(handler, b);

        var result = _explorer.FindPathsBetween([a.Id, b.Id], 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.Elements, Is.Empty);
            Assert.That(result.Relationships, Is.Empty);
        });
    }

    /// <summary>
    ///     The hierarchy is not a path. Otherwise every two elements would be connected through their
    ///     common ancestor and the result would be worthless.
    /// </summary>
    [Test]
    public void FindPathsBetween_DoesNotUseTheHierarchyAsPath()
    {
        var ns = _graph.CreateNamespace("Ns");
        var classA = _graph.CreateClass("A", ns);
        var methodA = _graph.CreateMethod("A.M", classA);
        var classB = _graph.CreateClass("B", ns);
        var methodB = _graph.CreateMethod("B.M", classB);

        // Explicit containment edges in addition to the parent/child links.
        Rel(ns, classA, RelationshipType.Containment);
        Rel(ns, classB, RelationshipType.Containment);
        Rel(classA, methodA, RelationshipType.Containment);
        Rel(classB, methodB, RelationshipType.Containment);

        var result = _explorer.FindPathsBetween([methodA.Id, methodB.Id], 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.Elements, Is.Empty);
            Assert.That(result.Relationships, Is.Empty);
        });
    }

    /// <summary>
    ///     Selecting a class and the namespace around it: every path between them would start and end
    ///     inside the same subtree, so the pair is skipped instead of reporting nonsense.
    /// </summary>
    [Test]
    public void FindPathsBetween_NestedSelection_IsSkipped()
    {
        var ns = _graph.CreateNamespace("Ns");
        var cls = _graph.CreateClass("C", ns);
        var a = _graph.CreateMethod("A", cls);
        var b = _graph.CreateMethod("B", cls);
        Rel(a, b);

        var result = _explorer.FindPathsBetween([ns.Id, cls.Id], 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.Elements, Is.Empty);
            Assert.That(result.Relationships, Is.Empty);
        });
    }

    [Test]
    public void FindPathsBetween_UnconnectedElements_ReturnEmptyResult()
    {
        var cls = _graph.CreateClass("C");
        var a = _graph.CreateMethod("A", cls);
        var b = _graph.CreateMethod("B", cls);
        var unrelated = _graph.CreateMethod("Unrelated", cls);
        Rel(a, unrelated);

        var result = _explorer.FindPathsBetween([a.Id, b.Id], 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.Elements, Is.Empty);
            Assert.That(result.Relationships, Is.Empty);
        });
    }

    /// <summary>
    ///     With more than two elements every pair is connected on its own - the result is the union,
    ///     not one path through all of them.
    /// </summary>
    [Test]
    public void FindPathsBetween_ThreeElements_ConnectsEveryPair()
    {
        var cls = _graph.CreateClass("C");
        var a = _graph.CreateMethod("A", cls);
        var b = _graph.CreateMethod("B", cls);
        var c = _graph.CreateMethod("C.M", cls);

        var aToB = Rel(a, _graph.CreateMethod("AB", cls));
        var abToB = Rel(_graph.Nodes["AB"], b);
        var bToC = Rel(b, _graph.CreateMethod("BC", cls));
        var bcToC = Rel(_graph.Nodes["BC"], c);

        var result = _explorer.FindPathsBetween([a.Id, b.Id, c.Id], 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.Relationships, Is.SupersetOf([aToB, abToB, bToC, bcToC]));
            Assert.That(Ids(result), Is.SupersetOf(["AB", "BC"]));
        });
    }

    [Test]
    public void FindPathsBetween_CycleOnTheWay_DoesNotHang()
    {
        var cls = _graph.CreateClass("C");
        var a = _graph.CreateMethod("A", cls);
        var m1 = _graph.CreateMethod("M1", cls);
        var m2 = _graph.CreateMethod("M2", cls);
        var b = _graph.CreateMethod("B", cls);
        Rel(a, m1);
        Rel(m1, m2);
        Rel(m2, m1); // back edge
        Rel(m2, b);

        var result = _explorer.FindPathsBetween([a.Id, b.Id], 5);

        Assert.That(Ids(result), Is.SupersetOf([m1.Id, m2.Id]));
    }

    [Test]
    public void FindPathsBetween_FewerThanTwoValidElements_ReturnsEmptyResult()
    {
        var cls = _graph.CreateClass("C");
        var a = _graph.CreateMethod("A", cls);
        Rel(a, cls, RelationshipType.Uses);

        Assert.Multiple(() =>
        {
            Assert.That(_explorer.FindPathsBetween([a.Id], 5).Elements, Is.Empty);
            Assert.That(_explorer.FindPathsBetween([], 5).Elements, Is.Empty);
            Assert.That(_explorer.FindPathsBetween([a.Id, "DoesNotExist"], 5).Elements, Is.Empty);
        });
    }

    [Test]
    public void FindPathsBetween_MaxLengthBelowOne_ReturnsEmptyResult()
    {
        var cls = _graph.CreateClass("C");
        var a = _graph.CreateMethod("A", cls);
        var b = _graph.CreateMethod("B", cls);
        Rel(a, b);

        Assert.That(_explorer.FindPathsBetween([a.Id, b.Id], 0).Relationships, Is.Empty);
    }
}
