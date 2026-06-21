namespace SubnauticaSpeedrunningRanked.Runtime.Ui
{
    internal sealed class RankedUiCustomizationModule : IRankedModule
    {
        public string Name
        {
            get { return "Ranked UI"; }
        }

        public void Initialize(RuntimeContext context)
        {
            RankedMainMenuRuntimeHost.Install(context);
        }
    }
}
