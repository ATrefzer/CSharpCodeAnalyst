using CodeParserTests.Helper;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.ArchitecturalRules;

[TestFixture]
public class PatternMatcherTests
{

    [SetUp]
    public void SetUp()
    {
        _codeGraph = new TestCodeGraph();
    }

    private TestCodeGraph _codeGraph;

    [Test]
    public void ResolvePattern_DuplicateFullName_ReturnsAllMatches()
    {
        // Overloaded methods share the same full path but are distinct elements. A pattern must
        // resolve to ALL of them, otherwise e.g. an ALLOW rule would only cover one overload.
        var ns = _codeGraph.CreateNamespace("MyApp.Business");
        var overload1 = new CodeElement("m1", CodeElementType.Method, "Save", "MyApp.Business.Service.Save", ns);
        var overload2 = new CodeElement("m2", CodeElementType.Method, "Save", "MyApp.Business.Service.Save", ns);
        _codeGraph.Nodes["m1"] = overload1;
        _codeGraph.Nodes["m2"] = overload2;
        ns.Children.Add(overload1);
        ns.Children.Add(overload2);

        var result = PatternMatcher.ResolvePattern("MyApp.Business.Service.Save", _codeGraph);

        Assert.That(result, Is.EquivalentTo(new[] { "m1", "m2" }));
    }

    [Test]
    public void ResolvePattern_ExactMatch_ShouldReturnElement()
    {
        // Arrange
        var businessNamespace = _codeGraph.CreateNamespace("MyApp.Business");
        var pattern = "MyApp.Business";

        // Act
        var result = PatternMatcher.ResolvePattern(pattern, _codeGraph);

        // Assert
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result.Contains(businessNamespace.Id));
    }

    [Test]
    public void ResolvePattern_DirectChildren_ShouldIncludeChildrenOnly()
    {
        // Arrange
        var businessNamespace = _codeGraph.CreateNamespace("MyApp.Business");
        var orderService = _codeGraph.CreateClass("OrderService", businessNamespace);
        var userService = _codeGraph.CreateClass("UserService", businessNamespace);
        var grandChild = _codeGraph.CreateMethod("ProcessOrder", orderService);

        var pattern = "MyApp.Business.*";

        // Act
        var result = PatternMatcher.ResolvePattern(pattern, _codeGraph);

        // Assert
        Assert.That(result.Count, Is.EqualTo(3)); // Business + OrderService + UserService (no grandchildren)
        Assert.That(result.Contains(businessNamespace.Id));
        Assert.That(result.Contains(orderService.Id));
        Assert.That(result.Contains(userService.Id));
        Assert.That(result.Contains(grandChild.Id), Is.False);
    }

    [Test]
    public void ResolvePattern_RecursiveChildren_ShouldIncludeAllDescendants()
    {
        // Arrange
        var businessNamespace = _codeGraph.CreateNamespace("MyApp.Business");
        var orderService = _codeGraph.CreateClass("OrderService", businessNamespace);
        var processMethod = _codeGraph.CreateMethod("ProcessOrder", orderService);
        var validateMethod = _codeGraph.CreateMethod("ValidateOrder", orderService);

        var pattern = "MyApp.Business.**";

        // Act
        var result = PatternMatcher.ResolvePattern(pattern, _codeGraph);

        // Assert
        Assert.That(result.Count, Is.EqualTo(4)); // All elements
        Assert.That(result.Contains(businessNamespace.Id));
        Assert.That(result.Contains(orderService.Id));
        Assert.That(result.Contains(processMethod.Id));
        Assert.That(result.Contains(validateMethod.Id));
    }

    [Test]
    public void ResolvePattern_NoMatch_ShouldReturnEmpty()
    {
        // Arrange
        _codeGraph.CreateNamespace("MyApp.Business");
        var pattern = "NonExistent.Namespace";

        // Act
        var result = PatternMatcher.ResolvePattern(pattern, _codeGraph);

        // Assert
        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public void ResolvePattern_CaseMismatch_ShouldNotMatch()
    {
        // C# identifiers are case-sensitive, so a pattern in the wrong case is a typo, not a match.
        // It must resolve to nothing so the engine can raise its no-match warning.
        _codeGraph.CreateNamespace("MyApp.Business");
        var pattern = "myapp.business";

        // Act
        var result = PatternMatcher.ResolvePattern(pattern, _codeGraph);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ResolvePattern_ExactCase_ShouldMatch()
    {
        // Arrange
        var businessNamespace = _codeGraph.CreateNamespace("MyApp.Business");

        // Act
        var result = PatternMatcher.ResolvePattern("MyApp.Business", _codeGraph);

        // Assert
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result.Contains(businessNamespace.Id));
    }

    [Test]
    public void ResolvePattern_EmptyPattern_ShouldReturnEmpty()
    {
        // Arrange
        _codeGraph.CreateNamespace("MyApp.Business");
        var pattern = "";

        // Act
        var result = PatternMatcher.ResolvePattern(pattern, _codeGraph);

        // Assert
        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public void ResolvePattern_PathWithoutTypeParameters_MatchesGenericElement()
    {
        // A rule file written before the element names carried type parameters must keep working -
        // a rule that stops matching is a no-op nobody notices.
        var ns = _codeGraph.CreateNamespace("MyApp.Business");
        var cache = AddClass("c1", "Cache<T>", ns);

        var result = PatternMatcher.ResolvePattern("MyApp.Business.Cache", _codeGraph);

        Assert.That(result, Is.EquivalentTo(new[] { cache.Id }));
    }

    [Test]
    public void ResolvePattern_PathWithoutTypeParameters_MatchesGenericAndNonGenericSibling()
    {
        // "Cache" and "Cache<T>" are two elements. The short path cannot tell them apart, so it means
        // both - the same reasoning as for overloaded members above.
        var ns = _codeGraph.CreateNamespace("MyApp.Business");
        var plain = AddClass("c1", "Cache", ns);
        var generic = AddClass("c2", "Cache<T>", ns);

        var result = PatternMatcher.ResolvePattern("MyApp.Business.Cache", _codeGraph);

        Assert.That(result, Is.EquivalentTo(new[] { plain.Id, generic.Id }));
    }

    [Test]
    public void ResolvePattern_PathWithTypeParameters_MatchesOnlyThatElement()
    {
        // Spelling the type parameters out is how a rule selects exactly one of the two.
        var ns = _codeGraph.CreateNamespace("MyApp.Business");
        AddClass("c1", "Cache", ns);
        var generic = AddClass("c2", "Cache<T>", ns);

        var result = PatternMatcher.ResolvePattern("MyApp.Business.Cache<T>", _codeGraph);

        Assert.That(result, Is.EquivalentTo(new[] { generic.Id }));
    }

    [Test]
    public void ResolvePattern_PathWithWrongTypeParameters_ShouldNotMatch()
    {
        // Written-out type parameters are compared literally, like every other part of the name.
        var ns = _codeGraph.CreateNamespace("MyApp.Business");
        AddClass("c1", "Cache<T>", ns);

        var result = PatternMatcher.ResolvePattern("MyApp.Business.Cache<TKey>", _codeGraph);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ResolvePattern_PathWithoutTypeParameters_MatchesMemberOfGenericType()
    {
        // The type parameters sit in the middle of the path here, and a generic method adds a second
        // list at the end - both have to drop out for the short path to match.
        var ns = _codeGraph.CreateNamespace("MyApp.Business");
        var cache = AddClass("c1", "Cache<T>", ns);
        var add = AddMethod("m1", "Add", cache);
        var map = AddMethod("m2", "Map<TResult>", cache);

        Assert.Multiple(() =>
        {
            Assert.That(PatternMatcher.ResolvePattern("MyApp.Business.Cache.Add", _codeGraph),
                Is.EquivalentTo(new[] { add.Id }));
            Assert.That(PatternMatcher.ResolvePattern("MyApp.Business.Cache.Map", _codeGraph),
                Is.EquivalentTo(new[] { map.Id }));
        });
    }

    [Test]
    public void ResolvePattern_RecursiveWildcardOnGenericType_ShouldIncludeMembers()
    {
        // The wildcard suffix is stripped before the path is resolved, so it works on both spellings.
        var ns = _codeGraph.CreateNamespace("MyApp.Business");
        var cache = AddClass("c1", "Cache<T>", ns);
        var add = AddMethod("m1", "Add", cache);

        Assert.Multiple(() =>
        {
            Assert.That(PatternMatcher.ResolvePattern("MyApp.Business.Cache.**", _codeGraph),
                Is.EquivalentTo(new[] { cache.Id, add.Id }));
            Assert.That(PatternMatcher.ResolvePattern("MyApp.Business.Cache<T>.**", _codeGraph),
                Is.EquivalentTo(new[] { cache.Id, add.Id }));
        });
    }

    [Test]
    public void ResolveSubtree_PathWithoutTypeParameters_MatchesGenericElement()
    {
        // NOCYCLES takes the same path resolution, so the short form has to work there as well.
        var ns = _codeGraph.CreateNamespace("MyApp.Business");
        var cache = AddClass("c1", "Cache<T>", ns);
        var add = AddMethod("m1", "Add", cache);

        var result = PatternMatcher.ResolveSubtree("MyApp.Business.Cache", _codeGraph);

        Assert.That(result, Is.EquivalentTo(new[] { cache.Id, add.Id }));
    }

    [Test]
    public void ResolvePattern_ComplexHierarchy_ShouldWorkCorrectly()
    {
        // Arrange
        // Create: MyApp.Business.Services.OrderService.ProcessOrder()
        var myApp = _codeGraph.CreateNamespace("MyApp");
        var business = _codeGraph.CreateNamespace("MyApp.Business", myApp);
        var services = _codeGraph.CreateNamespace("MyApp.Business.Services", business);
        var orderService = _codeGraph.CreateClass("MyApp.Business.Services.OrderService", services);
        var processMethod = _codeGraph.CreateMethod("MyApp.Business.Services.OrderService.ProcessOrder", orderService);

        // Test exact match
        var exactResult = PatternMatcher.ResolvePattern("MyApp.Business.Services.OrderService", _codeGraph);
        Assert.That(exactResult.Count, Is.EqualTo(1)); // OrderService + ProcessOrder (GetChildrenIncludingSelf)

        // Test recursive match
        var recursiveResult = PatternMatcher.ResolvePattern("MyApp.Business.**", _codeGraph);
        Assert.That(recursiveResult.Count, Is.EqualTo(4)); // Business + Services + OrderService + ProcessOrder
    }

    /// <summary>
    ///     The graph helper names an element after its id, which cannot express a name and a full name
    ///     that differ. These build the full name from the parent, the way the parser does, so a type
    ///     parameter list can sit in the middle of a path and not only at its end.
    /// </summary>
    private CodeElement AddClass(string id, string name, CodeElement parent)
    {
        return Add(id, CodeElementType.Class, name, parent);
    }

    private CodeElement AddMethod(string id, string name, CodeElement parent)
    {
        return Add(id, CodeElementType.Method, name, parent);
    }

    private CodeElement Add(string id, CodeElementType elementType, string name, CodeElement parent)
    {
        var element = new CodeElement(id, elementType, name, parent.FullName + "." + name, parent);
        parent.Children.Add(element);
        _codeGraph.Nodes[id] = element;
        return element;
    }
}