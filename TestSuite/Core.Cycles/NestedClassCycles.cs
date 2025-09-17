namespace Core.Cycles;

// Test nested class dependency cycles
public class OuterClassA
{
    public class DirectChildA
    {
        public OuterClassB.MiddleClassB.NestedInnerB? RelatedB;

        public void UseB()
        {
            RelatedB?.ProcessB();
        }
    }

    public class MiddleClassA
    {
        public class NestedInnerA
        {
            public OuterClassB.DirectChildB? RelatedDirectB;

            public void UseDirectB()
            {
                RelatedDirectB?.ProcessDirectB();
            }
        }
    }
}

public class OuterClassB
{
    public class DirectChildB
    {
        public OuterClassA.DirectChildA? RelatedA;

        public void ProcessDirectB()
        {
            RelatedA?.UseB();
        }
    }

    public class MiddleClassB
    {
        public class NestedInnerB
        {
            public OuterClassA.DirectChildA? RelatedA;

            public void ProcessB()
            {
                RelatedA?.UseB();
            }
        }
    }
}

// Self-referencing nested classes
public class SelfReferencingOuter
{

    public SelfReferencingInner? RootInner;

    public void CreateHierarchy()
    {
        RootInner = new SelfReferencingInner();
        var child = new SelfReferencingInner();
        child.LinkToParent(RootInner);
    }

    public class SelfReferencingInner
    {
        public SelfReferencingInner? Child;
        public SelfReferencingInner? Parent;

        public void LinkToParent(SelfReferencingInner parent)
        {
            Parent = parent;
            parent.Child = this;
        }
    }
}

// Multiple nesting level cycles
public class Level1A
{
    public class Level2A
    {

        public void ProcessLevel2A()
        {
            var level3 = new Level3A();
            level3.UseCrossReference();
        }

        public class Level3A
        {
            public Level1B.Level2B? CrossReference;

            public void UseCrossReference()
            {
                CrossReference?.ProcessLevel2B();
            }
        }
    }
}

public class Level1B
{
    public class Level2B
    {
        public Level1A.Level2A.Level3A? BackReference;

        public void ProcessLevel2B()
        {
            // Some processing
        }

        public void UseBackReference()
        {
            BackReference?.UseCrossReference();
        }
    }
}

// Generic nested class cycles
public class GenericOuter<T>
{
    public class GenericNested<U>
    {
        public GenericOuter<U>.GenericNested<T>? CrossGeneric;

        public void UseCrossGeneric()
        {
            CrossGeneric?.ProcessGeneric();
        }

        public void ProcessGeneric()
        {
            // Generic processing
        }
    }
}