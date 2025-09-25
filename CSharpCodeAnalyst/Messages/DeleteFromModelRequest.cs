namespace CSharpCodeAnalyst.Messages;

public class DeleteFromModelRequest(string id)
{
    public string Id { get; } = id;
}