namespace Core.BasicLanguageFeatures.TypeContexts;

// Type names in catch declarations, foreach variable types, using-statement declarations and
// array creation are captured as Uses.

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
