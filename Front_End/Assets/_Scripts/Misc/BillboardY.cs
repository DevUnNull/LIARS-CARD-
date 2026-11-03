using UnityEngine;

/// <summary>Xoay biển chữ quanh trục Y để luôn quay mặt về camera (biển số bàn trong quán).</summary>
public class BillboardY : MonoBehaviour
{
    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 d = transform.position - cam.transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
    }
}
