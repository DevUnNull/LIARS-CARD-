using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : Singleton<PlayerManager>
{
    public int MyPlayerId { get; private set; } = -1;

    [SerializeField] private GameObject remotePlayerPrefab;
    private readonly Dictionary<int, GameObject> remotePlayers = new();

    public void SetMyPlayerId(int playerId)
    {
        MyPlayerId = playerId;
    }

    public void AddRemotePlayer(int playerId)
    {
        if (MyPlayerId == playerId || remotePlayers.ContainsKey(playerId))
        {
            return;
        }

        Vector3 spawnPoint = new Vector3(0, 1, 0);
        GameObject remotePlayerIns = Instantiate(remotePlayerPrefab, spawnPoint, Quaternion.identity);
        remotePlayerIns.name = $"RemotePlayer_{playerId}";
        remotePlayers.Add(playerId, remotePlayerIns);
    }

    public void RemoveRemotePlayer(int playerId)
    {
        if (!remotePlayers.TryGetValue(playerId, out var remotePlayer))
        {
            return;
        }

        Destroy(remotePlayer);
        remotePlayers.Remove(playerId);
    }

    public void UpdateRemotePosition(int playerId, Vector3 position, float yaw)
    {
        if (!remotePlayers.TryGetValue(playerId, out var remotePlayer))
        {
            return;
        }

        remotePlayer.transform.SetPositionAndRotation(position,Quaternion.Euler(0f, yaw, 0f));
    }

    public void ClearPlayers()
    {
        foreach (var remotePlayer in remotePlayers.Values)
        {
            Destroy(remotePlayer);
        }

        remotePlayers.Clear();
        MyPlayerId = -1;
    }
}
