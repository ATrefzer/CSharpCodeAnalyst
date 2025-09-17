using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.MethodGroups;

// Test method groups in LINQ operations
public class LinqMethodGroups
{
    private readonly List<int> _numbers = new() { 1, 2, 3, 4, 5 };
    private readonly List<string> _strings = new() { "hello", "world", "test" };

    public void TestLinqWithMethodGroups()
    {
        // Method groups in LINQ - should create Uses relationships
        var evenNumbers = _numbers.Where(IsEven).ToList();
        var transformedStrings = _strings.Select(TransformString).ToList();
        var filteredStrings = _strings.Where(IsValidString).ToList();

        // Method groups with static methods
        var sortedNumbers = _numbers.OrderBy(Math.Abs).ToList();

        // Chained method groups
        var result = _numbers
            .Where(IsPositive)
            .Select(DoubleNumber)
            .Where(IsEven)
            .ToList();
    }

    public void TestDelegateAssignments()
    {
        // Various delegate assignments with method groups
        Predicate<int> evenPredicate = IsEven;
        var transformer = TransformString;
        var logger = LogString;

        // Use the delegates
        evenPredicate(4);
        transformer("test");
        logger("message");
    }

    private bool IsEven(int number)
    {
        return number % 2 == 0;
    }

    private bool IsPositive(int number)
    {
        return number > 0;
    }

    private int DoubleNumber(int number)
    {
        return number * 2;
    }

    private string TransformString(string input)
    {
        return input.ToUpperInvariant();
    }

    private bool IsValidString(string input)
    {
        return !string.IsNullOrEmpty(input);
    }

    private void LogString(string message)
    {
        Console.WriteLine($"Log: {message}");
    }
}