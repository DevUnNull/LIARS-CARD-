using System.Collections.Generic;
using UnityEngine;

public class RemotePlayerManager : Singleton<RemotePlayerManager>
{
    [SerializeField] private GameObject remotePlayerPrefab;

    private Dictionary<string, Transform> remotePlayers = new();
    // Skin có thể tới trước gói vị trí đầu tiên (lúc mới vào), nên nhớ lại để áp khi tạo remote
    private Dictionary<string, byte> skins = new();
    // Gói vị trí tới ~20 lần/giây: lưu đích rồi trượt tới mỗi frame thay vì nhảy thẳng
    private readonly Dictionary<Transform, (Vector3 pos, Quaternion rot)> targets = new();

    [Tooltip("Độ bám của người chơi khác theo vị trí mạng (càng lớn càng sát, càng nhỏ càng mượt). Khuyến nghị 12 - 20.")]
    [SerializeField, Min(1f)] private float smoothing = 15f;
    [Tooltip("Lệch quá khoảng này (mét) thì dịch chuyển thẳng, ví dụ ngồi xuống / hồi sinh.")]
    [SerializeField, Min(0.5f)] private float snapDistance = 3f;

    private void Update()
    {
        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        foreach (var kv in targets)
        {
            Transform r = kv.Key;
            if (r == null) continue;
            var (pos, rot) = kv.Value;
            if ((r.position - pos).sqrMagnitude > snapDistance * snapDistance) r.SetPositionAndRotation(pos, rot);
            else r.SetPositionAndRotation(Vector3.Lerp(r.position, pos, t), Quaternion.Slerp(r.rotation, rot, t));
        }
    }

    public void UpdateRemotePlayer(string playerId, Vector3 position, float rotationY, float headYaw = 0f, float headPitch = 0f)
    {
        if (!remotePlayers.TryGetValue(playerId, out Transform remote)
            || remote == null)
        {
            GameObject playerObject = Instantiate(remotePlayerPrefab, position, Quaternion.Euler(0f, rotationY, 0f), transform);

            playerObject.name = $"RemotePlayer_{playerId}";
            GameAudio.PlayAt("PlayerJoin", position, 0.9f, 1.5f, 40f);   // lúc mình mới vào sẽ tạo nhiều người cùng lúc -> chỉ kêu 1 lần

            remote = playerObject.transform;
            remotePlayers[playerId] = remote;

            if (skins.TryGetValue(playerId, out byte skinId) && playerObject.TryGetComponent<PlayerSkin>(out var skin))
                skin.SetSkin(skinId);
        }

        targets[remote] = (position, Quaternion.Euler(0f, rotationY, 0f));

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
        if (remote != null) targets.Remove(remote);

        if (remote != null)
        {
            GameAudio.PlayAt("PlayerLeave", remote.position, 0.9f, 1.5f, 40f);
            Destroy(remote.gameObject);
        }
    }
}
