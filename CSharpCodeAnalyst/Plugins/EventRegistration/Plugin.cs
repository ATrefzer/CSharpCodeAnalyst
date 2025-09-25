using System.Windows;
using Contracts.Graph;
using CSharpCodeAnalyst.Shared.Messaging;

namespace CSharpCodeAnalyst.Plugins.EventRegistration;

public class Plugin
{
    public void Analyze(CodeGraph graph, IPublisher messaging)
    {
        var imbalances = EventRegistrationAnalyzer.FindImbalances(graph);

        if (imbalances.Count == 0)
        {
            MessageBox.Show("No event handler registration / un-registration imbalances found");
            return;
        }

        var vm = new EventImbalancesViewModel(imbalances);
        messaging.Publish(new ShowPluginTabularDataRequest(vm));
    }
}