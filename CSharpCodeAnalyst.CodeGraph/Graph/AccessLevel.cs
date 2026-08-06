namespace CSharpCodeAnalyst.CodeGraph.Graph;

/// <summary>
///     How far a code element can be reached from. Modelled after C#, but the concept exists in every
///     language the tool imports.
///     <para>
///         <see cref="Unknown" /> is the default and means exactly that: nobody told us. Every importer
///         that does not know about visibility leaves it there, and so does a project file written before
///         this existed. It must never be read as "public" or as "private" - an analysis that draws a
///         conclusion from visibility has to treat Unknown as "no information".
///     </para>
///     <para>
///         Deliberately not called "Accessibility": WPF drags a global <c>Accessibility</c> namespace into
///         scope, so every file in the UI projects would have to fully qualify the type.
///     </para>
/// </summary>
public enum AccessLevel
{
    Unknown,

    /// <summary>Reachable only from inside the declaring type.</summary>
    Private,

    /// <summary>Reachable from the declaring type and everything derived from it.</summary>
    Protected,

    /// <summary>Reachable from inside the declaring assembly.</summary>
    Internal,

    /// <summary>C# "private protected": derived types, but only within the declaring assembly.</summary>
    ProtectedAndInternal,

    /// <summary>C# "protected internal": the declaring assembly, plus derived types anywhere.</summary>
    ProtectedOrInternal,

    /// <summary>Reachable from anywhere, including code that is not part of the analysis.</summary>
    Public
}

public static class AccessLevelExtensions
{
    /// <summary>
    ///     Whether everything that could reach this element is necessarily part of the analyzed code. Only
    ///     then is "nothing references it" the same as "nothing can reference it".
    ///     <para>
    ///         Private and internal (in either combination) are confined to the declaring type or assembly,
    ///         both of which we analyzed. Protected and public can be reached from code outside the
    ///         analysis, and <see cref="AccessLevel.Unknown" /> tells us nothing at all - all three answer
    ///         false.
    ///     </para>
    /// </summary>
    public static bool IsConfinedToAnalyzedCode(this AccessLevel accessLevel)
    {
        return accessLevel is AccessLevel.Private or AccessLevel.Internal or AccessLevel.ProtectedAndInternal;
    }
}
