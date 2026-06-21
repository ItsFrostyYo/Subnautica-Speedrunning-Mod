namespace SubnauticaSpeedrunningRanked.Runtime.RunTracking
{
    internal sealed class RankedRunTrackingModule : IRankedModule
    {
        public string Name
        {
            get { return "Ranked Run Tracking"; }
        }

        public void Initialize(RuntimeContext context)
        {
            SingleplayerRunRuntimeHost.Install(context);
        }
    }
}
