using System.Net.Http.Headers;
using System.Text.Json;
using SubnauticaSpeedrunningMod.Shared;

namespace SubnauticaSpeedrunningModInstaller;

internal static class ReleaseManifestClient
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static async Task<ReleasePackageInfo> GetLatestPackageAsync(CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, ModClientRelease.ReleaseManifestUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SubnauticaSpeedrunningModInstaller", ModClientRelease.SemanticVersion));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;

        string version = ReadString(root, "version");
        string zipFileName = ReadString(root, "zipFileName");
        string zipUrl = ReadString(root, "zipUrl");

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException("The release manifest did not contain a version.");
        }

        if (string.IsNullOrWhiteSpace(zipFileName))
        {
            throw new InvalidOperationException("The release manifest did not contain a zip file name.");
        }

        if (string.IsNullOrWhiteSpace(zipUrl))
        {
            zipUrl = ModClientRelease.ReleaseDownloadBaseUrl + zipFileName;
        }

        return new ReleasePackageInfo(version, zipFileName, zipUrl);
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return property.GetString() ?? string.Empty;
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        return client;
    }
}

internal sealed class ReleasePackageInfo
{
    public ReleasePackageInfo(string version, string zipFileName, string zipUrl)
    {
        Version = version;
        ZipFileName = zipFileName;
        ZipUrl = zipUrl;
    }

    public string Version { get; }
    public string ZipFileName { get; }
    public string ZipUrl { get; }
}
