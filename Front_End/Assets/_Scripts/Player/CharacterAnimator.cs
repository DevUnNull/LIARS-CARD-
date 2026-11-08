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

    [Header("Quay Đầu")]
    [Tooltip("Tốc độ đầu đuổi theo góc nhìn (càng lớn càng bám sát). Khuyến nghị 10 - 20.")]
    [SerializeField, Min(1f)] private float headFollow = 14f;
    [Tooltip("Tỉ lệ góc nhìn truyền vào xương đầu (1 = quay đúng bằng góc camera).")]
    [SerializeField, Range(0f, 1f)] private float headWeight = 0.85f;

    private Transform model;
    private float standY;
    private Vector3 lastPos;
    private float speed;
    private bool alive = true;
    private string current;
    private ChairItem[] chairs;
    private Transform head, headEnd;
    private float headYaw, headPitch, targetYaw, targetPitch;

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
        head = headEnd = null;
        current = null;
        foreach (var t in model.GetComponentsInChildren<Transform>())
        {
            if (t.name == "head") head = t;
            else if (t.name == "head_end") headEnd = t;
        }
    }

    private void Start() => chairs = FindObjectsByType<ChairItem>(FindObjectsSortMode.None);

    public void SetAlive(bool value) => alive = value;

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

        ChairItem chair = alive ? FindSeat(p) : null;
        Vector3 lp = model.localPosition;
        lp.y = chair != null ? chair.SeatTopY - p.y - sitHipHeight : standY;
        model.localPosition = lp;

        var gun = LiarsCard.Cards3D.RevolverGun.Instance;
        bool holdingGun = alive && gun != null && gun.IsHeldBy(transform);
        Play(!alive ? "die" : holdingGun ? "gun" : chair != null ? "sit" : speed > walkSpeedThreshold ? "walk" : "idle");
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
        current = state;
        animator.CrossFadeInFixedTime(state, crossFade);
    }

    [ContextMenu("Test: Chết")]
    private void DebugDie() => SetAlive(false);

    [ContextMenu("Test: Hồi Sinh")]
    private void DebugRevive() => SetAlive(true);
}
