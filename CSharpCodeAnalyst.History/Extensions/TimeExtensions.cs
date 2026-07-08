namespace CSharpCodeAnalyst.History.Extensions
{
    public static class TimeExtensions
    {
        public static TimeSpan Days(this int days)
        {
            return TimeSpan.FromDays(days);
        }

        public static string ToIsoShort(this DateTime date)
        {
            return date.ToString("yyyy-MM-dd");
        }
    }
}