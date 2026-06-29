using CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Method groups used in LINQ operators (Where/Select/OrderBy, chained) and in explicit delegate
///     assignments. Each in-source method group becomes an IsMethodGroup "Uses" edge; external operators
///     (Where, OrderBy, Math.Abs) produce no edges. Migrated from the former Core.MethodGroups approval
///     fixture (LinqMethodGroups.cs).
/// </summary>
[TestFixture]
public class MethodGroups_LinqParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                     using System;
                                     using System.Collections.Generic;
                                     using System.Linq;

                                     namespace Demo;

                                     public class LinqMethodGroups
                                     {
                                         private readonly List<int> _numbers = new() { 1, 2, 3, 4, 5 };
                                         private readonly List<string> _strings = new() { "hello", "world", "test" };

                                         public void TestLinqWithMethodGroups()
                                         {
                                             var evenNumbers = _numbers.Where(IsEven).ToList();
                                             var transformedStrings = _strings.Select(TransformString).ToList();
                                             var filteredStrings = _strings.Where(IsValidString).ToList();
                                             var sortedNumbers = _numbers.OrderBy(Math.Abs).ToList();
                                             var result = _numbers.Where(IsPositive).Select(DoubleNumber).Where(IsEven).ToList();
                                         }

                                         public void TestDelegateAssignments()
                                         {
                                             Predicate<int> evenPredicate = IsEven;
                                             var transformer = TransformString;
                                             var logger = LogString;
                                             evenPredicate(4);
                                             transformer("test");
                                             logger("message");
                                         }

                                         private bool IsEven(int number) { return number % 2 == 0; }
                                         private bool IsPositive(int number) { return number > 0; }
                                         private int DoubleNumber(int number) { return number * 2; }
                                         private string TransformString(string input) { return input.ToUpperInvariant(); }
                                         private bool IsValidString(string input) { return !string.IsNullOrEmpty(input); }
                                         private void LogString(string message) { Console.WriteLine($"Log: {message}"); }
                                     }
                                     """;

    [Test]
    public void Classes_AreDetected()
    {
        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(new[] { "LinqMethodGroups" }));
    }

    [Test]
    public void Methods_AreDetected()
    {
        var expected = new[]
        {
            "LinqMethodGroups.DoubleNumber", "LinqMethodGroups.IsEven", "LinqMethodGroups.IsPositive",
            "LinqMethodGroups.IsValidString", "LinqMethodGroups.LogString", "LinqMethodGroups.TestDelegateAssignments",
            "LinqMethodGroups.TestLinqWithMethodGroups", "LinqMethodGroups.TransformString"
        };

        Assert.That(PathsOf(CodeElementType.Method), Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodGroupUsages_AreDetected()
    {
        var expected = new[]
        {
            "LinqMethodGroups.TestLinqWithMethodGroups -> LinqMethodGroups.IsEven",
            "LinqMethodGroups.TestLinqWithMethodGroups -> LinqMethodGroups.TransformString",
            "LinqMethodGroups.TestLinqWithMethodGroups -> LinqMethodGroups.IsValidString",
            "LinqMethodGroups.TestLinqWithMethodGroups -> LinqMethodGroups.DoubleNumber",
            "LinqMethodGroups.TestLinqWithMethodGroups -> LinqMethodGroups.IsPositive",
            "LinqMethodGroups.TestDelegateAssignments -> LinqMethodGroups.IsEven",
            "LinqMethodGroups.TestDelegateAssignments -> LinqMethodGroups.TransformString",
            "LinqMethodGroups.TestDelegateAssignments -> LinqMethodGroups.LogString"
        };

        Assert.That(MethodGroupUsages(), Is.EquivalentTo(expected));
    }
}
