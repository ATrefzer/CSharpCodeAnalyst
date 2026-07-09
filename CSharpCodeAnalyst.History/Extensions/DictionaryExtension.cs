namespace CSharpCodeAnalyst.History.Extensions
{
    /// <summary>
    ///     Accumulate work stored in a dictionary value.
    /// </summary>
    public static class DictionaryExtension
    {
        public static void AddToValue<TKey>(
                this Dictionary<TKey, uint> dict, TKey key, uint work) where TKey : notnull
        {
            if (dict.TryGetValue(key, out var currentValue))
            {
                dict[key] = currentValue + work;
            }
            else
            {
                dict[key] = work;
            }
        }

        /// <summary>
        ///     Copies path-keyed entries into a dictionary that compares keys case-insensitively,
        ///     while preserving the original key casing. This is THE single place that establishes
        ///     the "path lookups are case-insensitive" contract for the whole history pipeline: it
        ///     is applied at the source (when a dictionary is first built) and again right after a
        ///     JSON round-trip (which silently drops the comparer). Keys that differ only in casing
        ///     - Git can track those - are deduplicated, first one wins.
        /// </summary>
        public static Dictionary<string, TValue> ToCaseInsensitivePathKeys<TValue>(
                this IEnumerable<KeyValuePair<string, TValue>> source)
        {
            var result = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in source)
            {
                result.TryAdd(pair.Key, pair.Value);
            }

            return result;
        }
    }
}