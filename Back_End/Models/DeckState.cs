using System.Security.Cryptography;

namespace Back_End.Models
{
    public class DeckState
    {
        public List<Card> Cards { get; set; } = new();

        public static List<Card> CreateStandardDeck()
        {
            var deck = new List<Card>(20);

            // 6 Ace
            for (int i = 1; i <= 6; i++)
            {
                deck.Add(new Card($"card_A_{Guid.NewGuid():N}", Card.RankAce));
            }

            // 6 King
            for (int i = 1; i <= 6; i++)
            {
                deck.Add(new Card($"card_K_{Guid.NewGuid():N}", Card.RankKing));
            }

            // 6 Queen
            for (int i = 1; i <= 6; i++)
            {
                deck.Add(new Card($"card_Q_{Guid.NewGuid():N}", Card.RankQueen));
            }

            // 2 Joker
            for (int i = 1; i <= 2; i++)
            {
                deck.Add(new Card($"card_Joker_{Guid.NewGuid():N}", Card.RankJoker));
            }

            return deck;
        }

        public static void Shuffle(List<Card> cards)
        {
            // Fisher-Yates shuffle algorithm with cryptographic random
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }
    }
}
