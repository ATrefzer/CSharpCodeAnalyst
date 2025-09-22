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
    IsMethodGroup = 32,
    EventRegistration = 64,
    EventUnregistration = 128
}


public static class RelationshipAttributeExtensions
{
    private static readonly List<RelationshipAttribute> Flags = Enum.GetValues(typeof(RelationshipAttribute))
        .Cast<RelationshipAttribute>()
        .Where(r => r != RelationshipAttribute.None)
        .ToList();

    public static string FormatAttributes(this RelationshipAttribute attr)
    {
        if (attr == RelationshipAttribute.None)
        {
            return string.Empty;
        }

        var attributes = new List<string>();
        foreach (var flag in Flags)
        {
            if (attr.HasFlag(flag))
            {
                attributes.Add(flag.ToString());
            }
        }

        return "[" + string.Join(", ", attributes) + "]";
    }

}