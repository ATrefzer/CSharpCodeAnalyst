using CSharpCodeAnalyst.CodeGraph.Declarations;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     A member that implements or overrides something from outside the analyzed code has no incoming
///     reference anywhere in the graph - the framework is the caller. The relationship model cannot carry
///     the fact, so the parser records it in the <see cref="ExternalContractStore" /> beside the graph.
///     This fixture pins both routes: the "override" keyword and an implicit interface implementation.
/// </summary>
[TestFixture]
public class ExternalContractParseTests
{
    [OneTimeSetUp]
    public async Task ParseCode()
    {
        const string code = """
                            using System;
                            using System.Collections;

                            namespace Demo;

                            public interface IOwn
                            {
                                void Handle();
                            }

                            public class Widget : IDisposable, IOwn
                            {
                                // Implicit implementation of a framework interface - no "override" keyword.
                                public void Dispose() { }

                                // Implementation of one of our own interfaces: a real Implements edge exists.
                                public void Handle() { }

                                // Overrides a framework member.
                                public override string ToString() => "widget";

                                // Neither. Nothing keeps this alive.
                                public void Unused() { }

                                // Overrides a framework member declared as a property.
                                public override int GetHashCode() => 0;
                            }

                            public abstract class OwnBase
                            {
                                public abstract void Run();
                            }

                            public class OwnDerived : OwnBase
                            {
                                // Overrides one of ours: a real Overrides edge exists.
                                public override void Run() { }
                            }

                            public class Sequence : IEnumerable
                            {
                                public IEnumerator GetEnumerator() => throw new NotImplementedException();
                            }
                            """;

        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false));
        var result = await parser.ParseSourceAsync(code);

        _graph = result.CodeGraph;
        _contracts = result.ExternalContracts;
    }

    private CodeGraph _graph = null!;
    private ExternalContractStore _contracts = null!;

    private string? ContractOf(string path)
    {
        var element = _graph.Nodes.Values.Single(n => PathOf(n) == path);
        return _contracts.TryGet(element.Id);
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

    [Test]
    public void ImplicitImplementationOfAFrameworkInterface_IsRecorded()
    {
        Assert.That(ContractOf("Widget.Dispose"), Is.EqualTo("IDisposable.Dispose"));
    }

    [Test]
    public void OverrideOfAFrameworkMember_IsRecorded()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ContractOf("Widget.ToString"), Is.EqualTo("Object.ToString"));
            Assert.That(ContractOf("Widget.GetHashCode"), Is.EqualTo("Object.GetHashCode"));
        });
    }

    [Test]
    public void ImplementationOfAFrameworkInterfaceOnAnotherType_IsRecorded()
    {
        Assert.That(ContractOf("Sequence.GetEnumerator"), Is.EqualTo("IEnumerable.GetEnumerator"));
    }

    [Test]
    public void ImplementationOfOurOwnInterface_IsNotRecorded()
    {
        // The graph already has the Implements edge; the dead code analysis propagates liveness along it.
        Assert.That(ContractOf("Widget.Handle"), Is.Null);
    }

    [Test]
    public void OverrideOfOurOwnBaseClass_IsNotRecorded()
    {
        Assert.That(ContractOf("OwnDerived.Run"), Is.Null);
    }

    [Test]
    public void OrdinaryMember_IsNotRecorded()
    {
        Assert.That(ContractOf("Widget.Unused"), Is.Null);
    }

    [Test]
    public void TheTypeItself_IsNeverRecorded()
    {
        // Implementing IDisposable is not a use of the class - it must still be reportable as dead code.
        Assert.That(ContractOf("Widget"), Is.Null);
    }
}
