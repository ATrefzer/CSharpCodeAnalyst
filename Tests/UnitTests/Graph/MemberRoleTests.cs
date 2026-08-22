using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Graph;

/// <summary>
///     The contract the analyses rest on: which roles count as lifecycle, and that the two roles
///     meaning "not a lifecycle member" - the producer said so, and nobody said anything - both answer
///     false without being confused for one another.
/// </summary>
[TestFixture]
public class MemberRoleTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
    }

    private TestCodeGraph _graph = null!;

    [TestCase(MemberRole.Constructor)]
    [TestCase(MemberRole.StaticConstructor)]
    [TestCase(MemberRole.Finalizer)]
    public void AMemberThatBringsAnObjectIntoShape_IsALifecycleMember(MemberRole role)
    {
        Assert.That(role.IsLifecycle(), Is.True);
    }

    [TestCase(MemberRole.Normal)]
    [TestCase(MemberRole.Unknown)]
    public void EverythingElse_IsNot(MemberRole role)
    {
        Assert.That(role.IsLifecycle(), Is.False);
    }

    [Test]
    public void TheNameSaysNothing_OnlyTheRoleDoes()
    {
        // A C++ constructor is called like its class, a Dart one may be called anything at all. The
        // graph carries the answer instead of letting a consumer guess it - and conversely, a method
        // merely named like a C# constructor is not one.
        var cppConstructor = _graph.CreateMethod("Widget", memberRole: MemberRole.Constructor);
        var impostor = new CodeElement("1", CodeElementType.Method, ".ctor", ".ctor", null)
        {
            MemberRole = MemberRole.Normal
        };

        Assert.Multiple(() =>
        {
            Assert.That(cppConstructor.MemberRole.IsLifecycle(), Is.True);
            Assert.That(impostor.MemberRole.IsLifecycle(), Is.False);
        });
    }

    [Test]
    public void CloneSimple_KeepsTheRole()
    {
        // The MCP server answers from a clone, and the refactoring simulation works on one.
        var element = _graph.CreateMethod("Widget", memberRole: MemberRole.Constructor);

        Assert.That(element.CloneSimple().MemberRole, Is.EqualTo(MemberRole.Constructor));
    }
}
