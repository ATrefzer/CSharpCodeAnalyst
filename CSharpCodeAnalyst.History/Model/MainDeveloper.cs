namespace CSharpCodeAnalyst.History.Model
{
    public sealed class MainDeveloper
    {
        public MainDeveloper(string mainDeveloper, double percent)
        {
            Developer = mainDeveloper;
            Percent = percent;
        }

        public string Developer { get; }
        public double Percent { get; }
    }
}