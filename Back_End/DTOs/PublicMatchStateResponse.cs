using System.Text.Json.Serialization;

namespace Back_End.DTOs
{
    public class PublicPlayerStateDto
    {
        public string PlayerId { get; set; } = string.Empty;
        public bool IsAlive { get; set; }
        public int RemainingCardCount { get; set; }
        public bool IsConnected { get; set; }
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

        /// <summary>
        /// Trạng thái trận đấu ("WaitingForPlayers", "Playing", "Finished").
        /// </summary>
        public string Status => Phase == "MatchEnded" 
            ? "Finished" 
            : (Phase == "WaitingForPlayers" ? "WaitingForPlayers" : "Playing");

        public int StateVersion { get; set; }
        public List<PublicPlayerStateDto> Players { get; set; } = new();

        /// <summary>
        /// Số lượng người chơi còn sống tại thời điểm hiện tại.
        /// </summary>
        public int AlivePlayerCount => Players?.Count(p => p.IsAlive) ?? 0;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PublicLastPlayDto? LastPlay { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WinnerPlayerId { get; set; }

        /// <summary>
        /// Alias của WinnerPlayerId phục vụ chuẩn hợp đồng Blueprint.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WinnerId => WinnerPlayerId;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PenalizedPlayerId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CurrentPenaltyEventId { get; set; }
    }
}
