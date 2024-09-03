using CSharpCodeAnalyst.GraphArea;

namespace CSharpCodeAnalyst.Gallery;

[Serializable]
public class Gallery
{
    public List<GraphSession> Sessions { get; set; } = new();

    public void AddSession(GraphSession session)
    {
        Sessions.Add(session);
    }
}