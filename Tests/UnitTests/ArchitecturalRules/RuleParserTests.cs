using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

namespace CodeParserTests.UnitTests.ArchitecturalRules;

[TestFixture]
public class RuleParserTests
{
    [Test]
    public void ParseDenyRule_ValidSyntax_ShouldReturnDenyRule()
    {
        // Arrange
        var ruleText = "DENY: Business.** -> Data.**";

        // Act
        var rule = RuleParser.ParseRule(ruleText);

        // Assert
        Assert.That(rule, Is.InstanceOf<DenyRule>());
        var denyRule = (DenyRule)rule;
        Assert.That(denyRule.Source, Is.EqualTo("Business.**"));
        Assert.That(denyRule.Target, Is.EqualTo("Data.**"));
        Assert.That(denyRule.RuleText, Is.EqualTo(ruleText));
    }

    [Test]
    public void ParseRestrictRule_ValidSyntax_ShouldReturnRestrictRule()
    {
        // Arrange
        var ruleText = "RESTRICT: Controllers.** -> Services.**";

        // Act
        var rule = RuleParser.ParseRule(ruleText);

        // Assert
        Assert.That(rule, Is.InstanceOf<RestrictRule>());
        var restrictRule = (RestrictRule)rule;
        Assert.That(restrictRule.Source, Is.EqualTo("Controllers.**"));
        Assert.That(restrictRule.Target, Is.EqualTo("Services.**"));
    }

    [Test]
    public void ParseIsolateRule_ValidSyntax_ShouldReturnIsolateRule()
    {
        // Arrange
        var ruleText = "ISOLATE: Domain.**";

        // Act
        var rule = RuleParser.ParseRule(ruleText);

        // Assert
        Assert.That(rule, Is.InstanceOf<IsolateRule>());
        var isolateRule = (IsolateRule)rule;
        Assert.That(isolateRule.Source, Is.EqualTo("Domain.**"));
    }

    [Test]
    public void ParseAllowRule_ValidSyntax_ShouldReturnAllowRule()
    {
        // Arrange
        var ruleText = "ALLOW: Business.Reporting.** -> Data.**";

        // Act
        var rule = RuleParser.ParseRule(ruleText);

        // Assert
        Assert.That(rule, Is.InstanceOf<AllowRule>());
        var allowRule = (AllowRule)rule;
        Assert.That(allowRule.Source, Is.EqualTo("Business.Reporting.**"));
        Assert.That(allowRule.Target, Is.EqualTo("Data.**"));
        Assert.That(allowRule.RuleText, Is.EqualTo(ruleText));
    }

    [Test]
    public void ParseAllowRule_MissingTarget_ShouldThrow()
    {
        Assert.Throws<FormatException>(() => RuleParser.ParseRule("ALLOW: Business.**"));
    }

    // The threshold is a percentage, like the value the system metrics analyzer displays.
    [TestCase("MAXCYCLICITY = 15", 15.0)]
    [TestCase("maxcyclicity=7.5", 7.5)]
    [TestCase("MAXCYCLICITY = 100", 100.0)]
    [TestCase("MAXCYCLICITY = 0", 0.0)]
    public void ParseMaxCyclicityRule_ValidSyntax_ShouldReturnMaxCyclicityRule(string ruleText, double expected)
    {
        // Act
        var rule = RuleParser.ParseRule(ruleText);

        // Assert
        Assert.That(rule, Is.InstanceOf<MaxCyclicityRule>());
        var maxCyclicityRule = (MaxCyclicityRule)rule;
        Assert.That(maxCyclicityRule.Threshold, Is.EqualTo(expected));
        Assert.That(maxCyclicityRule.RuleText, Is.EqualTo(ruleText));
    }

    // Out of range, wrong decimal separator, missing value, unknown metric.
    [TestCase("MAXCYCLICITY = 150")]
    [TestCase("MAXCYCLICITY = -10")]
    [TestCase("MAXCYCLICITY = 0,15")]
    [TestCase("MAXCYCLICITY")]
    [TestCase("MAXCOHESION = 15")]
    public void ParseMetricRule_InvalidRule_ShouldThrow(string ruleText)
    {
        Assert.Throws<FormatException>(() => RuleParser.ParseRule(ruleText));
    }

    [Test]
    public void ParseMaxLinesRule_WithoutPattern_ScopesToTheWholeGraph()
    {
        var rule = RuleParser.ParseRule("MAXLINES = 50");

        Assert.That(rule, Is.InstanceOf<MaxLinesRule>());
        var maxLinesRule = (MaxLinesRule)rule;
        Assert.That(maxLinesRule.Threshold, Is.EqualTo(50.0));
        Assert.That(maxLinesRule.Source, Is.Empty);
    }

    [Test]
    public void ParseMaxLinesRule_WithPattern_KeepsPattern()
    {
        var rule = (MaxLinesRule)RuleParser.ParseRule("MAXLINES: MyApp.Business.** = 50");

        Assert.That(rule.Source, Is.EqualTo("MyApp.Business.**"));
        Assert.That(rule.Threshold, Is.EqualTo(50.0));
    }

    [Test]
    public void ParseSystemMetricRule_WithPattern_ShouldThrow()
    {
        // A system metric rule describes the whole code base, a pattern would be meaningless.
        Assert.Throws<FormatException>(() => RuleParser.ParseRule("MAXCYCLICITY: MyApp.** = 15"));
    }

    [Test]
    public void ParseMetricRule_UnknownKeyword_ShouldNameTheKnownRules()
    {
        var ex = Assert.Throws<FormatException>(() => RuleParser.ParseRule("MAXDEPTH = 3"));
        Assert.That(ex.Message, Contains.Substring(MaxCyclicityRule.RuleKeyword));
    }

    [Test]
    public void ParseRule_ConstructorMemberName_ShouldParse()
    {
        // Roslyn names constructors ".ctor", producing a double dot in the full path.
        var ruleText = "ALLOW: MyApp.Features.AnalyzerManager.LoadAnalyzers -> MyApp.Rules.Analyzer..ctor";

        var rule = RuleParser.ParseRule(ruleText);

        Assert.That(rule, Is.InstanceOf<AllowRule>());
        var allowRule = (AllowRule)rule;
        Assert.That(allowRule.Source, Is.EqualTo("MyApp.Features.AnalyzerManager.LoadAnalyzers"));
        Assert.That(allowRule.Target, Is.EqualTo("MyApp.Rules.Analyzer..ctor"));
    }

    [Test]
    public void ParseRule_StaticConstructorMemberName_ShouldParse()
    {
        // Static constructors are named ".cctor".
        var rule = RuleParser.ParseRule("DENY: MyApp.A..cctor -> MyApp.B.SomeType");

        Assert.That(rule, Is.InstanceOf<DenyRule>());
        Assert.That(((DenyRule)rule).Source, Is.EqualTo("MyApp.A..cctor"));
    }

    [Test]
    public void ParseRule_CaseInsensitive_ShouldWork()
    {
        // Arrange
        var ruleText = "deny: business.** -> data.**";

        // Act
        var rule = RuleParser.ParseRule(ruleText);

        // Assert
        Assert.That(rule, Is.InstanceOf<DenyRule>());
    }

    [Test]
    public void ParseRule_WithExtraSpaces_ShouldTrimCorrectly()
    {
        // Arrange
        var ruleText = "  DENY  :  Business.**  ->  Data.**  ";

        // Act
        var rule = RuleParser.ParseRule(ruleText);

        // Assert
        var denyRule = (DenyRule)rule;
        Assert.That(denyRule.Source, Is.EqualTo("Business.**"));
        Assert.That(denyRule.Target, Is.EqualTo("Data.**"));
    }

    [Test]
    public void ParseRule_InvalidSyntax_ShouldThrowFormatException()
    {
        // Arrange
        var invalidRuleText = "INVALID: Something wrong";

        // Act & Assert
        var ex = Assert.Throws<FormatException>(() => RuleParser.ParseRule(invalidRuleText));
        Assert.That(ex.Message, Contains.Substring("Invalid rule syntax"));
    }

    [Test]
    public void ParseRules_MultipleRules_ShouldParseAll()
    {
        // Arrange
        var rulesText = """
                        // Comment line
                        DENY: Business.** -> Data.**

                        RESTRICT: Controllers.** -> Services.**
                        ISOLATE: Domain.**
                        """;

        // Act
        var rules = RuleParser.ParseRules(rulesText);

        // Assert
        Assert.That(rules.Count, Is.EqualTo(3));
        Assert.That(rules[0], Is.InstanceOf<DenyRule>());
        Assert.That(rules[1], Is.InstanceOf<RestrictRule>());
        Assert.That(rules[2], Is.InstanceOf<IsolateRule>());
    }

    [Test]
    public void ParseRules_SkipsCommentsAndEmptyLines()
    {
        // Arrange
        var rulesText = """
                        // This is a comment

                        DENY: Business.** -> Data.**
                        // Another comment

                        ISOLATE: Domain.**
                        """;

        // Act
        var rules = RuleParser.ParseRules(rulesText);

        // Assert
        Assert.That(rules.Count, Is.EqualTo(2));
    }

    [Test]
    public void ParseRules_InvalidRuleInMiddle_ShouldThrowWithLineNumber()
    {
        // Arrange
        var rulesText = """
                        DENY: Business.** -> Data.**
                        INVALID: Wrong syntax
                        ISOLATE: Domain.**
                        """;

        // Act & Assert
        var ex = Assert.Throws<FormatException>(() => RuleParser.ParseRules(rulesText));
        Assert.That(ex.Message, Contains.Substring("line 2"));
    }
}