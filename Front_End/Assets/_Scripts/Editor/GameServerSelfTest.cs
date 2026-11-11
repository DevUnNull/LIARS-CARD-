using System.Collections.Generic;
using LiarsCard.Network;
using LiarsCard.Server;
using UnityEditor;
using UnityEngine;

/// <summary>Tự kiểm tra GameServer: 2 người ngồi bàn, vào trận, đánh / tố / bóp cò cho tới khi có người thắng.</summary>
public static class GameServerSelfTest
{
    [MenuItem("Tools/Liars Card/Self-test GameServer")]
    public static string Run()
    {
        var packets = new List<(string to, byte type)>();
        var server = new GameServer((to, data) => packets.Add((to, data[0])));
        server.PlayerJoined("a", "An");
        server.PlayerJoined("b", "Bình");

        T Ok<T>(string who, string route, RouteArgs args)
        {
            var (ok, body) = server.Handle(who, route, JsonUtility.ToJson(args));
            Debug.Assert(ok, $"{route} lỗi: {body}");
            if (!ok) throw new System.Exception($"{route}: {body}");
            return JsonUtility.FromJson<T>(body);
        }

        Ok<TableResponseDto>("a", "tables/sit", new RouteArgs { tableId = 1, seatIndex = 0 });
        Ok<TableResponseDto>("b", "tables/sit", new RouteArgs { tableId = 1, seatIndex = 1 });
        var dup = server.Handle("a", "tables/sit", JsonUtility.ToJson(new RouteArgs { tableId = 1, seatIndex = 2 }));
        Debug.Assert(!dup.ok && dup.body.Contains("PLAYER_ALREADY_SEATED"), "Ngồi 2 ghế phải bị chặn");

        var notReady = server.Handle("a", "tables/start", JsonUtility.ToJson(new RouteArgs { tableId = 1 }));
        Debug.Assert(!notReady.ok && notReady.body.Contains("TABLE_NOT_READY"), "Chưa ready mà bắt đầu được");
        server.Handle("b", "tables/ready", JsonUtility.ToJson(new RouteArgs { ready = true }));
        string matchId = Ok<StartMatchResponseDto>("a", "tables/start", new RouteArgs { tableId = 1 }).matchId;

        Ok<PublicMatchStateResponse>("a", "matches/ready", new RouteArgs { matchId = matchId });
        var state = Ok<PublicMatchStateResponse>("b", "matches/ready", new RouteArgs { matchId = matchId });
        Debug.Assert(state.phase == "AwaitPlayOrChallenge" && state.players.TrueForAll(p => p.remainingCardCount == 5), "Chưa chia 5 lá");

        var handA = Ok<PlayerHandResponse>("a", "matches/hand", new RouteArgs { matchId = matchId });
        var handB = Ok<PlayerHandResponse>("b", "matches/hand", new RouteArgs { matchId = matchId });
        Debug.Assert(handA.cards.TrueForAll(c => !handB.cards.Exists(d => d.cardInstanceId == c.cardInstanceId)), "Hai người trùng lá bài");

        // Đánh / tố tới khi trận kết thúc (tối đa 6 lần bóp cò mỗi người là chắc chắn có người chết)
        for (int guard = 0; guard < 200; guard++)
        {
            state = Ok<PublicMatchStateResponse>("a", "matches/state", new RouteArgs { matchId = matchId });
            if (state.phase == "MatchEnded") break;
            string turn = state.currentTurnPlayerId;

            if (state.phase == "PenaltyResolution")
            {
                Ok<PullTriggerResponse>(turn, "deck/pull", new RouteArgs { matchId = matchId });
                continue;
            }
            if (state.lastPlay != null && !string.IsNullOrEmpty(state.lastPlay.playId))
            {
                Ok<ChallengeResponse>(turn, "deck/challenge", new RouteArgs { matchId = matchId, expectedVersion = state.stateVersion, targetPlayId = state.lastPlay.playId });
                continue;
            }
            var hand = Ok<PlayerHandResponse>(turn, "matches/hand", new RouteArgs { matchId = matchId });
            var other = turn == "a" ? "b" : "a";
            var wrong = server.Handle(other, "deck/play", JsonUtility.ToJson(new RouteArgs { matchId = matchId, expectedVersion = state.stateVersion, cardInstanceIds = new List<string> { hand.cards[0].cardInstanceId } }));
            Debug.Assert(!wrong.ok, "Đánh bài của người khác / sai lượt mà vẫn được");
            Ok<PlayCardsResponse>(turn, "deck/play", new RouteArgs { matchId = matchId, expectedVersion = state.stateVersion, cardInstanceIds = new List<string> { hand.cards[0].cardInstanceId } });
        }

        Debug.Assert(state.phase == "MatchEnded" && !string.IsNullOrEmpty(state.winnerPlayerId), "Trận không kết thúc");
        var table = Ok<TableResponseDto>("a", "tables/get", new RouteArgs { tableId = 1 });
        Debug.Assert(table.status == 0 && string.IsNullOrEmpty(table.currentMatchId), "Bàn không được giải phóng sau trận");
        Debug.Assert(packets.Exists(p => p.type == 11) && packets.Exists(p => p.type == 12), "Thiếu gói phạt / kết quả bóp cò");

        // Người chơi thoát giữa trận: người còn lại thắng
        server.Handle("b", "tables/ready", JsonUtility.ToJson(new RouteArgs { ready = true }));
        string m2 = Ok<StartMatchResponseDto>("a", "tables/start", new RouteArgs { tableId = 1 }).matchId;
        server.PlayerLeft("b");
        var s2 = Ok<PublicMatchStateResponse>("a", "matches/state", new RouteArgs { matchId = m2 });
        Debug.Assert(s2.phase == "MatchEnded" && s2.winnerPlayerId == "a", "Thoát giữa trận không xử thua");

        string result = $"GameServer OK: thắng = {state.winnerPlayerId}, {packets.Count} gói tin";
        Debug.Log(result);
        return result;
    }
}
