namespace Back_End.DTOs
{
    public class CardDto
    {
        public string CardInstanceId { get; set; } = string.Empty;
        public string Rank { get; set; } = string.Empty;
    }

    public class PlayerHandResponse
    {
        public string MatchId { get; set; } = string.Empty;
        public List<CardDto> Cards { get; set; } = new();
    }
}
