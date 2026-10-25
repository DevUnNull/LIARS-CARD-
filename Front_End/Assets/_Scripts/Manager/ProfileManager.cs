
using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ProfileManager : MonoBehaviour
{
    [Header("Backend")]
    [SerializeField]
    private string apiBaseUrl = "http://localhost:5280";

    private string accessToken;

    [Header("Profile UI")]
    [SerializeField] private TMP_Text emailText;
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_InputField displayNameInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Image characterAvatarImage;
    [SerializeField] private TMP_Text characterNameText;

    [Serializable]
    private class ProfileResponse
    {
        public string id;
        public string email;
        public string displayName;
        public string createdAt;
        public string updatedAt;
        public CharacterData character;
    }

    [Serializable]
    private class CharacterData
    {
        public string id;
        public string name;
        public string avatarKey;
        public string description;
    }

    [Serializable]
    private class UpdateProfileRequest
    {
        public string displayName;
    }

    [Serializable]
    private class UpdateProfileResponse
    {
        public string message;
        public ProfileResponse profile;
    }

    [Serializable]
    private class ErrorResponse
    {
        public string message;
    }

    private void Start()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveProfile);

        if (refreshButton != null)
            refreshButton.onClick.AddListener(LoadProfile);

        // Lấy token từ AuthApiClient dùng chung giữa các Scene.
        if (AuthApiClient.Instance == null ||
            string.IsNullOrWhiteSpace(AuthApiClient.Instance.AccessToken))
        {
            SetStatus("Bạn cần đăng nhập trước.");
            return;
        }

        accessToken = AuthApiClient.Instance.AccessToken;
        LoadProfile();
    }

    private void OnDestroy()
    {
        if (saveButton != null)
            saveButton.onClick.RemoveListener(SaveProfile);

        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(LoadProfile);
    }



    private void DisplayCharacter(string characterName, string avatarKey)
    {
        if (characterNameText != null)
        {
            characterNameText.text = string.IsNullOrWhiteSpace(characterName)
                ? "Chưa chọn nhân vật"
                : characterName;
        }

        if (characterAvatarImage == null)
            return;

        if (string.IsNullOrWhiteSpace(avatarKey))
        {
            characterAvatarImage.sprite = null;
            characterAvatarImage.enabled = false;
            return;
        }

        Sprite avatar = Resources.Load<Sprite>(
            "CharacterAvatars/" + avatarKey
        );

        if (avatar != null)
        {
            characterAvatarImage.sprite = avatar;
            characterAvatarImage.enabled = true;
        }
        else
        {
            characterAvatarImage.sprite = null;
            characterAvatarImage.enabled = false;

            Debug.LogWarning(
                "[Profile] Không tìm thấy avatar: " + avatarKey
            );
        }
    }



    public void LoadProfile()
    {
        if (!CheckToken())
            return;

        StartCoroutine(GetProfileCoroutine());
    }

    private IEnumerator GetProfileCoroutine()
    {
        SetStatus("Đang tải hồ sơ...");

        using (UnityWebRequest request = UnityWebRequest.Get(
            apiBaseUrl.TrimEnd('/') + "/api/profile"))
        {
            request.SetRequestHeader(
                "Authorization", "Bearer " + accessToken);

            yield return request.SendWebRequest();

            Debug.Log("[Profile] URL: " + request.url);
            Debug.Log("[Profile] Result: " + request.result);
            Debug.Log("[Profile] Error: " + request.error);
            Debug.Log("[Profile] HTTP: " + request.responseCode);
            Debug.Log("[Profile] Body: " +
                (request.downloadHandler != null
                    ? request.downloadHandler.text
                    : "(empty)"));

            if (request.result != UnityWebRequest.Result.Success)
            {
                HandleError(request);
                yield break;
            }

            ProfileResponse profile =
                JsonUtility.FromJson<ProfileResponse>(
                    request.downloadHandler.text);

            if (profile == null)
            {
                SetStatus("Không đọc được dữ liệu hồ sơ.");
                yield break;
            }

            if (emailText != null)
                emailText.text = profile.email;

            if (displayNameText != null)
                displayNameText.text = profile.displayName;


            if (displayNameInput != null)
                displayNameInput.text = profile.displayName;

            // Hiển thị nhân vật lấy từ Backend và MongoDB.
            if (profile.character != null)
            {
                DisplayCharacter(
                    profile.character.name,
                    profile.character.avatarKey
                );
            }
            else
            {
                DisplayCharacter("Chưa chọn nhân vật", null);
            }

            SetStatus("Tải hồ sơ thành công.");

        }
    }

    public void SaveProfile()
    {
        if (!CheckToken())
            return;

        string newName = displayNameInput != null
            ? displayNameInput.text.Trim()
            : "";

        if (newName.Length < 3 || newName.Length > 20)
        {
            SetStatus("Tên phải dài từ 3 đến 20 ký tự.");
            return;
        }

        StartCoroutine(UpdateProfileCoroutine(newName));
    }

    private IEnumerator UpdateProfileCoroutine(string newName)
    {
        SetStatus("Đang lưu hồ sơ...");

        UpdateProfileRequest body = new UpdateProfileRequest
        {
            displayName = newName
        };

        string json = JsonUtility.ToJson(body);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(
            apiBaseUrl.TrimEnd('/') + "/api/profile", "PATCH"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader(
                "Content-Type", "application/json");

            request.SetRequestHeader(
                "Authorization", "Bearer " + accessToken);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                HandleError(request);
                yield break;
            }

            UpdateProfileResponse response =
                JsonUtility.FromJson<UpdateProfileResponse>(
                    request.downloadHandler.text);

            if (response?.profile == null)
            {
                SetStatus("Đã gửi yêu cầu nhưng không đọc được phản hồi.");
                yield break;
            }

            if (displayNameText != null)
                displayNameText.text = response.profile.displayName;

            if (displayNameInput != null)
                displayNameInput.text = response.profile.displayName;

            SetStatus(response.message);
        }
    }

    private bool CheckToken()
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            SetStatus("Bạn cần đăng nhập trước.");
            return false;
        }

        return true;
    }


    private void HandleError(UnityWebRequest request)
    {
        Debug.LogError("[Profile] Request URL: " + request.url);
        Debug.LogError("[Profile] Request Result: " + request.result);
        Debug.LogError("[Profile] Request Error: " + request.error);
        Debug.LogError("[Profile] HTTP Status: " + request.responseCode);

        string body = request.downloadHandler != null
            ? request.downloadHandler.text
            : "";

        Debug.LogError("[Profile] Response Body: " + body);

        string message = !string.IsNullOrEmpty(request.error)
            ? request.error
            : "Lỗi kết nối backend.";

        if (request.responseCode == 401)
            message = "Token không hợp lệ hoặc đã hết hạn.";
        else if (request.responseCode == 404)
            message = "Không tìm thấy endpoint hoặc hồ sơ.";
        else if (request.responseCode >= 500)
            message = "Backend gặp lỗi khi xử lý yêu cầu.";

        SetStatus("HTTP " + request.responseCode + ": " + message);
    }


    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log("[Profile] " + message);
    }
}
