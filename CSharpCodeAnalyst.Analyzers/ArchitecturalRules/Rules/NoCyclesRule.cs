namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     Demands that a code element and everything below it are free of dependency cycles.
///     Syntax: NOCYCLES Path - the path takes no wildcard, the whole subtree is always meant.
///     There is only one sensible reading, so the rule does not offer the pattern suffixes of
///     the other rules ("X" alone could never be violated, "X.*" would require knowing in
///     advance on which level the cycles will appear).
///     <para>
///         Validation runs on the same search graph as the interactive cycle search, so the rule
///         reports exactly the cycle groups the Cycles view shows - including cycles that only
///         exist between namespaces and are invisible on the plain type graph (which is what
///         MAXCYCLICITY measures).
///     </para>
///     <para>
///         A cycle is a property of the whole group, not of a single edge. ALLOW exceptions
///         therefore do not apply, and a violation is never baselined: whoever writes NOCYCLES
///         means zero - MAXCYCLICITY is the gradual rule for code that is not there yet.
///     </para>
/// </summary>
public class NoCyclesRule : RuleBase
{
    public const string RuleKeyword = "NOCYCLES";

    /// <summary>The path of the element whose subtree must be cycle free.</summary>
    public string Source { get; set; } = string.Empty;
}
