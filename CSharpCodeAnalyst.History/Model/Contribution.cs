using CSharpCodeAnalyst.History.Calculation;

namespace CSharpCodeAnalyst.History.Model
{
    /// <summary>
    /// Contributions for a single file or logical component.
    /// Note: There is no alias mapping involved here.
    /// </summary>
    [Serializable]
    public sealed class Contribution
    {
        public Dictionary<string, uint> DeveloperToContribution { get; }

        public Contribution(Dictionary<string, uint> developerToContribution)
        {
            DeveloperToContribution = developerToContribution;
        }

        public double CalculateFractalValue()
        {
            return FractalValue.Calculate(DeveloperToContribution);
        }

        /// <summary>
        /// Returns the main developer for a single file
        /// </summary>
        public MainDeveloper GetMainDeveloper()
        {
            // Find main developer
            string? mainDeveloper = null;
            double linesOfWork = 0;

            double lineCount = DeveloperToContribution.Values.Sum(w => w);

            foreach (var pair in DeveloperToContribution)
            {
                if (pair.Value > linesOfWork)
                {
                    mainDeveloper = pair.Key;
                    linesOfWork = pair.Value;
                }
            }

            // No recorded work (empty or all-zero contributions): there is no main developer.
            // Return a well-defined "unknown" instead of a null developer and a NaN percentage
            // (0/0). "" is the same "unknown" convention the KnowledgeBuilder already treats as
            // the default color.
            if (mainDeveloper == null || lineCount <= 0)
            {
                return new MainDeveloper("", 0.0);
            }

            return new MainDeveloper(mainDeveloper, 100.0 * linesOfWork / lineCount);
        }
    }
}