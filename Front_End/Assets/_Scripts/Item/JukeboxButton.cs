using System.Collections;
using UnityEngine;

/// <summary>
/// Một nút bấm trên máy Jukebox (phát/dừng, bài tiếp, bài trước, tăng/giảm âm lượng).
/// Người chơi nhắm vào nút (raycast) và nhấn E. Nút lún vào một chút khi bấm.
/// </summary>
[RequireComponent(typeof(Collider))]
public class JukeboxButton : Item
{
    [Header("Chức Năng")]
    [Tooltip("Lệnh mà nút này gửi cho Jukebox.")]
    [SerializeField] private JukeboxCommand command = JukeboxCommand.PlayStop;

    [Header("Hiệu Ứng Bấm")]
    [Tooltip("Độ lún của nút khi bấm (mét), theo hướng ngược trục Z (forward) của nút.")]
    [SerializeField, Min(0f)] private float pressDepth = 0.015f;

    [Tooltip("Thời gian nút giữ trạng thái lún (giây).")]
    [SerializeField, Min(0f)] private float pressTime = 0.12f;

    private Vector3 restPosition;

    private void Awake() => restPosition = transform.localPosition;

    public override void OnInteract()
    {
        base.OnInteract();
        if (command == JukeboxCommand.Ended) return;   // lệnh nội bộ, không gán cho nút

        JukeboxItem.Command(command);
        StopAllCoroutines();
        StartCoroutine(Press());
    }

    private IEnumerator Press()
    {
        transform.localPosition = restPosition;
        transform.position -= transform.forward * pressDepth;
        yield return new WaitForSeconds(pressTime);
        transform.localPosition = restPosition;
    }

    [ContextMenu("Test: Bấm Nút")]
    private void DebugPress() => OnInteract();
}
