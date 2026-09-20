using CSharpCodeAnalyst.CodeParser.Parser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     A `file` type (C# 11) is unique only within the file that declares it. Two unrelated files may each
///     declare their own type of the same simple name - source generators lean on exactly this: .NET's
///     ComInterfaceGenerator emits one `file class InterfaceImplementation` per [GeneratedComInterface]
///     interface, so a project with several such interfaces ends up with several distinct types sharing one
///     name. <see cref="SymbolExtensions.Key" /> must tell them apart, or phase 1 collapses them onto a
///     single CodeElement and phase 2 derives every relationship from whichever declaration happened to be
///     visited first.
/// </summary>
[TestFixture]
public class SymbolExtensionsTests
{
    private static (INamedTypeSymbol first, INamedTypeSymbol second) DeclareSameNamedFileLocalTypeInTwoFiles(
        string classBody = "")
    {
        var code = $"file class Duplicate {{ {classBody} }}";

        var treeA = CSharpSyntaxTree.ParseText(code, path: "A.cs");
        var treeB = CSharpSyntaxTree.ParseText(code, path: "B.cs");

        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var compilation = CSharpCompilation.Create("Test", [treeA, treeB], references);

        INamedTypeSymbol GetDeclaredType(SyntaxTree tree)
        {
            var declaration = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
            return (INamedTypeSymbol)compilation.GetSemanticModel(tree).GetDeclaredSymbol(declaration)!;
        }

        return (GetDeclaredType(treeA), GetDeclaredType(treeB));
    }

    [Test]
    public void Key_TwoFileLocalTypesWithTheSameName_AreDistinct()
    {
        var (first, second) = DeclareSameNamedFileLocalTypeInTwoFiles();

        // Before the fix both keys were "Duplicate_NamedType" - identical, even though these are two
        // unrelated Roslyn symbols from two different files.
        Assert.That(first.Key(), Is.Not.EqualTo(second.Key()));
    }

    [Test]
    public void Key_MembersOfFileLocalTypesWithTheSameName_AreDistinct()
    {
        var (first, second) = DeclareSameNamedFileLocalTypeInTwoFiles("public void Run() { }");

        var firstRun = first.GetMembers("Run").Single();
        var secondRun = second.GetMembers("Run").Single();

        // The member's key is built from the parent chain, so once the containing type's key is
        // disambiguated, its members follow automatically.
        Assert.That(firstRun.Key(), Is.Not.EqualTo(secondRun.Key()));
    }

    [Test]
    public void Key_SameFileLocalTypeAskedTwice_IsStable()
    {
        var (first, _) = DeclareSameNamedFileLocalTypeInTwoFiles();

        Assert.That(first.Key(), Is.EqualTo(first.Key()));
    }
}
