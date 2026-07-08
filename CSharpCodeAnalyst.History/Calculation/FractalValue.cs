namespace CSharpCodeAnalyst.History.Calculation
{
    public static class FractalValue
    {
        /// <summary>
        /// Calculates how fragmented the work is.
        /// 0 = only a single developer worked on this piece of code
        /// The more fragmented the work gets the more the value converges to 1 (never reached)
        /// See paper http://www.inf.usi.ch/lanza/Downloads/DAmb05b.pdf
        /// </summary>
        public static double Calculate(Dictionary<string, uint> developerToContribution)
        {
            var fractalValue = 0.0;

            var allContribution = developerToContribution.Values.Sum(x => x);

            foreach (var contribution in developerToContribution)
            {
                double contributionOfThisDeveloper = contribution.Value;
                fractalValue += Math.Pow(contributionOfThisDeveloper / allContribution, 2.0);
            }

            return 1 - fractalValue;
        }
    }
}