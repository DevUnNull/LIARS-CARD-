using System;
using UnityEngine;

/// <summary>
/// Danh tính người chơi trên bản Photon: chỉ có tên hiển thị, không tài khoản / mật khẩu.
/// Mỗi lần mở game sinh một playerId mới (2 cửa sổ game trên cùng máy vẫn là 2 người khác nhau).
/// </summary>
[DefaultExecutionOrder(-200)]
public class AuthApiClient : Singleton<AuthApiClient>
{
    public const int MaxNameLength = 20;

    public string CurrentUserId { get; private set; }
    public string DisplayName { get; private set; }

    // shortcut: không còn JWT, giữ tên AccessToken = playerId cho các chỗ đang kiểm tra "đã vào game chưa"
    public string AccessToken => CurrentUserId;

    protected override void Awake()
    {
        base.Awake();
        if (Instance == this) DontDestroyOnLoad(gameObject);
    }

    /// <summary>Đặt tên hiển thị. Trả lỗi (tiếng Việt) nếu tên không hợp lệ, null nếu ổn.</summary>
    public string SignIn(string displayName)
    {
        displayName = displayName?.Trim() ?? "";
        if (displayName.Length == 0) return "Nhập tên của bạn.";
        if (displayName.Length > MaxNameLength) return $"Tên tối đa {MaxNameLength} ký tự.";

        DisplayName = displayName;
        if (string.IsNullOrEmpty(CurrentUserId)) CurrentUserId = Guid.NewGuid().ToString("N");
        return null;
    }
}
