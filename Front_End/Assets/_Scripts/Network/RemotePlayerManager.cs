using System.Collections.Generic;
using UnityEngine;

public class RemotePlayerManager : Singleton<RemotePlayerManager>
{
    [SerializeField] private GameObject remotePlayerPrefab;

    private Dictionary<string, Transform> remotePlayers = new();

    public void UpdateRemotePlayer(string playerId, Vector3 position, float rotationY)
    {
        if (!remotePlayers.TryGetValue(playerId, out Transform remote)
            || remote == null)
        {
            GameObject playerObject = Instantiate(remotePlayerPrefab, position, Quaternion.Euler(0f, rotationY, 0f), transform);

            playerObject.name = $"RemotePlayer_{playerId}";

            remote = playerObject.transform;
            remotePlayers[playerId] = remote;
        }

        remote.SetPositionAndRotation(position, Quaternion.Euler(0f, rotationY, 0f));
    }

    public void RemoveRemotePlayer(string playerId)
    {
        if (!remotePlayers.TryGetValue(playerId, out Transform remote))
            return;

        remotePlayers.Remove(playerId);

        if (remote != null)
        {
            Destroy(remote.gameObject);
        }
    }
}
