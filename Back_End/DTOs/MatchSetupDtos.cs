namespace Back_End.DTOs
{
    public class CreateMatchRequest
    {
        public string? MatchId { get; set; }
        public List<string> PlayerIds { get; set; } = new();
    }

    public class ReadyMatchRequest
    {
        public bool? IsReady { get; set; } = true;
    }
}
