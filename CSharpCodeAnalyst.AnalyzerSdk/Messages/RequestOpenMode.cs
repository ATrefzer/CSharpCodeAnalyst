namespace CSharpCodeAnalyst.AnalyzerSdk.Messages;

public enum RequestOpenMode
{
    /// <summary>
    ///     Open or activate
    /// </summary>
    Normal,

    /// <summary>
    ///     Ignore if not already open
    /// </summary>
    UpdateOnly
}