using CodeParser.Parser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Tests for SyntaxExtensions.IsInsideNameOf - the decision that drives routing a property reference
///     inside nameof(...) to a "Uses" edge on the property instead of a getter/setter access.
/// </summary>
[TestFixture]
public class NameOfDetectionTests
{
    [Test]
    public void UnqualifiedNameOf_IsDetected()
    {
        Assert.That(IsPropInNameOf("var s = nameof(Prop);"), Is.True);
    }

    [Test]
    public void QualifiedNameOf_IsDetected()
    {
        // "this.Prop" inside nameof - the analyzer classifies the member-access node.
        Assert.That(IsMemberAccessInNameOf("var s = nameof(this.Prop);"), Is.True);
    }

    [Test]
    public void PlainRead_IsNotNameOf()
    {
        Assert.That(IsPropInNameOf("var x = Prop;"), Is.False);
    }

    [Test]
    public void AssignmentTarget_IsNotNameOf()
    {
        Assert.That(IsPropInNameOf("Prop = 1;"), Is.False);
    }

    [Test]
    public void RealMethodArgument_IsNotNameOf()
    {
        // Foo is a real method; its invocation must not be mistaken for nameof.
        Assert.That(IsPropInNameOf("Foo(Prop);"), Is.False);
    }

    private static bool IsPropInNameOf(string statement)
    {
        var (root, model) = Compile(statement);
        var node = root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .First(id => id.Identifier.Text == "Prop");
        return node.IsInsideNameOf(model);
    }

    private static bool IsMemberAccessInNameOf(string statement)
    {
        var (root, model) = Compile(statement);
        var node = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .First(m => m.Name.Identifier.Text == "Prop");
        return node.IsInsideNameOf(model);
    }

    private static (SyntaxNode root, SemanticModel model) Compile(string statement)
    {
        var code = $$"""
                     class C
                     {
                         int Prop { get; set; }
                         void Foo(int x) { }
                         void M() { {{statement}} }
                     }
                     """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        return (tree.GetRoot(), compilation.GetSemanticModel(tree));
    }
}
