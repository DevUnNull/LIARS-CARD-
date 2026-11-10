using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mở game là full màn hình theo độ phân giải gốc của màn hình,
/// và mọi Canvas gốc đều co giãn theo màn hình (16:9, 16:10, 21:9, 4:3...).
/// </summary>
public static class ScreenSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
#if !UNITY_EDITOR
        // Unity nhớ chế độ cửa sổ của lần chạy trước, nên ép lại full màn hình mỗi lần mở.
        var res = Screen.currentResolution;
        Screen.SetResolution(res.width, res.height, FullScreenMode.FullScreenWindow);
#endif
        SceneManager.sceneLoaded += (_, __) => FitCanvases();
    }

    // shortcut: chỉ xử lý Canvas có sẵn lúc load scene; Canvas tạo runtime phải tự thêm CanvasScaler (như MirrorItem).
    private static void FitCanvases()
    {
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // isRootCanvas sai với object đang ẩn, nên tự kiểm tra cha
            var parent = canvas.transform.parent;
            if (parent != null && parent.GetComponentInParent<Canvas>(true) != null) continue;
            if (canvas.renderMode == RenderMode.WorldSpace) continue;
            if (!canvas.TryGetComponent(out CanvasScaler scaler)) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
