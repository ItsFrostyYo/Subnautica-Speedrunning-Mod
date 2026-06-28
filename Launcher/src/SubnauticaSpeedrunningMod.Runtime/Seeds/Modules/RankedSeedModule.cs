namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal sealed class ModSeedModule : IModModule
    {
        public string Name
        {
            get { return "Ranked Seeds"; }
        }

        public void Initialize(RuntimeContext context)
        {
            ModSeedRuntimeHost.Install(context);
        }
    }
}
