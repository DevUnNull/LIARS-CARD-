using UnityEngine;

/// <summary>
/// Đổi model nhân vật (skin) trên Player / RemotePlayer, giữ nguyên Animator Controller nên đủ animation
/// idle / walk / sit / die / gun. Người chơi local lưu skin vào PlayerPrefs và gửi cho server.
/// </summary>
[RequireComponent(typeof(CharacterAnimator))]
public class PlayerSkin : MonoBehaviour
{
    public const string PrefKey = "skin_id";

    [Header("Tham Chiếu")]
    [Tooltip("Danh sách skin dùng chung (SkinCatalog asset).")]
    [SerializeField] private SkinCatalog catalog;

    public SkinCatalog Catalog => catalog;
    // Prefab đã mang sẵn model của skin 0 (Character.fbx)
    public int Current { get; private set; } = 0;

    public static int SavedSkin => PlayerPrefs.GetInt(PrefKey, 0);

    private void Start()
    {
        // Người chơi local: mặc skin đã lưu. Remote: chờ RemotePlayerManager gọi SetSkin.
        if (TryGetComponent<PlayerController>(out _)) SetSkin(SavedSkin);
    }

    /// <summary>Thay model. ID ngoài phạm vi (máy khác có catalog mới hơn) thì bỏ qua.</summary>
    public void SetSkin(int id)
    {
        if (catalog == null || id < 0 || id >= catalog.Count || id == Current || catalog.models[id] == null) return;

        var anim = GetComponent<CharacterAnimator>();
        Animator oldAnimator = anim.Animator;
        Transform old = oldAnimator.transform;

        GameObject model = Instantiate(catalog.models[id], old.parent);
        model.name = old.name;
        model.transform.SetLocalPositionAndRotation(old.localPosition, old.localRotation);
        model.transform.localScale = old.localScale;
        SetLayer(model.transform, old.gameObject.layer);

        // Giữ chế độ đổ bóng của model cũ (local = ShadowsOnly để camera góc nhìn thứ nhất không thấy thân mình)
        var oldRenderer = old.GetComponentInChildren<Renderer>();
        if (oldRenderer != null)
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = oldRenderer.shadowCastingMode;

        if (!model.TryGetComponent<Animator>(out var animator)) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;
        animator.applyRootMotion = oldAnimator.applyRootMotion;
        animator.cullingMode = oldAnimator.cullingMode;

        old.gameObject.SetActive(false);
        Destroy(old.gameObject);
        anim.Bind(animator);
        Current = id;
    }

    /// <summary>Người chơi local chốt skin: lưu lại và báo cho mọi người.</summary>
    public void SaveAndBroadcast()
    {
        PlayerPrefs.SetInt(PrefKey, Current);
        PlayerPrefs.Save();
        if (NetworkManager.Instance != null) NetworkManager.Instance.SendSkin((byte)Current);
    }

    private static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayer(c, layer);
    }
}
