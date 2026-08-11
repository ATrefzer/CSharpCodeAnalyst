using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Search;

namespace CodeParserTests.UnitTests.Search;

/// <summary>
///     Pins the search grammar reachable through <see cref="SearchExpressionFactory" />: AND (space),
///     OR (pipe), negation (leading minus) and the "type:" / "source:" terms. The same expression feeds
///     the tree, the graph search, the Advanced Search and every analyzer table, so the behaviour is
///     shared by all of them.
/// </summary>
[TestFixture]
public class SearchExpressionTests
{
    private static CodeElement Element(string fullName, string? name = null,
        CodeElementType type = CodeElementType.Class, bool isExternal = false, bool isGenerated = false)
    {
        return new CodeElement(fullName, type, name ?? fullName, fullName, null)
        {
            IsExternal = isExternal,
            IsGenerated = isGenerated
        };
    }

    private static bool Matches(string search, CodeElement? element,
        SearchExpressionFactory.TextSearchField field = SearchExpressionFactory.TextSearchField.FullName)
    {
        return SearchExpressionFactory.CreateSearchExpression(search, field).Evaluate(element);
    }

    [Test]
    public void Term_MatchesSubstringOfTheFullName()
    {
        var element = Element("App.Resources.Strings.Close_Button");

        Assert.Multiple(() =>
        {
            Assert.That(Matches("resources", element), Is.True);
            Assert.That(Matches("missing", element), Is.False);
        });
    }

    [Test]
    public void LowerCaseTerm_IsCaseInsensitive()
    {
        Assert.That(Matches("strings", Element("App.Resources.Strings.Close")), Is.True);
    }

    [Test]
    public void Space_MeansAnd()
    {
        var element = Element("App.Resources.Strings.Close");

        Assert.Multiple(() =>
        {
            Assert.That(Matches("resources close", element), Is.True);
            Assert.That(Matches("resources missing", element), Is.False);
        });
    }

    [Test]
    public void Pipe_MeansOr()
    {
        var element = Element("App.Views.MainWindow");

        Assert.Multiple(() =>
        {
            Assert.That(Matches("missing | views", element), Is.True);
            Assert.That(Matches("missing | absent", element), Is.False);
        });
    }

    [Test]
    public void Or_BindsLessThanAnd()
    {
        // "views window | absent" reads as "(views AND window) OR absent".
        Assert.Multiple(() =>
        {
            Assert.That(Matches("views window | absent", Element("App.Views.MainWindow")), Is.True);
            Assert.That(Matches("views absent | dialog", Element("App.Other.Dialog")), Is.True);
            Assert.That(Matches("views absent | missing", Element("App.Views.MainWindow")), Is.False);
        });
    }

    [Test]
    public void MinusPrefix_ExcludesTheTerm()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Matches("-resources", Element("App.Resources.Strings.Close")), Is.False);
            Assert.That(Matches("-resources", Element("App.Views.MainWindow")), Is.True);
        });
    }

    [Test]
    public void Negation_CombinesWithAPositiveTerm()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Matches("rules -tooltip", Element("App.Strings.Rules_Clear")), Is.True);
            Assert.That(Matches("rules -tooltip", Element("App.Strings.Rules_Clear_Tooltip")), Is.False);
        });
    }

    [Test]
    public void SeveralNegations_ExcludeAll()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Matches("-tests -dsmsuite", Element("App.Views.MainWindow")), Is.True);
            Assert.That(Matches("-tests -dsmsuite", Element("Tests.UnitTests.Foo")), Is.False);
            Assert.That(Matches("-tests -dsmsuite", Element("DsmSuite.Viewer.Bar")), Is.False);
        });
    }

    [Test]
    public void Negation_BelongsToItsOwnOrGroup()
    {
        // "views -window | dialog" reads as "((NOT views) AND window) OR dialog" - the negation does not
        // reach across the pipe.
        Assert.That(Matches("views -window | dialog", Element("App.Other.Dialog")), Is.True);
    }

    [Test]
    public void Negation_AppliesToATypeTerm()
    {
        var property = Element("App.Strings.Close", type: CodeElementType.Property);
        var method = Element("App.Service.Run", type: CodeElementType.Method);

        Assert.Multiple(() =>
        {
            Assert.That(Matches("-type:property", property), Is.False);
            Assert.That(Matches("-type:property", method), Is.True);
        });
    }

    [Test]
    public void Negation_AppliesToASourceTerm()
    {
        var external = Element("System.String", isExternal: true);
        var internalElement = Element("App.Service");

        Assert.Multiple(() =>
        {
            Assert.That(Matches("-source:extern", external), Is.False);
            Assert.That(Matches("-source:extern", internalElement), Is.True);
        });
    }

    [Test]
    public void SourceGenerated_SelectsWhatAToolWrote()
    {
        var generated = Element("App.MainWindow.Connect", type: CodeElementType.Method, isGenerated: true);
        var handWritten = Element("App.MainWindow.OnClick", type: CodeElementType.Method);

        Assert.Multiple(() =>
        {
            Assert.That(Matches("source:generated", generated), Is.True);
            Assert.That(Matches("source:generated", handWritten), Is.False);

            // The useful direction: drop the rows nobody can act on.
            Assert.That(Matches("-source:generated", generated), Is.False);
            Assert.That(Matches("-source:generated", handWritten), Is.True);
        });
    }

    /// <summary>The three source terms are independent - generated code is internal code, not a third kind.</summary>
    [Test]
    public void SourceGenerated_IsIndependentOfInternAndExtern()
    {
        var generated = Element("App.MainWindow.Connect", type: CodeElementType.Method, isGenerated: true);

        Assert.Multiple(() =>
        {
            Assert.That(Matches("source:intern", generated), Is.True);
            Assert.That(Matches("source:extern", generated), Is.False);

            // Combined with AND, which is how it is typed in the search box.
            Assert.That(Matches("source:intern -source:generated", generated), Is.False);
        });
    }

    [Test]
    public void Negation_AppliesToAPascalCaseTerm()
    {
        // An upper case letter switches the term to the ReSharper style regex ("DDG" -> DynamicDataGrid).
        Assert.Multiple(() =>
        {
            Assert.That(Matches("DDG", Element("App.Grids.DynamicDataGrid")), Is.True);
            Assert.That(Matches("-DDG", Element("App.Grids.DynamicDataGrid")), Is.False);
            Assert.That(Matches("-DDG", Element("App.Views.MainWindow")), Is.True);
        });
    }

    [Test]
    public void NegationDisabled_TreatsTheMinusAsPartOfTheTerm()
    {
        // The tree turns negation off: it expands and highlights every ancestor of a match, so an
        // excluding search would unfold the whole tree. The '-' then searches literally and finds nothing
        // instead of matching everything.
        var element = Element("App.Resources.Strings.Close");

        var withNegation = SearchExpressionFactory.CreateSearchExpression("-resources");
        var withoutNegation = SearchExpressionFactory.CreateSearchExpression("-resources",
            SearchExpressionFactory.TextSearchField.FullName, false);

        Assert.Multiple(() =>
        {
            Assert.That(withNegation.Evaluate(element), Is.False);
            Assert.That(withoutNegation.Evaluate(element), Is.False);
            Assert.That(withoutNegation.Evaluate(Element("App.Views.MainWindow")), Is.False);
        });
    }

    [Test]
    public void NegationDisabled_LeavesPositiveTermsUntouched()
    {
        var expression = SearchExpressionFactory.CreateSearchExpression("resources close",
            SearchExpressionFactory.TextSearchField.FullName, false);

        Assert.That(expression.Evaluate(Element("App.Resources.Strings.Close")), Is.True);
    }

    [Test]
    public void LoneMinus_IsALiteralTerm()
    {
        // Nothing to negate. It must not become an empty term, which would match everything and turn the
        // expression into "match nothing".
        Assert.Multiple(() =>
        {
            Assert.That(Matches("-", Element("App.My-Package.Widget")), Is.True);
            Assert.That(Matches("-", Element("App.Views.MainWindow")), Is.False);
        });
    }

    [Test]
    public void MinusInsideATerm_IsNotANegation()
    {
        Assert.That(Matches("my-package", Element("App.My-Package.Widget")), Is.True);
    }

    [Test]
    public void NullElement_NeverMatches_NotEvenANegation()
    {
        // The tree has a virtual root without a code element. It answers "no" to every term, and an
        // exclusion must not turn that into a match.
        Assert.Multiple(() =>
        {
            Assert.That(Matches("anything", null), Is.False);
            Assert.That(Matches("-anything", null), Is.False);
            Assert.That(Matches("-type:property", null), Is.False);
        });
    }

    [Test]
    public void NameField_SearchesTheNameInsteadOfTheFullName()
    {
        var element = Element("App.Resources.Strings", "Strings");

        Assert.Multiple(() =>
        {
            Assert.That(Matches("resources", element, SearchExpressionFactory.TextSearchField.Name), Is.False);
            Assert.That(Matches("strings", element, SearchExpressionFactory.TextSearchField.Name), Is.True);
            Assert.That(Matches("-resources", element, SearchExpressionFactory.TextSearchField.Name), Is.True);
        });
    }

    [Test]
    public void EmptySearch_MatchesNothing()
    {
        // Documents today's behaviour: an empty text yields no OR group at all, and an OR over nothing is
        // false. Every caller short-circuits on empty input before building an expression, so this never
        // shows up as "the filter hid everything".
        Assert.That(Matches("", Element("App.Views.MainWindow")), Is.False);
    }
}
