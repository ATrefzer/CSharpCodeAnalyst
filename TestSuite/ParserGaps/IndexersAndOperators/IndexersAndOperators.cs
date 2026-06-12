namespace ParserGaps.IndexersAndOperators;

// KNOWN GAP: IndexerDeclarationSyntax, OperatorDeclarationSyntax, ConversionOperatorDeclarationSyntax
// and DestructorDeclarationSyntax are not handled in HierarchyAnalyzer.ProcessNodeForHierarchy.
// These members are not created as code elements and - more importantly - their bodies are never
// analyzed in phase 2. All dependencies inside them are invisible.

public class DataStore
{
    public int Compute(string key)
    {
        return key.Length;
    }
}

public class Catalog
{
    // Contrast case: the field initializer IS analyzed (Catalog -creates-> DataStore).
    private readonly DataStore _store = new DataStore();

    public int Count { get; set; }

    // GAP: the call to DataStore.Compute is invisible because the indexer body is never walked.
    public int this[string key]
    {
        get { return _store.Compute(key); }
    }

    // GAP: the object creation and the call to Absorb are invisible.
    public static Catalog operator +(Catalog left, Catalog right)
    {
        var merged = new Catalog();
        merged.Absorb(left);
        merged.Absorb(right);
        return merged;
    }

    // GAP: the call to ComputeTotal is invisible.
    public static implicit operator int(Catalog catalog)
    {
        return catalog.ComputeTotal();
    }

    // GAP: the call to Cleanup is invisible.
    ~Catalog()
    {
        Cleanup();
    }

    public void Absorb(Catalog other)
    {
        Count = Count + other.Count;
    }

    private int ComputeTotal()
    {
        return Count;
    }

    private void Cleanup()
    {
    }
}
