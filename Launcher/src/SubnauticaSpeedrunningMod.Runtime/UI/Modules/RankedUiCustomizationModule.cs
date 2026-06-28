namespace SubnauticaSpeedrunningMod.Runtime.Ui
{
    internal sealed class ModUiCustomizationModule : IModModule
    {
        public string Name
        {
            get { return "Ranked UI"; }
        }

        public void Initialize(RuntimeContext context)
        {
            ModMainMenuRuntimeHost.Install(context);
        }
    }
}
