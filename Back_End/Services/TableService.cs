using Back_End.DTOs;
using Back_End.Models;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Back_End.Services
{
    public class TableService
    {
        private readonly List<GameTable> tables = new();
        private readonly object tableLock = new();

        public TableService()
        {
            SetUpTable();
        }

        private void SetUpTable()
        {
            for (int tableId = 1; tableId <= 3; tableId++)
            {
                GameTable table = new GameTable
                {
                    Id = tableId,
                    Status = TableStatus.Waiting
                };

                for (int seatIndex = 0; seatIndex < 4; seatIndex++)
                {
                    table.Seats.Add(new TableSeat
                    {
                        SeatIndex = seatIndex,
                        PlayerId = null,
                        IsReady = false
                    });
                }

                tables.Add(table);
            }
        }

        public List<TableResponse> GetTables()
        {
            lock (tableLock)
            {
                List<TableResponse> result = new();

                for (int i = 0; i < tables.Count; i++)
                {
                    TableResponse tableResponse = new TableResponse
                    {
                        TableId = tables[i].Id,
                        Status = tables[i].Status,
                        Capacity = tables[i].Seats.Count,
                        PlayerCount = tables[i].Seats.Count(seat => seat.PlayerId != null)
                    };

                    for (int j = 0; j < tables[i].Seats.Count; j++)
                    {
                        tableResponse.Seats.Add(new SeatResponse
                        {
                            SeatIndex = tables[i].Seats[j].SeatIndex,
                            PlayerId = tables[i].Seats[j].PlayerId,
                            IsReady = tables[i].Seats[j].IsReady,
                        });
                    }

                    result.Add(tableResponse);
                }

                return result;
            }
        }

        public TableResponse GetTableByID(int id)
        {
            lock (tableLock)
            {
                if (id <= 0 || id > tables.Count)
                {
                    return null;
                }

                GameTable table = tables[id - 1];

                TableResponse response = new TableResponse
                {
                    TableId = table.Id,
                    Status = table.Status,
                    Capacity = table.Seats.Count,
                    PlayerCount = table.Seats.Count(seat => seat.PlayerId != null)
                };

                for (int i = 0; i < table.Seats.Count; i++)
                {
                    response.Seats.Add(new SeatResponse
                    {
                        SeatIndex = table.Seats[i].SeatIndex,
                        PlayerId = table.Seats[i].PlayerId,
                        IsReady = table.Seats[i].IsReady
                    });
                }

                return response;
            }
        }

        public string? SitDown(int tableId, int seatIndex, string playerId)
        {
            lock (tableLock)
            {
                GameTable? table = tables.Find(t => t.Id == tableId);

                if (table == null)
                {
                    return "TABLE_NOT_FOUND";
                }

                if (table.Status != TableStatus.Waiting)
                {
                    return "TABLE_NOT_WAITING";
                }

                TableSeat? seat = table.Seats.Find(s => s.SeatIndex == seatIndex);

                if (seat == null)
                {
                    return "INVALID_SEAT";
                }

                if (seat.PlayerId != null)
                {
                    return "SEAT_TAKEN";
                }

                for (int i = 0; i < tables.Count; i++)
                {
                    for (int j = 0; j < tables[i].Seats.Count; j++)
                    {
                        if (tables[i].Seats[j].PlayerId == playerId)
                        {
                            return "PLAYER_ALREADY_SEATED";
                        }
                    }
                }

                seat.PlayerId = playerId;
                seat.IsReady = false;
                return null;
            }
        }

        public string? LeaveSeat(string playerId)
        {
            lock (tableLock)
            {
                for (int i = 0; i < tables.Count; i++)
                {
                    TableSeat? seat = tables[i].Seats.Find(
                        s => s.PlayerId == playerId
                    );

                    if (seat == null)
                    {
                        continue;
                    }

                    if (tables[i].Status != TableStatus.Waiting)
                    {
                        return "TABLE_NOT_WAITING";
                    }

                    seat.PlayerId = null;
                    seat.IsReady = false;

                    return null;
                }

                return "PLAYER_NOT_SEATED";
            }
        }

        public string? SetReady(string playerId, bool isReady)
        {
            lock (tableLock)
            {
                for (int i = 0; i < tables.Count; i++)
                {
                    TableSeat? seat = tables[i].Seats.Find(
                        s => s.PlayerId == playerId
                    );

                    if (seat == null)
                    {
                        continue;
                    }

                    if (tables[i].Status != TableStatus.Waiting)
                    {
                        return "TABLE_NOT_WAITING";
                    }

                    seat.IsReady = isReady;

                    return null;
                }

                return "PLAYER_NOT_SEATED";
            }
        }
    }
}
