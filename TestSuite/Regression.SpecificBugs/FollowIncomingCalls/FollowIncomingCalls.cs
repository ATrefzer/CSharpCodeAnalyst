namespace Regression.SpecificBugs.FollowIncomingCalls;

// Test cases for following incoming calls regression
public class Base
{
    private Base? _base;

    public virtual void AddToSlave()
    {
        _base?.AddToSlave(); // Base method calls itself on another instance
    }

    public void Build()
    {
        AddToSlave(); // Calls virtual method
    }
}

public class ViewModelAdapter1 : Base
{
    public override void AddToSlave()
    {
        base.AddToSlave(); // Calls base implementation
    }
}

public class ViewModelAdapter2 : Base
{
    public override void AddToSlave()
    {
        base.AddToSlave(); // Another override
    }
}

public class Driver
{
    private readonly ViewModelAdapter1 _adapter1;
    private readonly ViewModelAdapter2 _adapter2;

    public Driver()
    {
        _adapter1 = new ViewModelAdapter1();
        _adapter2 = new ViewModelAdapter2();
        _adapter1.Build(); // Triggers call chain
    }
}