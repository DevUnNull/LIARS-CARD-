using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LiarsCard.Network
{
    public class BypassCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }

    public class MatchApiClient : MonoBehaviour
    {
        public static MatchApiClient Instance { get; private set; }

        [Header("Backend Settings")]
        public string baseUrl = "http://localhost:5000"; // Hoac https://localhost:7189

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public IEnumerator GetHand(string matchId, string token, Action<PlayerHandResponse> onSuccess, Action<string> onError)
        {
            string url = $"{baseUrl}/api/matches/{matchId}/me/hand";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                SetupRequest(req, token);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<PlayerHandResponse>(req.downloadHandler.text);
                    onSuccess?.Invoke(res);
                }
                else
                {
                    onError?.Invoke(ExtractError(req));
                }
            }
        }

        public IEnumerator GetState(string matchId, string token, Action<PublicMatchStateResponse> onSuccess, Action<string> onError)
        {
            string url = $"{baseUrl}/api/matches/{matchId}/state";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                SetupRequest(req, token);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<PublicMatchStateResponse>(req.downloadHandler.text);
                    onSuccess?.Invoke(res);
                }
                else
                {
                    onError?.Invoke(ExtractError(req));
                }
            }
        }

        public IEnumerator Ready(string matchId, string token, Action<PublicMatchStateResponse> onSuccess, Action<string> onError)
        {
            string url = $"{baseUrl}/api/matches/{matchId}/ready";
            string bodyJson = "{\"isReady\": true}";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                SetupRequest(req, token);

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<PublicMatchStateResponse>(req.downloadHandler.text);
                    onSuccess?.Invoke(res);
                }
                else
                {
                    onError?.Invoke(ExtractError(req));
                }
            }
        }

        public IEnumerator PlayCards(string matchId, string token, PlayCardsRequest request, Action<PlayCardsResponse> onSuccess, Action<string> onError)
        {
            string url = $"{baseUrl}/api/matches/{matchId}/deck/play-cards";
            string json = JsonUtility.ToJson(request);

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                SetupRequest(req, token);

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<PlayCardsResponse>(req.downloadHandler.text);
                    onSuccess?.Invoke(res);
                }
                else
                {
                    onError?.Invoke(ExtractError(req));
                }
            }
        }

        public IEnumerator Challenge(string matchId, string token, ChallengeRequest request, Action<ChallengeResponse> onSuccess, Action<string> onError)
        {
            string url = $"{baseUrl}/api/matches/{matchId}/deck/challenge";
            string json = JsonUtility.ToJson(request);

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                SetupRequest(req, token);

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<ChallengeResponse>(req.downloadHandler.text);
                    onSuccess?.Invoke(res);
                }
                else
                {
                    onError?.Invoke(ExtractError(req));
                }
            }
        }

        private void SetupRequest(UnityWebRequest req, string token)
        {
            req.certificateHandler = new BypassCertificateHandler();
            if (!string.IsNullOrEmpty(token))
            {
                req.SetRequestHeader("Authorization", "Bearer " + token);
            }
        }

        private string ExtractError(UnityWebRequest req)
        {
            try
            {
                if (!string.IsNullOrEmpty(req.downloadHandler?.text))
                {
                    var err = JsonUtility.FromJson<GameErrorResponse>(req.downloadHandler.text);
                    if (err != null && !string.IsNullOrEmpty(err.code))
                    {
                        return $"[{err.code}] {err.message}";
                    }
                }
            }
            catch { }
            return req.error ?? "Lá»—i máº¡ng khÃ´ng xÃ¡c Ä‘á»‹nh";
        }
    }
}