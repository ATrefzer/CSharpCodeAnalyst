namespace FollowHeuristic;

public class BaseClass
{
    public virtual void BuiltUp()
    {
        AddToMaster();
    }

    public virtual void AddToMaster()
    {
    }
}


class Derived1 : BaseClass
{
    public override void BuiltUp()
    {
        base.BuiltUp();
    }
    
    public override void AddToMaster()
    {
        base.AddToMaster();
    }
}

class Derived2 : BaseClass
{
    public override void BuiltUp()
    {
        base.BuiltUp();
    }

    public override void AddToMaster()
    {
        base.AddToMaster();
    }
}

