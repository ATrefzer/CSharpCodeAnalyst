using CodeParserTests.Helper;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

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
    public void ResolvePattern_CaseInsensitive_ShouldMatch()
    {
        // Arrange
        var businessNamespace = _codeGraph.CreateNamespace("MyApp.Business");
        var pattern = "myapp.business";

        // Act
        var result = PatternMatcher.ResolvePattern(pattern, _codeGraph);

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
}