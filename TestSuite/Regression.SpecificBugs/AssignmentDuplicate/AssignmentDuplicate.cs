namespace Regression.SpecificBugs.AssignmentDuplicate;

public class AssignmentDuplicate
{
    public string TestField;
    public string TestProperty { get; set; }

    public void TestMethod()
    {
        // Diese Assignment-Expressions sollten nur EINMAL als Relationship erfasst werden,
        // nicht doppelt durch AnalyzeAssignment() und AnalyzeIdentifier()/AnalyzeMemberAccess()

        // Simple property assignment - sollte nur 1x TestProperty-Relationship haben
        TestProperty = "value";

        // Field assignment - sollte nur 1x TestField-Relationship haben
        TestField = "value";

        // Member access assignment - sollte nur 1x TestProperty-Relationship haben
        TestProperty = "another value";

        // Complex assignment - sollte nur 1x TestProperty pro Seite haben
        TestProperty = TestField;
    }
}