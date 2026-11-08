using System.Collections.Generic;
using UnityEngine;

public class RemotePlayerManager : Singleton<RemotePlayerManager>
{
    [SerializeField] private GameObject remotePlayerPrefab;

    private Dictionary<string, Transform> remotePlayers = new();
    // Skin có thể tới trước gói vị trí đầu tiên (lúc mới vào), nên nhớ lại để áp khi tạo remote
    private Dictionary<string, byte> skins = new();

    public void UpdateRemotePlayer(string playerId, Vector3 position, float rotationY, float headYaw = 0f, float headPitch = 0f)
    {
        if (!remotePlayers.TryGetValue(playerId, out Transform remote)
            || remote == null)
        {
            GameObject playerObject = Instantiate(remotePlayerPrefab, position, Quaternion.Euler(0f, rotationY, 0f), transform);

            playerObject.name = $"RemotePlayer_{playerId}";

            remote = playerObject.transform;
            remotePlayers[playerId] = remote;

            if (skins.TryGetValue(playerId, out byte skinId) && playerObject.TryGetComponent<PlayerSkin>(out var skin))
                skin.SetSkin(skinId);
        }

        remote.SetPositionAndRotation(position, Quaternion.Euler(0f, rotationY, 0f));

        if (remote.TryGetComponent<CharacterAnimator>(out var anim))
            anim.SetHeadLook(headYaw, headPitch);
    }

    public void SetRemoteSkin(string playerId, byte skinId)
    {
        skins[playerId] = skinId;

        if (TryGetRemote(playerId, out Transform remote) && remote.TryGetComponent<PlayerSkin>(out var skin))
            skin.SetSkin(skinId);
    }

    public bool TryGetRemote(string playerId, out Transform remote)
    {
        return remotePlayers.TryGetValue(playerId ?? "", out remote) && remote != null;
    }

    public void RemoveRemotePlayer(string playerId)
    {
        skins.Remove(playerId);

        if (!remotePlayers.TryGetValue(playerId, out Transform remote))
            return;

        remotePlayers.Remove(playerId);

        if (remote != null)
        {
            Destroy(remote.gameObject);
        }
    }
}
