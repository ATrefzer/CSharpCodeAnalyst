using CodeParserTests.Helper;
using CSharpCodeAnalyst.Analyzers.ConsistencyRules;

namespace CodeParserTests.UnitTests.ConsistencyRules;

[TestFixture]
public class PatternMatcherTests
{
    private TestCodeGraph _codeGraph;

    [SetUp]
    public void SetUp()
    {
        _codeGraph = new TestCodeGraph();
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
        Assert.AreEqual(1, result.Count);
        Assert.Contains(businessNamespace.Id, result);
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
        Assert.AreEqual(3, result.Count); // Business + OrderService + UserService (no grandchildren)
        Assert.Contains(businessNamespace.Id, result);
        Assert.Contains(orderService.Id, result);
        Assert.Contains(userService.Id, result);
        Assert.IsFalse(result.Contains(grandChild.Id));
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
        Assert.AreEqual(4, result.Count); // All elements
        Assert.Contains(businessNamespace.Id, result);
        Assert.Contains(orderService.Id, result);
        Assert.Contains(processMethod.Id, result);
        Assert.Contains(validateMethod.Id, result);
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
        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void ResolvePattern_CaseInsensitive_ShouldMatch()
    {
        // Arrange
        var businessNamespace = _codeGraph.CreateNamespace("MyApp.Business");
        var pattern = "myapp.business";

        // Act
        var result = PatternMatcher.ResolvePattern(pattern, _codeGraph);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.Contains(businessNamespace.Id, result);
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
        Assert.AreEqual(0, result.Count);
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
        Assert.AreEqual(2, exactResult.Count); // OrderService + ProcessOrder (GetChildrenIncludingSelf)

        // Test recursive match
        var recursiveResult = PatternMatcher.ResolvePattern("MyApp.Business.**", _codeGraph);
        Assert.AreEqual(4, recursiveResult.Count); // Business + Services + OrderService + ProcessOrder
    }
}