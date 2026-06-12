namespace FollowHeuristic.StaticReset;

// Scenario for FollowIncomingCallsHeuristically:
// Logger.Log is reached via a static call from the virtual method WorkerA.Work.
// Exploring further up from WorkerA.Work must keep the hierarchy restriction:
// the implicit call to Work() inside WorkerB resolves to WorkerBase.Work but can
// never dispatch to WorkerA.Work at runtime.

public static class Logger
{
    public static void Log()
    {
    }
}

public class WorkerBase
{
    public virtual void Work()
    {
    }

    public void Drive()
    {
        // Implicit call from within the hierarchy. May dispatch to WorkerA.Work,
        // must stay in the result.
        Work();
    }
}

public class WorkerA : WorkerBase
{
    public override void Work()
    {
        Logger.Log();
    }
}

// Intentionally does not override Work.
public class WorkerB : WorkerBase
{
    public void Other()
    {
        Work();
    }
}
