namespace Core.BasicLanguageFeatures;

public class TypeOf
{
    public void Experiment1(object x)
    {
        if (x.GetType() == typeof(BaseClass))
        {
        }
    }


    public void Experiment2(object x)
    {
        var isOfType = x is BaseClass;
    }


    public void Experiment3(object x)
    {
        var y = x as BaseClass;
    }
}