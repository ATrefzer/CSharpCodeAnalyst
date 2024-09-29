namespace Contracts.Graph;

public enum RelationshipType
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

    // Relationship type for event invocation
    Invokes,

    // Relationship type for event handler registration
    // This is not a code dependency. It is actually the other direction.
    Handles
}