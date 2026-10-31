using System.Security.Cryptography;

namespace Back_End.Services
{
    public class ConnectionTicketService
    {
        public const int LIFE_TIME = 30;

        private readonly Dictionary<string, TicketInfo> _tickets = new();
        private readonly object _lockObj = new();

        public string Create(string playerId)
        {
            return IssueTicket(playerId, null, null);
        }

        public string IssueTicket(string playerId, string? matchId, int? tableId)
        {
            lock (_lockObj)
            {
                DateTime now = DateTime.UtcNow;

                // Dọn dẹp ticket cũ của cùng người chơi hoặc ticket đã hết hạn
                var oldTickets = _tickets
                    .Where(entry =>
                        entry.Value.ExpiresAt <= now ||
                        (entry.Value.PlayerId == playerId && entry.Value.MatchId == matchId))
                    .Select(entry => entry.Key)
                    .ToArray();

                foreach (string oldTicket in oldTickets)
                {
                    _tickets.Remove(oldTicket);
                }

                string ticket = Convert.ToHexString(
                    RandomNumberGenerator.GetBytes(32));

                _tickets.Add(
                    ticket,
                    new TicketInfo(
                        playerId,
                        now.AddSeconds(LIFE_TIME),
                        matchId,
                        tableId));

                return ticket;
            }
        }

        public string? GetActiveTicket(string playerId, int? tableId)
        {
            lock (_lockObj)
            {
                DateTime now = DateTime.UtcNow;
                var entry = _tickets.FirstOrDefault(t => 
                    t.Value.PlayerId == playerId && 
                    t.Value.TableId == tableId && 
                    t.Value.ExpiresAt > now);

                return entry.Key;
            }
        }

        public string? Consume(string ticket)
        {
            lock (_lockObj)
            {
                if (!_tickets.Remove(ticket, out var info))
                {
                    return null;
                }

                if (info.ExpiresAt <= DateTime.UtcNow)
                {
                    return null;
                }

                return info.PlayerId;
            }
        }

        public (bool isValid, string? error) ValidateAndConsume(string ticket, string playerId, string matchId)
        {
            lock (_lockObj)
            {
                if (!_tickets.TryGetValue(ticket, out var info))
                {
                    return (false, "TICKET_NOT_FOUND_OR_EXPIRED");
                }

                if (info.ExpiresAt <= DateTime.UtcNow)
                {
                    _tickets.Remove(ticket);
                    return (false, "TICKET_EXPIRED");
                }

                if (!string.Equals(info.PlayerId, playerId, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "TICKET_OWNER_MISMATCH");
                }

                if (!string.IsNullOrEmpty(info.MatchId) && !string.Equals(info.MatchId, matchId, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "TICKET_MATCH_MISMATCH");
                }

                // Consume ticket (dùng một lần)
                _tickets.Remove(ticket);
                return (true, null);
            }
        }
    }

    public record TicketInfo(string PlayerId, DateTime ExpiresAt, string? MatchId = null, int? TableId = null);
}
