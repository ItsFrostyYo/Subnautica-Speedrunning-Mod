namespace SubnauticaSpeedrunningMod.Runtime
{
    public interface IModModule
    {
        string Name { get; }

        void Initialize(RuntimeContext context);
    }
}
