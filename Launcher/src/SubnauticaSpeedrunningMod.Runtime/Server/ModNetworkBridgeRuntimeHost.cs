using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace SubnauticaSpeedrunningMod.Runtime
{
    internal static class ModNetworkBridgeRuntimeHost
    {
        private const string LocalBridgeUrl = "http://127.0.0.1:5079/";
        private static readonly object Sync = new object();
        private static bool _initialized;
        private static string _effectiveBaseUrl = string.Empty;

        public static string PrepareEffectiveApiBaseUrl(string modRoot, string configuredBaseUrl)
        {
            lock (Sync)
            {
                if (_initialized)
                {
                    return _effectiveBaseUrl;
                }

                _initialized = true;
                _effectiveBaseUrl = NormalizeBaseUrl(configuredBaseUrl);

                if (string.IsNullOrEmpty(_effectiveBaseUrl) ||
                    _effectiveBaseUrl.IndexOf("example.invalid", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return _effectiveBaseUrl;
                }

                Uri uri;
                if (!Uri.TryCreate(_effectiveBaseUrl, UriKind.Absolute, out uri))
                {
                    return _effectiveBaseUrl;
                }

                if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    return _effectiveBaseUrl;
                }

                if (IsBridgeHealthy(_effectiveBaseUrl))
                {
                    _effectiveBaseUrl = LocalBridgeUrl;
                    ModLog.Info("Using existing local network bridge at " + LocalBridgeUrl);
                    return _effectiveBaseUrl;
                }

                string bridgeExecutablePath = Path.Combine(Path.Combine(modRoot, "Bridge"), "SubnauticaSpeedrunningMod.NetworkBridge.exe");
                if (!File.Exists(bridgeExecutablePath))
                {
                    ModLog.Warn(
                        "Network bridge executable is missing at " + bridgeExecutablePath +
                        ". Falling back to direct HTTPS networking. On some Windows installs that direct HTTPS path will fail, so this usually means the bridge package is incomplete.");
                    return _effectiveBaseUrl;
                }

                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = bridgeExecutablePath,
                        Arguments =
                            "--listen \"" + LocalBridgeUrl + "\" " +
                            "--remote \"" + _effectiveBaseUrl + "\" " +
                            "--parent-pid " + Process.GetCurrentProcess().Id,
                        WorkingDirectory = Path.GetDirectoryName(bridgeExecutablePath),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process.Start(startInfo);
                    ModLog.Info("Started local network bridge for HTTPS relay.");
                }
                catch (Exception ex)
                {
                    ModLog.Warn(
                        "Failed to start local network bridge: " + ex.Message +
                        ". Falling back to direct HTTPS networking.");
                    return _effectiveBaseUrl;
                }

                if (WaitForBridge(_effectiveBaseUrl))
                {
                    _effectiveBaseUrl = LocalBridgeUrl;
                    ModLog.Info("Local network bridge is ready at " + LocalBridgeUrl);
                }
                else
                {
                    ModLog.Warn(
                        "Local network bridge did not become ready in time. Falling back to direct HTTPS networking. " +
                        "This usually means the bridge was blocked, crashed, or is still extracting on first launch.");
                }

                return _effectiveBaseUrl;
            }
        }

        private static bool WaitForBridge(string expectedRemoteBaseUrl)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                if (IsBridgeHealthy(expectedRemoteBaseUrl))
                {
                    return true;
                }

                Thread.Sleep(250);
            }

            return false;
        }

        private static bool IsBridgeHealthy(string expectedRemoteBaseUrl)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(new Uri(LocalBridgeUrl), "health"));
                request.Method = "GET";
                request.Timeout = 1500;
                request.ReadWriteTimeout = 1500;
                request.KeepAlive = false;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream() ?? Stream.Null))
                {
                    string body = reader.ReadToEnd();
                    return response.StatusCode == HttpStatusCode.OK &&
                           body.IndexOf(expectedRemoteBaseUrl, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                return string.Empty;
            }

            string trimmed = baseUrl.Trim();
            if (!trimmed.EndsWith("/", StringComparison.Ordinal))
            {
                trimmed += "/";
            }

            return trimmed;
        }
    }
}
