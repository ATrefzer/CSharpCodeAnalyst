namespace FollowHeuristic.PropertyChain;

// Scenario for FollowIncomingCallsHeuristically:
// The incoming call chain passes through a property getter. The traversal must
// continue at the property and find Client.Consume as the origin.

public class Repository
{
    public int Compute()
    {
        return 42;
    }
}

public class Facade
{
    private readonly Repository _repository = new();

    public int Value
    {
        get { return _repository.Compute(); }
    }
}

public class Client
{
    public void Consume()
    {
        var facade = new Facade();
        var unused = facade.Value;
    }
}
