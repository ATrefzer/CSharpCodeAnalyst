using System.Reflection;

namespace CSharpCodeAnalyst.Mcp;

/// <summary>
///     What the server says about itself when a client connects. Kept apart from the hosting so the
///     answer does not depend on which transport happens to be carrying it - a client must not be able
///     to tell the two apart, and two copies of a text this long would drift.
/// </summary>
internal static class ServerIdentity
{
    /// <summary>
    ///     Not the product name: this is one of the few strings a client shows a model, and the product
    ///     is named after a language it is not limited to. What it serves is a code graph, whichever
    ///     language that graph was built from.
    /// </summary>
    public const string Name = "code-graph";

    /// <summary>
    ///     The application is named after C#, and so is the name most clients are configured with - but
    ///     it imports C++, Dart, Python and Java as well, and the graph is the same model for all of
    ///     them. Naming the languages here, and saying outright that the name does not carry the
    ///     answer, is what keeps a caller from ruling the server out before asking it anything.
    /// </summary>
    public const string Instructions =
        "Answers questions about the code dependency graph currently loaded in CSharp Code " +
        "Analyst: who calls what, what depends on what, how two elements are connected, and " +
        "what a change would hit - dependencies, call graph, blast radius, architecture, " +
        "layering. The loaded code base can be C#, C++, Dart, Python or Java; neither the " +
        "name of this server nor the name of the application says which, so do not conclude " +
        "from either that a question is out of scope. Call graph_info first: it reports the " +
        "languages actually loaded, the size of the graph and how current it is. Element ids " +
        "are opaque and only valid for the running server - always start with " +
        "search_elements to obtain one.";

    public static string GetVersion()
    {
        var assembly = typeof(ServerIdentity).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // A deterministic build appends "+<commit sha>" to the informational version. Useful in a
        // crash report, noise in a protocol field a client displays.
        var plus = informational?.IndexOf('+') ?? -1;
        if (plus > 0)
        {
            return informational![..plus];
        }

        return informational
               ?? assembly.GetName().Version?.ToString()
               ?? "0.0.0";
    }
}
