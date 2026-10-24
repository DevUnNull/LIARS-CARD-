using System.Security.Cryptography;

namespace Back_End.Services
{
    public class ConnectionTicketService
    {
        public const int LIFE_TIME = 30;

        private Dictionary<string, TicketInfo> tickets = new();
        private object lockObj = new();

        public string Create(string playerId)
        {
            lock (lockObj)
            {
                DateTime now = DateTime.UtcNow;

                var oldTickets = tickets
                    .Where(entry =>
                        entry.Value.ExpiresAt <= now ||
                        entry.Value.PlayerId == playerId)
                    .Select(entry => entry.Key)
                    .ToArray();

                foreach (string oldTicket in oldTickets)
                {
                    tickets.Remove(oldTicket);
                }

                string ticket = Convert.ToHexString(
                    RandomNumberGenerator.GetBytes(32));

                tickets.Add(
                    ticket,
                    new TicketInfo(
                        playerId,
                        now.AddSeconds(LIFE_TIME)));

                return ticket;
            }
        }

        public string? Consume(string ticket)
        {
            lock (lockObj)
            {
                if (!tickets.Remove(ticket, out var info))
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
    }

    public record TicketInfo(string PlayerId, DateTime ExpiresAt);
}
