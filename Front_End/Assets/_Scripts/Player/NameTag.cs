using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Tên hiển thị nổi trên đầu nhân vật, luôn quay về camera đang render.
/// Người khác: luôn hiện. Chính mình (góc nhìn thứ nhất): chỉ hiện trong gương và cảnh quay người thắng.
/// </summary>
public class NameTag : MonoBehaviour
{
    private const float AboveHead = 0.28f;
    private static readonly List<NameTag> all = new List<NameTag>();

    private string playerId;
    private bool isLocal;
    private TextMeshPro text;
    private CharacterAnimator anim;

    /// <summary>local = tag của chính mình (playerId lấy từ phiên đăng nhập).</summary>
    public void Init(string id, bool local = false)
    {
        playerId = id;
        isLocal = local;
        anim = GetComponent<CharacterAnimator>();
        var go = new GameObject("NameTag");
        go.transform.SetParent(transform, false);
        text = go.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 1.6f;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enabled = !local;
        all.Add(this);
    }

    private void OnDestroy() => all.Remove(this);

    private void LateUpdate() => Face(Camera.main, false);

    private void Face(Camera cam, bool mirrored)
    {
        if (text == null || cam == null) return;
        string id = isLocal && TableSessionManager.Instance != null ? TableSessionManager.Instance.MyPlayerId : playerId;
        text.text = PlayerNames.Of(id);   // tên có thể tới sau khi avatar đã xuất hiện
        Vector3 pos = (anim != null && anim.TryGetHeadCenter(out var head) ? head : transform.position + Vector3.up * 0.6f) + Vector3.up * AboveHead;
        Vector3 away = pos - cam.transform.position;
        // Ảnh gương bị lật ngang -> quay mặt sau chữ về camera gương để sau khi lật vẫn đọc xuôi
        text.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mirrored ? -away : away, Vector3.up));
    }

    /// <summary>Camera gương bắt đầu render: tên quay về gương, hiện cả tên mình.</summary>
    public static void BeginMirror(Camera mirrorCam)
    {
        foreach (var t in all)
        {
            t.Face(mirrorCam, true);
            if (t.isLocal && t.text != null) t.text.enabled = true;
        }
    }

    public static void EndMirror()
    {
        foreach (var t in all)
        {
            t.Face(Camera.main, false);
            if (t.isLocal && t.text != null) t.text.enabled = localShown;
        }
    }

    private static bool localShown;
    /// <summary>Cảnh quay người thắng là chính mình: hiện tên mình.</summary>
    public static void ShowLocal(bool show)
    {
        localShown = show;
        foreach (var t in all)
            if (t.isLocal && t.text != null) t.text.enabled = show;
    }
}
