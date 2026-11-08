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
    [Tooltip("Độ lún của nút khi bấm (mét), theo hướng Press Axis.")]
    [SerializeField, Min(0f)] private float pressDepth = 0.015f;

    [Tooltip("Hướng nút lún vào, theo trục local của nút. Nút cube cũ: (0,0,-1). Phím model Jukebox_3500: (0,1,0).")]
    [SerializeField] private Vector3 pressAxis = Vector3.back;

    [Tooltip("Thời gian nút giữ trạng thái lún (giây).")]
    [SerializeField, Min(0f)] private float pressTime = 0.12f;

    private Vector3 restPosition;

    private void Awake() => restPosition = transform.localPosition;

    public override void OnInteract()
    {
        base.OnInteract();
        if (command == JukeboxCommand.Ended) return;   // lệnh nội bộ, không gán cho nút

        JukeboxItem.Command(command);
        GameAudio.PlayAt("ButtonPress", transform.position, 0.8f, 0f, 10f);
        StopAllCoroutines();
        StartCoroutine(Press());
    }

    private IEnumerator Press()
    {
        transform.localPosition = restPosition;
        transform.position += transform.TransformDirection(pressAxis.normalized) * pressDepth;
        yield return new WaitForSeconds(pressTime);
        transform.localPosition = restPosition;
    }

    [ContextMenu("Test: Bấm Nút")]
    private void DebugPress() => OnInteract();
}
