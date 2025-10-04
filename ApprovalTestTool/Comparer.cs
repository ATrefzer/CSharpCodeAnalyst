namespace ApprovalTestTool;

public static class Comparer
{
    public static void CreateDiffFile(string referenceFile, string newFile)
    {
        var diffFile = Path.ChangeExtension(newFile, null) + "_diff.txt";

        using var reader1 = new StreamReader(referenceFile);
        using var reader2 = new StreamReader(newFile);
        using var writer = new StreamWriter(diffFile);

        writer.WriteLine($"Diff between '{referenceFile}' (reference) and '{newFile}'");
        writer.WriteLine();

        var line1 = reader1.ReadLine();
        var line2 = reader2.ReadLine();

        var missingInNewFile = new List<string>();
        var onlyInNewFile = new List<string>();

        while (line1 != null || line2 != null)
        {
            var cmp = string.Compare(line1, line2, StringComparison.Ordinal);
            if (cmp == 0)
            {
                // Same line, skip
                line1 = reader1.ReadLine();
                line2 = reader2.ReadLine();
            }
            else if (line1 != null && (line2 == null || cmp < 0))
            {
                // line1 precedes line2 in sort order
                // line is missing in newFile

                missingInNewFile.Add(line1);
                line1 = reader1.ReadLine();
            }
            else if (line2 != null && (line1 == null || cmp > 0))
            {
                onlyInNewFile.Add(line2);
                line2 = reader2.ReadLine();
            }
        }

        // Output
        writer.Write("Missing in newFile");
        foreach (var line in missingInNewFile)
        {
            writer.WriteLine(line);
        }

        writer.WriteLine();
        
        writer.WriteLine("Only in newFile");
        foreach (var line in onlyInNewFile)
        {
            writer.WriteLine(line);
        }
    }
}