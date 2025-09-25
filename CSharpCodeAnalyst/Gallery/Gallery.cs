using CSharpCodeAnalyst.Areas.GraphArea;

namespace CSharpCodeAnalyst.Gallery;

[Serializable]
public class Gallery
{
    public List<GraphSession> Sessions { get; set; } = [];

    public void AddSession(GraphSession session)
    {
        Sessions.Add(session);
    }
}