namespace Regression.SpecificBugs
{
    public class AssignmentDuplicateTest
    {
        public string TestProperty { get; set; }
        public string TestField;

        public void TestMethod()
        {
            // Diese Assignment-Expressions sollten nur EINMAL als Relationship erfasst werden,
            // nicht doppelt durch AnalyzeAssignment() und AnalyzeIdentifier()/AnalyzeMemberAccess()

            // Simple property assignment - sollte nur 1x TestProperty-Relationship haben
            TestProperty = "value";

            // Field assignment - sollte nur 1x TestField-Relationship haben
            TestField = "value";

            // Member access assignment - sollte nur 1x TestProperty-Relationship haben
            this.TestProperty = "another value";

            // Complex assignment - sollte nur 1x TestProperty pro Seite haben
            TestProperty = this.TestField;
        }
    }
}