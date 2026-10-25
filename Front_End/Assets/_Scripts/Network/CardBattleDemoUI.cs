using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiarsCard.Network
{
    public class CardBattleDemoUI : MonoBehaviour
    {
        [Header("Match Setup")]
        public string matchId = "match_001";
        [TextArea(2, 3)]
        public string jwtToken = "";

        [Header("Current Match State")]
        public int roundNumber;
        public string tableRank = "-";
        public string currentTurnPlayerId = "-";
        public int stateVersion = 1;
        public string lastPlayId = "";

        [Header("Local Player Hand")]
        public List<CardDto> myCards = new List<CardDto>();
        public List<bool> selectedCards = new List<bool>();

        private string statusLog = "San sang. Nhan 'Ready' hoac 'Get Hand' de bat dau van bai.";
        private float pollTimer = 0f;

        private void Start()
        {
            if (GetComponent<MatchApiClient>() == null)
            {
                gameObject.AddComponent<MatchApiClient>();
            }

            var auth = FindAnyObjectByType<AuthApiClient>();
            if (auth != null && !string.IsNullOrEmpty(auth.AccessToken) && string.IsNullOrEmpty(jwtToken))
            {
                jwtToken = auth.AccessToken;
            }
        }

        private void Update()
        {
            pollTimer += Time.deltaTime;
            if (pollTimer >= 2.0f && !string.IsNullOrEmpty(matchId) && !string.IsNullOrEmpty(jwtToken))
            {
                pollTimer = 0f;
                RefreshState(silent: true);
            }
        }

        public void ActionReady()
        {
            statusLog = "Dang gui Ready len Server...";
            StartCoroutine(MatchApiClient.Instance.Ready(matchId, jwtToken,
                onSuccess: res =>
                {
                    ApplyState(res);
                    statusLog = $"[READY THANH CONG] Round: {res.roundNumber} | Table Rank: {res.tableRank} | Luot: {res.currentTurnPlayerId}";
                    ActionGetHand();
                },
                onError: err => statusLog = $"Loi Ready: {err}"
            ));
        }

        public void ActionGetHand()
        {
            StartCoroutine(MatchApiClient.Instance.GetHand(matchId, jwtToken,
                onSuccess: res =>
                {
                    myCards = res.cards;
                    selectedCards.Clear();
                    for (int i = 0; i < myCards.Count; i++) selectedCards.Add(false);
                    statusLog = $"Da nhan {myCards.Count} la bai bi mat tu server.";
                },
                onError: err => statusLog = $"Loi lay bai: {err}"
            ));
        }

        public void ActionPlayCards()
        {
            var chosenCards = new List<string>();
            for (int i = 0; i < myCards.Count; i++)
            {
                if (selectedCards[i]) chosenCards.Add(myCards[i].cardInstanceId);
            }

            if (chosenCards.Count < 1 || chosenCards.Count > 3)
            {
                statusLog = "LOI LUAT: Moi luot chi duoc danh tu 1 den 3 la bai!";
                return;
            }

            var req = new PlayCardsRequest
            {
                actionId = Guid.NewGuid().ToString(),
                expectedVersion = stateVersion,
                cardInstanceIds = chosenCards
            };

            statusLog = $"Dang danh {chosenCards.Count} la bai (Bluff)...";
            StartCoroutine(MatchApiClient.Instance.PlayCards(matchId, jwtToken, req,
                onSuccess: res =>
                {
                    lastPlayId = res.playId;
                    stateVersion = res.stateVersion;
                    statusLog = $"DANH BAI THANH CONG! PlayId: {res.playId}. Con lai {res.remainingCardCount} la.";
                    ActionGetHand();
                    RefreshState(silent: true);
                },
                onError: err => statusLog = $"Loi danh bai: {err}"
            ));
        }

        public void ActionChallenge()
        {
            if (string.IsNullOrEmpty(lastPlayId))
            {
                statusLog = "Chua co luot danh nao de to cao!";
                return;
            }

            var req = new ChallengeRequest
            {
                actionId = Guid.NewGuid().ToString(),
                expectedVersion = stateVersion,
                targetPlayId = lastPlayId
            };

            statusLog = "DANG TO CAO DOI THU (CALL BLUFF)...";
            StartCoroutine(MatchApiClient.Instance.Challenge(matchId, jwtToken, req,
                onSuccess: res =>
                {
                    stateVersion = res.stateVersion;
                    string liarResult = res.challengeSucceeded ? "TO CAO DUNG! Doi thu da noi doi (Bluff)!" : "TO CAO SAI! Doi thu danh that!";
                    string gunResult = res.eliminated ? "-> BUM! TRUNG DAN! Nguoi choi bi loai!" : "-> CACH! Song sot qua buong dan trong!";
                    statusLog = $"{liarResult}\nNguoi bi phat: {res.penalizedPlayerId} {gunResult}\nNext Phase: {res.nextPhase}";
                    ActionGetHand();
                    RefreshState(silent: true);
                },
                onError: err => statusLog = $"Loi to cao: {err}"
            ));
        }

        private void RefreshState(bool silent = false)
        {
            StartCoroutine(MatchApiClient.Instance.GetState(matchId, jwtToken,
                onSuccess: res => ApplyState(res),
                onError: err => { if (!silent) statusLog = $"Loi cap nhat state: {err}"; }
            ));
        }

        private void ApplyState(PublicMatchStateResponse res)
        {
            roundNumber = res.roundNumber;
            tableRank = res.tableRank;
            currentTurnPlayerId = res.currentTurnPlayerId;
            stateVersion = res.stateVersion;
            if (res.lastPlay != null) lastPlayId = res.lastPlay.playId;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(10, 10, 520, 620), "LIARS CARD â€” GAMEPLAY DEMO (CORE SCENE)");

            GUILayout.BeginArea(new Rect(20, 35, 500, 585));

            GUILayout.Label("<b>1. THIET LAP KET NOI (CONNECTION)</b>");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Match ID:", GUILayout.Width(75));
            matchId = GUILayout.TextField(matchId);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("JWT Token:", GUILayout.Width(75));
            jwtToken = GUILayout.TextField(jwtToken);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Bao San Sang (Ready)", GUILayout.Height(30))) ActionReady();
            if (GUILayout.Button("Lay Bai (Get Hand)", GUILayout.Height(30))) ActionGetHand();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("<b>2. TRANG THAI BAN (TABLE STATE)</b>");
            GUILayout.Label($"Vong: <b>{roundNumber}</b> | Rank Ban: <color=yellow><b>{tableRank}</b></color> | State Version: <b>{stateVersion}</b>");
            GUILayout.Label($"Luot danh hien tai: <b>{currentTurnPlayerId}</b>");
            if (!string.IsNullOrEmpty(lastPlayId))
            {
                GUILayout.Label($"Luot danh gan nhat: <color=cyan>{lastPlayId}</color>");
            }

            GUILayout.Space(8);
            GUILayout.Label("<b>3. BAI TREN TAY (CHON 1-3 LA DE DANH / BLUFF)</b>");
            if (myCards.Count == 0)
            {
                GUILayout.Label("<i>(Chua co bai, nhan 'Ready' hoac 'Lay Bai' de chia bai)</i>");
            }
            else
            {
                for (int i = 0; i < myCards.Count; i++)
                {
                    string idShort = myCards[i].cardInstanceId.Length > 16 
                        ? myCards[i].cardInstanceId.Substring(0, 16) + "..." 
                        : myCards[i].cardInstanceId;

                    selectedCards[i] = GUILayout.Toggle(selectedCards[i], 
                        $" [ La {i + 1} ]: Rank <b><color=orange>{myCards[i].rank}</color></b> (ID: {idShort})");
                }
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUI.color = Color.green;
            if (GUILayout.Button("DANH BAI (PLAY CARDS)", GUILayout.Height(35))) ActionPlayCards();

            GUI.color = Color.red;
            if (GUILayout.Button("TO CAO (CHALLENGE / LIAR)", GUILayout.Height(35))) ActionChallenge();
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("<b>4. NHAT KY VAN BAI (LOG):</b>");
            GUI.color = Color.yellow;
            GUILayout.TextArea(statusLog, GUILayout.Height(105));
            GUI.color = Color.white;

            GUILayout.EndArea();
        }
    }
}