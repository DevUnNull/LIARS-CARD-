
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Back_End.Models
{
    public class CharacterSelection
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string CharacterId { get; set; } = string.Empty;

        public DateTime SelectedAt { get; set; } = DateTime.UtcNow;
    }
}
