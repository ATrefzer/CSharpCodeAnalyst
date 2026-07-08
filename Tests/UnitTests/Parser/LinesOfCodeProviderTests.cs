using CSharpCodeAnalyst.CodeParser.Parser;
using CSharpCodeAnalyst.History.Metrics;

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

    [Test]
    public void CSharp_VerbatimString_StartingWithEscapedQuote_IsNotMisreadAsRawString()
    {
        // @""" is a verbatim string whose content starts with an escaped quote - raw strings
        // cannot have an @ prefix. Misreading it as a raw string would keep the region open
        // past the real closing quote and swallow the comment on the next line.
        var lines = new[]
        {
            "var s = @\"\"\"escaped start\";",
            "// real comment after the string"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".cs");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(comments, Is.EqualTo(1));
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

    [Test]
    public void Xml_AttributeEndingWithBackslash_DoesNotSwallowClosingQuote()
    {
        // XML has no backslash escaping - Path="C:\Temp\" must close at the final quote
        // instead of running the "string" on and hiding the comment on the next line.
        var lines = new[]
        {
            "<Dir Path=\"C:\\Temp\\\" />",
            "<!-- a comment -->"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".xml");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(comments, Is.EqualTo(1));
    }

    // -------------------------------------------------------------------
    // HTML
    // -------------------------------------------------------------------

    [Test]
    public void Html_ApostropheInProse_DoesNotOpenAStringRegion()
    {
        // HTML prose is full of apostrophes; they must not open a never-closing
        // "string" that swallows all following comments.
        var lines = new[]
        {
            "<p>don't stop</p>",
            "<!-- a comment -->"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".html");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(comments, Is.EqualTo(1));
    }

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
    // TypeScript
    // -------------------------------------------------------------------

    [Test]
    public void TypeScript_TsExtension_UsesSameRulesAsJavaScript()
    {
        var lines = new[]
        {
            "const s: string = `line one",
            "// looks like a comment but is not",
            "line three`;",
            "// real comment"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".ts");

        Assert.That(code, Is.EqualTo(3));
        Assert.That(comments, Is.EqualTo(1));
    }

    // -------------------------------------------------------------------
    // Java
    // -------------------------------------------------------------------

    [Test]
    public void Java_BasicMix_CountsCodeAndComments()
    {
        var lines = new[]
        {
            "int x = 1;",
            "// leading comment",
            "/* block",
            "comment */",
            "int y = 2; // trailing"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".java");

        Assert.That(code, Is.EqualTo(2));
        Assert.That(comments, Is.EqualTo(3));
    }

    [Test]
    public void Java_TextBlock_MultiLineWithFakeComment_IsAllCode()
    {
        var lines = new[]
        {
            "String sql = \"\"\"",
            "// this text is DATA inside the text block, not a comment",
            "with \"embedded quotes\"",
            "\"\"\";"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".java");

        Assert.That(code, Is.EqualTo(4));
        Assert.That(comments, Is.EqualTo(0));
    }

    // -------------------------------------------------------------------
    // CSS
    // -------------------------------------------------------------------

    [Test]
    public void Css_MultiLineBlockComment_SpansLinesCorrectly()
    {
        var lines = new[]
        {
            "body { margin: 0; }",
            "/* this comment",
            "spans lines */",
            ".foo { color: red; } /* trailing */"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".css");

        Assert.That(code, Is.EqualTo(2));
        Assert.That(comments, Is.EqualTo(2));
    }

    [Test]
    public void Css_DoubleSlash_IsNotACommentInCss()
    {
        // Standard CSS has no single-line comments; "//" in a url is content.
        var lines = new[]
        {
            "// this is NOT a css comment",
            ".foo { background: url(\"https://example.com/x.png\"); }"
        };

        var (code, comments) = _sut.AnalyzeLines(lines, ".css");

        Assert.That(code, Is.EqualTo(2));
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

    [Test]
    public void AnalyzeDirectory_CustomHandlerForUnknownExtension_IsInvoked()
    {
        // A custom handler may cover an extension that has no built-in FileTypeInfo;
        // such files must not be filtered out before the handler gets a chance to run.
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "lib.rs");
            File.WriteAllText(file, "fn main() {}");

            var provider = new LinesOfCodeProvider(null);
            provider.RegisterCustomProvider(".rs", _ => (42, 7));

            var result = provider.AnalyzeDirectory(dir, null);

            Assert.That(result, Contains.Key(file));
            Assert.That(result[file].Code, Is.EqualTo(42));
            Assert.That(result[file].Comments, Is.EqualTo(7));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}