namespace FollowHeuristic.ContextRace;

// Scenario for FollowIncomingCallsHeuristically:
// Base.Helper is reachable over two paths with different contexts:
//   1. Left.Target -> (Overrides) -> Base.Target -> caller Base.Helper
//      The start context forbids the sibling class Right.
//   2. Left.Target -> caller Base.OnRaised (instance call) -> (Handles) -> Raised
//      -> (Invokes) -> Base.Helper
//      The instance call resets the context, no restrictions remain.
// Right.X is a real origin: X() -> Helper() -> raises Raised -> OnRaised -> left.Target().
// It is only visible under the second (unrestricted) context. The traversal must process
// Base.Helper with both contexts and merge the results (union over all paths).

public abstract class Base
{
    public event EventHandler? Raised;

    public abstract void Target();

    public virtual void Helper()
    {
        Target();
        Raised?.Invoke(this, EventArgs.Empty);
    }

    public void Register()
    {
        Raised += OnRaised;
    }

    public void OnRaised(object? sender, EventArgs e)
    {
        if (sender is Left left)
        {
            left.Target();
        }
    }
}

public class Left : Base
{
    public override void Target()
    {
    }
}

public class Right : Base
{
    public override void Target()
    {
    }

    public void X()
    {
        Helper();
    }
}
