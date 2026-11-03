using System.Collections.Generic;

namespace Back_End.DTOs
{
    public class StartMatchResponse
    {
        public string MatchId { get; set; } = string.Empty;
        public int TableId { get; set; }
        public string Status { get; set; } = "WaitingForPlayers";
        public int PlayerCount { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> PlayerIds { get; set; } = new();
    }
}
