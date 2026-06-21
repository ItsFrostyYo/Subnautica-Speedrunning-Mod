namespace SubnauticaSpeedrunningRanked.Runtime
{
    public sealed class RankedServerApiClient
    {
        public string BaseUrl { get; private set; }

        public RankedServerApiClient(string baseUrl)
        {
            BaseUrl = baseUrl ?? string.Empty;
        }
    }
}
