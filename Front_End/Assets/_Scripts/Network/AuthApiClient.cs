
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AuthApiClient : Singleton<AuthApiClient>
{
    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
            return;

        DontDestroyOnLoad(gameObject);
    }

    [SerializeField]
    public string baseUrl = "http://localhost:5000";

    public string AccessToken { get; private set; }
    public string RefreshToken { get; private set; }


    public void TestMeButton()
    {
        if (string.IsNullOrEmpty(AccessToken))
        {
            Debug.LogError("Chưa có Access Token. Hãy Login trước.");
            return;
        }

        StartCoroutine(TestMeRoutine());
    }

    private IEnumerator TestMeRoutine()
    {
        using (var request = UnityWebRequest.Get(
            baseUrl + "/api/auth/me"))
        {
            request.SetRequestHeader(
                "Authorization",
                "Bearer " + AccessToken);

            yield return request.SendWebRequest();

            Debug.Log("GET /api/auth/me");
            Debug.Log("HTTP Status: " + request.responseCode);
            Debug.Log("Response: " + request.downloadHandler.text);

            if (request.responseCode == 401)
            {
                Debug.LogError("Token bị từ chối.");
            }
        }
    }


    [Serializable]
    public class AuthResponse
    {
        public string message;
        public UserData user;
        public string accessToken;
        public string refreshToken;
    }

    [Serializable]
    public class UserData
    {
        public string id;
        public string email;
        public string displayName;
    }

    [Serializable]
    public class ApiError
    {
        public string message;
        public string title;
    }

    [Serializable]
    public class SitDownData
    {
        public int seatIndex;
    }

    public IEnumerator Register(
        string email,
        string displayName,
        string password,
        Action<bool, string> callback)
    {
        string json = JsonUtility.ToJson(new RegisterData
        {
            email = email.Trim(),
            displayName = displayName.Trim(),
            password = password
        });

        yield return SendRequest(
            "/api/auth/register", json, false, (success, body) =>
            {
                callback?.Invoke(
                    success,
                    success ? "Đăng ký thành công!" : GetError(body));
            });
    }

    public IEnumerator Login(
        string email,
        string password,
        Action<bool, string> callback)
    {
        string json = JsonUtility.ToJson(new LoginData
        {
            email = email.Trim(),
            password = password
        });

        yield return SendRequest(
            "/api/auth/login", json, false, (success, body) =>
            {
                if (!success)
                {
                    callback?.Invoke(false, GetError(body));
                    return;
                }

                AuthResponse response;

                try
                {
                    response = JsonUtility.FromJson<AuthResponse>(body);
                }
                catch (Exception)
                {
                    callback?.Invoke(false, "Phản hồi server không hợp lệ.");
                    return;
                }

                if (response == null ||
                    string.IsNullOrEmpty(response.accessToken) ||
                    string.IsNullOrEmpty(response.refreshToken))
                {
                    callback?.Invoke(false, "Server không trả đủ token.");
                    return;
                }

                AccessToken = response.accessToken;
                RefreshToken = response.refreshToken;

                Debug.Log("Login thành công. User ID: " +
                    (response.user != null ? response.user.id : "Không có"));

                callback?.Invoke(true, "Đăng nhập thành công!");
            });
    }

    private IEnumerator SendRequest(
        string path,
        string json,
        bool authenticated,
        Action<bool, string> callback)
    {
        using (UnityWebRequest request = new UnityWebRequest(
            baseUrl + path, "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            if (authenticated && !string.IsNullOrEmpty(AccessToken))
            {
                request.SetRequestHeader(
                    "Authorization", "Bearer " + AccessToken);
            }

            yield return request.SendWebRequest();

            bool success =
                request.result == UnityWebRequest.Result.Success;

            callback?.Invoke(
                success, request.downloadHandler?.text ?? "");
        }
    }

    private string GetError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "Không nhận được phản hồi từ server.";

        try
        {
            ApiError error = JsonUtility.FromJson<ApiError>(body);

            if (error != null)
            {
                if (!string.IsNullOrEmpty(error.message))
                    return error.message;

                if (!string.IsNullOrEmpty(error.title))
                    return error.title;
            }
        }
        catch (Exception)
        {
            // Giữ thông báo mặc định bên dưới.
        }

        return "Yêu cầu thất bại: " + body;
    }
    public IEnumerator TestMe(
    System.Action<bool, string> callback)
    {
        using (var request = UnityWebRequest.Get(
            baseUrl + "/api/auth/me"))
        {
            request.SetRequestHeader(
                "Authorization",
                "Bearer " + AccessToken);

            yield return request.SendWebRequest();

            bool success =
                request.result == UnityWebRequest.Result.Success;

            callback?.Invoke(
                success,
                request.downloadHandler.text);
        }
    }


    [Serializable]
    private class RegisterData
    {
        public string email;
        public string displayName;
        public string password;
    }

    [Serializable]
    private class LoginData
    {
        public string email;
        public string password;
    }
}
