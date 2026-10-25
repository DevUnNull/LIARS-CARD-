using Back_End.DTOs;
using Back_End.Exceptions;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;
using Back_End.Rules;
using Back_End.Services;
using Xunit;

namespace Back_End.Tests
{
    public class LiarsCardTests
    {
        private (DeckActionService actionService, ChallengeService challengeService, MatchService matchService, MatchQueryService queryService, InMemoryMatchStore store, DeckRoundService roundService, PenaltyService penaltyService) CreateTestSystem()
        {
            var store = new InMemoryMatchStore();
            var ruleEngine = new DeckRuleEngine();
            var turnStateMachine = new TurnStateMachine();
            var roundService = new DeckRoundService(turnStateMachine);
            var penaltyService = new PenaltyService(roundService);
            var actionService = new DeckActionService(store, ruleEngine, turnStateMachine);
            var challengeService = new ChallengeService(store, ruleEngine, penaltyService);
            var matchService = new MatchService(store, roundService);
            var queryService = new MatchQueryService(store);

            return (actionService, challengeService, matchService, queryService, store, roundService, penaltyService);
        }

        private GameMatch CreateActiveMatchWithPlayers(MatchService matchService, InMemoryMatchStore store, int playerCount = 2, string fixedRank = "K")
        {
            var playerIds = Enumerable.Range(1, playerCount).Select(i => $"player_{i:D2}").ToList();
            var match = matchService.CreateMatch("match_test", playerIds);

            // Mark all ready to start round
            foreach (var pid in playerIds)
            {
                var player = match.FindPlayer(pid)!;
                player.IsReady = true;
            }

            var turnStateMachine = new TurnStateMachine();
            var roundService = new DeckRoundService(turnStateMachine);
            roundService.StartRound(match, preferredStartingSeat: 0, fixedTableRank: fixedRank);
            store.SaveMatch(match);

            return match;
        }

        // 1. Bộ bài tạo ra đúng 20 lá.
        [Fact]
        public void Test01_DeckHasExactly20Cards()
        {
            var deck = DeckState.CreateStandardDeck();
            Assert.Equal(20, deck.Count);

            int aceCount = deck.Count(c => c.Rank == Card.RankAce);
            int kingCount = deck.Count(c => c.Rank == Card.RankKing);
            int queenCount = deck.Count(c => c.Rank == Card.RankQueen);
            int jokerCount = deck.Count(c => c.Rank == Card.RankJoker);

            Assert.Equal(6, aceCount);
            Assert.Equal(6, kingCount);
            Assert.Equal(6, queenCount);
            Assert.Equal(2, jokerCount);
        }

        // 2. Mỗi lá có ID duy nhất.
        [Fact]
        public void Test02_EachCardHasUniqueId()
        {
            var deck = DeckState.CreateStandardDeck();
            var distinctIds = deck.Select(c => c.CardInstanceId).Distinct().ToList();
            Assert.Equal(20, distinctIds.Count);
        }

        // 3. Không chia trùng bài.
        [Fact]
        public void Test03_NoDuplicateCardsDealt()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 4);

            var allDealtCards = match.Players.SelectMany(p => p.Hand).Select(c => c.CardInstanceId).ToList();
            Assert.Equal(20, allDealtCards.Count);
            Assert.Equal(20, allDealtCards.Distinct().Count());
        }

        // 4. Không chia quá số bài hiện có.
        [Fact]
        public void Test04_CannotDealMoreThanAvailableCards()
        {
            var sys = CreateTestSystem();
            // 5 players * 5 cards = 25 > 20
            var playerIds = Enumerable.Range(1, 5).Select(i => $"player_{i:D2}").ToList();
            var match = sys.matchService.CreateMatch("match_large", playerIds);

            Assert.Throws<InvalidOperationException>(() =>
            {
                sys.roundService.StartRound(match);
            });
        }

        // 5. Người chơi nhận đúng số lá (5 lá cho người còn sống).
        [Fact]
        public void Test05_LivingPlayersReceiveExactly5Cards()
        {
            var sys = CreateTestSystem();
            var match = sys.matchService.CreateMatch("match_5cards", new List<string> { "p1", "p2", "p3" });
            match.Players[2].IsAlive = false; // p3 is dead

            sys.roundService.StartRound(match);

            Assert.Equal(5, match.Players[0].Hand.Count);
            Assert.Equal(5, match.Players[1].Hand.Count);
            Assert.Empty(match.Players[2].Hand);
        }

        // 6. Đánh một lá hợp lệ.
        [Fact]
        public async Task Test06_PlayOneCardValid()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            string currentTurnPlayerId = match.CurrentTurnPlayerId!;
            var player = match.FindPlayer(currentTurnPlayerId)!;
            var cardToPlay = player.Hand.First();

            var req = new PlayCardsRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = new List<string> { cardToPlay.CardInstanceId }
            };

            var res = await sys.actionService.PlayCardsAsync(match.MatchId, currentTurnPlayerId, req);

            Assert.True(res.Accepted);
            Assert.Equal(1, res.PlayedCardCount);
            Assert.Equal(4, res.RemainingCardCount);
            Assert.NotNull(match.LastPlay);
            Assert.Equal(1, match.LastPlay!.CardCount);
        }

        // 7. Đánh ba lá hợp lệ.
        [Fact]
        public async Task Test07_PlayThreeCardsValid()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            string currentTurnPlayerId = match.CurrentTurnPlayerId!;
            var player = match.FindPlayer(currentTurnPlayerId)!;
            var cardsToPlay = player.Hand.Take(3).Select(c => c.CardInstanceId).ToList();

            var req = new PlayCardsRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = cardsToPlay
            };

            var res = await sys.actionService.PlayCardsAsync(match.MatchId, currentTurnPlayerId, req);

            Assert.True(res.Accepted);
            Assert.Equal(3, res.PlayedCardCount);
            Assert.Equal(2, res.RemainingCardCount);
            Assert.Equal(3, match.LastPlay!.CardCount);
        }

        // 8. Đánh bốn lá bị từ chối.
        [Fact]
        public async Task Test08_PlayFourCardsRejected()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            string currentTurnPlayerId = match.CurrentTurnPlayerId!;
            var player = match.FindPlayer(currentTurnPlayerId)!;
            var cardsToPlay = player.Hand.Take(4).Select(c => c.CardInstanceId).ToList();

            var req = new PlayCardsRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = cardsToPlay
            };

            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                sys.actionService.PlayCardsAsync(match.MatchId, currentTurnPlayerId, req));

            Assert.Equal("INVALID_CARD_COUNT", ex.Code);
        }

        // 9. Đánh lá không sở hữu bị từ chối.
        [Fact]
        public async Task Test09_PlayUnownedCardRejected()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            string currentTurnPlayerId = match.CurrentTurnPlayerId!;
            var req = new PlayCardsRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = new List<string> { "fake_card_id" }
            };

            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                sys.actionService.PlayCardsAsync(match.MatchId, currentTurnPlayerId, req));

            Assert.Equal("CARD_NOT_OWNED", ex.Code);
        }

        // 10. Đánh sai lượt bị từ chối.
        [Fact]
        public async Task Test10_PlayOutOfTurnRejected()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            string wrongPlayerId = match.Players.First(p => p.PlayerId != match.CurrentTurnPlayerId).PlayerId;
            var wrongPlayer = match.FindPlayer(wrongPlayerId)!;

            var req = new PlayCardsRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = new List<string> { wrongPlayer.Hand.First().CardInstanceId }
            };

            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                sys.actionService.PlayCardsAsync(match.MatchId, wrongPlayerId, req));

            Assert.Equal("NOT_YOUR_TURN", ex.Code);
        }

        // 11. Đánh bài bluff được chấp nhận.
        [Fact]
        public async Task Test11_BluffPlayAccepted()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2, fixedRank: "K");

            string currentTurnPlayerId = match.CurrentTurnPlayerId!;
            var player = match.FindPlayer(currentTurnPlayerId)!;

            // Give player Queen intentionally to test bluff
            var bluffCard = new Card("card_bluff_Q", Card.RankQueen);
            player.Hand[0] = bluffCard;

            var req = new PlayCardsRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = new List<string> { bluffCard.CardInstanceId }
            };

            var res = await sys.actionService.PlayCardsAsync(match.MatchId, currentTurnPlayerId, req);

            Assert.True(res.Accepted);
            Assert.Equal(1, res.PlayedCardCount);
        }

        // 12. Tố cáo đúng (Người đánh bài nói dối -> người đánh bài bị phạt).
        [Fact]
        public async Task Test12_ChallengeSuccessfulWhenLiar()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2, fixedRank: "K");

            string p1Id = match.CurrentTurnPlayerId!;
            var p1 = match.FindPlayer(p1Id)!;
            string p2Id = match.Players.First(p => p.PlayerId != p1Id).PlayerId;
            var p2 = match.FindPlayer(p2Id)!;

            // p1 plays a Queen when table rank is King (Bluff)
            var bluffCard = new Card("card_bluff", Card.RankQueen);
            p1.Hand[0] = bluffCard;

            // Make sure p1 revolver won't kill them immediately in this test so round can continue
            p1.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0);

            var playRes = await sys.actionService.PlayCardsAsync(match.MatchId, p1Id, new PlayCardsRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = new List<string> { bluffCard.CardInstanceId }
            });

            // Turn is now p2
            var challengeReq = new ChallengeRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                TargetPlayId = playRes.PlayId
            };

            var challengeRes = await sys.challengeService.ChallengeAsync(match.MatchId, p2Id, challengeReq);

            Assert.True(challengeRes.ChallengeSucceeded);
            Assert.Equal(p1Id, challengeRes.PenalizedPlayerId);
        }

        // 13. Tố cáo sai (Người đánh bài nói thật -> người tố cáo bị phạt).
        [Fact]
        public async Task Test13_ChallengeFailedWhenHonest()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2, fixedRank: "K");

            string p1Id = match.CurrentTurnPlayerId!;
            var p1 = match.FindPlayer(p1Id)!;
            string p2Id = match.Players.First(p => p.PlayerId != p1Id).PlayerId;
            var p2 = match.FindPlayer(p2Id)!;

            // p1 plays a King (Honest)
            var honestCard = new Card("card_honest", Card.RankKing);
            p1.Hand[0] = honestCard;

            // Make sure p2 revolver survives
            p2.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0);

            var playRes = await sys.actionService.PlayCardsAsync(match.MatchId, p1Id, new PlayCardsRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = new List<string> { honestCard.CardInstanceId }
            });

            var challengeReq = new ChallengeRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                TargetPlayId = playRes.PlayId
            };

            var challengeRes = await sys.challengeService.ChallengeAsync(match.MatchId, p2Id, challengeReq);

            Assert.False(challengeRes.ChallengeSucceeded);
            Assert.Equal(p2Id, challengeRes.PenalizedPlayerId);
        }

        // 14. Không thể tố cáo lại lượt đã xử lý.
        [Fact]
        public async Task Test14_CannotChallengeAlreadyResolvedPlay()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2, fixedRank: "K");

            string p1Id = match.CurrentTurnPlayerId!;
            string p2Id = match.Players.First(p => p.PlayerId != p1Id).PlayerId;
            var p1 = match.FindPlayer(p1Id)!;
            p1.Hand[0] = new Card("c1", Card.RankKing);

            var playRes = await sys.actionService.PlayCardsAsync(match.MatchId, p1Id, new PlayCardsRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = new List<string> { "c1" }
            });

            // Mark play resolved
            match.LastPlay!.Resolved = true;
            match.Phase = RoundPhase.AwaitPlayOrChallenge;
            match.CurrentTurnPlayerId = p2Id;
            sys.store.SaveMatch(match);

            var challengeReq = new ChallengeRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                TargetPlayId = playRes.PlayId
            };

            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                sys.challengeService.ChallengeAsync(match.MatchId, p2Id, challengeReq));

            Assert.Equal("ALREADY_RESOLVED", ex.Code);
        }

        // 15. Không thể tố cáo khi đang xử phạt.
        [Fact]
        public async Task Test15_CannotChallengeDuringPenalty()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            match.Phase = RoundPhase.PenaltyResolution;
            sys.store.SaveMatch(match);

            var challengeReq = new ChallengeRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                TargetPlayId = "some_play"
            };

            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                sys.challengeService.ChallengeAsync(match.MatchId, match.Players[0].PlayerId, challengeReq));

            Assert.Equal("INVALID_PHASE", ex.Code);
        }

        // 16. Người hết bài được bỏ qua đúng luật.
        [Fact]
        public void Test16_PlayerOutOfCardsIsSkipped()
        {
            var turnStateMachine = new TurnStateMachine();
            var match = new GameMatch
            {
                MatchId = "m_skip",
                Players = new List<MatchPlayer>
                {
                    new() { PlayerId = "p1", SeatIndex = 0, IsAlive = true, Hand = new List<Card>() }, // 0 cards
                    new() { PlayerId = "p2", SeatIndex = 1, IsAlive = true, Hand = new List<Card> { new("c1", "A") } }, // 1 card
                    new() { PlayerId = "p3", SeatIndex = 2, IsAlive = true, Hand = new List<Card> { new("c2", "A") } }  // 1 card
                }
            };

            // Current turn p3 plays -> next turn should skip p1 (0 cards) and go straight to p2
            var nextPlayer = turnStateMachine.GetNextTurnPlayer(match, "p3");
            Assert.NotNull(nextPlayer);
            Assert.Equal("p2", nextPlayer!.PlayerId);
        }

        // 17. Người bị loại không được đánh.
        [Fact]
        public async Task Test17_EliminatedPlayerCannotPlay()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            string currentPid = match.CurrentTurnPlayerId!;
            var player = match.FindPlayer(currentPid)!;
            player.IsAlive = false;
            sys.store.SaveMatch(match);

            var req = new PlayCardsRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = new List<string> { player.Hand.First().CardInstanceId }
            };

            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                sys.actionService.PlayCardsAsync(match.MatchId, currentPid, req));

            Assert.Equal("NOT_YOUR_TURN", ex.Code);
        }

        // 18. Request trùng actionId không làm thay đổi state lần hai (Idempotency).
        [Fact]
        public async Task Test18_DuplicateActionIdDoesNotMutateTwice()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            string currentPid = match.CurrentTurnPlayerId!;
            var player = match.FindPlayer(currentPid)!;
            var cardId = player.Hand.First().CardInstanceId;
            var actionId = Guid.NewGuid();

            var req = new PlayCardsRequest
            {
                ActionId = actionId,
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = new List<string> { cardId }
            };

            var res1 = await sys.actionService.PlayCardsAsync(match.MatchId, currentPid, req);
            int versionAfterFirst = match.StateVersion;
            int handAfterFirst = player.RemainingCardCount;

            // Resend same actionId with same payload
            var res2 = await sys.actionService.PlayCardsAsync(match.MatchId, currentPid, req);

            Assert.Equal(res1.PlayId, res2.PlayId);
            Assert.Equal(res1.StateVersion, res2.StateVersion);
            Assert.Equal(versionAfterFirst, match.StateVersion);
            Assert.Equal(handAfterFirst, player.RemainingCardCount);
        }

        // 19. Hai request cùng expectedVersion không cùng thành công (STALE_VERSION).
        [Fact]
        public async Task Test19_ConcurrentMismatchedVersionFails()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            int staleVersion = match.StateVersion - 1; // Wrong version
            var player = match.FindPlayer(match.CurrentTurnPlayerId!)!;

            var req = new PlayCardsRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = staleVersion,
                CardInstanceIds = new List<string> { player.Hand.First().CardInstanceId }
            };

            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                sys.actionService.PlayCardsAsync(match.MatchId, player.PlayerId, req));

            Assert.Equal("STALE_VERSION", ex.Code);
        }

        // 20. Không truy cập được hand của người khác.
        [Fact]
        public void Test20_CannotAccessOtherPlayerHand()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            // Accessing hand of non-member or outsider
            var ex = Assert.Throws<GameRuleException>(() =>
                sys.queryService.GetPlayerHand(match.MatchId, "outsider_player"));

            Assert.Equal("NOT_MATCH_MEMBER", ex.Code);
        }

        // 21. Kết thúc match khi còn một người sống.
        [Fact]
        public void Test21_MatchEndsWhenOneSurvivorRemains()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            var p1 = match.Players[0];
            var p2 = match.Players[1];

            // Set p1 revolver so it hits bullet on pull
            p1.Revolver = new PlayerRevolver(bulletChamber: 0, currentChamber: 0);

            var res = sys.penaltyService.ApplyPenalty(match, p1.PlayerId);

            Assert.True(res.Eliminated);
            Assert.False(res.Survived);
            Assert.Equal("MatchEnded", res.NextPhase);
            Assert.Equal(RoundPhase.MatchEnded, match.Phase);
            Assert.Equal(p2.PlayerId, match.WinnerPlayerId);
        }

        // 22. Vòng mới reset chính xác dữ liệu vòng cũ.
        [Fact]
        public void Test22_NewRoundResetsOldRoundDataCorrectly()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            match.LastPlay = new LastPlay { PlayId = "old_play", CardCount = 2, PlayerId = "p1" };
            int oldRound = match.RoundNumber;
            int oldVersion = match.StateVersion;

            sys.roundService.StartRound(match);

            Assert.Equal(oldRound + 1, match.RoundNumber);
            Assert.True(match.StateVersion > oldVersion);
            Assert.Null(match.LastPlay);
            Assert.Equal(5, match.Players[0].Hand.Count);
            Assert.Equal(5, match.Players[1].Hand.Count);
            Assert.Contains(match.TableRank, new[] { "A", "K", "Q" });
            Assert.Equal(RoundPhase.AwaitPlayOrChallenge, match.Phase);
        }

        // 23. Retry challenge không xử phạt hai lần.
        [Fact]
        public async Task Test23_RetryChallengeDoesNotPenalizeTwice()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2, fixedRank: "K");

            string p1Id = match.CurrentTurnPlayerId!;
            var p1 = match.FindPlayer(p1Id)!;
            string p2Id = match.Players.First(p => p.PlayerId != p1Id).PlayerId;
            var p2 = match.FindPlayer(p2Id)!;

            p1.Hand[0] = new Card("c1", Card.RankKing);
            p2.Revolver = new PlayerRevolver(bulletChamber: 5, currentChamber: 0);

            var playRes = await sys.actionService.PlayCardsAsync(match.MatchId, p1Id, new PlayCardsRequest
            {
                ActionId = Guid.NewGuid(),
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = new List<string> { "c1" }
            });

            var challengeActionId = Guid.NewGuid();
            var challengeReq = new ChallengeRequest
            {
                ActionId = challengeActionId,
                ExpectedVersion = match.StateVersion,
                TargetPlayId = playRes.PlayId
            };

            var challengeRes1 = await sys.challengeService.ChallengeAsync(match.MatchId, p2Id, challengeReq);
            int pullsCountAfterFirst = p2.Revolver.PullsCount;
            int versionAfterFirst = match.StateVersion;

            // Retry same challenge request
            var challengeRes2 = await sys.challengeService.ChallengeAsync(match.MatchId, p2Id, challengeReq);

            Assert.Equal(challengeRes1.StateVersion, challengeRes2.StateVersion);
            Assert.Equal(challengeRes1.PenalizedPlayerId, challengeRes2.PenalizedPlayerId);
            Assert.Equal(pullsCountAfterFirst, p2.Revolver.PullsCount);
            Assert.Equal(versionAfterFirst, match.StateVersion);
        }

        // 24. Request actionId cũ nhưng payload khác bị từ chối (DUPLICATE_ACTION).
        [Fact]
        public async Task Test24_OldActionIdWithDifferentPayloadRejected()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            string currentPid = match.CurrentTurnPlayerId!;
            var player = match.FindPlayer(currentPid)!;
            var actionId = Guid.NewGuid();

            var req1 = new PlayCardsRequest
            {
                ActionId = actionId,
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = new List<string> { player.Hand[0].CardInstanceId }
            };

            await sys.actionService.PlayCardsAsync(match.MatchId, currentPid, req1);

            // Reusing same actionId with a different payload
            var req2 = new PlayCardsRequest
            {
                ActionId = actionId,
                ExpectedVersion = match.StateVersion,
                CardInstanceIds = new List<string> { player.Hand[1].CardInstanceId }
            };

            var ex = await Assert.ThrowsAsync<GameRuleException>(() =>
                sys.actionService.PlayCardsAsync(match.MatchId, currentPid, req2));

            Assert.Equal("DUPLICATE_ACTION", ex.Code);
        }

        // 25. Không lộ deck hoặc thông tin bí mật trong public state.
        [Fact]
        public void Test25_PublicStateDoesNotLeakSecretCards()
        {
            var sys = CreateTestSystem();
            var match = CreateActiveMatchWithPlayers(sys.matchService, sys.store, 2);

            var publicState = sys.queryService.GetPublicState(match.MatchId);

            Assert.NotNull(publicState);
            Assert.Equal(match.MatchId, publicState.MatchId);
            Assert.Equal(2, publicState.Players.Count);

            // Verify public state only shows card counts, not actual cards or ranks
            foreach (var playerDto in publicState.Players)
            {
                Assert.Equal(5, playerDto.RemainingCardCount);
            }
        }

        // 26. Table SitDown gán Host cho người đầu tiên và cập nhật đúng thông tin.
        [Fact]
        public void Test26_TableSitAssignsHostAndTracksSeats()
        {
            var tableService = new TableService();
            var err1 = tableService.SitDown(1, 0, "p1");
            Assert.Null(err1);

            var table = tableService.GetTableByID(1);
            Assert.NotNull(table);
            Assert.Equal("p1", table.HostPlayerId);
            Assert.Equal(1, table.PlayerCount);

            var err2 = tableService.SitDown(1, 1, "p2");
            Assert.Null(err2);

            table = tableService.GetTableByID(1);
            Assert.NotNull(table);
            Assert.Equal("p1", table.HostPlayerId); // Host vẫn là p1
            Assert.Equal(2, table.PlayerCount);
        }

        // 27. Chủ bàn bắt đầu trận đấu: Thất bại nếu chưa đủ 2 người chơi.
        [Fact]
        public void Test27_TableStartMatch_FailsWhenInsufficientPlayers()
        {
            var tableService = new TableService();
            tableService.SitDown(1, 0, "host_player");

            var (error, playerIds) = tableService.ValidateAndStartMatch(1, "host_player");
            Assert.Equal("INSUFFICIENT_PLAYERS", error);
            Assert.Null(playerIds);
        }

        // 28. Chủ bàn bắt đầu trận đấu: Thất bại nếu người gọi không phải chủ bàn.
        [Fact]
        public void Test28_TableStartMatch_FailsWhenNotHost()
        {
            var tableService = new TableService();
            tableService.SitDown(1, 0, "host_player");
            tableService.SitDown(1, 1, "member_player");
            tableService.SetReady("member_player", true);

            var (error, playerIds) = tableService.ValidateAndStartMatch(1, "member_player");
            Assert.Equal("NOT_TABLE_HOST", error);
            Assert.Null(playerIds);
        }

        // 29. Chủ bàn bắt đầu trận đấu: Thất bại nếu có thành viên chưa sẵn sàng.
        [Fact]
        public void Test29_TableStartMatch_FailsWhenMembersNotReady()
        {
            var tableService = new TableService();
            tableService.SitDown(1, 0, "host_player");
            tableService.SitDown(1, 1, "member_player");
            // member_player chưa gọi ready

            var (error, playerIds) = tableService.ValidateAndStartMatch(1, "host_player");
            Assert.Equal("TABLE_NOT_READY", error);
            Assert.Null(playerIds);
        }

        // 30. Chủ bàn bắt đầu trận đấu: Thành công khi đủ người và mọi người đã ready.
        [Fact]
        public void Test30_TableStartMatch_SucceedsWhenValid()
        {
            var tableService = new TableService();
            tableService.SitDown(1, 0, "host_player");
            tableService.SitDown(1, 1, "member_player");
            tableService.SetReady("member_player", true);

            var (error, playerIds) = tableService.ValidateAndStartMatch(1, "host_player");
            Assert.Null(error);
            Assert.NotNull(playerIds);
            Assert.Equal(2, playerIds.Count);
            Assert.Equal("host_player", playerIds[0]);
            Assert.Equal("member_player", playerIds[1]);

            var table = tableService.GetTableByID(1);
            Assert.Equal(TableStatus.Playing, table!.Status);

            // Gán MatchId
            tableService.SetCurrentMatch(1, "m_table_1_xyz");
            table = tableService.GetTableByID(1);
            Assert.Equal("m_table_1_xyz", table!.CurrentMatchId);
        }
    }
}