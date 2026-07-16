using System;

namespace SubnauticaSpeedrunningMod.Runtime
{
    [Serializable]
    public sealed class ModMatchmakingEnqueueRequestDto
    {
        public string playerId;
        public string displayName;
        public string mode;
    }

    [Serializable]
    public sealed class ModMatchmakingCancelRequestDto
    {
        public string playerId;
    }

    [Serializable]
    public sealed class ModMatchmakingAssignmentDto
    {
        public string matchId;
        public string mode;
        public string seedId;
        public string seedValue;
        public double seedMultiplier;
        public string spawnProfile;
        public string playerId;
        public string opponentPlayerId;
        public string opponentDisplayName;
    }

    [Serializable]
    public sealed class ModMatchmakingTicketDto
    {
        public string ticketId;
        public string status;
        public string mode;
        public string playerId;
        public string displayName;
        public string message;
        public string createdUtc;
        public string updatedUtc;
        public ModMatchmakingAssignmentDto match;
    }

    [Serializable]
    public sealed class ModPrivateRaceHostRequestDto
    {
        public string hostPlayerId;
        public string hostDisplayName;
        public string mode;
    }

    [Serializable]
    public sealed class ModPrivateRaceJoinRequestDto
    {
        public string playerId;
        public string displayName;
    }

    [Serializable]
    public sealed class ModPrivateRaceLeaveRequestDto
    {
        public string playerId;
    }

    [Serializable]
    public sealed class ModPrivateRaceStartRequestDto
    {
        public string playerId;
        public string mode;
        public double seedMultiplier;
        public string spawnProfile;
        public string seedId;
        public string seedValue;
        public float spawnX;
        public float spawnZ;
    }

    [Serializable]
    public sealed class ModMatchReadyRequestDto
    {
        public string playerId;
        public bool isReady;
    }

    [Serializable]
    public sealed class ModMatchSplitRequestDto
    {
        public string playerId;
        public string splitKey;
        public string splitDisplayName;
        public long elapsedMilliseconds;
        public long loadlessElapsedMilliseconds;
        public int sequenceNumber;
    }

    [Serializable]
    public sealed class ModMatchFinishRequestDto
    {
        public string playerId;
        public long elapsedMilliseconds;
        public long loadlessElapsedMilliseconds;
        public string notes;
    }

    [Serializable]
    public sealed class ModMatchForfeitRequestDto
    {
        public string playerId;
        public string reason;
    }

    [Serializable]
    public sealed class ModMatchPlayerDto
    {
        public string playerId;
        public string displayName;
        public bool ready;
        public bool connected;
        public string result;
        public int pointsDelta;
        public int splitCount;
        public string currentSplitKey;
        public string currentSplitDisplayName;
        public long currentSplitElapsedMilliseconds;
        public long currentLoadlessElapsedMilliseconds;
        public long finalElapsedMilliseconds;
        public long finalLoadlessElapsedMilliseconds;
    }

    [Serializable]
    public sealed class ModMatchSplitDto
    {
        public int sequenceNumber;
        public string playerId;
        public string splitKey;
        public string splitDisplayName;
        public long elapsedMilliseconds;
        public long loadlessElapsedMilliseconds;
        public string recordedUtc;
    }

    [Serializable]
    public sealed class ModMatchStateDto
    {
        public string matchId;
        public string roomCode;
        public string status;
        public string mode;
        public double seedMultiplier;
        public string spawnProfile;
        public string seedId;
        public string seedValue;
        public float spawnX;
        public float spawnZ;
        public string createdUtc;
        public string startedUtc;
        public string completedUtc;
        public ModMatchPlayerDto[] players;
        public ModMatchSplitDto[] splits;
    }
}
