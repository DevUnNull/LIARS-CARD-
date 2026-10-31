using System.Security.Cryptography;
using Back_End.Exceptions;
using Back_End.Models;
using Back_End.Rules;

namespace Back_End.Services
{
    public class DeckRoundService
    {
        private readonly TurnStateMachine _turnStateMachine;
        private static readonly string[] ValidTableRanks = { Card.RankAce, Card.RankKing, Card.RankQueen };

        public DeckRoundService(TurnStateMachine turnStateMachine)
        {
            _turnStateMachine = turnStateMachine;
        }

        /// <summary>
        /// Creates a standard 20-card deck and shuffles it using Fisher-Yates.
        /// </summary>
        public List<Card> CreateAndShuffleDeck()
        {
            var deck = DeckState.CreateStandardDeck();
            DeckState.Shuffle(deck);
            return deck;
        }

        /// <summary>
        /// Starts a new round in the match:
        /// 1. Identifies alive players
        /// 2. Creates and shuffles 20-card deck
        /// 3. Deals 5 cards to each alive player
        /// 4. Picks table rank (A, K, or Q)
        /// 5. Determines starting player
        /// 6. Resets previous turn, challenge, and penalty data
        /// 7. Sets phase to AwaitPlayOrChallenge
        /// 8. Increments RoundNumber and StateVersion
        /// </summary>
        public void StartRound(GameMatch match, int? preferredStartingSeat = null, string? fixedTableRank = null)
        {
            var alivePlayers = match.AlivePlayers;
            if (alivePlayers.Count < 2 && match.Players.Count >= 2)
            {
                // Only 1 or 0 players left alive, match has ended
                match.Phase = RoundPhase.MatchEnded;
                match.WinnerPlayerId = alivePlayers.FirstOrDefault()?.PlayerId;
                match.StateVersion++;
                return;
            }

            const int cardsPerPlayer = 5;
            int totalCardsNeeded = alivePlayers.Count * cardsPerPlayer;
            if (totalCardsNeeded > 20)
            {
                throw new InvalidOperationException($"Số người chơi còn sống ({alivePlayers.Count}) vượt quá sức chứa bộ bài 20 lá.");
            }

            // Create and shuffle deck
            var deck = CreateAndShuffleDeck();

            // Deal 5 cards to each alive player
            int cardIndex = 0;
            foreach (var player in alivePlayers)
            {
                player.Hand.Clear();
                for (int i = 0; i < cardsPerPlayer; i++)
                {
                    player.Hand.Add(deck[cardIndex++]);
                }
            }

            // Select table rank: A, K, or Q
            if (!string.IsNullOrEmpty(fixedTableRank) && ValidTableRanks.Contains(fixedTableRank))
            {
                match.TableRank = fixedTableRank;
            }
            else
            {
                int rankIdx = RandomNumberGenerator.GetInt32(ValidTableRanks.Length);
                match.TableRank = ValidTableRanks[rankIdx];
            }

            // Reset round data
            match.LastPlay = null;
            match.RoundNumber++;
            match.PenalizedPlayerId = null;

            // Determine starting player
            int startingSeat = preferredStartingSeat ?? (match.RoundNumber - 1) % Math.Max(1, match.Players.Count);
            var initialPlayer = _turnStateMachine.GetInitialTurnPlayer(match, startingSeat);
            match.CurrentTurnPlayerId = initialPlayer?.PlayerId;

            // Set phase
            match.Phase = RoundPhase.AwaitPlayOrChallenge;

            // Increment state version
            match.StateVersion++;
        }
    }
}
