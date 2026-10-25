using System.Collections.Generic;

namespace Back_End.DTOs
{
    public class StartMatchResponse
    {
        public int TableId { get; set; }
        public string MatchId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<string> PlayerIds { get; set; } = new();
    }
}
