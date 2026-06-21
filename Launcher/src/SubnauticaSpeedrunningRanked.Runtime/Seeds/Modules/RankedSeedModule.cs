namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal sealed class RankedSeedModule : IRankedModule
    {
        public string Name
        {
            get { return "Ranked Seeds"; }
        }

        public void Initialize(RuntimeContext context)
        {
            RankedSeedRuntimeHost.Install(context);
        }
    }
}
