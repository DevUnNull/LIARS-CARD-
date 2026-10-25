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
        public string phase;
        public int stateVersion;
        public List<PublicPlayerStateDto> players = new List<PublicPlayerStateDto>();
        public PublicLastPlayDto lastPlay;
        public string winnerPlayerId;
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
    }

    [Serializable]
    public class GameErrorResponse
    {
        public string code;
        public string message;
        public int? currentVersion;
    }
}