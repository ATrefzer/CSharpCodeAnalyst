namespace Contracts.Graph;

[Flags]
public enum RelationshipAttribute : uint
{
    None = 0,

    // Call specific attributes
    IsBaseCall = 1,
    IsStaticCall = 2,
    IsThisCall = 4,
    IsInstanceCall = 8,
    IsExtensionMethodCall = 16,
}