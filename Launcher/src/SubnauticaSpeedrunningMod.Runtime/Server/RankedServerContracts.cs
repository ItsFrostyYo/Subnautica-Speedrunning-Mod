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
}
