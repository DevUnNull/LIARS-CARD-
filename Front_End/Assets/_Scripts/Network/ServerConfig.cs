using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Địa chỉ server. Lúc mở game tải server.json trên GitHub để đổi URL tunnel mà không cần build lại.
/// Tải lỗi thì dùng giá trị mặc định bên dưới.
/// </summary>
public static class ServerConfig
{
    // Editor = test local: chạy Back_End từ IDE, profile http (API :5280, UDP :9050). Bản build = server online.
#if UNITY_EDITOR
    public const bool UseOnlineServer = false;
#else
    public const bool UseOnlineServer = true;
#endif
    // shortcut: raw.githubusercontent cache ~5 phút, sửa server.json xong đợi vài phút client mới thấy
    public const string ConfigUrl = "https://raw.githubusercontent.com/DevUnNull/LIARS-CARD-/main/server.json";

    public static string ApiBaseUrl = UseOnlineServer ? "https://diamonds-travis-consult-buddy.trycloudflare.com" : "http://127.0.0.1:5280";
    public static string UdpHost = UseOnlineServer ? "chords-duty.tun.ply.gg" : "127.0.0.1";
    public static int UdpPort = UseOnlineServer ? 12475 : 9050;

    public static bool IsSet => !string.IsNullOrWhiteSpace(ApiBaseUrl);
    public static bool Ready { get; private set; } = !UseOnlineServer;

    [Serializable]
    private class Data { public string apiBaseUrl; public string udpHost; public int udpPort; }

    public static IEnumerator Fetch()
    {
        if (Ready) yield break;
        using (var req = UnityWebRequest.Get(ConfigUrl + "?t=" + DateTime.UtcNow.Ticks))
        {
            req.timeout = 8;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var d = JsonUtility.FromJson<Data>(req.downloadHandler.text);
                    if (!string.IsNullOrWhiteSpace(d.apiBaseUrl)) ApiBaseUrl = d.apiBaseUrl.TrimEnd('/');
                    if (!string.IsNullOrWhiteSpace(d.udpHost)) UdpHost = d.udpHost;
                    if (d.udpPort > 0) UdpPort = d.udpPort;
                }
                catch (Exception e) { Debug.LogWarning("server.json sai định dạng: " + e.Message); }
            }
            else Debug.LogWarning("Không tải được server.json, dùng địa chỉ mặc định: " + req.error);
        }
        Ready = true;
        Debug.Log($"[ServerConfig] API={ApiBaseUrl} UDP={UdpHost}:{UdpPort}");
    }
}
