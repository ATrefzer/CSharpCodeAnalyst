using CSharpCodeAnalyst.CodeParser.Parser;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class LinesOfCodeProviderTests
{
    private readonly LinesOfCodeProvider _sut = new(null);

    // -------------------------------------------------------------------
    // C#
    // -------------------------------------------------------------------

    [Test]
    public void CSharp_BasicMix_CountsCodeCommentsAndBlanks()
    {
        var lines = new[]
        {
            "using System;",
            "",
            "// leading comment",
            "class Foo",
            "{",
            "    int x = 1; // trailing comment",
            "}"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".cs");

        // A line with both code and a trailing comment counts as code.
        Assert.That(code, Is.EqualTo(5));
        Assert.That(comments, Is.EqualTo(1));
    }

    [Test]
    public void CSharp_MultiLineBlockComment_SpansLinesCorrectly()
    {
        var lines = new[]
        {
            "int a = 1;",
            "/* this comment",
            "spans several",
            "lines */",
            "int b = 2;"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".cs");

        Assert.That(code, Is.EqualTo(2));
        Assert.That(comments, Is.EqualTo(3));
    }

    [Test]
    public void CSharp_RegularStringContainingSlashSlash_IsNotTreatedAsComment()
    {
        var lines = new[] { "var s = \"not a // comment\";" };

        var (code, comments) = _sut.AnalyzeLines(lines, ".cs");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(comments, Is.EqualTo(0));
    }

    [Test]
    public void CSharp_VerbatimString_MultiLineWithEscapedQuotesAndFakeComment_IsAllCode()
    {
        // Torture test: escaped-quote pair ("") followed by a line that
        // LOOKS like a "//" comment but is actually verbatim string
        // content, followed by the real closing quote.
        var lines = new[]
        {
            "var s = @\"start \"\"escaped\"\" middle",
            "// looks like a comment but is not",
            "end\"\"\";"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".cs");

        Assert.That(code, Is.EqualTo(3));
        Assert.That(comments, Is.EqualTo(0));
    }

    [Test]
    public void CSharp_VerbatimString_EmbeddedBlankLine_CountsAsBlank()
    {
        // A whitespace-only line is counted as blank even inside a string,
        // matching common LOC-tool convention (e.g. cloc).
        var lines = new[]
        {
            "var s = @\"line one",
            "",
            "line three\";"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".cs");

        Assert.That(code, Is.EqualTo(2));
        Assert.That(comments, Is.EqualTo(0));
    }

    [Test]
    public void CSharp_VerbatimString_TrailingBackslashBeforeClosingQuote_ClosesCorrectly()
    {
        // Regression test: '\' has no escape meaning inside @"...", so a path like
        // @"C:\Users\Andreas\" must still close at the final quote instead of being
        // swallowed as an "escaped" character, which would run the string on into
        // the next line and hide the real comment there.
        var lines = new[]
        {
            "var path = @\"C:\\Users\\Andreas\\\";",
            "// real comment after the string"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".cs");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(comments, Is.EqualTo(1));
    }

    [Test]
    public void CSharp_VerbatimInterpolatedString_AtDollarPrefix_TreatsBackslashAsLiteral()
    {
        // Covers the "@$" prefix ordering (as opposed to "$@"), which needs its own
        // look-back check in IsVerbatimStringPrefix.
        var lines = new[]
        {
            "var path = @$\"C:\\Users\\\";",
            "// real comment after the string"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".cs");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(comments, Is.EqualTo(1));
    }

    [Test]
    public void CSharp_RawString_TripleQuote_MultiLine_IsAllCode()
    {
        var lines = new[]
        {
            "var x = \"\"\"",
            "hello \"world\"",
            "more content",
            "\"\"\";",
            "Console.WriteLine(x);"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".cs");

        Assert.That(code, Is.EqualTo(5));
        Assert.That(comments, Is.EqualTo(0));
    }

    [Test]
    public void CSharp_RawString_QuadQuoteDelimiter_WithEmbeddedFakeComment_IsAllCode()
    {
        // Regression test for the bug we found: an even-length delimiter
        // (four quotes) used to desync the naive quote-toggle parser,
        // making it misread this line as a real "//" comment.
        var lines = new[]
        {
            "var sql = \"\"\"\"",
            "// this text is DATA inside the raw string, not a comment",
            "\"\"\"\";"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".cs");

        Assert.That(code, Is.EqualTo(3));
        Assert.That(comments, Is.EqualTo(0));
    }

    [Test]
    public void CSharp_RawString_OpensAndClosesOnSameLine()
    {
        var lines = new[] { "var z = \"\"\"abc\"\"\";  // comment after" };

        var (code, comments) = _sut.AnalyzeLines(lines, ".cs");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(comments, Is.EqualTo(0));
    }

    // -------------------------------------------------------------------
    // Python
    // -------------------------------------------------------------------

    [Test]
    public void Python_BasicMix_CountsCodeCommentsAndBlanks()
    {
        var lines = new[]
        {
            "import os",
            "",
            "# leading comment",
            "x = 1  # trailing comment"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".py");

        Assert.That(code, Is.EqualTo(2));
        Assert.That(comments, Is.EqualTo(1));
    }

    [Test]
    public void Python_TripleSingleQuoteDocstring_SpansMultipleLines()
    {
        var lines = new[]
        {
            "def foo():",
            "    '''",
            "    This is a docstring",
            "    spanning lines",
            "    '''",
            "    return 1"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".py");

        Assert.That(code, Is.EqualTo(2));
        Assert.That(comments, Is.EqualTo(4));
    }

    [Test]
    public void Python_TripleDoubleQuoteDocstring_ContainingHash_StaysComment()
    {
        var lines = new[]
        {
            "def foo():",
            "    \"\"\"",
            "    contains a # character, still a comment",
            "    \"\"\"",
            "    return 1"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".py");

        Assert.That(code, Is.EqualTo(2));
        Assert.That(comments, Is.EqualTo(3));
    }

    // -------------------------------------------------------------------
    // XML
    // -------------------------------------------------------------------

    [Test]
    public void Xml_BasicMix_CountsCodeAndBlanks()
    {
        var lines = new[]
        {
            "<Root>",
            "",
            "<Child>value</Child>"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".xml");

        Assert.That(code, Is.EqualTo(2));
        Assert.That(comments, Is.EqualTo(0));
    }

    [Test]
    public void Xml_MultiLineComment_SpansLinesCorrectly()
    {
        var lines = new[]
        {
            "<Root>",
            "<!-- this is",
            "a comment",
            "spanning lines -->",
            "<Child>value</Child>"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".xml");

        Assert.That(code, Is.EqualTo(2));
        Assert.That(comments, Is.EqualTo(3));
    }

    [Test]
    public void Xml_SameLineElementWithTrailingComment_CountsAsCode()
    {
        var lines = new[] { "<Foo/> <!-- inline comment -->" };

        var (code, comments) = _sut.AnalyzeLines(lines, ".xml");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(comments, Is.EqualTo(0));
    }

    [Test]
    public void Xml_BackToBackComments_OnOwnLine_CountsAsComment()
    {
        var lines = new[]
        {
            "<!-- first --><!-- second -->",
            "<Root/>"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".xml");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(comments, Is.EqualTo(1));
    }

    // -------------------------------------------------------------------
    // HTML
    // -------------------------------------------------------------------

    [Test]
    public void Html_MultiLineComment_SpansLinesCorrectly()
    {
        var lines = new[]
        {
            "<html>",
            "<!-- this is",
            "a comment",
            "spanning lines -->",
            "<body>hi</body>"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".html");

        Assert.That(code, Is.EqualTo(2));
        Assert.That(comments, Is.EqualTo(3));
    }

    [Test]
    public void Html_HtmExtension_UsesSameRulesAsHtml()
    {
        var lines = new[] { "<!-- comment -->", "<p>text</p>" };

        var (code, comments) = _sut.AnalyzeLines(lines, ".htm");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(comments, Is.EqualTo(1));
    }

    // -------------------------------------------------------------------
    // JavaScript
    // -------------------------------------------------------------------

    [Test]
    public void JavaScript_BasicMix_CountsCodeAndComments()
    {
        var lines = new[]
        {
            "const x = 1;",
            "// leading comment",
            "/* block",
            "comment */",
            "const y = 2; // trailing"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".js");

        Assert.That(code, Is.EqualTo(2));
        Assert.That(comments, Is.EqualTo(3));
    }

    [Test]
    public void JavaScript_TemplateLiteral_MultiLineWithFakeCommentAndInterpolation_IsAllCode()
    {
        var lines = new[]
        {
            "const s = `line one ${x}",
            "// looks like a comment but is not",
            "line three`;"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".js");

        Assert.That(code, Is.EqualTo(3));
        Assert.That(comments, Is.EqualTo(0));
    }

    [Test]
    public void JavaScript_RegularStringContainingSlashSlash_IsNotTreatedAsComment()
    {
        var lines = new[] { "const s = 'not a // comment';" };

        var (code, comments) = _sut.AnalyzeLines(lines, ".js");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(comments, Is.EqualTo(0));
    }

    // -------------------------------------------------------------------
    // Text
    // -------------------------------------------------------------------

    [Test]
    public void Text_HasNoCommentSyntax_EveryNonBlankLineIsCode()
    {
        var lines = new[]
        {
            "some text",
            "",
            "// this looks like a comment marker but plain text has none",
            "\"quotes\" and 'apostrophes' don't start anything either"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".txt");

        Assert.That(code, Is.EqualTo(3));
        Assert.That(comments, Is.EqualTo(0));
    }

    // -------------------------------------------------------------------
    // Misc
    // -------------------------------------------------------------------

    [Test]
    public void UnsupportedExtension_Throws()
    {
        Assert.Throws<ArgumentException>(() => _sut.AnalyzeLines(new[] { "code" }, ".rs"));
    }
}