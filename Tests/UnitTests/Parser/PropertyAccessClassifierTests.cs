using CodeParser.Parser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class PropertyAccessClassifierTests
{
    [Test]
    public void SimpleRead_FromLocalAssignment_IsRead()
    {
        Assert.That(ClassifyProp("var x = Prop;"), Is.EqualTo(PropertyAccessKind.Read));
    }

    [Test]
    public void MethodArgument_IsRead()
    {
        Assert.That(ClassifyProp("M(Prop);"), Is.EqualTo(PropertyAccessKind.Read));
    }

    [Test]
    public void ReturnValue_IsRead()
    {
        var code = """
                   class C
                   {
                       int Prop { get; set; }
                       int M() { return Prop; }
                   }
                   """;
        Assert.That(ClassifyPropIn(code), Is.EqualTo(PropertyAccessKind.Read));
    }

    [Test]
    public void SimpleAssignmentTarget_IsWrite()
    {
        Assert.That(ClassifyProp("Prop = 1;"), Is.EqualTo(PropertyAccessKind.Write));
    }

    [Test]
    public void ParenthesizedAssignmentTarget_IsWrite()
    {
        Assert.That(ClassifyProp("(Prop) = 1;"), Is.EqualTo(PropertyAccessKind.Write));
    }

    [Test]
    public void CompoundAssignmentTarget_IsReadWrite()
    {
        Assert.That(ClassifyProp("Prop += 1;"), Is.EqualTo(PropertyAccessKind.ReadWrite));
    }

    [Test]
    public void CoalesceAssignmentTarget_IsReadWrite()
    {
        // ??= reads the current value and writes only if null. A nullable property keeps the snippet valid.
        var code = """
                   class C
                   {
                       int? Prop { get; set; }
                       void M() { Prop ??= 1; }
                   }
                   """;
        Assert.That(ClassifyPropIn(code), Is.EqualTo(PropertyAccessKind.ReadWrite));
    }

    [Test]
    public void PostfixIncrement_IsReadWrite()
    {
        Assert.That(ClassifyProp("Prop++;"), Is.EqualTo(PropertyAccessKind.ReadWrite));
    }

    [Test]
    public void PrefixDecrement_IsReadWrite()
    {
        Assert.That(ClassifyProp("--Prop;"), Is.EqualTo(PropertyAccessKind.ReadWrite));
    }

    [Test]
    public void AssignmentRightHandSide_IsRead()
    {
        // Prop appears on the right side of an assignment to another target.
        Assert.That(ClassifyProp("other = Prop;"), Is.EqualTo(PropertyAccessKind.Read));
    }

    [Test]
    public void ReadingMemberOfPropertyValue_IsRead()
    {
        // The property is the receiver of a further member access ("Prop.Inner = 1"): its value is
        // read to reach Inner; only Inner is written. Uses a reference type with a real field so the
        // snippet is valid C#.
        var code = """
                   class Box { public int Inner; }
                   class C
                   {
                       Box Prop { get; set; }
                       void M() { Prop.Inner = 1; }
                   }
                   """;
        Assert.That(ClassifyPropIn(code), Is.EqualTo(PropertyAccessKind.Read));
    }

    [Test]
    public void QualifiedAssignmentTarget_IsWrite()
    {
        // "this.Prop = 1": the member access "this.Prop" is the write target.
        var kind = ClassifyMemberAccess("this.Prop = 1;", "Prop");
        Assert.That(kind, Is.EqualTo(PropertyAccessKind.Write));
    }

    [Test]
    public void ObjectInitializerMember_IsWrite()
    {
        // Object initializer "new C { Prop = 1 }" assigns the property.
        var statement = "var c = new C { Prop = 1 };";
        var reference = FindFirstIdentifier(ParseStatement(statement), "Prop");
        Assert.That(PropertyAccessClassifier.Classify(reference), Is.EqualTo(PropertyAccessKind.Write));
    }

    /// <summary>
    ///     Parses a statement, finds the first identifier named "Prop" and classifies it.
    /// </summary>
    private static PropertyAccessKind ClassifyProp(string statement)
    {
        var reference = FindFirstIdentifier(ParseStatement(statement), "Prop");
        return PropertyAccessClassifier.Classify(reference);
    }

    /// <summary>
    ///     Parses a full compilation unit, finds the first identifier named "Prop" and classifies it.
    ///     Used by cases that need a specific declaration context (a non-void method, a nullable property,
    ///     a receiver type with a field) to stay valid C#.
    /// </summary>
    private static PropertyAccessKind ClassifyPropIn(string code)
    {
        var reference = FindFirstIdentifier(CSharpSyntaxTree.ParseText(code).GetRoot(), "Prop");
        return PropertyAccessClassifier.Classify(reference);
    }

    /// <summary>
    ///     Parses a statement and classifies the member-access expression whose member name matches
    ///     <paramref name="memberName" /> (e.g. the "this.Prop" node itself, not the inner identifier).
    /// </summary>
    private static PropertyAccessKind ClassifyMemberAccess(string statement, string memberName)
    {
        var root = ParseStatement(statement);
        var memberAccess = root.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .First(m => m.Name.Identifier.Text == memberName);
        return PropertyAccessClassifier.Classify(memberAccess);
    }

    private static IdentifierNameSyntax FindFirstIdentifier(SyntaxNode root, string name)
    {
        return root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .First(id => id.Identifier.Text == name);
    }

    // Shared context for the simple cases. Prop is an int and M is void so that reads, plain/compound
    // assignments and ++/-- are all valid C#. Cases that would not compile here (return, ??=, member
    // access on the value) bring their own compilation unit via ClassifyPropIn.
    private static SyntaxNode ParseStatement(string statement)
    {
        var code = $$"""
                     class C
                     {
                         int Prop { get; set; }
                         int other;
                         void M(int x) { {{statement}} }
                     }
                     """;
        return CSharpSyntaxTree.ParseText(code).GetRoot();
    }
}
