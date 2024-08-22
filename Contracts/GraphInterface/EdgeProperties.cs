namespace GraphLib.Contracts;

public class EdgeProperties
{
    public double Weight { get; set; } = 1;

    public static EdgeProperties ForWeight(double weight)
    {
        var edgeProperties = new EdgeProperties
        {
            Weight = weight
        };
        return edgeProperties;
    }
}