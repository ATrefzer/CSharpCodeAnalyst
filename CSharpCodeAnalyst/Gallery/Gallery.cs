using CSharpCodeAnalyst.GraphArea;

namespace CSharpCodeAnalyst.Gallery;

[Serializable]
public class Gallery
{
    public Gallery()
    {

    }

    public List<GraphSessionState> Sessions { get; set; } = new();

    public void AddSession(GraphSessionState session)
    {
        Sessions.Add(session);
    }
}