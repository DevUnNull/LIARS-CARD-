using UnityEngine;

/// <summary>Bartender "sống": thở nhẹ, đầu quay theo camera, tay lau ly.</summary>
public class BartenderIdle : MonoBehaviour
{
    [SerializeField] private Transform head;
    [SerializeField] private Transform body;
    [SerializeField] private Transform wipeArm;
    [SerializeField] private float maxHeadYaw = 50f;

    private Quaternion headRest, armRest;
    private Vector3 bodyScale;

    private void Awake()
    {
        if (head != null) headRest = head.localRotation;
        if (wipeArm != null) armRest = wipeArm.localRotation;
        if (body != null) bodyScale = body.localScale;
    }

    private void LateUpdate()
    {
        float t = Time.time;
        if (body != null) body.localScale = bodyScale * (1f + Mathf.Sin(t * 1.6f) * 0.012f);
        if (wipeArm != null) wipeArm.localRotation = armRest * Quaternion.Euler(Mathf.Sin(t * 3f) * 8f, 0f, Mathf.Cos(t * 3f) * 6f);

        var cam = Camera.main;
        if (head == null || cam == null) return;
        Vector3 d = cam.transform.position - head.position;
        float yaw = Mathf.Clamp(Mathf.DeltaAngle(transform.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg), -maxHeadYaw, maxHeadYaw);
        head.localRotation = Quaternion.Slerp(head.localRotation, headRest * Quaternion.Euler(0f, yaw, 0f), Time.deltaTime * 4f);
    }
}
