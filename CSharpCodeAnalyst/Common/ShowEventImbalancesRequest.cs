using CodeParser.Analysis.EventRegistration;

namespace CSharpCodeAnalyst.Common;

public class ShowEventImbalancesRequest
{
    public List<EventRegistrationImbalance> Imbalances { get; }

    public ShowEventImbalancesRequest(List<EventRegistrationImbalance> imbalances)
    {
        Imbalances = imbalances;
    }
}