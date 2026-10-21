using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Back_End.Models
{
    public class RevokedAccessToken
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Jti { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

    }
}