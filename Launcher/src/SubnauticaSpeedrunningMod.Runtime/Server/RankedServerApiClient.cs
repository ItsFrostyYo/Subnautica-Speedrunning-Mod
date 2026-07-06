using System;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime
{
    public sealed class ModServerApiClient
    {
        private readonly string _baseUrl;

        public ModServerApiClient(string baseUrl)
        {
            _baseUrl = NormalizeBaseUrl(baseUrl);
        }

        public string BaseUrl
        {
            get { return _baseUrl; }
        }

        public bool IsConfigured
        {
            get
            {
                return !string.IsNullOrEmpty(_baseUrl) &&
                    _baseUrl.IndexOf("example.invalid", StringComparison.OrdinalIgnoreCase) < 0;
            }
        }

        public ModMatchmakingTicketDto EnqueueMatchmaking(ModClientIdentity identity, string mode)
        {
            EnsureTls();
            EnsureConfigured();

            ModMatchmakingEnqueueRequestDto request = new ModMatchmakingEnqueueRequestDto
            {
                playerId = identity == null ? string.Empty : identity.PlayerId ?? string.Empty,
                displayName = identity == null ? string.Empty : identity.DisplayName ?? string.Empty,
                mode = mode ?? string.Empty
            };

            string responseJson = SendJsonRequest("/api/matchmaking/enqueue", "POST", JsonUtility.ToJson(request));
            return ParseTicket(responseJson);
        }

        public ModMatchmakingTicketDto GetMatchmakingTicket(string ticketId)
        {
            EnsureTls();
            EnsureConfigured();
            string responseJson = SendJsonRequest("/api/matchmaking/" + Uri.EscapeDataString(ticketId ?? string.Empty), "GET", null);
            return ParseTicket(responseJson);
        }

        public ModMatchmakingTicketDto CancelMatchmaking(string ticketId, string playerId)
        {
            EnsureTls();
            EnsureConfigured();

            ModMatchmakingCancelRequestDto request = new ModMatchmakingCancelRequestDto
            {
                playerId = playerId ?? string.Empty
            };

            string responseJson = SendJsonRequest(
                "/api/matchmaking/" + Uri.EscapeDataString(ticketId ?? string.Empty) + "/cancel",
                "POST",
                JsonUtility.ToJson(request));
            return ParseTicket(responseJson);
        }

        private void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("Networking is not configured. Set EnableNetworking=true and ApiBaseUrl in loader.config.xml.");
            }
        }

        private static void EnsureTls()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)768 | (SecurityProtocolType)3072;
            }
            catch
            {
            }
        }

        private string SendJsonRequest(string relativePath, string method, string requestBody)
        {
            HttpWebRequest request = CreateRequest(relativePath, method);
            if (!string.IsNullOrEmpty(requestBody))
            {
                byte[] payload = Encoding.UTF8.GetBytes(requestBody);
                request.ContentType = "application/json";
                request.ContentLength = payload.Length;
                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(payload, 0, payload.Length);
                }
            }

            return ReadResponse(request);
        }

        private HttpWebRequest CreateRequest(string relativePath, string method)
        {
            Uri requestUri = new Uri(new Uri(_baseUrl), relativePath.TrimStart('/'));
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUri);
            request.Method = method;
            request.Accept = "application/json";
            request.UserAgent = "SubnauticaSpeedrunningMod/" + Shared.ModClientRelease.DisplayVersion;
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            return request;
        }

        private static string ReadResponse(HttpWebRequest request)
        {
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream() ?? Stream.Null))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                string body = string.Empty;
                try
                {
                    if (ex.Response != null)
                    {
                        using (StreamReader reader = new StreamReader(ex.Response.GetResponseStream() ?? Stream.Null))
                        {
                            body = reader.ReadToEnd();
                        }
                    }
                }
                catch
                {
                }

                if (!string.IsNullOrEmpty(body))
                {
                    throw new InvalidOperationException(body);
                }

                throw new InvalidOperationException(ex.Message);
            }
        }

        private static ModMatchmakingTicketDto ParseTicket(string json)
        {
            ModMatchmakingTicketDto ticket = JsonUtility.FromJson<ModMatchmakingTicketDto>(json ?? string.Empty);
            if (ticket == null)
            {
                throw new InvalidOperationException("The ranked server returned an empty matchmaking response.");
            }

            return ticket;
        }

        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                return string.Empty;
            }

            string trimmed = baseUrl.Trim();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            if (!trimmed.EndsWith("/", StringComparison.Ordinal))
            {
                trimmed += "/";
            }

            return trimmed;
        }
    }
}
