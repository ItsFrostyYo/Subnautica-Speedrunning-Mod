namespace SubnauticaSpeedrunningMod.Runtime
{
    public sealed class ModServerApiClient
    {
        public string BaseUrl { get; private set; }

        public ModServerApiClient(string baseUrl)
        {
            BaseUrl = baseUrl ?? string.Empty;
        }
    }
}
