namespace CSharpCodeAnalyst.Mcp.Tools;

/// <summary>
///     Shared wording for answers every tool can end up giving.
///     <para>
///         Tool results are read by a language model, so they are plain text rather than serialized
///         objects: a JSON graph of code elements spends most of its tokens on field names the reader
///         does not need. The same reasoning drives the phrasing - an answer says what to do next
///         instead of only stating that something is missing.
///     </para>
/// </summary>
internal static class ToolText
{
    /// <summary>
    ///     The application is running but has no project open. Not an error, so it is answered rather
    ///     than thrown: an exception would surface as a protocol failure and tell the caller nothing.
    /// </summary>
    public const string NoProjectLoaded =
        "No project is loaded in CSharp Code Analyst. Ask the user to open a solution or a saved " +
        "project in the application, then try again.";

    /// <summary>
    ///     Ids are regenerated on every parse, so a stale one is the single most likely mistake a caller
    ///     can make - and it looks exactly like "the element does not exist". The answer names both
    ///     possibilities, because the recovery differs: search again, or accept that it is gone.
    /// </summary>
    public static string UnknownId(string id)
    {
        return $"No element with id '{id}' exists in the loaded graph. Ids are only valid while this " +
               "server runs and change whenever the project is re-parsed, so an id from an earlier " +
               "session will not resolve. Use search_elements to look the element up again.";
    }
}
