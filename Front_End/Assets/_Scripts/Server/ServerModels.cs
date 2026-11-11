using System;
using System.Collections.Generic;

namespace LiarsCard.Server
{
    // Trạng thái trận chỉ nằm trong RAM của Master Client (bản chuyển từ Back_End/Models).

    public enum RoundPhase { WaitingForPlayers, RoundStart, AwaitPlayOrChallenge, PenaltyResolution, MatchEnded }

    public enum TableStatus { Waiting = 0, Playing = 1, Summary = 2 }

    public class Card
    {
        public const string RankAce = "A", RankKing = "K", RankQueen = "Q", RankJoker = "Joker";
        public string Id;
        public string Rank;

        public Card(string id, string rank) { Id = id; Rank = rank; }

        public bool MatchesRank(string tableRank) => Rank == RankJoker || string.Equals(Rank, tableRank, StringComparison.OrdinalIgnoreCase);
    }

    public class PlayerRevolver
    {
        public const int MaxChambers = 6;
        public int BulletChamber = ServerRandom.Next(MaxChambers);
        public int CurrentChamber;
        /// <summary>Số phát của lần bóp cò gần nhất, đếm từ 1 (hiện dạng X/6).</summary>
        public int LastShotNumber;

        /// <summary>Trả true nếu trúng đạn.</summary>
        public bool PullTrigger()
        {
            LastShotNumber = CurrentChamber + 1;
            if (CurrentChamber == BulletChamber) return true;
            CurrentChamber = (CurrentChamber + 1) % MaxChambers;
            return false;
        }
    }

    public class MatchPlayer
    {
        public string PlayerId;
        public string DisplayName;
        public int SeatIndex;
        public bool IsAlive = true;
        public bool IsReady;
        public List<Card> Hand = new List<Card>();
        public PlayerRevolver Revolver = new PlayerRevolver();
        public int RemainingCardCount => Hand.Count;
    }

    public class LastPlay
    {
        public string PlayId;
        public string PlayerId;
        public List<Card> Cards = new List<Card>();
        public bool Resolved;
    }

    public class GameMatch
    {
        public string MatchId;
        public int TableId;
        public int RoundNumber;
        public string TableRank = "";
        public string CurrentTurnPlayerId;
        public RoundPhase Phase = RoundPhase.WaitingForPlayers;
        public int StateVersion = 1;
        public List<MatchPlayer> Players = new List<MatchPlayer>();
        public LastPlay LastPlay;
        public string WinnerPlayerId;
        public string PenalizedPlayerId;
        public bool PenaltyResolved;
        /// <summary>Những người đã "Tin" lượt đánh cuối (khi người đánh đã hết bài).</summary>
        public HashSet<string> TrustedPlayers = new HashSet<string>();

        public MatchPlayer FindPlayer(string playerId) => Players.Find(p => p.PlayerId == playerId);
        public List<MatchPlayer> AlivePlayers => Players.FindAll(p => p.IsAlive);
    }

    public class TableSeat
    {
        public int SeatIndex;
        public string PlayerId;
        public bool IsReady;
    }

    public class GameTable
    {
        public int Id;
        public TableStatus Status = TableStatus.Waiting;
        public string HostPlayerId;
        public string CurrentMatchId;
        public List<TableSeat> Seats = new List<TableSeat>();
    }

    public class GameRuleException : Exception
    {
        public readonly string Code;
        public GameRuleException(string code, string message) : base(message) { Code = code; }

        public static GameRuleException NotYourTurn() => new GameRuleException("NOT_YOUR_TURN", "Không phải lượt của bạn.");
        public static GameRuleException InvalidCardCount() => new GameRuleException("INVALID_CARD_COUNT", "Mỗi lượt chỉ được đánh từ 1 đến 3 lá bài.");
        public static GameRuleException CardNotOwned() => new GameRuleException("CARD_NOT_OWNED", "Bạn không sở hữu những lá bài này.");
        public static GameRuleException CannotChallenge(string message) => new GameRuleException("CANNOT_CHALLENGE", message);
        public static GameRuleException AlreadyResolved() => new GameRuleException("ALREADY_RESOLVED", "Lượt đánh này đã được giải quyết.");
        public static GameRuleException StaleVersion() => new GameRuleException("STALE_VERSION", "Match state has changed.");
        public static GameRuleException MatchNotFound() => new GameRuleException("MATCH_NOT_FOUND", "Không tìm thấy trận đấu.");
        public static GameRuleException NotMatchMember() => new GameRuleException("NOT_MATCH_MEMBER", "Người chơi không thuộc trận đấu này.");
        public static GameRuleException InvalidPhase(string message) => new GameRuleException("INVALID_PHASE", message);
    }

    public static class ServerRandom
    {
        // Xáo bài bằng RNG mật mã như server cũ để không đoán được bài
        private static readonly System.Security.Cryptography.RandomNumberGenerator rng = System.Security.Cryptography.RandomNumberGenerator.Create();

        public static int Next(int maxExclusive)
        {
            var b = new byte[4];
            rng.GetBytes(b);
            return (int)(BitConverter.ToUInt32(b, 0) % (uint)maxExclusive);
        }

        public static string Id(string prefix) => prefix + Guid.NewGuid().ToString("N").Substring(0, 12);
    }
}
