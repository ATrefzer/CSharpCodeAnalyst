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
        if (message is CodeElementsDeleted codeElementDeleted)
        {
            foreach (var session in Sessions)
            {
                session.CodeElementIds.RemoveAll(id => codeElementDeleted.DeletedIds.Contains(id));
                session.Relationships.RemoveAll(r => codeElementDeleted.DeletedIds.Contains(r.SourceId));
                session.Relationships.RemoveAll(r => codeElementDeleted.DeletedIds.Contains(r.TargetId));
                session.PresentationState.RemoveStates(codeElementDeleted.DeletedIds);
            }
        }

        if (message is RelationshipsDeleted relationshipsDeleted)
        {
            foreach (var session in Sessions)
            {
                foreach (var relationship in relationshipsDeleted.Deleted)
                {
                    session.Relationships.RemoveAll(r => r.Equals(relationship));
                }
            }
        }

    }
}