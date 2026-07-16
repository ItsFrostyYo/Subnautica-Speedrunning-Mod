using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

string listenUrl = "http://127.0.0.1:5079/";
string remoteBaseUrl = string.Empty;
int parentPid = 0;

for (int i = 0; i < args.Length; i++)
{
    if (string.Equals(args[i], "--listen", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        listenUrl = args[++i];
        continue;
    }

    if (string.Equals(args[i], "--remote", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        remoteBaseUrl = args[++i];
        continue;
    }

    if (string.Equals(args[i], "--parent-pid", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        int.TryParse(args[++i], out parentPid);
        continue;
    }
}

if (string.IsNullOrWhiteSpace(remoteBaseUrl))
{
    throw new InvalidOperationException("Missing required --remote argument.");
}

if (!remoteBaseUrl.EndsWith("/", StringComparison.Ordinal))
{
    remoteBaseUrl += "/";
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(listenUrl);

builder.Services.AddSingleton(new BridgeOptions(listenUrl, remoteBaseUrl, parentPid));
builder.Services.AddHttpClient("relay", client =>
{
    client.Timeout = TimeSpan.FromSeconds(75);
    client.DefaultRequestHeaders.ConnectionClose = true;
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SubnauticaSpeedrunningMod.NetworkBridge");
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    UseProxy = false
});

var app = builder.Build();

BridgeOptions options = app.Services.GetRequiredService<BridgeOptions>();
IHttpClientFactory httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();

if (options.ParentPid > 0)
{
    _ = Task.Run(async () =>
    {
        while (true)
        {
            await Task.Delay(2000);
            try
            {
                Process process = Process.GetProcessById(options.ParentPid);
                if (process.HasExited)
                {
                    await app.StopAsync();
                    break;
                }
            }
            catch
            {
                await app.StopAsync();
                break;
            }
        }
    });
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    remoteBaseUrl = options.RemoteBaseUrl
}));

app.MapMethods("{**path}", new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS" }, async context =>
{
    string path = context.Request.RouteValues.TryGetValue("path", out object? rawPath) ? Convert.ToString(rawPath) ?? string.Empty : string.Empty;
    string query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value! : string.Empty;
    Uri targetUri = new Uri(new Uri(options.RemoteBaseUrl), path + query);

    using HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);

    if (context.Request.ContentLength.GetValueOrDefault() > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
    {
        using MemoryStream buffer = new MemoryStream();
        await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
        buffer.Position = 0;
        requestMessage.Content = new ByteArrayContent(buffer.ToArray());
        if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
        {
            requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
        }
    }

    foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in context.Request.Headers)
    {
        if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(header.Key, "Connection", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(header.Key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(header.Key, "Expect", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
        {
            requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    HttpClient client = httpClientFactory.CreateClient("relay");
    using HttpResponseMessage response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

    context.Response.StatusCode = (int)response.StatusCode;

    foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    context.Response.Headers.Remove("transfer-encoding");
    await response.Content.CopyToAsync(context.Response.Body);
});

await app.RunAsync();

internal sealed record BridgeOptions(string ListenUrl, string RemoteBaseUrl, int ParentPid);
