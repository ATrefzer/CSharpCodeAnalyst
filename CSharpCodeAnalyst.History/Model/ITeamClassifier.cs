namespace CSharpCodeAnalyst.History.Model
{
    public interface ITeamClassifier
    {
        string GetAssociatedTeam(string committer, DateTime checkInDate);
    }
}