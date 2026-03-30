namespace CSharpCodeAnalyst.Shared.Messages;

public class LocateInTreeRequest(string id)
{
    public string Id { get; } = id;
}