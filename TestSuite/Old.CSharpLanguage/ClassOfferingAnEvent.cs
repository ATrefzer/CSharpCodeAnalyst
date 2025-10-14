namespace CSharpLanguage;

public class MyEventArgs : EventArgs
{
}

public delegate void MyDelegate(object sender, MyEventArgs e);

public class ClassOfferingAnEvent
{
    public event EventHandler MyEvent2;

    public event MyDelegate MyEvent1;

    private void OnEvent()
    {
        MyEvent2(null, null);
    }
}