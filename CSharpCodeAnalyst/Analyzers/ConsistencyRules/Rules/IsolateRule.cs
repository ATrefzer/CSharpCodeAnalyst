namespace CSharpCodeAnalyst.Analyzers.ConsistencyRules.Rules;

/// <summary>
/// Isolates source pattern from any external dependencies
/// Syntax: ISOLATE: Source
/// </summary>
public class IsolateRule : ConsistencyRuleBase
{
    // No target - source is completely isolated
}