using Contracts.Graph;

namespace CodeParser.Analysis.Cycles;

public static class CodeElementClassifier
{
    public static int GetContainerLevel(CodeElementType type)
    {
        if (type is CodeElementType.Assembly or CodeElementType.Namespace)
        {
            return 30;
        }

        // Treat enum as a type
        if (type is CodeElementType.Class or
            CodeElementType.Interface or
            CodeElementType.Struct or
            CodeElementType.Enum or
            CodeElementType.Record or
            CodeElementType.Delegate)
        {
            return 20;
        }

        // Keep it simple and account inner methods to the method itself.     

        return 0;
    }
}