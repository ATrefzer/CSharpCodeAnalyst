namespace Contracts.Graph;

public enum DependencyType
{
    Calls,
    Creates,
    Uses,
    Inherits,

    // Whole interface or a single method.
    Implements,

    Overrides,

    // Special dependency to model the hierarchy.
    // In the CodeElement this dependency is modeled via.
    // Parent / Children 
    Containment,

    UsesAttribute,

    // Dependency type for event invocation
    Invokes,

    // Dependency type for event handler registration
    Handles
}