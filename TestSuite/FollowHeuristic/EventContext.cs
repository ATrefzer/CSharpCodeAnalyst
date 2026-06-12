namespace FollowHeuristic.EventContext;

// Scenario for FollowIncomingCallsHeuristically:
// Subscriber and Publisher share a base class, so the start context forbids Publisher.
// Raising an event dispatches via delegate, the publisher side is unrelated to the
// subscriber's hierarchy. The chain Trigger -> Raise -> Changed -> OnChanged is a real
// origin and must not be filtered by the subscriber's hierarchy restriction.

public class Base
{
}

public class Subscriber : Base
{
    public void Attach(Publisher publisher)
    {
        publisher.Changed += OnChanged;
    }

    public void OnChanged(object? sender, EventArgs e)
    {
    }
}

public class Publisher : Base
{
    public event EventHandler? Changed;

    public void Raise()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Trigger()
    {
        Raise();
    }
}
