namespace FollowHeuristic.ThisCallSibling;

// Scenario for FollowIncomingCallsHeuristically:
// "this.Process()" inside the sibling class Right resolves to Base.Process but can
// never dispatch to Left.Process at runtime. It must be filtered like the implicit call.

public class Base
{
    public virtual void Process()
    {
    }
}

public class Left : Base
{
    public override void Process()
    {
    }
}

// Intentionally does not override Process.
public class Right : Base
{
    public void RunWithThis()
    {
        this.Process();
    }

    public void RunImplicit()
    {
        Process();
    }
}

public class Consumer
{
    public void Use(Base b)
    {
        // Instance call. May dispatch to Left.Process, must stay in the result.
        b.Process();
    }
}
