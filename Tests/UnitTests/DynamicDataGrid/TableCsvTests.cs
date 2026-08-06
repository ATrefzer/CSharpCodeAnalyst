using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;

namespace CodeParserTests.UnitTests.DynamicDataGrid;

[TestFixture]
public class TableCsvTests
{
    private static readonly TableColumnDefinition[] Columns =
    [
        new() { Header = "Name", PropertyName = nameof(Row.Name) },
        new() { Header = "Count", PropertyName = nameof(Row.Count) },
        new() { Header = "Note", PropertyName = nameof(Row.Note) }
    ];

    private static string[] Lines(string csv)
    {
        return csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    [Test]
    public void Build_WritesTheHeaderAndOneLinePerRow()
    {
        var csv = TableCsv.Build(Columns, [new Row("A", 1, null), new Row("B", 2, null)], ";");

        Assert.That(Lines(csv), Is.EqualTo(new[] { "Name;Count;Note", "A;1;", "B;2;" }));
    }

    [Test]
    public void Build_ValueContainingTheSeparator_IsQuoted()
    {
        var csv = TableCsv.Build(Columns, [new Row("A", 1, "used by X; and Y")], ";");

        Assert.That(Lines(csv)[1], Is.EqualTo("A;1;\"used by X; and Y\""));
    }

    /// <summary>A comma separator must not quote a value that merely contains a semicolon, and vice versa.</summary>
    [Test]
    public void Build_QuotingFollowsTheChosenSeparator()
    {
        var csv = TableCsv.Build(Columns, [new Row("A", 1, "used by X; and Y")], ",");

        Assert.That(Lines(csv)[1], Is.EqualTo("A,1,used by X; and Y"));
    }

    [Test]
    public void Build_QuotesAndLineBreaks_AreEscaped()
    {
        var csv = TableCsv.Build(Columns, [new Row("A", 1, "say \"hi\"\nagain")], ";");

        Assert.That(csv, Does.Contain("\"say \"\"hi\"\"\nagain\""));
    }

    /// <summary>A column bound to nothing (a button, an image) must not break the row.</summary>
    [Test]
    public void Build_ColumnWithoutAProperty_YieldsAnEmptyCell()
    {
        TableColumnDefinition[] columns =
        [
            new() { Header = "Name", PropertyName = nameof(Row.Name) },
            new() { Header = "Action", PropertyName = string.Empty },
            new() { Header = "Unknown", PropertyName = "DoesNotExist" }
        ];

        var csv = TableCsv.Build(columns, [new Row("A", 1, null)], ";");

        Assert.That(Lines(csv), Is.EqualTo(new[] { "Name;Action;Unknown", "A;;" }));
    }

    [Test]
    public void Build_WithoutRows_StillWritesTheHeader()
    {
        Assert.That(Lines(TableCsv.Build(Columns, [], ";")), Is.EqualTo(new[] { "Name;Count;Note" }));
    }

    private sealed record Row(string Name, int Count, string? Note);
}
