using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Back_End.Models
{
    public class MatchResult
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string MatchId { get; set; } = string.Empty;
        public string? WinnerId { get; set; }
        public List<string> ParticipantIds { get; set; } = new();
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public int TotalRounds { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

