namespace ParserGaps.TypeContexts;

// KNOWN GAP: Type names in several syntax contexts are dropped because AnalyzeIdentifier
// ignores INamedTypeSymbol and there is no dedicated handler for these contexts:
// - catch declarations (CatchDeclarationSyntax)
// - foreach variable types (ForEachStatementSyntax)
// - using statement declarations (VariableDeclarationSyntax inside UsingStatementSyntax,
//   which is not the handled LocalDeclarationStatementSyntax)
// - array creation (ArrayCreationExpressionSyntax)

public class ParsingFailedException : System.Exception
{
}

public class InventoryItem
{
}

public class PooledResource : System.IDisposable
{
    public void Dispose()
    {
    }
}

public class TypeContextUser
{
    private readonly InventoryItem[] _items = [];

    // GAP: no relationship CatchClause -> ParsingFailedException.
    public int CatchClause()
    {
        try
        {
            return Work();
        }
        catch (ParsingFailedException)
        {
            return 0;
        }
    }

    // Contrast case: the object creation in a throw statement IS detected.
    public void Throwing()
    {
        throw new ParsingFailedException();
    }

    // GAP: no relationship ForEachLoop -> InventoryItem.
    public int ForEachLoop()
    {
        var sum = 0;
        foreach (InventoryItem item in _items)
        {
            sum++;
        }

        return sum;
    }

    // GAP: no relationship ArrayCreation -> InventoryItem.
    public object ArrayCreation()
    {
        return new InventoryItem[5];
    }

    // GAP: no relationship UsingStatement -> PooledResource.
    public void UsingStatement()
    {
        using (PooledResource resource = CreateResource())
        {
        }
    }

    private PooledResource CreateResource()
    {
        return new PooledResource();
    }

    private int Work()
    {
        return 1;
    }
}
