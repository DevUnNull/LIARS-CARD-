using MongoDB.Bson.Serialization.Attributes;

namespace Back_End.Models
{
    public class Character
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string AvatarKey { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
