namespace ApprovalTestTool;

public static class Comparer
{
    public static void CreateDiffFile(string referenceFile, string newFile)
    {
        var diffFile = Path.ChangeExtension(newFile, null) + "_diff.txt";

        var referenceLines = File.ReadAllLines(referenceFile).ToHashSet();
        var newFileLines = File.ReadAllLines(newFile).ToHashSet();

        var missingInNewFile = referenceLines.Except(newFileLines).OrderBy(x => x).ToList();
        var onlyInNewFile = newFileLines.Except(referenceLines).OrderBy(x => x).ToList();

        using var writer = new StreamWriter(diffFile);

        writer.WriteLine($"Diff between '{referenceFile}' (reference) and '{newFile}'");
        writer.WriteLine();

        writer.WriteLine("Missing in newFile:");
        foreach (var line in missingInNewFile)
        {
            writer.WriteLine(line);
        }

        writer.WriteLine();

        writer.WriteLine("Only in newFile:");
        foreach (var line in onlyInNewFile)
        {
            writer.WriteLine(line);
        }
    }
}