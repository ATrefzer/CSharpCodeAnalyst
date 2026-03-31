namespace CSharpCodeAnalyst.Persistence.Dto;

/// <summary>
///     Per-project view settings that are persisted alongside the code graph.
///     Replaces the loosely-typed <c>Dictionary&lt;string, string&gt;</c> that was
///     previously used in <see cref="ProjectData.Settings" />.
/// </summary>
public class ProjectSettings
{
    public bool ShowFlatGraph { get; set; }
    public bool ShowDataFlow { get; set; }

    /// <summary>
    ///     Serialized exclusion filter expression for this project
    ///     (see <c>ProjectExclusionRegExCollection.ToString()</c>).
    /// </summary>
    public string ExclusionFilter { get; set; } = string.Empty;
}
