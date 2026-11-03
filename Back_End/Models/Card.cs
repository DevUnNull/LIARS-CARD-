namespace Back_End.Models
{
    public class Card
    {
        public string CardInstanceId { get; set; } = string.Empty;
        public string Rank { get; set; } = string.Empty;

        public const string RankAce = "A";
        public const string RankKing = "K";
        public const string RankQueen = "Q";
        public const string RankJoker = "Joker";

        public Card() { }

        public Card(string cardInstanceId, string rank)
        {
            CardInstanceId = cardInstanceId;
            Rank = rank;
        }

        public bool IsJoker => Rank == RankJoker;

        public bool MatchesRank(string targetRank)
        {
            return IsJoker || string.Equals(Rank, targetRank, StringComparison.OrdinalIgnoreCase);
        }
    }
}
