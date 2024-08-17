using Contracts.Graph;

namespace Contracts.Colors;

public static class ColorDefinitions
{
    public static int GetRbgOf(CodeElementType codeElement)
    {
        return codeElement switch
        {
            // Property is a little bit darker. Both are treated as callable elements.
            CodeElementType.Method => 0x569CD6,
            CodeElementType.Property => 0x4677a2,

            CodeElementType.Interface => 0xB8D7A3,
            CodeElementType.Class => 0xFFD700,
            CodeElementType.Record => 0xFFD700,
            CodeElementType.Struct => 0xFFA500,
            CodeElementType.Namespace => 0x4EC9B0,
            CodeElementType.Enum => 0x9370DB,
            CodeElementType.Field => 0xD7BA7D,
            CodeElementType.Assembly => 0xEEEEEE,
            CodeElementType.Event => 0xFF69B4,
            CodeElementType.Delegate => 0xC586C0,
            _ => 0xFFFFFF
        };
    }
}