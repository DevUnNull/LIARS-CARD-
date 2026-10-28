using System.Text.Json.Serialization;

namespace Back_End.DTOs
{
    public class PublicPlayerStateDto
    {
        public string PlayerId { get; set; } = string.Empty;
        public bool IsAlive { get; set; }
        public int RemainingCardCount { get; set; }
    }

    public class PublicLastPlayDto
    {
        public string PlayId { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public int CardCount { get; set; }
    }

    public class PublicMatchStateResponse
    {
        public string MatchId { get; set; } = string.Empty;
        public int RoundNumber { get; set; }
        public string TableRank { get; set; } = string.Empty;
        public string? CurrentTurnPlayerId { get; set; }
        public string Phase { get; set; } = string.Empty;
        public int StateVersion { get; set; }
        public List<PublicPlayerStateDto> Players { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PublicLastPlayDto? LastPlay { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WinnerPlayerId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PenalizedPlayerId { get; set; }
    }
}
