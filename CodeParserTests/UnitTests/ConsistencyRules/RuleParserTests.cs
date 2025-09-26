using CSharpCodeAnalyst.Analyzers.ConsistencyRules;
using CSharpCodeAnalyst.Analyzers.ConsistencyRules.Rules;

namespace CodeParserTests.UnitTests.ConsistencyRules;

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
        Assert.IsInstanceOf<DenyRule>(rule);
        var denyRule = (DenyRule)rule;
        Assert.AreEqual("Business.**", denyRule.Source);
        Assert.AreEqual("Data.**", denyRule.Target);
        Assert.AreEqual(ruleText, denyRule.RuleText);
    }

    [Test]
    public void ParseRestrictRule_ValidSyntax_ShouldReturnRestrictRule()
    {
        // Arrange
        var ruleText = "RESTRICT: Controllers.** -> Services.**";

        // Act
        var rule = RuleParser.ParseRule(ruleText);

        // Assert
        Assert.IsInstanceOf<RestrictRule>(rule);
        var restrictRule = (RestrictRule)rule;
        Assert.AreEqual("Controllers.**", restrictRule.Source);
        Assert.AreEqual("Services.**", restrictRule.Target);
    }

    [Test]
    public void ParseIsolateRule_ValidSyntax_ShouldReturnIsolateRule()
    {
        // Arrange
        var ruleText = "ISOLATE: Domain.**";

        // Act
        var rule = RuleParser.ParseRule(ruleText);

        // Assert
        Assert.IsInstanceOf<IsolateRule>(rule);
        var isolateRule = (IsolateRule)rule;
        Assert.AreEqual("Domain.**", isolateRule.Source);
    }

    [Test]
    public void ParseRule_CaseInsensitive_ShouldWork()
    {
        // Arrange
        var ruleText = "deny: business.** -> data.**";

        // Act
        var rule = RuleParser.ParseRule(ruleText);

        // Assert
        Assert.IsInstanceOf<DenyRule>(rule);
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
        Assert.AreEqual("Business.**", denyRule.Source);
        Assert.AreEqual("Data.**", denyRule.Target);
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
        Assert.AreEqual(3, rules.Count);
        Assert.IsInstanceOf<DenyRule>(rules[0]);
        Assert.IsInstanceOf<RestrictRule>(rules[1]);
        Assert.IsInstanceOf<IsolateRule>(rules[2]);
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
        Assert.AreEqual(2, rules.Count);
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