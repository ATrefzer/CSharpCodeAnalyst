namespace CSharpLanguage.Regression_FollowIncomingCalls2;

internal abstract class Base
{
    protected virtual void AddToSlave()
    {
    }

    public void Build()
    {
        AddToSlave();
    }
}

internal class ViewModelAdapter1 : Base
{
    protected override void AddToSlave()
    {
        base.AddToSlave();
    }
}

internal class ViewModelAdapter2 : Base
{
    protected override void AddToSlave()
    {
        base.AddToSlave();
    }
}

internal class Driver
{
    private readonly ViewModelAdapter1 _adpater1 = new();
    private ViewModelAdapter2 _adpater2 = new();

    public Driver()
    {
        _adpater1.Build();
    }
}