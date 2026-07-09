using CSharpCodeAnalyst.History.Extensions;

namespace CodeParserTests.UnitTests.History;

[TestFixture]
public class DictionaryExtensionTests
{
    [Test]
    public void ToCaseInsensitivePathKeys_LookupIgnoresCasing()
    {
        var source = new Dictionary<string, int>
        {
            [@"C:\Repo\Foo.cs"] = 1
        };

        var result = source.ToCaseInsensitivePathKeys();

        Assert.That(result[@"c:\repo\foo.cs"], Is.EqualTo(1));
        Assert.That(result[@"C:\REPO\FOO.CS"], Is.EqualTo(1));
    }

    [Test]
    public void ToCaseInsensitivePathKeys_PreservesOriginalKeyCasing()
    {
        var source = new Dictionary<string, int> { [@"C:\Repo\Foo.cs"] = 1 };

        var result = source.ToCaseInsensitivePathKeys();

        Assert.That(result.Keys.Single(), Is.EqualTo(@"C:\Repo\Foo.cs"));
    }

    [Test]
    public void ToCaseInsensitivePathKeys_DeduplicatesKeysDifferingOnlyInCasing_FirstWins()
    {
        // Git can track paths that differ only in casing; the copy must not throw and must
        // keep the first occurrence.
        var source = new List<KeyValuePair<string, int>>
        {
            new(@"C:\Repo\Foo.cs", 1),
            new(@"c:\repo\foo.cs", 2)
        };

        var result = source.ToCaseInsensitivePathKeys();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.Values.Single(), Is.EqualTo(1));
    }
}
