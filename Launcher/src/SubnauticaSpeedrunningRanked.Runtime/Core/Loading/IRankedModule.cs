namespace SubnauticaSpeedrunningRanked.Runtime
{
    public interface IRankedModule
    {
        string Name { get; }

        void Initialize(RuntimeContext context);
    }
}
