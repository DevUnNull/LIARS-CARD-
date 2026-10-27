using MongoDB.Bson.Serialization.Attributes;

namespace Back_End.Models
{
    [BsonIgnoreExtraElements]
    public sealed class MatchResultDocument
    {
        [BsonElement("matchId")]
        public string MatchId { get; set; } = string.Empty;

        [BsonElement("winnerPlayerId")]
        public string? WinnerPlayerId { get; set; }

        [BsonElement("players")]
        public List<MatchResultPlayerDocument> Players { get; set; } = new();

        [BsonElement("matchCreatedAtUtc")]
        public DateTime MatchCreatedAtUtc { get; set; }

        [BsonElement("endedAtUtc")]
        public DateTime EndedAtUtc { get; set; }

        [BsonElement("finalStateVersion")]
        public int FinalStateVersion { get; set; }
    }

    public sealed class MatchResultPlayerDocument
    {
        [BsonElement("playerId")]
        public string PlayerId { get; set; } = string.Empty;

        [BsonElement("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [BsonElement("seatIndex")]
        public int SeatIndex { get; set; }

        [BsonElement("isAliveAtEnd")]
        public bool IsAliveAtEnd { get; set; }

        [BsonElement("isWinner")]
        public bool IsWinner { get; set; }

        [BsonElement("remainingCardCountAtEnd")]
        public int RemainingCardCountAtEnd { get; set; }
    }
}
