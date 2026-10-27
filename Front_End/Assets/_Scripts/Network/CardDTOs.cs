using System;
using System.Collections.Generic;

namespace LiarsCard.Network
{
    [Serializable]
    public class CardDto
    {
        public string cardInstanceId;
        public string rank;
    }

    [Serializable]
    public class PlayerHandResponse
    {
        public string matchId;
        public List<CardDto> cards = new List<CardDto>();
    }

    [Serializable]
    public class PublicPlayerStateDto
    {
        public string playerId;
        public bool isAlive;
        public int remainingCardCount;
    }

    [Serializable]
    public class PublicLastPlayDto
    {
        public string playId;
        public string playerId;
        public int cardCount;
    }

    [Serializable]
    public class PublicMatchStateResponse
    {
        public string matchId;
        public int roundNumber;
        public string tableRank;
        public string currentTurnPlayerId;
        public string currentPlayerId;
        public string phase;
        public int stateVersion;
        public List<PublicPlayerStateDto> players = new List<PublicPlayerStateDto>();
        public PublicLastPlayDto lastPlay;
        public string winnerPlayerId;
    }

    [Serializable]
    public class MatchResultPlayerResponse
    {
        public string playerId;
        public string displayName;
        public int seatIndex;
        public bool isAliveAtEnd;
        public bool isWinner;
        public int remainingCardCountAtEnd;
    }

    [Serializable]
    public class MatchResultResponse
    {
        public string matchId;
        public string winnerPlayerId;
        public List<MatchResultPlayerResponse> players = new List<MatchResultPlayerResponse>();
        public string matchCreatedAtUtc;
        public string endedAtUtc;
        public int finalStateVersion;
    }

    [Serializable]
    public class PlayerEliminatedEvent
    {
        public string matchId;
        public string playerId;
        public int stateVersion;
    }

    [Serializable]
    public class MatchEndedEvent
    {
        public string matchId;
        public int stateVersion;
        public string winnerPlayerId;
    }

    [Serializable]
    public class RoundStartedEvent
    {
        public string matchId;
        public string penaltyEventId;
        public int roundNumber;
        public int stateVersion;
        public string phase;
        public string tableRank;
        public string currentTurnPlayerId;
    }

    [Serializable]
    public class PlayCardsRequest
    {
        public string actionId;
        public int expectedVersion;
        public List<string> cardInstanceIds = new List<string>();
    }

    [Serializable]
    public class PlayCardsResponse
    {
        public bool accepted;
        public string playId;
        public int playedCardCount;
        public int remainingCardCount;
        public int stateVersion;
    }

    [Serializable]
    public class ChallengeRequest
    {
        public string actionId;
        public int expectedVersion;
        public string targetPlayId;
    }

    [Serializable]
    public class ChallengeResponse
    {
        public bool challengeSucceeded;
        public string penalizedPlayerId;
        public bool survived;
        public bool eliminated;
        public string nextPhase;
        public int stateVersion;
        public string penaltyEventId;
    }

    [Serializable]
    public class PenaltyAnimationStarted
    {
        public string matchId;
        public string penaltyEventId;
        public int stateVersion;
        public string deadlineUtc;
        public bool challengeSucceeded;
        public string penalizedPlayerId;
        public bool survived;
        public bool eliminated;
    }

    [Serializable]
    public class PenaltyAnimationEnded
    {
        public string matchId;
        public string penaltyEventId;
        public string status;
        public int acknowledgedCount;
        public int requiredAckCount;
    }

    [Serializable]
    public class PenaltyAnimationSnapshot
    {
        public PenaltyAnimationStarted @event;
        public string status;
        public bool acknowledgedByCaller;
        public int acknowledgedCount;
        public int requiredAckCount;
    }

    [Serializable]
    public class PenaltyAnimationAckResponse
    {
        public bool acknowledged;
        public string status;
        public int acknowledgedCount;
        public int requiredAckCount;
    }

    [Serializable]
    public class PenaltyAnimationAckRequest
    {
        public string matchId;
        public string penaltyEventId;
    }

    [Serializable]
    public class GameErrorResponse
    {
        public string code;
        public string message;
        public int? currentVersion;
    }

    [Serializable]
    public class SeatResponseDto
    {
        public int seatIndex;
        public string playerId;
        public bool isReady;
    }

    [Serializable]
    public class TableResponseDto
    {
        public int tableId;
        public int status;
        public string hostPlayerId;
        public string currentMatchId;
        public int capacity;
        public int playerCount;
        public List<SeatResponseDto> seats = new List<SeatResponseDto>();
    }

    [Serializable]
    public class StartMatchResponseDto
    {
        public int tableId;
        public string matchId;
        public string message;
        public List<string> playerIds = new List<string>();
    }
}