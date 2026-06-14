namespace Core.BasicLanguageFeatures.Overloads;

// Overloads that differ only by parameter ref-kind, and overloaded indexers, must each become a
// distinct code element. Before the Key() fix both overloads shared the same symbol key, so the
// second element was dropped in phase 1 and its body was never walked in phase 2 - one of the
// relationships below (the "ref"/"out" overload, or the string indexer) would then be missing.

public class ByValueResult
{
}

public class ByRefResult
{
}

public class ByOutResult
{
}

public class Calculator
{
    // Three legal overloads that differ only in ref-kind. Each body creates a distinct type.
    public void Compute(int value)
    {
        var result = new ByValueResult();
    }

    public void Compute(ref int value)
    {
        var result = new ByRefResult();
    }

    public void Compute(out int value)
    {
        value = 0;
        var result = new ByOutResult();
    }
}

public class IntStore
{
}

public class TextStore
{
}

public class Repository
{
    private readonly IntStore _byIndex = new IntStore();
    private readonly TextStore _byKey = new TextStore();

    // Overloaded indexers: same name this[], distinguished only by parameter type. Each body reads a
    // different field, so both overloads must survive as elements for both Uses edges to appear.
    public IntStore this[int index]
    {
        get { return _byIndex; }
    }

    public TextStore this[string key]
    {
        get { return _byKey; }
    }
}
