namespace Core.BasicLanguageFeatures;

// Test basic method calls and property usage
public class BasicCalls
{
    private string _privateField = "initial";

    public BasicCalls()
    {
        // Constructor calls
        InitializeData();
        SetProperty("constructor value");
    }

    public string PublicProperty { get; set; } = "default";

    public void TestMethodCalls()
    {
        // Direct method calls
        var result = ProcessData("input");

        // Property access
        var current = PublicProperty;
        PublicProperty = "new value";

        // Field access
        _privateField = "updated";
        var fieldValue = _privateField;

        // Static method calls
        var length = CalculateLength("test");

        // Method chaining
        var final = ProcessData("chain")
            .ToUpperInvariant()
            .Trim();
    }

    private void InitializeData()
    {
        _privateField = "initialized";
    }

    private void SetProperty(string value)
    {
        PublicProperty = value;
    }

    private string ProcessData(string input)
    {
        return $"Processed: {input}";
    }

    private static int CalculateLength(string input)
    {
        return input?.Length ?? 0;
    }
}

// Test simple inheritance