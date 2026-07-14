namespace CSharpCodeAnalyst.Contracts
{
    /// <summary>
    ///     How raw leaf weights (e.g. commit counts) are mapped onto the 0..1 color scale of a tree map.
    /// </summary>
    public enum WeightNormalizationStrategy
    {
        /// <summary>
        ///     Rank-based percentile mapping. Encodes only the ORDER of the leaves; distances are lost
        ///     (the 2nd hottest leaf looks almost as hot as the hottest even with a fraction of the
        ///     weight). Spreads heavily skewed data evenly across the ramp. This is the default.
        /// </summary>
        RankPercentile,

        /// <summary>
        ///     Dampened min-max mapping with a square root. Keeps the real proportions between weights
        ///     but compresses outliers so the rest of the ramp stays usable.
        /// </summary>
        ProportionalSqrt
    }
}
