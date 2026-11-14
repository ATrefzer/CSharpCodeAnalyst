using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class GenericMemberResolutionParseTests : InMemoryParseTestBase
{
    protected override string Code => """
        using System;
        namespace Demo;
        class Token { }
        interface IContract
        {
            int Value { get; set; }
            void Run(int value);
            event Action<int> Changed;
        }
        class Base<T>
        {
            public T Value { get; set; }
            public T this[int index] { get => Value; set => Value = value; }
            public void Run(T value) { }
            public event Action<T> Changed;
        }
        class Derived : Base<int>, IContract { }
        class Subscriber<T>
        {
            public void Handle(T value) { }
        }
        class Caller
        {
            Base<Token> KeepArguments;
            void Read(Base<int> value) { _ = value.Value; }
            void Write(Base<int> value) { value.Value = 1; }
            void ReadIndex(Base<int> value) { _ = value[0]; }
            void WriteIndex(Base<int> value) { value[0] = 1; }
            void Subscribe(Base<int> value, Subscriber<int> subscriber)
            {
                value.Changed += subscriber.Handle;
            }
        }
        """;

    [TestCase("Read", "Value.get_Value")]
    [TestCase("Write", "Value.set_Value")]
    [TestCase("ReadIndex", "this[].get_Item")]
    [TestCase("WriteIndex", "this[].set_Item")]
    public void ConstructedAccessors_ResolveToDeclaredAccessor(string caller, string target)
    {
        Assert.That(RelsOf(RelationshipType.Calls).Where(r => r.StartsWith($"Caller.{caller} -> ")),
            Is.EquivalentTo(new[] { $"Caller.{caller} -> Base<T>.{target}" }));
    }

    [TestCase("Run")]
    [TestCase("Value.get_Value")]
    [TestCase("Value.set_Value")]
    [TestCase("Changed")]
    public void InheritedGenericImplementation_ResolvesToBaseMember(string member)
    {
        Assert.That(RelsOf(RelationshipType.Implements), Does.Contain($"Base<T>.{member} -> IContract.{member}"));
    }

    [Test]
    public void GenericEventAndHandler_KeepHandlesEdge()
    {
        Assert.That(RelsOf(RelationshipType.Handles), Does.Contain("Subscriber<T>.Handle -> Base<T>.Changed"));
    }

    [Test]
    public void NormalizedTypeLookup_PreservesConcreteTypeArgument()
    {
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Caller.KeepArguments -> Token"));
    }
}
