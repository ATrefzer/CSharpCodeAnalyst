using CSharpCodeAnalyst.Areas.GraphArea;
using CSharpCodeAnalyst.Messages;

namespace CSharpCodeAnalyst.Gallery;

[Serializable]
public class Gallery
{
    public List<GraphSession> Sessions { get; set; } = [];

    public void AddSession(GraphSession session)
    {
        Sessions.Add(session);
    }

    public void HandleCodeGraphRefactored(CodeGraphRefactored message)
    {
        // Remove deleted elements from the gallery.
        // Movement should be handled automatically but without parent, since we only store ids.
        if (message is CodeElementsDeleted deleted && Sessions.Any())
        {
            foreach (var session in Sessions)
            {
                session.CodeElementIds.RemoveAll(id => deleted.DeletedIds.Contains(id));
                session.Relationships.RemoveAll(r => deleted.DeletedIds.Contains(r.SourceId));
                session.Relationships.RemoveAll(r => deleted.DeletedIds.Contains(r.TargetId));
                session.PresentationState.RemoveStates(deleted.DeletedIds);
            }
        }
    }
}