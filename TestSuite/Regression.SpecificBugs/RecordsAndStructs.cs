using System;

namespace Regression.SpecificBugs;

// Test records and struct interfaces
public record RecordA(string Name, RecordB RecordB);

public record RecordB(int Value, RecordA RecordA);

public struct StructWithInterface : IComparable<StructWithInterface>
{
    public int Value { get; init; }

    public int CompareTo(StructWithInterface other)
    {
        return Value.CompareTo(other.Value);
    }
}

// Extension methods
public static class Extensions
{
    public static string Slice(this ExtendedType source, int start, int length)
    {
        return source.Data.Substring(start, length);
    }
}

public class ExtendedType
{
    public string Data { get; set; } = string.Empty;
}

// Partial classes
public partial class PartialClient
{
    partial void OnCreated();

    public PartialClient CreateInstance()
    {
        var instance = new PartialClient();
        instance.OnCreated();
        return instance;
    }
}

public partial class PartialClient
{
    partial void OnCreated()
    {
        // Implementation
    }
}