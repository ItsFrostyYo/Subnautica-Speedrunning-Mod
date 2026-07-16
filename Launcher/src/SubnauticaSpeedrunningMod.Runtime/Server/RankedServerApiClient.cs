using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using LitJson;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime
{
    public sealed class ModServerApiClient
    {
        private const SecurityProtocolType Tls10 = (SecurityProtocolType)192;
        private const SecurityProtocolType Tls11 = (SecurityProtocolType)768;
        private const SecurityProtocolType Tls12 = (SecurityProtocolType)3072;

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

        public ModMatchStateDto HostPrivateRace(string playerId, string displayName, string mode)
        {
            EnsureTls();
            EnsureConfigured();

            ModPrivateRaceHostRequestDto request = new ModPrivateRaceHostRequestDto
            {
                hostPlayerId = playerId ?? string.Empty,
                hostDisplayName = displayName ?? string.Empty,
                mode = mode ?? string.Empty
            };

            string responseJson = SendJsonRequest("/api/private-races/host", "POST", JsonUtility.ToJson(request));
            return ParseMatchState(responseJson);
        }

        public ModMatchStateDto GetPrivateRaceRoom(string roomCode)
        {
            EnsureTls();
            EnsureConfigured();
            string responseJson = SendJsonRequest("/api/private-races/" + Uri.EscapeDataString(roomCode ?? string.Empty), "GET", null);
            return ParseMatchState(responseJson);
        }

        public ModMatchStateDto JoinPrivateRace(string roomCode, ModPrivateRaceJoinRequestDto request)
        {
            EnsureTls();
            EnsureConfigured();
            string responseJson = SendJsonRequest(
                "/api/private-races/" + Uri.EscapeDataString(roomCode ?? string.Empty) + "/join",
                "POST",
                JsonUtility.ToJson(request));
            return ParseMatchState(responseJson);
        }

        public ModMatchStateDto StartPrivateRace(string roomCode, ModPrivateRaceStartRequestDto request)
        {
            EnsureTls();
            EnsureConfigured();
            string responseJson = SendJsonRequest(
                "/api/private-races/" + Uri.EscapeDataString(roomCode ?? string.Empty) + "/start",
                "POST",
                JsonUtility.ToJson(request));
            return ParseMatchState(responseJson);
        }

        public void LeavePrivateRace(string roomCode, string playerId)
        {
            EnsureTls();
            EnsureConfigured();

            ModPrivateRaceLeaveRequestDto request = new ModPrivateRaceLeaveRequestDto
            {
                playerId = playerId ?? string.Empty
            };

            SendJsonRequest(
                "/api/private-races/" + Uri.EscapeDataString(roomCode ?? string.Empty) + "/leave",
                "POST",
                JsonUtility.ToJson(request));
        }

        public ModMatchStateDto GetMatchState(string matchId)
        {
            EnsureTls();
            EnsureConfigured();
            string responseJson = SendJsonRequest("/api/matches/" + Uri.EscapeDataString(matchId ?? string.Empty), "GET", null);
            return ParseMatchState(responseJson);
        }

        public ModMatchStateDto SetMatchReady(string matchId, string playerId, bool isReady)
        {
            EnsureTls();
            EnsureConfigured();

            ModMatchReadyRequestDto request = new ModMatchReadyRequestDto
            {
                playerId = playerId ?? string.Empty,
                isReady = isReady
            };

            string responseJson = SendJsonRequest(
                "/api/matches/" + Uri.EscapeDataString(matchId ?? string.Empty) + "/ready",
                "POST",
                JsonUtility.ToJson(request));
            return ParseMatchState(responseJson);
        }

        public ModMatchStateDto RecordMatchSplit(string matchId, ModMatchSplitRequestDto request)
        {
            EnsureTls();
            EnsureConfigured();
            string responseJson = SendJsonRequest(
                "/api/matches/" + Uri.EscapeDataString(matchId ?? string.Empty) + "/split",
                "POST",
                JsonUtility.ToJson(request));
            return ParseMatchState(responseJson);
        }

        public ModMatchStateDto FinishMatch(string matchId, ModMatchFinishRequestDto request)
        {
            EnsureTls();
            EnsureConfigured();
            string responseJson = SendJsonRequest(
                "/api/matches/" + Uri.EscapeDataString(matchId ?? string.Empty) + "/finish",
                "POST",
                JsonUtility.ToJson(request));
            return ParseMatchState(responseJson);
        }

        public ModMatchStateDto ForfeitMatch(string matchId, ModMatchForfeitRequestDto request)
        {
            EnsureTls();
            EnsureConfigured();
            string responseJson = SendJsonRequest(
                "/api/matches/" + Uri.EscapeDataString(matchId ?? string.Empty) + "/forfeit",
                "POST",
                JsonUtility.ToJson(request));
            return ParseMatchState(responseJson);
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
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.CheckCertificateRevocationList = false;
                ServicePointManager.SecurityProtocol = Tls12;
            }
            catch
            {
                try
                {
                    ServicePointManager.SecurityProtocol = Tls12 | Tls11 | Tls10;
                }
                catch
                {
                }
            }

            try
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate
                {
                    return true;
                };
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
            request.Timeout = 75000;
            request.ReadWriteTimeout = 75000;
            request.KeepAlive = false;
            request.Proxy = null;
            request.ProtocolVersion = HttpVersion.Version11;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.AllowWriteStreamBuffering = true;
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
            JsonData root = ParseJson(json);
            ModMatchmakingTicketDto ticket = new ModMatchmakingTicketDto
            {
                ticketId = ReadString(root, "ticketId"),
                status = ReadString(root, "status"),
                mode = ReadString(root, "mode"),
                playerId = ReadString(root, "playerId"),
                displayName = ReadString(root, "displayName"),
                message = ReadString(root, "message"),
                createdUtc = ReadString(root, "createdUtc"),
                updatedUtc = ReadString(root, "updatedUtc"),
                match = ParseAssignment(ReadChild(root, "match"))
            };

            if (ticket == null)
            {
                throw new InvalidOperationException("The ranked server returned an empty matchmaking response.");
            }

            return ticket;
        }

        private static ModMatchStateDto ParseMatchState(string json)
        {
            JsonData root = ParseJson(json);
            ModMatchStateDto state = new ModMatchStateDto
            {
                matchId = ReadString(root, "matchId"),
                roomCode = ReadString(root, "roomCode"),
                status = ReadString(root, "status"),
                mode = ReadString(root, "mode"),
                seedMultiplier = ReadDouble(root, "seedMultiplier"),
                spawnProfile = ReadString(root, "spawnProfile"),
                seedId = ReadString(root, "seedId"),
                seedValue = ReadString(root, "seedValue"),
                spawnX = (float)ReadDouble(root, "spawnX"),
                spawnZ = (float)ReadDouble(root, "spawnZ"),
                createdUtc = ReadString(root, "createdUtc"),
                startedUtc = ReadString(root, "startedUtc"),
                completedUtc = ReadString(root, "completedUtc"),
                players = ParsePlayers(ReadChild(root, "players")),
                splits = ParseSplits(ReadChild(root, "splits"))
            };

            if (state == null)
            {
                throw new InvalidOperationException("The ranked server returned an empty match response.");
            }

            if (state.players == null)
            {
                state.players = new ModMatchPlayerDto[0];
            }

            if (state.splits == null)
            {
                state.splits = new ModMatchSplitDto[0];
            }

            return state;
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

        private static JsonData ParseJson(string json)
        {
            JsonData root = JsonMapper.ToObject(string.IsNullOrEmpty(json) ? "{}" : json);
            if (root == null || !root.IsObject)
            {
                throw new InvalidOperationException("The ranked server returned an invalid JSON payload.");
            }

            return root;
        }

        private static ModMatchmakingAssignmentDto ParseAssignment(JsonData data)
        {
            if (data == null || !data.IsObject)
            {
                return null;
            }

            return new ModMatchmakingAssignmentDto
            {
                matchId = ReadString(data, "matchId"),
                mode = ReadString(data, "mode"),
                seedId = ReadString(data, "seedId"),
                seedValue = ReadString(data, "seedValue"),
                seedMultiplier = ReadDouble(data, "seedMultiplier"),
                spawnProfile = ReadString(data, "spawnProfile"),
                playerId = ReadString(data, "playerId"),
                opponentPlayerId = ReadString(data, "opponentPlayerId"),
                opponentDisplayName = ReadString(data, "opponentDisplayName")
            };
        }

        private static ModMatchPlayerDto[] ParsePlayers(JsonData data)
        {
            if (data == null || !data.IsArray || data.Count <= 0)
            {
                return new ModMatchPlayerDto[0];
            }

            ModMatchPlayerDto[] players = new ModMatchPlayerDto[data.Count];
            for (int index = 0; index < data.Count; index++)
            {
                JsonData item = data[index];
                players[index] = new ModMatchPlayerDto
                {
                    playerId = ReadString(item, "playerId"),
                    displayName = ReadString(item, "displayName"),
                    ready = ReadBool(item, "ready"),
                    connected = ReadBool(item, "connected"),
                    result = ReadString(item, "result"),
                    pointsDelta = ReadInt(item, "pointsDelta"),
                    splitCount = ReadInt(item, "splitCount"),
                    currentSplitKey = ReadString(item, "currentSplitKey"),
                    currentSplitDisplayName = ReadString(item, "currentSplitDisplayName"),
                    currentSplitElapsedMilliseconds = ReadLong(item, "currentSplitElapsedMilliseconds"),
                    currentLoadlessElapsedMilliseconds = ReadLong(item, "currentLoadlessElapsedMilliseconds"),
                    finalElapsedMilliseconds = ReadLong(item, "finalElapsedMilliseconds"),
                    finalLoadlessElapsedMilliseconds = ReadLong(item, "finalLoadlessElapsedMilliseconds")
                };
            }

            return players;
        }

        private static ModMatchSplitDto[] ParseSplits(JsonData data)
        {
            if (data == null || !data.IsArray || data.Count <= 0)
            {
                return new ModMatchSplitDto[0];
            }

            ModMatchSplitDto[] splits = new ModMatchSplitDto[data.Count];
            for (int index = 0; index < data.Count; index++)
            {
                JsonData item = data[index];
                splits[index] = new ModMatchSplitDto
                {
                    sequenceNumber = ReadInt(item, "sequenceNumber"),
                    playerId = ReadString(item, "playerId"),
                    splitKey = ReadString(item, "splitKey"),
                    splitDisplayName = ReadString(item, "splitDisplayName"),
                    elapsedMilliseconds = ReadLong(item, "elapsedMilliseconds"),
                    loadlessElapsedMilliseconds = ReadLong(item, "loadlessElapsedMilliseconds"),
                    recordedUtc = ReadString(item, "recordedUtc")
                };
            }

            return splits;
        }

        private static string ReadString(JsonData data, string propertyName)
        {
            JsonData child = ReadChild(data, propertyName);
            if (child == null)
            {
                return string.Empty;
            }

            if (child.IsString)
            {
                return (string)child;
            }

            if (child.IsBoolean)
            {
                return ((bool)child).ToString();
            }

            if (child.IsInt)
            {
                return ((int)child).ToString(CultureInfo.InvariantCulture);
            }

            if (child.IsLong)
            {
                return ((long)child).ToString(CultureInfo.InvariantCulture);
            }

            if (child.IsDouble)
            {
                return ((double)child).ToString(CultureInfo.InvariantCulture);
            }

            return child.ToJson();
        }

        private static bool ReadBool(JsonData data, string propertyName)
        {
            JsonData child = ReadChild(data, propertyName);
            if (child == null)
            {
                return false;
            }

            if (child.IsBoolean)
            {
                return (bool)child;
            }

            if (child.IsString)
            {
                bool boolValue;
                if (bool.TryParse((string)child, out boolValue))
                {
                    return boolValue;
                }

                long longValue;
                if (long.TryParse((string)child, NumberStyles.Integer, CultureInfo.InvariantCulture, out longValue))
                {
                    return longValue != 0L;
                }
            }

            if (child.IsInt)
            {
                return (int)child != 0;
            }

            if (child.IsLong)
            {
                return (long)child != 0L;
            }

            if (child.IsDouble)
            {
                return Math.Abs((double)child) > double.Epsilon;
            }

            return false;
        }

        private static int ReadInt(JsonData data, string propertyName)
        {
            JsonData child = ReadChild(data, propertyName);
            if (child == null)
            {
                return 0;
            }

            if (child.IsInt)
            {
                return (int)child;
            }

            if (child.IsLong)
            {
                return Convert.ToInt32((long)child);
            }

            if (child.IsDouble)
            {
                return Convert.ToInt32((double)child);
            }

            if (child.IsBoolean)
            {
                return (bool)child ? 1 : 0;
            }

            if (child.IsString)
            {
                int intValue;
                if (int.TryParse((string)child, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
                {
                    return intValue;
                }

                long longValue;
                if (long.TryParse((string)child, NumberStyles.Integer, CultureInfo.InvariantCulture, out longValue))
                {
                    return Convert.ToInt32(longValue);
                }

                double doubleValue;
                if (double.TryParse((string)child, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out doubleValue))
                {
                    return Convert.ToInt32(doubleValue);
                }
            }

            return 0;
        }

        private static long ReadLong(JsonData data, string propertyName)
        {
            JsonData child = ReadChild(data, propertyName);
            if (child == null)
            {
                return 0L;
            }

            if (child.IsLong)
            {
                return (long)child;
            }

            if (child.IsInt)
            {
                return (int)child;
            }

            if (child.IsDouble)
            {
                return Convert.ToInt64((double)child);
            }

            if (child.IsBoolean)
            {
                return (bool)child ? 1L : 0L;
            }

            if (child.IsString)
            {
                long longValue;
                if (long.TryParse((string)child, NumberStyles.Integer, CultureInfo.InvariantCulture, out longValue))
                {
                    return longValue;
                }

                double doubleValue;
                if (double.TryParse((string)child, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out doubleValue))
                {
                    return Convert.ToInt64(doubleValue);
                }
            }

            return 0L;
        }

        private static double ReadDouble(JsonData data, string propertyName)
        {
            JsonData child = ReadChild(data, propertyName);
            if (child == null)
            {
                return 0d;
            }

            if (child.IsDouble)
            {
                return (double)child;
            }

            if (child.IsInt)
            {
                return (int)child;
            }

            if (child.IsLong)
            {
                return (long)child;
            }

            if (child.IsBoolean)
            {
                return (bool)child ? 1d : 0d;
            }

            if (child.IsString)
            {
                double doubleValue;
                if (double.TryParse((string)child, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out doubleValue))
                {
                    return doubleValue;
                }
            }

            return 0d;
        }

        private static JsonData ReadChild(JsonData data, string propertyName)
        {
            if (data == null || !data.IsObject || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            try
            {
                foreach (string key in data.Keys)
                {
                    if (string.Equals(key, propertyName, StringComparison.Ordinal))
                    {
                        return data[propertyName];
                    }
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
