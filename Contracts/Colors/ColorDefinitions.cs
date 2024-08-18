using Contracts.Graph;

namespace Contracts.Colors;

public static class ColorDefinitions
{
    /// <summary>
    /// Claude.ai suggested following colors as used by IDEs like Visual Studio and JetBrains.
    /// I don't know if this is true but the colors look good.
    /// 
    /// Types:
    /// Namespace: Light Blue(#4EC9B0)
    /// Class: Bright Yellow (#FFD700)
    /// Interface: Light Green (#B8D7A3)
    /// Struct: Light Orange (#FFA500)
    /// Enum: Purple (#9370DB)
    /// Method: Blue (#569CD6)
    /// Property: Teal (#4EC9B0)
    /// Field: Dark Yellow (#D7BA7D)
    /// Event: Pink (#FF69B4)
    /// Delegate: Light Purple (#C586C0)
    /// 
    /// Dependencies:
    /// Inheritance: Dark Green (#008000)
    /// Implementation: Light Green (#90EE90)
    /// Calls: Blue (#0000FF)
    /// Uses: Gray (#808080)
    /// Creates: Orange (#FFA500)
    /// Overrides: Dark Blue (#00008B)
    /// </summary>
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