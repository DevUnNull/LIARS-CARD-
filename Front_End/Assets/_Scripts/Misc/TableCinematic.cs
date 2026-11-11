using LiarsCard.Cards3D;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Camera Cinemachine cho bàn đang chơi của CHÍNH người chơi này (mỗi client tự chạy, không ảnh hưởng bàn khác):
/// - Winner: từ trên cao chiếu xuống người thắng đang giơ tay ăn mừng, quay chậm qua lại.
/// - Spectator: người đã bị loại xem bàn mình từ góc trái / góc phải (đổi góc có blend mượt).
/// Hết cảnh quay thì trả camera góc nhìn thứ nhất về y như cũ.
/// </summary>
public class TableCinematic : MonoBehaviour
{
    private enum Mode { Winner, Spectator }

    // Người thắng: camera cao hơn đầu và lùi ra trước mặt, chúc xuống
    private const float WinnerDistance = 2.1f;
    private const float WinnerHeight = 1.3f;
    private const float WinnerOrbitDegrees = 20f;
    private const float WinnerOrbitPeriod = 9f;
    // Người xem: 2 góc chéo hai bên bàn, cao nhìn xuống tâm bàn
    private const float SpectatorHeight = 2.2f;
    private const float SpectatorRadiusScale = 1.5f;   // so với khoảng cách mình -> tâm bàn
    private const float BlendSeconds = 1.6f;

    private static TableCinematic instance;
    private static CinemachineBrain brain;

    private Mode mode;
    private Camera cam;
    private Vector3 camLocalPos;
    private Quaternion camLocalRot;
    private float camFov;
    private CinemachineCamera startCam, winnerCam;
    private CinemachineCamera[] spectatorCams;
    private int spectatorAngle;
    private Transform winner;
    private CharacterAnimator winnerAnim;
    private SkinnedMeshRenderer[] shownBody;
    private float startTime;

    public static bool IsWinnerPlaying => instance != null && instance.mode == Mode.Winner;
    public static bool IsSpectating => instance != null && instance.mode == Mode.Spectator;
    /// <summary>0 = góc trái, 1 = góc phải.</summary>
    public static int SpectatorAngle => instance != null ? instance.spectatorAngle : 0;

    /// <summary>Người thắng ăn mừng. winner = avatar (remote hoặc chính mình).</summary>
    public static void PlayWinner(Transform winner, bool isLocal)
    {
        if (winner == null || Camera.main == null) return;
        var c = Create(Mode.Winner);
        c.winner = winner;
        if (winner.TryGetComponent(out c.winnerAnim)) c.winnerAnim.SetCelebrating(true);
        if (isLocal) c.ShowLocalBody(winner);
        c.winnerCam = c.NewCam("WinnerCam", 45f);
        c.WinnerShot(out var p, out var r);
        c.winnerCam.transform.SetPositionAndRotation(p, r);
        c.StartCoroutine(c.EnableNextFrame(c.winnerCam));
    }

    /// <summary>Người đã bị loại: xem bàn đang chơi từ góc trái/phải.</summary>
    public static void PlaySpectator()
    {
        if (Camera.main == null || PlayerController.Instance == null) return;
        var c = Create(Mode.Spectator);
        c.spectatorCams = new[] { c.NewCam("SpectateLeft", 55f), c.NewCam("SpectateRight", 55f) };
        c.PlaceSpectatorCams();
        c.spectatorCams[1].enabled = false;   // mặc định góc trái
        c.StartCoroutine(c.EnableNextFrame(c.spectatorCams[0]));
    }

    /// <summary>Đổi góc xem (0 trái, 1 phải). Cinemachine tự blend giữa 2 góc.</summary>
    public static void SetSpectatorAngle(int angle)
    {
        if (!IsSpectating || angle == instance.spectatorAngle) return;
        instance.spectatorAngle = angle;
        instance.spectatorCams[1 - angle].enabled = false;
        instance.spectatorCams[angle].enabled = true;
    }

    public static void Stop()
    {
        if (instance == null) return;
        instance.Restore();
        Destroy(instance.gameObject);
        instance = null;
    }

    private static TableCinematic Create(Mode mode)
    {
        // Đang có cảnh quay (vd. đang xem thì trận kết thúc): giữ đúng hình đang thấy làm điểm bắt đầu blend
        Camera main = Camera.main;
        Vector3 pos = main.transform.position;
        Quaternion rot = main.transform.rotation;
        float fov = main.fieldOfView;
        Stop();

        var c = new GameObject("TableCinematic").AddComponent<TableCinematic>();
        instance = c;
        c.mode = mode;
        c.cam = main;
        c.camLocalPos = main.transform.localPosition;
        c.camLocalRot = main.transform.localRotation;
        c.camFov = main.fieldOfView;
        c.startTime = Time.time;

        if (brain == null || brain.gameObject != main.gameObject)
            if (!main.TryGetComponent(out brain)) brain = main.gameObject.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, BlendSeconds);
        brain.enabled = true;

        // Camera ảo trùng hình hiện tại được bật trước, camera đích bật sau (ưu tiên hơn) => blend thay vì cắt cảnh
        c.startCam = c.NewCam("StartCam", fov);
        c.startCam.transform.SetPositionAndRotation(pos, rot);
        return c;
    }

    // Cùng frame với StartCam thì brain nhảy thẳng vào camera đích (cắt cảnh); bật ở frame sau mới blend
    private System.Collections.IEnumerator EnableNextFrame(CinemachineCamera target)
    {
        target.enabled = false;
        yield return null;
        if (target != null && (spectatorCams == null || target == spectatorCams[spectatorAngle])) target.enabled = true;
    }

    private CinemachineCamera NewCam(string name, float fov)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var vcam = go.AddComponent<CinemachineCamera>();
        var lens = vcam.Lens;
        lens.FieldOfView = fov;
        lens.NearClipPlane = cam.nearClipPlane;
        lens.FarClipPlane = cam.farClipPlane;
        vcam.Lens = lens;
        return vcam;
    }

    private void ShowLocalBody(Transform body)
    {
        // Thân mình ở góc nhìn thứ nhất chỉ đổ bóng -> bật hiện để camera thấy; chỉ nhớ những cái đã đổi
        NameTag.ShowLocal(true);
        shownBody = body.GetComponentsInChildren<SkinnedMeshRenderer>();
        for (int i = 0; i < shownBody.Length; i++)
        {
            if (shownBody[i].shadowCastingMode == ShadowCastingMode.ShadowsOnly) shownBody[i].shadowCastingMode = ShadowCastingMode.On;
            else shownBody[i] = null;
        }
    }

    private void WinnerShot(out Vector3 pos, out Quaternion rot)
    {
        Vector3 head = winnerAnim != null && winnerAnim.TryGetHeadCenter(out var h) ? h : winner.position + Vector3.up * 1.3f;
        Vector3 fwd = winner.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        float orbit = Mathf.Sin((Time.time - startTime) * Mathf.PI * 2f / WinnerOrbitPeriod) * WinnerOrbitDegrees;
        Vector3 dir = Quaternion.AngleAxis(orbit, Vector3.up) * fwd.normalized;
        Vector3 focus = head - Vector3.up * 0.3f;   // thấp hơn đầu một chút để khung hình có cả tay giơ lên
        pos = focus + dir * WinnerDistance + Vector3.up * WinnerHeight;
        rot = Quaternion.LookRotation(focus - pos, Vector3.up);
    }

    // Trái/phải tính theo hướng mình đang ngồi nhìn vào bàn
    private void PlaceSpectatorCams()
    {
        Vector3 center = TableCenterPile3D.GetTableTopCenter();
        Vector3 me = PlayerController.Instance.transform.position;
        Vector3 toCenter = center - me; toCenter.y = 0f;
        float dist = Mathf.Max(toCenter.magnitude, 1.2f);
        Vector3 fwd = toCenter.sqrMagnitude > 0.001f ? toCenter.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        float radius = dist * SpectatorRadiusScale;
        Vector3 look = center + Vector3.up * 0.2f;
        for (int i = 0; i < 2; i++)
        {
            Vector3 side = i == 0 ? -right : right;
            Vector3 dir = (side - fwd * 0.6f).normalized;   // chéo về phía mình để thấy cả bàn lẫn người ngồi đối diện
            Vector3 pos = center + dir * radius + Vector3.up * SpectatorHeight;
            spectatorCams[i].transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
        }
    }

    private void LateUpdate()
    {
        if (mode == Mode.Winner)
        {
            if (winner == null) { Stop(); return; }
            WinnerShot(out var pos, out var rot);
            winnerCam.transform.SetPositionAndRotation(pos, rot);
        }
    }

    // Chạy ngay (không đợi Destroy cuối frame) để cảnh quay mới tạo cùng frame đọc đúng vị trí camera gốc
    private void Restore()
    {
        if (winnerAnim != null) winnerAnim.SetCelebrating(false);
        if (shownBody != null)
        {
            NameTag.ShowLocal(false);
            foreach (var r in shownBody)
                if (r != null) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        if (brain != null) brain.enabled = false;
        if (cam != null)
        {
            cam.transform.localPosition = camLocalPos;
            cam.transform.localRotation = camLocalRot;
            cam.fieldOfView = camFov;
        }
    }
}
