namespace CSharpLanguage.Regression_FollowIncomingCalls1
{
    abstract class Base
    {
        Base _base;

        protected virtual void AddToSlave()
        {
            _base.AddToSlave();
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