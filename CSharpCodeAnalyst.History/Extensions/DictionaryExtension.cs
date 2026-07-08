namespace CSharpCodeAnalyst.History.Extensions
{
    /// <summary>
    ///     Accumulate work stored in a dictionary value.
    /// </summary>
    public static class DictionaryExtension
    {
        public static void AddToValue<TKey>(
                this Dictionary<TKey, uint> dict, TKey key, uint work)
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
    }
}