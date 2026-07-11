namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     A dependency rule that constrains the edges from its source to a second pattern.
///     Syntax: KEYWORD Source -> Target
///     <para>
///         ISOLATE is the dependency rule that does <em>not</em> belong here: it constrains the edges
///         leaving its source, whatever their target.
///     </para>
/// </summary>
public abstract class TargetedDependencyRule : DependencyRule
{
    public string Target { get; set; } = string.Empty;
}
