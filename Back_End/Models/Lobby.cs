namespace Back_End.Models
{
    public class Lobby
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name { get; set; } = string.Empty;

        public string HostId { get; set; } = string.Empty;

        public int MaxPlayers { get; set; } = 4;

        public List<string> PlayerIds { get; set; } = new();

        public bool IsInGame { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
