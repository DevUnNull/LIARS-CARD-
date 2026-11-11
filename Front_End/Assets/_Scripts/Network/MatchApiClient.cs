using System;
using System.Collections;
using LiarsCard.Server;
using UnityEngine;

namespace LiarsCard.Network
{
    /// <summary>
    /// API trận đấu / bàn chơi. Trước đây gọi HTTP tới Back_End; giờ gửi request qua GameLink tới
    /// GameServer trên Master Client. Giữ nguyên chữ ký hàm để UI không phải sửa (tham số token bỏ qua).
    /// </summary>
    public class MatchApiClient : MonoBehaviour
    {
        public static MatchApiClient Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private static IEnumerator Call<T>(string route, RouteArgs args, Action<T> onSuccess, Action<string> onError)
        {
            yield return GameLink.Request(route, args, (ok, body) =>
            {
                if (!ok) { onError?.Invoke(body); return; }
                T res;
                try { res = JsonUtility.FromJson<T>(body); }
                catch (Exception e) { onError?.Invoke("Phản hồi không hợp lệ: " + e.Message); return; }
                onSuccess?.Invoke(res);
            });
        }

        private static IEnumerator CallText(string route, RouteArgs args, Action<string> onSuccess, Action<string> onError)
        {
            yield return GameLink.Request(route, args, (ok, body) => { if (ok) onSuccess?.Invoke(body); else onError?.Invoke(body); });
        }

        public IEnumerator GetHand(string matchId, string token, Action<PlayerHandResponse> onSuccess, Action<string> onError) =>
            Call("matches/hand", new RouteArgs { matchId = matchId }, onSuccess, onError);

        public IEnumerator GetState(string matchId, string token, Action<PublicMatchStateResponse> onSuccess, Action<string> onError) =>
            Call("matches/state", new RouteArgs { matchId = matchId }, onSuccess, onError);

        public IEnumerator Ready(string matchId, string token, Action<PublicMatchStateResponse> onSuccess, Action<string> onError) =>
            Call("matches/ready", new RouteArgs { matchId = matchId }, onSuccess, onError);

        public IEnumerator PlayCards(string matchId, string token, PlayCardsRequest request, Action<PlayCardsResponse> onSuccess, Action<string> onError) =>
            Call("deck/play", new RouteArgs { matchId = matchId, expectedVersion = request.expectedVersion, cardInstanceIds = request.cardInstanceIds }, onSuccess, onError);

        // action: "challenge" (tố) hoặc "trust" (tin lượt đánh cuối của người vừa hết bài)
        public IEnumerator Challenge(string matchId, string token, ChallengeRequest request, Action<ChallengeResponse> onSuccess, Action<string> onError, string action = "challenge") =>
            Call("deck/" + action, new RouteArgs { matchId = matchId, expectedVersion = request.expectedVersion, targetPlayId = request.targetPlayId }, onSuccess, onError);

        public IEnumerator PullTrigger(string matchId, string token, PullTriggerRequest request, Action<PullTriggerResponse> onSuccess, Action<string> onError) =>
            Call("deck/pull", new RouteArgs { matchId = matchId }, onSuccess, onError);

        public IEnumerator GetTable(int tableId, string token, Action<TableResponseDto> onSuccess, Action<string> onError) =>
            Call("tables/get", new RouteArgs { tableId = tableId }, onSuccess, onError);

        public IEnumerator SitDown(int tableId, int seatIndex, string token, Action<string> onSuccess, Action<string> onError) =>
            CallText("tables/sit", new RouteArgs { tableId = tableId, seatIndex = seatIndex }, onSuccess, onError);

        public IEnumerator LeaveSeat(Action<string> onSuccess, Action<string> onError) =>
            CallText("tables/leave", null, onSuccess, onError);

        public IEnumerator SetTableReady(bool ready, string token, Action<string> onSuccess, Action<string> onError) =>
            CallText("tables/ready", new RouteArgs { ready = ready }, onSuccess, onError);

        public IEnumerator StartTableMatch(int tableId, string token, Action<StartMatchResponseDto> onSuccess, Action<string> onError) =>
            Call("tables/start", new RouteArgs { tableId = tableId }, onSuccess, onError);
    }
}
