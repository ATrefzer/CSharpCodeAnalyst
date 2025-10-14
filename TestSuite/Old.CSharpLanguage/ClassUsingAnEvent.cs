namespace CSharpLanguage;

internal class ClassUsingAnEvent
{
    private void Init()
    {
        var x = new ClassOfferingAnEvent();
        x.MyEvent1 += MyEventHandler;

        x.MyEvent2 += MyEventHandler2;

        var foo = new TheExtendedType();
        foo.Slice(1, 2);


        foo.Slice(1, 2);
    }

    private void MyEventHandler2(object? sender, EventArgs e)
    {
        throw new NotImplementedException();
    }

    private void MyEventHandler(object sender, MyEventArgs e)
    {
        throw new CustomException();
    }
}