using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Gương phẳng: camera phụ đặt ở vị trí đối xứng của mắt người chơi qua mặt kính, dùng frustum lệch trục
/// đúng bằng khung kính rồi render vào RenderTexture dán lên kính (UV lật ngang). Không cần shader riêng.
/// Khi camera gương render, thân người chơi local (bình thường chỉ đổ bóng) được hiện lên để tự soi được.
/// </summary>
public class PlanarMirror : MonoBehaviour
{
    [Header("Tham Chiếu")]
    [Tooltip("Renderer mặt kính (UV 0..1 phủ kín, (0,0) = góc dưới-trái nhìn từ phía trước).")]
    [SerializeField] private Renderer glass;

    [Header("Cài Đặt")]
    [Tooltip("Chiều cao RenderTexture (pixel). Khuyến nghị 512 - 1024.")]
    [SerializeField, Range(256, 2048)] private int resolution = 768;
    [Tooltip("Chỉ render gương khi người chơi ở gần hơn khoảng này (mét) để tiết kiệm hiệu năng.")]
    [SerializeField, Min(1f)] private float activeDistance = 9f;
    [Tooltip("Độ tối nhẹ của kính (1 = phản chiếu nguyên màu).")]
    [SerializeField, Range(0.5f, 1f)] private float tint = 0.9f;

    private Camera mirrorCam;
    private RenderTexture rt;
    private Vector3 llLocal, lrLocal, ulLocal;
    private Renderer[] ownRenderers;
    private readonly List<Renderer> shownBody = new();

    private void Awake()
    {
        FindGlassCorners();
        ownRenderers = GetComponentsInChildren<Renderer>();

        Vector3 size = glass.transform.TransformVector(lrLocal - llLocal);
        float aspect = size.magnitude / glass.transform.TransformVector(ulLocal - llLocal).magnitude;
        rt = new RenderTexture(Mathf.RoundToInt(resolution * aspect), resolution, 24) { name = "MirrorRT" };

        var go = new GameObject("MirrorCamera");
        go.transform.SetParent(transform, false);
        mirrorCam = go.AddComponent<Camera>();
        mirrorCam.targetTexture = rt;
        mirrorCam.enabled = false;
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;
        data.requiresDepthTexture = false;
        data.requiresColorTexture = false;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "MirrorGlass" };
        mat.SetTexture("_BaseMap", rt);
        mat.SetColor("_BaseColor", new Color(tint, tint, tint, 1f));
        mat.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));   // ảnh camera nhìn từ sau kính -> lật ngang
        mat.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
        glass.sharedMaterial = mat;
        glass.shadowCastingMode = ShadowCastingMode.Off;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        RenderPipelineManager.endCameraRendering += OnEndCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
        if (mirrorCam != null) mirrorCam.enabled = false;
    }

    private void OnDestroy()
    {
        if (rt != null) rt.Release();
    }

    // Lấy 3 góc kính theo UV để không phụ thuộc trục xuất FBX
    private void FindGlassCorners()
    {
        Mesh mesh = glass.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] v = mesh.vertices;
        Vector2[] uv = mesh.uv;
        float bLL = float.MaxValue, bLR = float.MaxValue, bUL = float.MaxValue;
        for (int i = 0; i < v.Length; i++)
        {
            float dLL = (uv[i] - new Vector2(0, 0)).sqrMagnitude;
            float dLR = (uv[i] - new Vector2(1, 0)).sqrMagnitude;
            float dUL = (uv[i] - new Vector2(0, 1)).sqrMagnitude;
            if (dLL < bLL) { bLL = dLL; llLocal = v[i]; }
            if (dLR < bLR) { bLR = dLR; lrLocal = v[i]; }
            if (dUL < bUL) { bUL = dUL; ulLocal = v[i]; }
        }
    }

    private void LateUpdate()
    {
        Camera eye = Camera.main;
        if (eye == null) { mirrorCam.enabled = false; return; }

        Transform g = glass.transform;
        Vector3 pa = g.TransformPoint(llLocal);   // dưới-trái (nhìn từ trước)
        Vector3 pb = g.TransformPoint(lrLocal);   // dưới-phải
        Vector3 pc = g.TransformPoint(ulLocal);   // trên-trái
        Vector3 vr = (pb - pa).normalized;
        Vector3 vu = (pc - pa).normalized;
        Vector3 n = Vector3.Cross(vu, vr).normalized;    // hướng từ kính ra phòng

        Vector3 pe = eye.transform.position;
        float side = Vector3.Dot(pe - pa, n);
        Vector3 center = (pb + pc) * 0.5f;
        bool visible = side > 0.05f && (pe - center).sqrMagnitude < activeDistance * activeDistance;
        mirrorCam.enabled = visible;
        if (!visible) return;

        // Mắt ảo phía sau kính, nhìn ra phòng; với camera này "phải" = -vr nên góc dưới-trái ảnh là pb
        Vector3 pv = pe - 2f * side * n;
        mirrorCam.transform.SetPositionAndRotation(pv, Quaternion.LookRotation(n, vu));

        float d = side;                                // khoảng cách mắt ảo -> mặt kính
        float near = Mathf.Max(0.01f, d - 0.005f);     // near plane trùng mặt kính => cắt tường sau gương
        float k = near / d;
        Vector3 camRight = -vr;
        float l = Vector3.Dot(pb - pv, camRight) * k;
        float r = Vector3.Dot(pa - pv, camRight) * k;
        float b = Vector3.Dot(pa - pv, vu) * k;
        float t = Vector3.Dot(pc - pv, vu) * k;

        mirrorCam.nearClipPlane = near;
        mirrorCam.farClipPlane = eye.farClipPlane;
        mirrorCam.cullingMask = eye.cullingMask;
        mirrorCam.clearFlags = eye.clearFlags;
        mirrorCam.backgroundColor = eye.backgroundColor;
        mirrorCam.projectionMatrix = Matrix4x4.Frustum(l, r, b, t, near, mirrorCam.farClipPlane);
    }

    private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != mirrorCam) return;

        // Không render chính khung/kính gương (kính đang là texture đích)
        foreach (var r in ownRenderers) if (r != null) r.enabled = false;
        NameTag.BeginMirror(cam);

        // Hiện thân người chơi local trong gương
        var player = PlayerController.Instance;
        if (player == null) return;
        foreach (var r in player.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (r.shadowCastingMode != ShadowCastingMode.ShadowsOnly) continue;
            r.shadowCastingMode = ShadowCastingMode.On;
            shownBody.Add(r);
        }
    }

    private void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != mirrorCam) return;

        foreach (var r in ownRenderers) if (r != null) r.enabled = true;
        NameTag.EndMirror();
        foreach (var r in shownBody) if (r != null) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        shownBody.Clear();
    }
}
