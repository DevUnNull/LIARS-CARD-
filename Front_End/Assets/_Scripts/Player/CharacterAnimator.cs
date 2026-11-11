using UnityEngine;

/// <summary>
/// Chọn animation idle / walk / sit / die cho model nhân vật.
/// Mạng chỉ đồng bộ vị trí + hướng, nên trạng thái được suy ra: đi = tốc độ di chuyển,
/// ngồi = đang đứng đúng chỗ một ChairItem, chết = SetAlive(false) từ trạng thái trận.
/// </summary>
public class CharacterAnimator : MonoBehaviour
{
    [Header("Tham Chiếu")]
    [Tooltip("Animator trên model nhân vật (child CharacterModel).")]
    [SerializeField] private Animator animator;

    [Header("Cài Đặt Trạng Thái")]
    [Tooltip("Tốc độ ngang tối thiểu để chuyển sang walk (mét/giây). Khuyến nghị 0.2 - 0.5.")]
    [SerializeField, Min(0f)] private float walkSpeedThreshold = 0.3f;
    [Tooltip("Khoảng cách ngang tối đa tới tâm ghế để coi là đang ngồi (mét).")]
    [SerializeField, Min(0f)] private float seatRadius = 0.35f;
    [Tooltip("Độ cao hông so với gốc model khi chơi clip sit (mét, đã nhân scale model).")]
    [SerializeField] private float sitHipHeight = 0.07f;
    [Tooltip("Thời gian chuyển giữa 2 animation (giây).")]
    [SerializeField, Range(0f, 0.5f)] private float crossFade = 0.15f;

    [Header("Âm Thanh")]
    [Tooltip("Quãng đường đi được giữa 2 tiếng bước chân (mét). Khuyến nghị 1.2 - 1.8.")]
    [SerializeField, Min(0.1f)] private float stepDistance = 1.5f;
    [Tooltip("Âm lượng tiếng bước chân (0 - 1).")]
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.45f;

    [Header("Quay Đầu")]
    [Tooltip("Tốc độ đầu đuổi theo góc nhìn (càng lớn càng bám sát). Khuyến nghị 10 - 20.")]
    [SerializeField, Min(1f)] private float headFollow = 14f;
    [Tooltip("Tỉ lệ góc nhìn truyền vào xương đầu (1 = quay đúng bằng góc camera).")]
    [SerializeField, Range(0f, 1f)] private float headWeight = 0.85f;

    private Transform model;
    private float standY;
    private Vector3 lastPos;
    private float speed;
    private float stepTravel;
    private bool alive = true;
    private string current;
    private ChairItem[] chairs;
    private Transform head, headEnd, armL, armR;
    private Vector3 tipL, tipR;   // đầu bàn tay trong toạ độ xương tay
    private float headYaw, headPitch, targetYaw, targetPitch;
    private int holdCards;   // 0 = không cầm, 1 = cầm trước ngực, 2 = giơ lên xem

    private void Reset() => animator = GetComponentInChildren<Animator>();

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        standY = animator.transform.localPosition.y;
        lastPos = transform.position;
        Bind(animator);
    }

    /// <summary>Model nhân vật hiện tại (child CharacterModel).</summary>
    public Animator Animator => animator;

    /// <summary>Gắn sang model mới khi đổi skin (PlayerSkin gọi).</summary>
    public void Bind(Animator newAnimator)
    {
        animator = newAnimator;
        model = animator.transform;
        head = headEnd = armL = armR = null;
        current = null;
        foreach (var t in model.GetComponentsInChildren<Transform>())
        {
            if (t.name == "head") head = t;
            else if (t.name == "head_end") headEnd = t;
            else if (t.name == "arm-left") armL = t;
            else if (t.name == "arm-right") armR = t;
        }
        if (armL != null) tipL = ArmTipLocal(model, armL);
        if (armR != null) tipR = ArmTipLocal(model, armR);
    }

    /// <summary>Bài trên tay (RemoteHands gọi): 0 = không cầm, 1 = cầm trước ngực (sitHold), 2 = giơ lên xem (sitPeek).</summary>
    public void SetHoldCards(int mode) => holdCards = mode;

    /// <summary>Điểm giữa 2 bàn tay khi đang chơi clip cầm bài (để cụm bài nằm đúng trong tay).</summary>
    public bool TryGetHandsCenter(out Vector3 center)
    {
        bool ok = armL != null && armR != null && current != null && current.StartsWith("sit") && current != "sit";
        center = ok ? (armL.TransformPoint(tipL) + armR.TransformPoint(tipR)) * 0.5f : default;
        return ok;
    }

    /// <summary>
    /// Đầu bàn tay (toạ độ local của xương tay) = đỉnh mesh xa vai nhất trong số đỉnh gắn vào xương này.
    /// Cần vì xương arm→arm_end của model Kenney chĩa ngang, không trùng cánh tay nhìn thấy.
    /// </summary>
    public static Vector3 ArmTipLocal(Transform model, Transform arm)
    {
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            int bi = System.Array.IndexOf(smr.bones, arm);
            if (bi < 0) continue;
            var baked = new Mesh();
            smr.BakeMesh(baked);   // đỉnh ra đúng kích thước thật (model scale 100): chỉ còn cộng vị trí + xoay
            var verts = baked.vertices;
            var weights = smr.sharedMesh.boneWeights;
            var locals = new System.Collections.Generic.List<Vector3>();
            float maxSq = 0f;
            for (int i = 0; i < verts.Length; i++)
            {
                if (weights[i].boneIndex0 != bi || weights[i].weight0 < 0.5f) continue;
                Vector3 local = arm.InverseTransformPoint(smr.transform.position + smr.transform.rotation * verts[i]);
                locals.Add(local);
                maxSq = Mathf.Max(maxSq, local.sqrMagnitude);
            }
            // Tâm cụm đỉnh ở đầu tay (xa vai >= 80%): lấy 1 đỉnh góc thì 2 tay lệch nhau
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var l in locals)
                if (l.sqrMagnitude >= 0.64f * maxSq) { sum += l; n++; }
            Vector3 tip = n > 0 ? sum / n : Vector3.zero;
            if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked);   // editor script cũng gọi hàm này
            return tip;
        }
        return Vector3.zero;
    }

    private void Start() => chairs = FindObjectsByType<ChairItem>(FindObjectsSortMode.None);

    public void SetAlive(bool value) => alive = value;

    private bool celebrating;
    /// <summary>Người thắng: đứng dậy giơ tay ăn mừng (clip "cheer") cho tới khi tắt.</summary>
    public void SetCelebrating(bool value) => celebrating = value;

    /// <summary>Góc nhìn của người chơi: yaw so với thân (+ = phải), pitch (+ = cúi). Local lấy từ chuột, remote từ mạng.</summary>
    public void SetHeadLook(float yaw, float pitch)
    {
        targetYaw = yaw;
        targetPitch = pitch;
    }

    // Chạy sau Animator: cộng góc nhìn vào xương đầu (quanh trục của thân) để thấy gật / lắc đầu
    private void LateUpdate()
    {
        float k = 1f - Mathf.Exp(-headFollow * Time.deltaTime);
        headYaw = Mathf.Lerp(headYaw, targetYaw, k);
        headPitch = Mathf.Lerp(headPitch, targetPitch, k);

        if (head == null || !alive) return;

        Transform body = transform;
        head.rotation = Quaternion.AngleAxis(headYaw * headWeight, body.up)
                      * Quaternion.AngleAxis(headPitch * headWeight, body.right)
                      * head.rotation;
    }

    /// <summary>Tâm đầu của model (để RevolverGun dí súng đúng vào đầu nhân vật thay vì độ cao camera).</summary>
    public bool TryGetHeadCenter(out Vector3 center)
    {
        center = head != null && headEnd != null ? (head.position + headEnd.position) * 0.5f : default;
        return head != null && headEnd != null;
    }

    private void Update()
    {
        Vector3 p = transform.position;
        Vector3 d = p - lastPos; d.y = 0f;
        lastPos = p;
        if (Time.deltaTime > 0f) speed = Mathf.Lerp(speed, d.magnitude / Time.deltaTime, 10f * Time.deltaTime);

        if (current == "walk")
        {
            stepTravel += Mathf.Min(d.magnitude, 0.5f);   // bỏ qua cú dịch chuyển tức thời (ngồi xuống, hồi sinh)
            if (stepTravel >= stepDistance)
            {
                stepTravel = 0f;
                GameAudio.PlayAt("Footstep", p, footstepVolume, 0f, 15f);
            }
        }

        bool cheer = alive && celebrating;
        ChairItem chair = alive && !cheer ? FindSeat(p) : null;
        Vector3 lp = model.localPosition;
        lp.y = chair != null ? chair.SeatTopY - p.y - sitHipHeight : standY;
        model.localPosition = lp;

        var gun = LiarsCard.Cards3D.RevolverGun.Instance;
        bool holdingGun = alive && gun != null && gun.IsHeldBy(transform);
        Play(!alive ? "die" : cheer ? "cheer" : holdingGun ? "gun" : chair != null ? (holdCards == 2 ? "sitPeek" : holdCards == 1 ? "sitHold" : "sit") : speed > walkSpeedThreshold ? "walk" : "idle");
    }

    private ChairItem FindSeat(Vector3 p)
    {
        if (chairs == null) return null;
        foreach (var c in chairs)
        {
            if (c == null) continue;
            Vector3 o = c.transform.position - p; o.y = 0f;
            if (o.sqrMagnitude < seatRadius * seatRadius && p.y > c.SeatTopY) return c;
        }
        return null;
    }

    private void Play(string state)
    {
        if (state == current) return;

        // Cầm súng vẫn là đang ngồi; lần đầu (vừa xuất hiện) thì không kêu
        bool wasSeated = current != null && (current.StartsWith("sit") || current == "gun");
        bool seated = state.StartsWith("sit") || state == "gun";
        if (current != null && seated != wasSeated && state != "die")
            GameAudio.PlayAt(seated ? "SitDown" : "StandUp", transform.position, 0.8f, 0f, 15f);

        current = state;
        animator.CrossFadeInFixedTime(state, crossFade);
    }

    [ContextMenu("Test: Chết")]
    private void DebugDie() => SetAlive(false);

    [ContextMenu("Test: Hồi Sinh")]
    private void DebugRevive() => SetAlive(true);
}
