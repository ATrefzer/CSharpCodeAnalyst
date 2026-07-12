namespace CSharpCodeAnalyst.CodeGraph.Graph;

[Flags]
public enum RelationshipAttribute : uint
{
    None = 0,

    // Call specific attributes
    IsBaseCall = 1,
    IsStaticCall = 2,

    // Explicit this.Foo()
    IsThisCall = 4,

    // obj.Foo();
    IsInstanceCall = 8,
    IsExtensionMethodCall = 16,
    IsMethodGroup = 32,
    EventRegistration = 64,
    EventUnregistration = 128
}

public static class RelationshipAttributeExtensions
{
    /// <summary>The attributes that describe how a call is dispatched.</summary>
    private const RelationshipAttribute CallKindMask =
        RelationshipAttribute.IsBaseCall |
        RelationshipAttribute.IsStaticCall |
        RelationshipAttribute.IsThisCall |
        RelationshipAttribute.IsInstanceCall |
        RelationshipAttribute.IsExtensionMethodCall;

    private static readonly List<RelationshipAttribute> Flags = Enum.GetValues(typeof(RelationshipAttribute))
        .Cast<RelationshipAttribute>()
        .Where(r => r != RelationshipAttribute.None)
        .ToList();

    /// <summary>
    ///     Whether the call dispatches on the runtime type of the current "this" instance:
    ///     implicit calls (no call-kind attribute at all), explicit "this" calls and "base" calls.
    ///     Instance, static and extension method calls break the dispatch chain instead.
    ///     The check masks out orthogonal attributes (e.g. IsMethodGroup), so a call carrying only
    ///     those still counts as implicit - comparing against None would misclassify it.
    /// </summary>
    public static bool DispatchesOnCurrentInstance(this Relationship call)
    {
        var callKind = call.Attributes & CallKindMask;
        return callKind == RelationshipAttribute.None ||
               (callKind & (RelationshipAttribute.IsBaseCall | RelationshipAttribute.IsThisCall)) != 0;
    }

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