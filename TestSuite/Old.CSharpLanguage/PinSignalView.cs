namespace CSharpLanguage;

internal class PinSignalView
{
    private void OnCreteAutomationPeer()
    {
        var x = new PinSignalViewAutomationPeer(this);
    }

    private class PinSignalViewAutomationPeer
    {
        private readonly PinSignalView _owner;

        public PinSignalViewAutomationPeer(PinSignalView owner)
        {
            _owner = owner;
        }
    }
}