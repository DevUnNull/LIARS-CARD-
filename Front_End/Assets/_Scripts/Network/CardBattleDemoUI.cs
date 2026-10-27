using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LiarsCard.Network
{
    public class CardBattleDemoUI : MonoBehaviour
    {
        [Header("Lobby / Table Setup")]
        public int tableId = 1;
        public int seatIndex = 0;

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
        public string matchPhase = "-";
        public string winnerPlayerId = "";
        public bool localPlayerAlive;

        [Header("Local Player Hand")]
        public List<CardDto> myCards = new List<CardDto>();
        public List<bool> selectedCards = new List<bool>();

        private string statusLog = "San sang. Hay ngoi vao ban, ready hoac nhap Match ID.";
        private float pollTimer = 0f;
        private string currentPlayerId = "";
        private string appliedStateMatchId = "";
        private bool hasAuthoritativeMatchState;
        private readonly HashSet<string> displayedRoundKeys = new HashSet<string>();
        private readonly HashSet<string> displayedEliminationKeys = new HashSet<string>();
        private readonly HashSet<string> displayedMatchEndKeys = new HashSet<string>();
        private MatchResultResponse savedMatchResult;
        private string savedResultMatchId = "";
        private string savedResultStatus = "NotLoaded";
        private string savedResultError = "";

        private bool CanSendGameplayAction =>
            hasAuthoritativeMatchState &&
            string.Equals(appliedStateMatchId, matchId, StringComparison.Ordinal) &&
            localPlayerAlive &&
            !string.Equals(matchPhase, "MatchEnded", StringComparison.Ordinal) &&
            string.Equals(currentTurnPlayerId, currentPlayerId, StringComparison.Ordinal);

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
            if (pollTimer >= 2.0f && !string.IsNullOrEmpty(jwtToken))
            {
                pollTimer = 0f;
                if (!string.IsNullOrEmpty(matchId))
                {
                    RefreshState(silent: true);
                }
            }
        }

        public void ActionSitDown()
        {
            statusLog = $"Dang ngoi vao Ban {tableId}, Ghe {seatIndex}...";
            StartCoroutine(MatchApiClient.Instance.SitDown(tableId, seatIndex, jwtToken,
                onSuccess: res =>
                {
                    statusLog = $"[NGOI GHE THANH CONG] Ban {tableId}, Ghe {seatIndex}. Hay bam 'San Sang Ban'.";
                    ActionCheckTable();
                },
                onError: err => statusLog = $"Loi ngoi ghe: {err}"
            ));
        }

        public void ActionTableReady()
        {
            statusLog = "Dang cap nhat San Sang tai ban...";
            StartCoroutine(MatchApiClient.Instance.SetTableReady(true, jwtToken,
                onSuccess: res =>
                {
                    statusLog = "[SAN SANG THANH CONG] Da ready tai ban! Neu la Chu ban, hay bam 'Bat Dau Tran Dau'.";
                    ActionCheckTable();
                },
                onError: err => statusLog = $"Loi ready ban: {err}"
            ));
        }

        public void ActionStartTableMatch()
        {
            statusLog = $"[CHU BAN] Dang yeu cau Server bat dau tran dau tai Ban {tableId}...";
            StartCoroutine(MatchApiClient.Instance.StartTableMatch(tableId, jwtToken,
                onSuccess: res =>
                {
                    matchId = res.matchId;
                    statusLog = $"[TAO TRAN THANH CONG] Match ID: {res.matchId}. Tat ca nguoi choi tu dong dong bo!";
                    ActionReady();
                },
                onError: err => statusLog = $"Loi bat dau tran: {err}"
            ));
        }

        public void ActionCheckTable()
        {
            StartCoroutine(MatchApiClient.Instance.GetTable(tableId, jwtToken,
                onSuccess: res =>
                {
                    statusLog = $"[BAN {res.tableId}] Trang thai: {res.status} | Chu ban: {res.hostPlayerId} | So nguoi: {res.playerCount}/{res.capacity}";
                    if (!string.IsNullOrEmpty(res.currentMatchId))
                    {
                        matchId = res.currentMatchId;
                        statusLog += $" | Match dang dien ra: {res.currentMatchId}";
                    }
                },
                onError: err => statusLog = $"Loi lay thong tin ban: {err}"
            ));
        }

        public void ActionReady()
        {
            statusLog = "Dang gui Ready len Server...";
            StartCoroutine(MatchApiClient.Instance.Ready(matchId, jwtToken,
                onSuccess: res =>
                {
                    ApplyState(res);
                    statusLog = $"[READY THANH CONG] Round: {res.roundNumber} | Table Rank: {res.tableRank} | Luot: {res.currentTurnPlayerId}";
                },
                onError: err => statusLog = $"Loi Ready: {err}"
            ));
        }

        public void ActionGetHand()
        {
            RequestHand(matchId, roundNumber, announceResult: true);
        }

        public void ActionLoadSavedResult()
        {
            savedResultMatchId = matchId;
            if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrEmpty(jwtToken))
            {
                savedResultStatus = "Error";
                savedResultError = "Can Match ID va access token de tai ket qua.";
                return;
            }

            if (savedResultStatus == "Loading")
            {
                return;
            }
            savedMatchResult = null;
            savedResultStatus = "Loading";
            savedResultError = "";

            StartCoroutine(MatchApiClient.Instance.GetMatchResult(
                matchId,
                jwtToken,
                onSuccess: result =>
                {
                    if (!string.Equals(matchId, savedResultMatchId, StringComparison.Ordinal))
                    {
                        return;
                    }

                    savedMatchResult = result;
                    savedResultStatus = "SavedResultLoaded";
                    savedResultError = "";
                },
                onNotFound: error =>
                {
                    if (!string.Equals(matchId, savedResultMatchId, StringComparison.Ordinal))
                    {
                        return;
                    }

                    savedResultStatus = "NotFound";
                    savedResultError = error;
                },
                onError: error =>
                {
                    if (!string.Equals(matchId, savedResultMatchId, StringComparison.Ordinal))
                    {
                        return;
                    }

                    savedResultStatus = "Error";
                    savedResultError = error;
                }));
        }

        private void RequestHand(string requestedMatchId, int requestedRoundNumber, bool announceResult)
        {
            StartCoroutine(MatchApiClient.Instance.GetHand(requestedMatchId, jwtToken,
                onSuccess: res =>
                {
                    if (!string.Equals(matchId, requestedMatchId, StringComparison.Ordinal) ||
                        (hasAuthoritativeMatchState &&
                         (roundNumber != requestedRoundNumber ||
                          string.Equals(matchPhase, "MatchEnded", StringComparison.Ordinal))))
                    {
                        return;
                    }

                    myCards = res.cards;
                    selectedCards.Clear();
                    for (int i = 0; i < myCards.Count; i++) selectedCards.Add(false);
                    if (announceResult)
                    {
                        statusLog = $"Da nhan {myCards.Count} la bai bi mat tu server.";
                    }
                },
                onError: err =>
                {
                    if (announceResult)
                    {
                        statusLog = $"Loi lay bai: {err}";
                    }
                }
            ));
        }

        public void ActionPlayCards()
        {
            if (!CanSendGameplayAction)
            {
                statusLog = "Khong the danh bai: ban da bi loai, tran da ket thuc, hoac khong phai luot cua ban.";
                return;
            }

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
                    stateVersion = Math.Max(stateVersion, res.stateVersion);
                    statusLog = $"DANH BAI THANH CONG! PlayId: {res.playId}. Con lai {res.remainingCardCount} la.";
                    ActionGetHand();
                    RefreshState(silent: true);
                },
                onError: err => statusLog = $"Loi danh bai: {err}"
            ));
        }

        public void ActionChallenge()
        {
            if (!CanSendGameplayAction)
            {
                statusLog = "Khong the to cao: ban da bi loai, tran da ket thuc, hoac khong phai luot cua ban.";
                return;
            }

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

            statusLog = $"Dang to cao luot danh {lastPlayId}...";
            StartCoroutine(MatchApiClient.Instance.Challenge(matchId, jwtToken, req,
                onSuccess: res =>
                {
                    stateVersion = Math.Max(stateVersion, res.stateVersion);
                    string outcome = res.challengeSucceeded ? "TO CAO DUNG! (Co nguoi noi doi)" : "TO CAO SAI! (Bai hoan toan hop le)";
                    string penalty = res.eliminated ? "BI LOAI (RUT SUNG TRUNG DAN)" : (res.survived ? "MAY MAN SONG SOT (PHEW)" : "DAN BAN KHONG TRUNG");
                    statusLog = $"KET QUA TO CAO: {outcome}\nNguoi bi phat: {res.penalizedPlayerId} -> {penalty}\nGiai doan tiep theo: {res.nextPhase}";
                    RefreshState(silent: true);
                },
                onError: err => statusLog = $"Loi to cao: {err}"
            ));
        }

        private void RefreshState(bool silent = false)
        {
            StartCoroutine(MatchApiClient.Instance.GetState(matchId, jwtToken,
                onSuccess: res => ApplyState(res),
                onError: err => { if (!silent) statusLog = $"Loi Refresh: {err}"; }
            ));
        }

        private void ApplyState(PublicMatchStateResponse res)
        {
            if (res == null ||
                string.IsNullOrWhiteSpace(res.matchId) ||
                !string.Equals(res.matchId, matchId, StringComparison.Ordinal))
            {
                return;
            }

            bool hadStateForMatch = hasAuthoritativeMatchState &&
                string.Equals(appliedStateMatchId, res.matchId, StringComparison.Ordinal);
            int previousRoundNumber = roundNumber;

            if (!string.Equals(appliedStateMatchId, res.matchId, StringComparison.Ordinal))
            {
                appliedStateMatchId = res.matchId;
                stateVersion = -1;
                hasAuthoritativeMatchState = false;
                localPlayerAlive = false;
                winnerPlayerId = "";
                lastPlayId = "";
                roundNumber = 0;
                myCards.Clear();
                selectedCards.Clear();
                displayedRoundKeys.Clear();
                displayedEliminationKeys.Clear();
                displayedMatchEndKeys.Clear();
                savedMatchResult = null;
                savedResultMatchId = "";
                savedResultStatus = "NotLoaded";
                savedResultError = "";
            }

            if (res.stateVersion < stateVersion)
            {
                return;
            }

            roundNumber = res.roundNumber;
            tableRank = res.tableRank;
            currentTurnPlayerId = string.IsNullOrEmpty(res.currentTurnPlayerId) ? "-" : res.currentTurnPlayerId;
            stateVersion = res.stateVersion;
            matchPhase = res.phase ?? "-";
            winnerPlayerId = res.winnerPlayerId ?? "";
            currentPlayerId = res.currentPlayerId ?? "";
            hasAuthoritativeMatchState = true;

            if (res.lastPlay != null)
            {
                lastPlayId = res.lastPlay.playId;
            }
            else
            {
                lastPlayId = "";
            }

            var players = res.players ?? new List<PublicPlayerStateDto>();
            var localPlayer = players.FirstOrDefault(player =>
                string.Equals(player.playerId, currentPlayerId, StringComparison.Ordinal));
            localPlayerAlive = localPlayer != null && localPlayer.isAlive;

            bool newRoundSnapshot = roundNumber > 0 &&
                (!hadStateForMatch || previousRoundNumber != roundNumber);
            if (newRoundSnapshot)
            {
                string roundKey = $"{res.matchId}:{roundNumber}";
                if (displayedRoundKeys.Add(roundKey))
                {
                    statusLog = hadStateForMatch
                        ? $"Da dong bo vong moi {roundNumber} tu trang thai authoritative cua Server."
                        : $"Da khoi phuc trang thai vong {roundNumber} tu Server.";
                }

                if (localPlayerAlive && !string.Equals(matchPhase, "MatchEnded", StringComparison.Ordinal))
                {
                    RequestHand(res.matchId, roundNumber, announceResult: false);
                }
            }

            foreach (var eliminatedPlayer in players.Where(player => !player.isAlive))
            {
                string eventKey = $"{res.matchId}:{eliminatedPlayer.playerId}";
                if (displayedEliminationKeys.Add(eventKey))
                {
                    statusLog = $"Nguoi choi {eliminatedPlayer.playerId} da bi loai theo trang thai Server.";
                }
            }

            if (string.Equals(matchPhase, "MatchEnded", StringComparison.Ordinal))
            {
                string eventKey = $"{res.matchId}:{res.stateVersion}";
                if (displayedMatchEndKeys.Add(eventKey))
                {
                    statusLog = string.IsNullOrEmpty(winnerPlayerId)
                        ? "TRAN DA KET THUC. Server khong ghi nhan nguoi chien thang."
                        : $"TRAN DA KET THUC. Nguoi chien thang: {winnerPlayerId}.";
                }

                if (!string.Equals(savedResultMatchId, res.matchId, StringComparison.Ordinal))
                {
                    ActionLoadSavedResult();
                }
            }
        }

        private void OnGUI()
        {
            GUI.skin.box.fontSize = 14;
            GUI.skin.button.fontSize = 13;
            GUI.skin.label.fontSize = 13;

            GUILayout.BeginArea(new Rect(20, 20, 520, 680), GUI.skin.box);

            GUILayout.Label("<size=16><b>♠♥ LIARS CARD - GAMEPLAY CONTROLLER ♦♣</b></size>");
            GUILayout.Space(4);

            // 0. LOBBY & TABLE
            GUILayout.Label("<b>0. CHON BAN & GHE (LOBBY TABLE - PHUONG AN B)</b>");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Ban ID:", GUILayout.Width(60));
            int.TryParse(GUILayout.TextField(tableId.ToString(), GUILayout.Width(40)), out tableId);
            GUILayout.Label("Ghe (0-3):", GUILayout.Width(65));
            int.TryParse(GUILayout.TextField(seatIndex.ToString(), GUILayout.Width(40)), out seatIndex);
            if (GUILayout.Button("Ngoi Ghe")) ActionSitDown();
            if (GUILayout.Button("Ready Ban")) ActionTableReady();
            if (GUILayout.Button("Check")) ActionCheckTable();
            GUILayout.EndHorizontal();

            GUI.color = new Color(1f, 0.7f, 0.2f);
            if (GUILayout.Button("<b>[CHU BAN] BAT DAU TRAN DAU (START MATCH)</b>", GUILayout.Height(30))) ActionStartTableMatch();
            GUI.color = Color.white;

            GUILayout.Space(6);

            // 1. MATCH CONFIG
            GUILayout.Label("<b>1. CAU HINH TRAN DAU (MATCH CONFIG)</b>");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Match ID:", GUILayout.Width(75));
            matchId = GUILayout.TextField(matchId);
            if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshState();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("JWT Token:", GUILayout.Width(75));
            jwtToken = GUILayout.TextField(jwtToken);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Bao San Sang (Ready)", GUILayout.Height(28))) ActionReady();
            if (GUILayout.Button("Lay Bai (Get Hand)", GUILayout.Height(28))) ActionGetHand();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("<b>2. TRANG THAI BAN (TABLE STATE)</b>");
            GUILayout.Label($"Vong: <b>{roundNumber}</b> | Rank Ban: <color=yellow><b>{tableRank}</b></color> | State Version: <b>{stateVersion}</b>");
            GUILayout.Label($"Luot danh hien tai: <b>{currentTurnPlayerId}</b>");
            GUILayout.Label($"Trang thai tran: <b>{matchPhase}</b>");
            if (hasAuthoritativeMatchState)
            {
                GUILayout.Label(localPlayerAlive
                    ? "<color=green>Ban con song</color>"
                    : "<color=red>Ban da bi loai hoac khong thuoc tran</color>");
            }

            if (string.Equals(matchPhase, "MatchEnded", StringComparison.Ordinal))
            {
                GUILayout.Label(string.IsNullOrEmpty(winnerPlayerId)
                    ? "<size=16><b>TRAN DA KET THUC - KHONG CO WINNER DUOC SERVER GHI NHAN</b></size>"
                    : $"<size=16><b>TRAN DA KET THUC - WINNER: {winnerPlayerId}</b></size>");
                GUILayout.Label("<b>Ket qua gameplay authoritative khong xac nhan MongoDB da luu.</b>");
                GUILayout.Label($"Trang thai ket qua luu: <b>{savedResultStatus}</b>");

                if (savedResultStatus == "Loading")
                {
                    GUILayout.Label("Dang tai ket qua da luu...");
                }
                else if (savedResultStatus == "Error" || savedResultStatus == "NotFound")
                {
                    GUILayout.Label(savedResultStatus == "NotFound"
                        ? "Chua tim thay ket qua da luu."
                        : $"Loi tai ket qua: {savedResultError}");
                    if (GUILayout.Button("Thu lai tai ket qua"))
                    {
                        ActionLoadSavedResult();
                    }
                }
                else if (savedResultStatus == "SavedResultLoaded" &&
                         savedMatchResult != null &&
                         string.Equals(savedMatchResult.matchId, matchId, StringComparison.Ordinal))
                {
                    GUILayout.Label(string.IsNullOrEmpty(savedMatchResult.winnerPlayerId)
                        ? "Ket qua da luu: khong co winner."
                        : $"Winner trong ket qua da luu: {savedMatchResult.winnerPlayerId}");

                    foreach (var resultPlayer in savedMatchResult.players)
                    {
                        GUILayout.Label(
                            $"{resultPlayer.displayName} ({resultPlayer.playerId}) - " +
                            $"{(resultPlayer.isAliveAtEnd ? "con song" : "bi loai")}, " +
                            $"{resultPlayer.remainingCardCountAtEnd} la" +
                            (resultPlayer.isWinner ? ", WINNER" : ""));
                    }

                    GUILayout.Label($"Ket thuc: {savedMatchResult.endedAtUtc}");
                }
            }

            if (!string.IsNullOrEmpty(lastPlayId))
            {
                GUILayout.Label($"Luot danh gan nhat: <color=cyan>{lastPlayId}</color>");
            }

            GUILayout.Space(6);
            GUILayout.Label("<b>3. BAI TREN TAY (CHON 1-3 LA DE DANH / BLUFF)</b>");
            GUI.enabled = CanSendGameplayAction;
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

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUI.color = Color.green;
            if (GUILayout.Button("DANH BAI (PLAY CARDS)", GUILayout.Height(32))) ActionPlayCards();

            GUI.color = Color.red;
            if (GUILayout.Button("TO CAO (CHALLENGE / LIAR)", GUILayout.Height(32))) ActionChallenge();
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            GUILayout.Space(6);
            GUILayout.Label("<b>4. NHAT KY VAN BAI (LOG):</b>");
            GUI.color = Color.yellow;
            GUILayout.TextArea(statusLog, GUILayout.Height(95));
            GUI.color = Color.white;

            GUILayout.EndArea();
        }
    }
}