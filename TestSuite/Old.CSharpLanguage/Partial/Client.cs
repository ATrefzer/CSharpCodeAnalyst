namespace CSharpLanguage.Partial;

internal class Client
{
    public static Client CreateInstance()
    {
        var p = new PartialClass();
        p.MethodInPartialClassPart1();
        p.MethodInPartialClassPart2();
        return new Client();
    }
}