namespace CSharpCodeAnalyst.CodeGraph.Graph;

/// <summary>
///     What a member is there for, as far as the language is concerned: does it do work, or does it
///     bring an object into (or out of) a valid state. Only methods carry a role.
///     <para>
///         <see cref="Unknown" /> is the default and means exactly that: nobody told us - a producer that
///         does not fill it, or an element that is not a method at all. It must never be read as
///         <see cref="Normal" />, and no consumer guesses a role from a name: a C++ constructor is called
///         like its class and a Dart one may be called anything, so no name test holds for more than one
///         language.
///     </para>
///     <para>
///         This is a language fact, not an analysis result. What an analysis makes of it - excluding
///         lifecycle members from the partitioning, treating a finalizer as reachable - belongs to the
///         analysis. See <see cref="MemberRoleExtensions.IsLifecycle" />.
///     </para>
/// </summary>
public enum MemberRole
{
    /// <summary>Nobody told us. Not the same as <see cref="Normal" />.</summary>
    Unknown,

    /// <summary>
    ///     The producer looked and says this is an ordinary member. A positive statement, and the reason
    ///     the enum has five values rather than four: a producer that fills roles at all says this about
    ///     every method it creates, so <see cref="Unknown" /> stays reserved for "not filled in".
    /// </summary>
    Normal,

    /// <summary>An instance constructor.</summary>
    Constructor,

    /// <summary>A type initializer that runs once before the type is first used (C# "static Foo()").</summary>
    StaticConstructor,

    /// <summary>A finalizer or destructor (C# "~Foo", C++ "~Foo", Python "__del__").</summary>
    Finalizer
}

public static class MemberRoleExtensions
{
    /// <summary>
    ///     Whether a member with this role exists to initialize or tear down an object rather than to do
    ///     work. A constructor assigns most of the state, so in a member graph it is a clique over all
    ///     fields; a finalizer is called by the runtime and by nobody else.
    /// </summary>
    public static bool IsLifecycle(this MemberRole role)
    {
        return role is MemberRole.Constructor or MemberRole.StaticConstructor or MemberRole.Finalizer;
    }
}
