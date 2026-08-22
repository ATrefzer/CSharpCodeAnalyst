using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     The C# parser states every method's role rather than leaving it to be guessed from the name.
///     Roslyn knows what it declared, so this is exact where the name test it replaced was a convention
///     that only ever held for C#.
/// </summary>
[TestFixture]
public class MemberRoleParseTests
{
    [OneTimeSetUp]
    public async Task ParseCode()
    {
        const string code = """
                            namespace Demo;

                            public class Widget
                            {
                                private int _count;

                                static Widget() { }

                                public Widget() { }

                                public Widget(int count) { _count = count; }

                                ~Widget() { }

                                public int Count { get; set; }

                                public void Work() { }

                                public static Widget operator +(Widget a, Widget b) => a;
                            }

                            /// <summary>
                            ///     A method that is called "Finalize" without being a finalizer. Legal C#, and
                            ///     exactly what the old name test got wrong.
                            /// </summary>
                            public class Impostor
                            {
                                public new void Finalize() { }
                            }
                            """;

        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false));
        var result = await parser.ParseSourceAsync(code);
        _graph = result.CodeGraph;
    }

    private CodeGraph _graph = null!;

    private List<CodeElement> MembersOf(string typeName)
    {
        return _graph.Nodes.Values.Where(n => n.Parent?.Name == typeName).ToList();
    }

    private CodeElement Member(string typeName, string name)
    {
        return MembersOf(typeName).Single(n => n.Name == name);
    }

    [Test]
    public void EveryKindOfLifecycleMember_IsMarked()
    {
        var members = MembersOf("Widget");

        Assert.Multiple(() =>
        {
            // Both constructors carry the same name, so only the role distinguishes them from a method.
            Assert.That(members.Where(m => m.Name == ".ctor").Select(m => m.MemberRole),
                Is.All.EqualTo(MemberRole.Constructor));
            Assert.That(members.Where(m => m.Name == ".ctor").Count(), Is.EqualTo(2));

            Assert.That(Member("Widget", ".cctor").MemberRole, Is.EqualTo(MemberRole.StaticConstructor));
            Assert.That(Member("Widget", "Finalize").MemberRole, Is.EqualTo(MemberRole.Finalizer));
        });
    }

    [Test]
    public void AMemberThatDoesWork_IsMarkedNormalRatherThanLeftUnknown()
    {
        // Normal is a statement, not an absence: it is what stops the name fallback from running.
        Assert.Multiple(() =>
        {
            Assert.That(Member("Widget", "Work").MemberRole, Is.EqualTo(MemberRole.Normal));
            Assert.That(Member("Widget", "op_Addition").MemberRole, Is.EqualTo(MemberRole.Normal));
        });
    }

    [Test]
    public void AMethodMerelyNamedFinalize_IsNotAFinalizer()
    {
        // The name test would have called this a lifecycle member and dropped it from the
        // partitioning - and the dead code analysis would never have reported it.
        var impostor = Member("Impostor", "Finalize");

        Assert.Multiple(() =>
        {
            Assert.That(impostor.MemberRole, Is.EqualTo(MemberRole.Normal));
            Assert.That(impostor.MemberRole.IsLifecycle(), Is.False);
        });
    }

    [Test]
    public void SomethingThatIsNotAMethod_HasNoRole()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Member("Widget", "_count").MemberRole, Is.EqualTo(MemberRole.Unknown));
            Assert.That(Member("Widget", "Count").MemberRole, Is.EqualTo(MemberRole.Unknown));
        });
    }
}
