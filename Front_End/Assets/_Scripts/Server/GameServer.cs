using System;
using System.Collections.Generic;
using System.Linq;
using LiarsCard.Network;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiarsCard.Server
{
    /// <summary>Tham số chung cho mọi request (JSON). Mỗi route chỉ đọc các trường nó cần.</summary>
    [Serializable]
    public class RouteArgs
    {
        public int tableId;
        public int seatIndex;
        public bool ready;
        public string matchId;
        public int expectedVersion;
        public List<string> cardInstanceIds = new List<string>();
        public string targetPlayId;
    }

    /// <summary>
    /// "Server" của game chạy trong RAM của Master Client (thay cho Back_End ASP.NET + LiteNetLib).
    /// Nhận request (route + JSON) và gói tin từ người chơi, trả JSON / gói tin y như server cũ
    /// để UI phía client giữ nguyên. Không biết gì về Photon: gửi đi qua delegate <see cref="send"/>.
    /// </summary>
    public class GameServer
    {
        public const int TableCount = 3, SeatsPerTable = 4, CardsPerPlayer = 5;
        private static readonly string[] TableRanks = { Card.RankAce, Card.RankKing, Card.RankQueen };

        private readonly List<GameTable> tables = new List<GameTable>();
        private readonly Dictionary<string, GameMatch> matches = new Dictionary<string, GameMatch>();
        private readonly Dictionary<string, string> names = new Dictionary<string, string>();
        private readonly HashSet<string> online = new HashSet<string>();
        private readonly Action<string, byte[]> send;          // (playerId, gói tin)
        private readonly Func<string, Vector3?> positionOf;     // vị trí người chơi để phát âm thanh 3D
        private readonly Jukebox jukebox = new Jukebox();

        public GameServer(Action<string, byte[]> send, Func<string, Vector3?> positionOf = null)
        {
            this.send = send;
            this.positionOf = positionOf ?? (_ => null);
            for (int t = 1; t <= TableCount; t++)
            {
                var table = new GameTable { Id = t };
                for (int s = 0; s < SeatsPerTable; s++) table.Seats.Add(new TableSeat { SeatIndex = s });
                tables.Add(table);
            }
        }

        // ───────────────────────── Kết nối ─────────────────────────

        public void PlayerJoined(string playerId, string displayName)
        {
            online.Add(playerId);
            names[playerId] = string.IsNullOrWhiteSpace(displayName) ? playerId : displayName.Trim();
            send(playerId, jukebox.StatePacket());   // vào sau vẫn nghe chung đúng đoạn nhạc
        }

        /// <summary>Người chơi thoát game: rời ghế, đang trong trận thì bị xử thua để bàn chơi tiếp.</summary>
        public void PlayerLeft(string playerId)
        {
            online.Remove(playerId);
            var match = FindActiveMatch(playerId);
            if (match != null) Forfeit(match, playerId);
            LeaveSeat(playerId);
        }

        // ───────────────────────── Request ─────────────────────────

        /// <summary>Xử lý một request. Thành công trả (true, JSON); lỗi trả (false, "[CODE] message").</summary>
        public (bool ok, string body) Handle(string playerId, string route, string json)
        {
            try
            {
                var a = string.IsNullOrEmpty(json) ? new RouteArgs() : JsonUtility.FromJson<RouteArgs>(json);
                object result = route switch
                {
                    "tables/get" => GetTable(a.tableId),
                    "tables/sit" => SitDown(a.tableId, a.seatIndex, playerId),
                    "tables/leave" => LeaveSeat(playerId),
                    "tables/ready" => SetTableReady(playerId, a.ready),
                    "tables/start" => StartMatch(a.tableId, playerId),
                    "matches/state" => PublicState(GetMatch(a.matchId)),
                    "matches/hand" => Hand(GetMatch(a.matchId), playerId),
                    "matches/ready" => SetMatchReady(GetMatch(a.matchId), playerId),
                    "deck/play" => PlayCards(GetMatch(a.matchId), playerId, a),
                    "deck/challenge" => Challenge(GetMatch(a.matchId), playerId, a),
                    "deck/trust" => Trust(GetMatch(a.matchId), playerId, a),
                    "deck/pull" => PullTrigger(GetMatch(a.matchId), playerId),
                    _ => throw new GameRuleException("UNKNOWN_ROUTE", route)
                };
                return (true, result is string s ? s : JsonUtility.ToJson(result));
            }
            catch (GameRuleException e)
            {
                return (false, $"[{e.Code}] {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return (false, "[SERVER_ERROR] " + e.Message);
            }
        }

        // ───────────────────────── Gói tin (thay LiteNetLib) ─────────────────────────

        /// <summary>Gói tin client gửi lên: 4 = tư thế súng, 5 = hành động bài trên tay, 7 = Jukebox.</summary>
        public void HandlePacket(string playerId, byte[] data)
        {
            if (data == null || data.Length < 1) return;
            var r = new NetDataReader(data);
            try
            {
                switch (r.GetByte())
                {
                    case 4:
                        {
                            if (r.AvailableBytes != 5) return;
                            byte stage = r.GetByte();
                            float pitch = r.GetFloat();
                            var m = FindActiveMatch(playerId);
                            if (stage < 1 || stage > 2 || !float.IsFinite(pitch) || m == null ||
                                m.Phase != RoundPhase.PenaltyResolution || m.PenaltyResolved || m.PenalizedPlayerId != playerId) return;
                            var w = Packet(13); w.Put(playerId); w.Put(stage); w.Put(Mathf.Clamp(pitch, -90f, 90f));
                            SendToMatch(m, w, except: playerId);
                            break;
                        }
                    case 5:
                        {
                            if (r.AvailableBytes != 2) return;
                            byte action = r.GetByte();
                            byte count = r.GetByte();
                            if (action > 2 || (action == 2 && (count < 1 || count > 3))) return;
                            var m = FindActiveMatch(playerId);
                            if (m == null) return;
                            var w = Packet(14); w.Put(playerId); w.Put(action); w.Put(action == 2 ? count : (byte)0);
                            SendToMatch(m, w, except: playerId);
                            break;
                        }
                    case 7:
                        {
                            var cmd = (JukeboxCommand)r.GetByte();
                            int ended = cmd == JukeboxCommand.Ended && r.AvailableBytes == 4 ? r.GetInt() : 0;
                            if (jukebox.Handle(cmd, ended))
                            {
                                byte[] state = jukebox.StatePacket();
                                foreach (var id in online) send(id, state);
                            }
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameServer] Gói tin lỗi từ {playerId}: {e.Message}");
            }
        }

        // ───────────────────────── Bàn chơi ─────────────────────────

        private string Name(string playerId) => playerId != null && names.TryGetValue(playerId, out var n) ? n : playerId;

        private GameTable Table(int id) =>
            tables.Find(t => t.Id == id) ?? throw new GameRuleException("TABLE_NOT_FOUND", "Không tìm thấy bàn.");

        private TableResponseDto GetTable(int tableId)
        {
            var t = Table(tableId);
            var dto = new TableResponseDto
            {
                tableId = t.Id,
                status = (int)t.Status,
                hostPlayerId = t.HostPlayerId,
                currentMatchId = t.CurrentMatchId,
                capacity = t.Seats.Count,
                playerCount = t.Seats.Count(s => s.PlayerId != null)
            };
            foreach (var s in t.Seats)
                dto.seats.Add(new SeatResponseDto { seatIndex = s.SeatIndex, playerId = s.PlayerId, displayName = Name(s.PlayerId), isReady = s.IsReady });
            return dto;
        }

        private string SitDown(int tableId, int seatIndex, string playerId)
        {
            var t = Table(tableId);
            if (t.Status != TableStatus.Waiting) throw new GameRuleException("TABLE_NOT_WAITING", "Bàn đang chơi.");
            var seat = t.Seats.Find(s => s.SeatIndex == seatIndex) ?? throw new GameRuleException("INVALID_SEAT", "Ghế không hợp lệ.");
            if (seat.PlayerId != null) throw new GameRuleException("SEAT_TAKEN", "Ghế đã có người.");
            if (tables.Any(x => x.Seats.Any(s => s.PlayerId == playerId)))
                throw new GameRuleException("PLAYER_ALREADY_SEATED", "Bạn đang ngồi ở ghế khác.");

            seat.PlayerId = playerId;
            seat.IsReady = false;
            if (string.IsNullOrEmpty(t.HostPlayerId)) t.HostPlayerId = playerId;
            return "{}";
        }

        private string LeaveSeat(string playerId)
        {
            foreach (var t in tables)
            {
                var seat = t.Seats.Find(s => s.PlayerId == playerId);
                if (seat == null) continue;

                seat.PlayerId = null;
                seat.IsReady = false;
                if (t.HostPlayerId == playerId) t.HostPlayerId = t.Seats.FirstOrDefault(s => s.PlayerId != null)?.PlayerId;
                if (t.Seats.All(s => s.PlayerId == null))
                {
                    t.Status = TableStatus.Waiting;
                    t.CurrentMatchId = null;
                    t.HostPlayerId = null;
                }
                return "{}";
            }
            throw new GameRuleException("PLAYER_NOT_SEATED", "Bạn chưa ngồi bàn nào.");
        }

        private string SetTableReady(string playerId, bool ready)
        {
            foreach (var t in tables)
            {
                var seat = t.Seats.Find(s => s.PlayerId == playerId);
                if (seat == null) continue;
                if (t.Status != TableStatus.Waiting) throw new GameRuleException("TABLE_NOT_WAITING", "Bàn đang chơi.");
                seat.IsReady = ready;
                return "{}";
            }
            throw new GameRuleException("PLAYER_NOT_SEATED", "Bạn chưa ngồi bàn nào.");
        }

        private StartMatchResponseDto StartMatch(int tableId, string playerId)
        {
            var t = Table(tableId);
            if (t.Status != TableStatus.Waiting) throw new GameRuleException("TABLE_NOT_WAITING", "Bàn không ở trạng thái chờ.");
            if (t.HostPlayerId != playerId) throw new GameRuleException("NOT_TABLE_HOST", "Chỉ chủ bàn mới có quyền bắt đầu trận đấu.");
            var seated = t.Seats.Where(s => s.PlayerId != null).OrderBy(s => s.SeatIndex).ToList();
            if (seated.Count < 2) throw new GameRuleException("INSUFFICIENT_PLAYERS", "Cần tối thiểu 2 người chơi để bắt đầu trận đấu.");
            if (!seated.Where(s => s.PlayerId != playerId).All(s => s.IsReady))
                throw new GameRuleException("TABLE_NOT_READY", "Vẫn còn người chơi trong bàn chưa sẵn sàng.");

            seated.First(s => s.PlayerId == playerId).IsReady = true;
            var match = new GameMatch { MatchId = ServerRandom.Id($"m_table_{tableId}_"), TableId = tableId };
            for (int i = 0; i < seated.Count; i++)
                match.Players.Add(new MatchPlayer { PlayerId = seated[i].PlayerId, DisplayName = Name(seated[i].PlayerId), SeatIndex = i });
            matches[match.MatchId] = match;

            t.Status = TableStatus.Playing;
            t.CurrentMatchId = match.MatchId;

            var res = new StartMatchResponseDto { tableId = tableId, matchId = match.MatchId, message = "Bắt đầu trận đấu thành công." };
            res.playerIds.AddRange(match.Players.Select(p => p.PlayerId));
            return res;
        }

        private void EndTable(GameMatch match)
        {
            var t = tables.Find(x => x.CurrentMatchId == match.MatchId);
            if (t == null) return;
            t.CurrentMatchId = null;
            t.Status = TableStatus.Waiting;
            foreach (var s in t.Seats) s.IsReady = false;
        }

        // ───────────────────────── Trận đấu ─────────────────────────

        private GameMatch GetMatch(string matchId) =>
            matchId != null && matches.TryGetValue(matchId, out var m) ? m : throw GameRuleException.MatchNotFound();

        private GameMatch FindActiveMatch(string playerId) =>
            matches.Values.FirstOrDefault(m => m.Phase != RoundPhase.MatchEnded && m.Players.Any(p => p.PlayerId == playerId));

        private static MatchPlayer Member(GameMatch m, string playerId) => m.FindPlayer(playerId) ?? throw GameRuleException.NotMatchMember();

        private PublicMatchStateResponse PublicState(GameMatch m)
        {
            var res = new PublicMatchStateResponse
            {
                matchId = m.MatchId,
                roundNumber = m.RoundNumber,
                tableRank = m.TableRank,
                currentTurnPlayerId = m.CurrentTurnPlayerId,
                phase = m.Phase.ToString(),
                stateVersion = m.StateVersion,
                winnerPlayerId = m.WinnerPlayerId,
                penalizedPlayerId = m.Phase == RoundPhase.PenaltyResolution && !m.PenaltyResolved ? m.PenalizedPlayerId : null,
                lastPlay = m.LastPlay == null ? null : new PublicLastPlayDto { playId = m.LastPlay.PlayId, playerId = m.LastPlay.PlayerId, cardCount = m.LastPlay.Cards.Count }
            };
            foreach (var p in m.Players)
                res.players.Add(new PublicPlayerStateDto
                {
                    playerId = p.PlayerId,
                    displayName = p.DisplayName,
                    isAlive = p.IsAlive,
                    remainingCardCount = p.RemainingCardCount,
                    shotCount = p.Revolver.CurrentChamber + 1
                });
            return res;
        }

        // Chỉ trả bài của chính người hỏi: Master Client có thể tự xem bài người khác, client thường thì không
        private static PlayerHandResponse Hand(GameMatch m, string playerId)
        {
            var res = new PlayerHandResponse { matchId = m.MatchId };
            foreach (var c in Member(m, playerId).Hand) res.cards.Add(new CardDto { cardInstanceId = c.Id, rank = c.Rank });
            return res;
        }

        private PublicMatchStateResponse SetMatchReady(GameMatch m, string playerId)
        {
            Member(m, playerId).IsReady = true;
            var alive = m.AlivePlayers;
            if (alive.Count >= 2 && alive.All(p => p.IsReady) && (m.Phase == RoundPhase.WaitingForPlayers || m.Phase == RoundPhase.MatchEnded))
                StartRound(m);
            return PublicState(m);
        }

        private PlayCardsResponse PlayCards(GameMatch m, string playerId, RouteArgs a)
        {
            var player = Member(m, playerId);
            if (m.StateVersion != a.expectedVersion) throw GameRuleException.StaleVersion();
            if (m.Phase != RoundPhase.AwaitPlayOrChallenge && m.Phase != RoundPhase.RoundStart)
                throw GameRuleException.InvalidPhase("Trận đấu đang không ở phase cho phép đánh bài.");
            if (m.CurrentTurnPlayerId != playerId || !player.IsAlive) throw GameRuleException.NotYourTurn();
            if (IsLastPlayOutOfCards(m)) throw GameRuleException.InvalidPhase("Đối thủ vừa đánh hết bài: bạn phải Tố (X) hoặc Tin (C).");

            var ids = a.cardInstanceIds ?? new List<string>();
            if (ids.Count < 1 || ids.Count > 3 || ids.Distinct().Count() != ids.Count) throw GameRuleException.InvalidCardCount();
            var played = player.Hand.Where(c => ids.Contains(c.Id)).ToList();
            if (played.Count != ids.Count) throw GameRuleException.CardNotOwned();

            // Được phép nói dối: không kiểm tra rank lá bài với rank của bàn
            player.Hand.RemoveAll(c => ids.Contains(c.Id));
            m.LastPlay = new LastPlay { PlayId = ServerRandom.Id("play_"), PlayerId = playerId, Cards = played };

            var next = NextTurnPlayer(m, playerId);
            m.CurrentTurnPlayerId = next?.PlayerId;
            if (next != null) m.Phase = RoundPhase.AwaitPlayOrChallenge;
            m.StateVersion++;

            BroadcastAudio(m, played.Count == 1 ? "Danh_1" : played.Count == 2 ? "Danh_2" : "Dnah_3", playerId);
            return new PlayCardsResponse
            {
                accepted = true,
                playId = m.LastPlay.PlayId,
                playedCardCount = played.Count,
                remainingCardCount = player.RemainingCardCount,
                stateVersion = m.StateVersion
            };
        }

        private void ValidateChallenge(GameMatch m, string playerId, RouteArgs a)
        {
            var challenger = Member(m, playerId);
            if (m.StateVersion != a.expectedVersion) throw GameRuleException.StaleVersion();
            if (m.Phase != RoundPhase.AwaitPlayOrChallenge) throw GameRuleException.InvalidPhase("Trận đấu đang không ở phase cho phép tố cáo.");
            if (m.CurrentTurnPlayerId != playerId || !challenger.IsAlive) throw GameRuleException.NotYourTurn();
            if (m.LastPlay == null) throw GameRuleException.CannotChallenge("Chưa có lượt đánh nào để tố cáo.");
            if (m.LastPlay.Resolved) throw GameRuleException.AlreadyResolved();
            if (!string.Equals(m.LastPlay.PlayId, a.targetPlayId, StringComparison.OrdinalIgnoreCase))
                throw GameRuleException.CannotChallenge("TargetPlayId không khớp với lượt đánh hiện tại.");
        }

        private ChallengeResponse Challenge(GameMatch m, string playerId, RouteArgs a)
        {
            ValidateChallenge(m, playerId, a);
            var lp = m.LastPlay;
            bool bluff = lp.Cards.Any(c => !c.MatchesRank(m.TableRank));
            lp.Resolved = true;
            string penalized = bluff ? lp.PlayerId : playerId;

            m.Phase = RoundPhase.PenaltyResolution;
            m.PenalizedPlayerId = penalized;
            m.CurrentTurnPlayerId = penalized;
            m.PenaltyResolved = false;
            m.TrustedPlayers.Clear();
            m.StateVersion++;

            BroadcastAudio(m, "To", playerId);
            var w = Packet(11); w.Put(penalized); w.Put(playerId); w.Put(bluff);
            SendToMatch(m, w);

            return new ChallengeResponse
            {
                challengeSucceeded = bluff,
                penalizedPlayerId = penalized,
                nextPhase = "PenaltyResolution",
                awaitingTriggerPull = true,
                stateVersion = m.StateVersion
            };
        }

        /// <summary>"Tin" lượt đánh cuối của người vừa hết bài. Mọi người còn bài đều Tin thì chia vòng mới.</summary>
        private ChallengeResponse Trust(GameMatch m, string playerId, RouteArgs a)
        {
            ValidateChallenge(m, playerId, a);
            if (!IsLastPlayOutOfCards(m)) throw GameRuleException.CannotChallenge("Chỉ được Tin khi đối thủ đã đánh hết bài.");

            m.TrustedPlayers.Add(playerId);
            string owner = m.LastPlay.PlayerId;
            var pending = m.Players.Where(p => p.IsAlive && p.RemainingCardCount > 0 && p.PlayerId != owner && !m.TrustedPlayers.Contains(p.PlayerId)).ToList();

            string nextPhase;
            if (pending.Count == 0)
            {
                m.LastPlay.Resolved = true;
                StartRound(m);
                nextPhase = m.Phase == RoundPhase.MatchEnded ? "MatchEnded" : "RoundStart";
            }
            else
            {
                int cur = m.Players.FindIndex(p => p.PlayerId == playerId);
                m.CurrentTurnPlayerId = Enumerable.Range(1, m.Players.Count)
                    .Select(s => m.Players[(cur + s) % m.Players.Count]).First(p => pending.Contains(p)).PlayerId;
                m.StateVersion++;
                nextPhase = "AwaitPlayOrChallenge";
            }
            return new ChallengeResponse { penalizedPlayerId = "", nextPhase = nextPhase, stateVersion = m.StateVersion };
        }

        private PullTriggerResponse PullTrigger(GameMatch m, string playerId)
        {
            var player = Member(m, playerId);
            if (m.Phase != RoundPhase.PenaltyResolution) throw GameRuleException.InvalidPhase("Trận đấu hiện không ở trong trạng thái xử phạt bóp cò súng!");
            if (!string.Equals(m.PenalizedPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
                throw new GameRuleException("NOT_PENALIZED_PLAYER", "Chỉ người chơi thua cuộc bị phạt mới có quyền tự bóp cò súng!");

            bool hit = player.Revolver.PullTrigger();
            int shot = player.Revolver.LastShotNumber;
            if (hit)
            {
                player.IsAlive = false;
                player.Hand.Clear();
                foreach (var p in m.AlivePlayers) p.Revolver = new PlayerRevolver();   // nạp lại súng cho vòng mới
            }
            m.PenaltyResolved = true;

            var alive = m.AlivePlayers;
            string nextPhase = alive.Count <= 1 ? "MatchEnded" : hit ? "RoundStart" : "AwaitPlayOrChallenge";
            string winner = alive.Count <= 1 ? alive.FirstOrDefault()?.PlayerId : null;

            if (alive.Count <= 1) FinishMatch(m);
            else if (hit) { m.PenalizedPlayerId = null; StartRound(m); }
            else ContinueRoundAfterMiss(m, playerId);

            BroadcastAudio(m, hit ? "Gunshot" : "DryFire", playerId);
            var w = Packet(12);
            w.Put(playerId); w.Put(hit); w.Put(!hit); w.Put(hit); w.Put(nextPhase); w.Put(winner ?? ""); w.Put((byte)shot);
            SendToMatch(m, w);

            return new PullTriggerResponse
            {
                matchId = m.MatchId,
                penalizedPlayerId = playerId,
                isBulletHit = hit,
                survived = !hit,
                eliminated = hit,
                nextPhase = nextPhase,
                winnerPlayerId = winner,
                stateVersion = m.StateVersion,
                shotNumber = shot
            };
        }

        /// <summary>Người chơi thoát giữa trận: coi như bị loại, vòng đang dở được chia lại.</summary>
        private void Forfeit(GameMatch m, string playerId)
        {
            var p = m.FindPlayer(playerId);
            if (p == null || !p.IsAlive) return;
            p.IsAlive = false;
            p.Hand.Clear();
            if (m.AlivePlayers.Count <= 1) FinishMatch(m);
            else if (m.CurrentTurnPlayerId == playerId || m.PenalizedPlayerId == playerId || m.LastPlay?.PlayerId == playerId)
            {
                m.PenalizedPlayerId = null;
                StartRound(m);
            }
            else m.StateVersion++;
        }

        private void FinishMatch(GameMatch m)
        {
            m.Phase = RoundPhase.MatchEnded;
            m.WinnerPlayerId = m.AlivePlayers.FirstOrDefault()?.PlayerId;
            m.CurrentTurnPlayerId = null;
            m.PenalizedPlayerId = null;
            m.StateVersion++;
            EndTable(m);
        }

        // ───────────────────────── Luật chơi ─────────────────────────

        private static bool IsLastPlayOutOfCards(GameMatch m)
        {
            var lp = m.LastPlay;
            if (lp == null || lp.Resolved) return false;
            var owner = m.FindPlayer(lp.PlayerId);
            return owner != null && owner.IsAlive && owner.RemainingCardCount == 0;
        }

        /// <summary>Người kế tiếp theo chiều ghế còn sống và còn bài; null nếu không còn ai có bài.</summary>
        private static MatchPlayer NextTurnPlayer(GameMatch m, string currentPlayerId)
        {
            int cur = Math.Max(0, m.Players.FindIndex(p => p.PlayerId == currentPlayerId));
            for (int step = 1; step <= m.Players.Count; step++)
            {
                var c = m.Players[(cur + step) % m.Players.Count];
                if (c.IsAlive && c.RemainingCardCount > 0) return c;
            }
            return null;
        }

        /// <summary>Chia vòng mới: bộ 20 lá (6A 6K 6Q 2 Joker), mỗi người còn sống 5 lá, chọn rank bàn ngẫu nhiên.</summary>
        private void StartRound(GameMatch m)
        {
            var alive = m.AlivePlayers;
            if (alive.Count < 2) { FinishMatch(m); return; }

            var deck = new List<Card>(20);
            foreach (var (rank, n) in new[] { (Card.RankAce, 6), (Card.RankKing, 6), (Card.RankQueen, 6), (Card.RankJoker, 2) })
                for (int i = 0; i < n; i++) deck.Add(new Card(ServerRandom.Id($"card_{rank}_"), rank));
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = ServerRandom.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }

            int k = 0;
            foreach (var p in alive)
            {
                p.Hand.Clear();
                for (int i = 0; i < CardsPerPlayer; i++) p.Hand.Add(deck[k++]);
            }

            m.TableRank = TableRanks[ServerRandom.Next(TableRanks.Length)];
            m.LastPlay = null;
            m.TrustedPlayers.Clear();
            m.RoundNumber++;
            m.PenalizedPlayerId = null;

            int startSeat = (m.RoundNumber - 1) % Math.Max(1, m.Players.Count);
            m.CurrentTurnPlayerId = alive.Where(p => p.RemainingCardCount > 0)
                .OrderBy(p => (p.SeatIndex - startSeat + m.Players.Count) % m.Players.Count).FirstOrDefault()?.PlayerId;
            m.Phase = RoundPhase.AwaitPlayOrChallenge;
            m.StateVersion++;
        }

        /// <summary>Bắn trượt: giữ nguyên bài và rank, chuyển lượt. Có người đã hết bài thì chia vòng mới.</summary>
        private void ContinueRoundAfterMiss(GameMatch m, string shooterId)
        {
            m.PenalizedPlayerId = null;
            m.LastPlay = null;
            m.TrustedPlayers.Clear();
            var next = NextTurnPlayer(m, shooterId);
            if (next == null || m.AlivePlayers.Any(p => p.RemainingCardCount == 0)) { StartRound(m); return; }
            m.CurrentTurnPlayerId = next.PlayerId;
            m.Phase = RoundPhase.AwaitPlayOrChallenge;
            m.StateVersion++;
        }

        // ───────────────────────── Gửi gói tin ─────────────────────────

        private static NetDataWriter Packet(byte type)
        {
            var w = new NetDataWriter();
            w.Put(type);
            return w;
        }

        private void SendToMatch(GameMatch m, NetDataWriter w, string except = null)
        {
            byte[] data = w.CopyData();
            foreach (var p in m.Players)
                if (p.PlayerId != except && online.Contains(p.PlayerId)) send(p.PlayerId, data);
        }

        /// <summary>Gói 10: âm thanh 3D tại vị trí người chơi.</summary>
        private void BroadcastAudio(GameMatch m, string sound, string sourcePlayerId)
        {
            Vector3 pos = positionOf(sourcePlayerId) ?? new Vector3(0f, 1.2f, 0f);
            var w = Packet(10); w.Put(sound); w.Put(pos.x); w.Put(pos.y); w.Put(pos.z);
            SendToMatch(m, w);
        }

        /// <summary>
        /// Jukebox chung của quán: server giữ số thứ tự bài, đang phát hay dừng, âm lượng và lúc bắt đầu
        /// để mọi người nghe cùng một đoạn. Gói trả về: 16 (bool playing, int index, long elapsedMs, byte volume).
        /// </summary>
        private class Jukebox
        {
            private const long CommandCooldownMs = 1000, VolumeCooldownMs = 150, MinPlayBeforeEndMs = 10000;
            private const byte MaxVolume = 10;
            private bool playing;
            private int index;
            private byte volume = 7;
            private long startedAt, lastCommand = long.MinValue / 2, lastVolume = long.MinValue / 2;
            private static long Now => System.Diagnostics.Stopwatch.GetTimestamp() * 1000 / System.Diagnostics.Stopwatch.Frequency;

            public bool Handle(JukeboxCommand cmd, int endedIndex)
            {
                long now = Now;
                switch (cmd)
                {
                    case JukeboxCommand.Next:
                    case JukeboxCommand.Prev:
                    case JukeboxCommand.PlayStop:
                        if (now - lastCommand < CommandCooldownMs) return false;
                        lastCommand = now;
                        if (cmd == JukeboxCommand.PlayStop && playing) playing = false;
                        else Start(cmd == JukeboxCommand.Next ? index + 1 : cmd == JukeboxCommand.Prev ? index - 1 : index, now);
                        return true;
                    case JukeboxCommand.Ended:
                        if (!playing || endedIndex != index || now - startedAt < MinPlayBeforeEndMs) return false;
                        Start(index + 1, now);
                        return true;
                    case JukeboxCommand.VolumeUp:
                    case JukeboxCommand.VolumeDown:
                        if (now - lastVolume < VolumeCooldownMs) return false;
                        byte next = cmd == JukeboxCommand.VolumeUp ? (byte)Math.Min(volume + 1, MaxVolume) : (byte)Math.Max(volume - 1, 0);
                        if (next == volume) return false;
                        lastVolume = now;
                        volume = next;
                        return true;
                }
                return false;
            }

            private void Start(int newIndex, long now) { playing = true; index = newIndex; startedAt = now; }

            public byte[] StatePacket()
            {
                var w = Packet(16);
                w.Put(playing); w.Put(index); w.Put(playing ? Now - startedAt : 0L); w.Put(volume);
                return w.CopyData();
            }
        }
    }
}
