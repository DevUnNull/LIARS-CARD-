using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Back_End.Models
{
    public class RefreshToken
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string TokenHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public DateTime? RevokedAt { get; set; }

        [BsonIgnore]
        public bool IsActive =>
            RevokedAt == null && DateTime.UtcNow < ExpiresAt;
    }
}
