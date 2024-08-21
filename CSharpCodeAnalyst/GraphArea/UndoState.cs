using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

internal struct UndoState
{
    public UndoState(CodeGraph codeGraph, PresentationState presentationState)
    {
        CodeGraph = codeGraph;
        PresentationState = presentationState;
    }

    public CodeGraph CodeGraph { get; }
    internal PresentationState PresentationState { get; }
}
