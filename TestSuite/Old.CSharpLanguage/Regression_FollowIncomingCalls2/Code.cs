namespace CSharpLanguage.Regression_FollowIncomingCalls2
{
    abstract class Base
    {
        protected virtual void AddToSlave()
        {
        }

        public void Build()
        {
            AddToSlave();
        }
    }


    class ViewModelAdapter1 : Base
    {
        protected override void AddToSlave()
        {
            base.AddToSlave();
        }
    }

    class ViewModelAdapter2 : Base
    {
        protected override void AddToSlave()
        {
            base.AddToSlave();
        }
    }

    class Driver
    {
        ViewModelAdapter1 _adpater1 = new ViewModelAdapter1();
        ViewModelAdapter2 _adpater2 = new ViewModelAdapter2();

        public Driver()
        {
            _adpater1.Build();
        }
    }
}