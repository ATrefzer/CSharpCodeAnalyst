using System.Windows.Media;
using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Features.Graph;

public interface IRelationshipContextCommand
{
    string Label { get; }
    ImageSource? Icon { get; }

    string? SubMenuGroup { get; }

    bool CanHandle(List<Relationship> relationships);

    /// <summary>
    ///     Whether the (visible) command is enabled for these relationships. Lets a command
    ///     always appear but render grayed-out when it does not apply (default: enabled).
    /// </summary>
    bool CanExecute(List<Relationship> relationships) => true;

    void Invoke(string sourceId, string targetId, List<Relationship> relationships);
}