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