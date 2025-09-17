namespace ModuleLevel2;

public sealed class SelfReferencingClass
{
    public SelfReferencingClass(string commitHash)
    {
        CommitHash = commitHash;
    }


    public object Commit { get; set; }


    public string CommitHash { get; }


    public List<SelfReferencingClass> Parents { get; } = new();
    public List<SelfReferencingClass> Children { get; } = new();
}