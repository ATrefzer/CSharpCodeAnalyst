using System.Reflection;

namespace CodeParserTests.UnitTests.Import;

/// <summary>
///     Paths shared by the two halves of the Dart fixture test: the recorded extractor output that
///     runs everywhere, and the recording itself, which needs a Dart SDK.
/// </summary>
internal static class DartFixture
{
    /// <summary>
    ///     Recorded output of the Dart extractor over <see cref="PackageDirectory" />, copied next to
    ///     the test binary as content.
    /// </summary>
    public static string RecordedGraphPath
    {
        get => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "TestData", "dart-fixture-graph.json");
    }

    /// <summary>
    ///     The handcrafted Dart package, relative to the test binary - the same way
    ///     ApprovalTestBase reaches TestSuite.sln.
    /// </summary>
    public static string PackageDirectory
    {
        get => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            "..", "..", "..", "..", "TestSuiteDart"));
    }
}
