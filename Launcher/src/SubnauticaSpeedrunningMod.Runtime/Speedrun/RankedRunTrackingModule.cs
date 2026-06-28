namespace SubnauticaSpeedrunningMod.Runtime.RunTracking
{
    internal sealed class ModRunTrackingModule : IModModule
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
