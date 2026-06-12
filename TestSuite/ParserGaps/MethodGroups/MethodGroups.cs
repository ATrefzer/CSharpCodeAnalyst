using System;

namespace ParserGaps.MethodGroups;

// KNOWN GAP: Method groups are only captured when they appear as invocation arguments
// (SyntaxWalkerBase.VisitArgument -> AnalyzeArgument). Method groups in assignments,
// return statements and local declarations resolve to an IMethodSymbol, which both
// AnalyzeIdentifier and AnalyzeMemberAccess ignore.
// Additionally the old style registration "event += new EventHandler(Handler)" produces
// no Handles relationship because the right side resolves to the delegate constructor.

public class Worker
{
    public void DoWork()
    {
    }

    public void DoMoreWork()
    {
    }

    public void DoExtraWork()
    {
    }

    public void DoParallelWork()
    {
    }

    public void OnTick(object? sender, EventArgs e)
    {
    }

    public void OnTock(object? sender, EventArgs e)
    {
    }
}

public class MethodGroupUser
{
    private readonly Worker _worker = new Worker();

    private Action? _callback;

    public event EventHandler? Ticked;

    // GAP: no relationship AssignToLocal -> Worker.DoWork.
    public void AssignToLocal()
    {
        Action action = _worker.DoWork;
        action();
    }

    // GAP: no relationship ReturnMethodGroup -> Worker.DoMoreWork.
    public Action ReturnMethodGroup()
    {
        return _worker.DoMoreWork;
    }

    // GAP: no relationship AssignToField -> Worker.DoExtraWork.
    public void AssignToField()
    {
        _callback = _worker.DoExtraWork;
    }

    // GAP: no Handles relationship Worker.OnTick -> Ticked.
    public void RegisterOldStyle()
    {
        Ticked += new EventHandler(_worker.OnTick);
    }

    // Contrast case: the modern registration produces Handles Worker.OnTock -> Ticked.
    public void RegisterNewStyle()
    {
        Ticked += _worker.OnTock;
    }

    // Contrast case: method groups as invocation arguments ARE captured (Uses + IsMethodGroup).
    public void PassAsArgument()
    {
        Run(_worker.DoParallelWork);
    }

    private void Run(Action action)
    {
        action();
    }
}
