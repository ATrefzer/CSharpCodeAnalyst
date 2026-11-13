using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     An unwritten <c>base()</c> can only mean a constructor the derived type is allowed to call. An
///     internal constructor of another assembly is not among them, however well its parameters fit -
///     which takes two projects to show, so this fixture builds its own solution.
///     <para>
///         Overloads share the path "Base..ctor"; every constructor calls a marker method of its own
///         and is identified by that.
///     </para>
/// </summary>
[TestFixture]
public class ImplicitBaseConstructorAccessibilityParseTests
{
    private const string LibraryCode = """
                                       namespace Lib;

                                       public static class Marker
                                       {
                                           public static void ProtectedParams() { }
                                           public static void InternalOptional() { }
                                           public static void GuardedProtectedParams() { }
                                           public static void GuardedPrivateOptional() { }
                                       }

                                       /// <summary>The better match is internal - callable inside this assembly only.</summary>
                                       public class Base
                                       {
                                           protected Base(params int[] values) { Marker.ProtectedParams(); }
                                           internal Base(int value = 0) { Marker.InternalOptional(); }
                                       }

                                       public class SameAssembly : Base { }

                                       /// <summary>The better match is private - callable from a nested type only.</summary>
                                       public class Guarded
                                       {
                                           protected Guarded(params int[] values) { Marker.GuardedProtectedParams(); }
                                           private Guarded(int value = 0) { Marker.GuardedPrivateOptional(); }

                                           public class Nested : Guarded { }
                                       }

                                       public class Outside : Guarded { }
                                       """;

    private const string ApplicationCode = """
                                           namespace App;

                                           public class Generated : Lib.Base { }

                                           public class Primary(int size) : Lib.Base
                                           {
                                               public int Size => size;
                                           }

                                           public class Written : Lib.Base
                                           {
                                               public Written() { }
                                           }
                                           """;

    private CodeGraph _graph = null!;

    [OneTimeSetUp]
    public async Task ParseTwoProjects()
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var library = ProjectId.CreateNewId();
        var application = ProjectId.CreateNewId();

        // ".csproj" paths and non-null document paths: see Parser.BuildAdhocSolution.
        var solution = new AdhocWorkspace().CurrentSolution
            .AddProject(ProjectInfo.Create(library, VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp, "Lib.csproj")
                .WithCompilationOptions(options)
                .WithMetadataReferences(references))
            .AddProject(ProjectInfo.Create(application, VersionStamp.Default, "App", "App", LanguageNames.CSharp, "App.csproj")
                .WithCompilationOptions(options)
                .WithMetadataReferences(references)
                .WithProjectReferences([new ProjectReference(library)]))
            .AddDocument(DocumentId.CreateNewId(library), "Lib.cs", SourceText.From(LibraryCode), filePath: "Lib.cs")
            .AddDocument(DocumentId.CreateNewId(application), "App.cs", SourceText.From(ApplicationCode), filePath: "App.cs");

        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false));
        _graph = (await parser.ParseSolutionInternal(solution)).CodeGraph;
    }

    private static string PathOf(CodeElement element)
    {
        var parts = new List<string>();
        var current = element;
        while (current is not null && current.ElementType is not (CodeElementType.Namespace or CodeElementType.Assembly))
        {
            parts.Insert(0, current.Name);
            current = current.Parent;
        }

        return string.Join(".", parts);
    }

    /// <summary>The elements calling the constructor that calls the given marker method.</summary>
    private string[] CallersOfTheConstructorMarkedWith(string marker)
    {
        var markerMethod = _graph.Nodes.Values.Single(n => PathOf(n) == $"Marker.{marker}");
        var constructor = _graph.Nodes.Values.Single(n => n.Relationships.Any(r => r.Type == RelationshipType.Calls && r.TargetId == markerMethod.Id));

        return _graph.Nodes.Values
            .Where(source => source.Relationships.Any(r => r.Type == RelationshipType.Calls && r.TargetId == constructor.Id))
            .Select(PathOf)
            .Order()
            .ToArray();
    }

    [Test]
    public void FromAnotherAssembly_TheInternalConstructorIsNoCandidate()
    {
        // All three forms: generated constructor, primary constructor, written constructor.
        Assert.That(CallersOfTheConstructorMarkedWith("ProtectedParams"),
            Is.EqualTo(new[] { "Generated", "Primary..ctor", "Written..ctor" }));
    }

    [Test]
    public void InsideItsAssembly_TheInternalConstructorIsTheBetterMatch()
    {
        Assert.That(CallersOfTheConstructorMarkedWith("InternalOptional"), Is.EqualTo(new[] { "SameAssembly" }));
    }

    [Test]
    public void APrivateConstructor_IsACandidateForANestedTypeOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CallersOfTheConstructorMarkedWith("GuardedPrivateOptional"), Is.EqualTo(new[] { "Guarded.Nested" }));
            Assert.That(CallersOfTheConstructorMarkedWith("GuardedProtectedParams"), Is.EqualTo(new[] { "Outside" }));
        });
    }
}
