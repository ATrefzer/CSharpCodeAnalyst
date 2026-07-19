using System.Xml.Linq;
using CSharpCodeAnalyst.CodeGraph.Export;

namespace CodeParserTests.UnitTests.Export;

/// <summary>
///     Label sanitizing of the DGML writer. Only characters XML cannot carry may be touched - the
///     check used to reject everything above ASCII, which turned an element named "Größe" into
///     "Cryptic_Größe".
/// </summary>
[TestFixture]
public class DgmlFileBuilderTests
{
    [SetUp]
    public void SetUp()
    {
        _file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dgml");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_file))
        {
            File.Delete(_file);
        }
    }

    private string _file = null!;

    private string WriteAndReadLabel(string nodeName)
    {
        var builder = new DgmlFileBuilder();
        builder.AddNodeById("id1", nodeName);
        builder.WriteOutput(_file);

        var document = XDocument.Load(_file);
        XNamespace ns = "http://schemas.microsoft.com/vs/2009/dgml";
        return document.Descendants(ns + "Node").Single().Attribute("Label")!.Value;
    }

    [TestCase("Größe")]
    [TestCase("Überprüfung")]
    [TestCase("naïve")]
    [TestCase("日本語")]
    [TestCase("Ordinary")]
    public void NonAsciiName_IsKeptUnchanged(string name)
    {
        Assert.That(WriteAndReadLabel(name), Is.EqualTo(name));
    }

    [Test]
    public void ControlCharacter_IsRemovedAndLabelIsMarked()
    {
        // XmlWriter throws on such a character, so flagging the name alone would not be enough -
        // it has to be removed for the file to be writable at all.
        var label = WriteAndReadLabel("Bad\u0001Name");

        Assert.That(label, Is.EqualTo("Cryptic_BadName"));
    }

    [Test]
    public void SurrogatePair_SurvivesIntact()
    {
        // A character outside the BMP is two chars; a naive per-char validity check would split it.
        const string name = "Emoji\U0001F600Name";

        Assert.That(WriteAndReadLabel(name), Is.EqualTo(name));
    }
}
