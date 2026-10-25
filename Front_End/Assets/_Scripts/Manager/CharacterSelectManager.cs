
using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("Backend")]
    [SerializeField]
    private string apiBaseUrl = "http://localhost:5280";

    [Header("Character Buttons")]
    [SerializeField] private Button char1Button;
    [SerializeField] private Button char2Button;
    [SerializeField] private Button char3Button;
    [SerializeField] private Button char4Button;

    [Header("UI")]
    [SerializeField] private TMP_Text selectedCharacterText;
    [SerializeField] private TMP_Text statusText;

    private string accessToken;
    private bool isSaving;

    [Serializable]
    private class SelectionRequest
    {
        public string characterId;
    }

    [Serializable]
    private class SelectionResponse
    {
        public string message;
        public string userId;
        public string characterId;
        public string selectedAt;
    }

    [Serializable]
    private class ErrorResponse
    {
        public string message;
    }

    private void Start()
    {
        if (char1Button != null)
            char1Button.onClick.AddListener(SelectChar1);

        if (char2Button != null)
            char2Button.onClick.AddListener(SelectChar2);

        if (char3Button != null)
            char3Button.onClick.AddListener(SelectChar3);

        if (char4Button != null)
            char4Button.onClick.AddListener(SelectChar4);

        if (AuthApiClient.Instance == null ||
            string.IsNullOrWhiteSpace(
                AuthApiClient.Instance.AccessToken))
        {
            SetStatus("Bạn cần đăng nhập trước.");
            SetButtonsInteractable(false);
            return;
        }

        accessToken = AuthApiClient.Instance.AccessToken;

        StartCoroutine(LoadSelection());
    }

    private void OnDestroy()
    {
        if (char1Button != null)
            char1Button.onClick.RemoveListener(SelectChar1);

        if (char2Button != null)
            char2Button.onClick.RemoveListener(SelectChar2);

        if (char3Button != null)
            char3Button.onClick.RemoveListener(SelectChar3);

        if (char4Button != null)
            char4Button.onClick.RemoveListener(SelectChar4);
    }

    public void SelectChar1()
    {
        SelectCharacter("char1");
    }

    public void SelectChar2()
    {
        SelectCharacter("char2");
    }

    public void SelectChar3()
    {
        SelectCharacter("char3");
    }

    public void SelectChar4()
    {
        SelectCharacter("char4");
    }

    private void SelectCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || isSaving)
            return;

        StartCoroutine(SaveSelection(characterId));
    }

    private IEnumerator LoadSelection()
    {
        SetStatus("Đang tải nhân vật đã chọn...");

        using (UnityWebRequest request = UnityWebRequest.Get(
            apiBaseUrl.TrimEnd('/') + "/api/character-selection"))
        {
            request.SetRequestHeader(
                "Authorization", "Bearer " + accessToken);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                HandleError(request);
                yield break;
            }

            SelectionResponse response =
                JsonUtility.FromJson<SelectionResponse>(
                    request.downloadHandler.text);

            if (response == null)
            {
                SetStatus("Không đọc được dữ liệu nhân vật.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(response.characterId))
            {
                SetSelectedCharacter("Chưa chọn nhân vật");
                SetStatus("Hãy chọn một nhân vật.");
            }
            else
            {
                SetSelectedCharacter(response.characterId);
                SetStatus("Đã tải nhân vật đã chọn.");
            }
        }
    }

    private IEnumerator SaveSelection(string characterId)
    {
        isSaving = true;
        SetButtonsInteractable(false);
        SetStatus("Đang lưu lựa chọn...");

        SelectionRequest body = new SelectionRequest
        {
            characterId = characterId
        };

        string json = JsonUtility.ToJson(body);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(
            apiBaseUrl.TrimEnd('/') + "/api/character-selection",
            "PUT"))
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
            }
            else
            {
                SelectionResponse response =
                    JsonUtility.FromJson<SelectionResponse>(
                        request.downloadHandler.text);

                if (response != null &&
                    !string.IsNullOrWhiteSpace(response.characterId))
                {
                    SetSelectedCharacter(response.characterId);
                    SetStatus(
                        response.message ?? "Chọn nhân vật thành công.");
                }
                else
                {
                    SetStatus("Server trả về dữ liệu không hợp lệ.");
                }
            }
        }

        isSaving = false;
        SetButtonsInteractable(true);
    }

    private void SetSelectedCharacter(string characterId)
    {
        string displayName;

        switch (characterId)
        {
            case "char1":
                displayName = "Khủng long";
                break;

            case "char2":
                displayName = "Steve";
                break;

            case "char3":
                displayName = "AI";
                break;

            case "char4":
                displayName = "Siêu Nhân";
                break;

            default:
                displayName = characterId;
                break;
        }

        if (selectedCharacterText != null)
            selectedCharacterText.text =
                "Nhân vật đã chọn: " + displayName;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (char1Button != null)
            char1Button.interactable = interactable;

        if (char2Button != null)
            char2Button.interactable = interactable;

        if (char3Button != null)
            char3Button.interactable = interactable;

        if (char4Button != null)
            char4Button.interactable = interactable;
    }

    private void HandleError(UnityWebRequest request)
    {
        string message = request.error;

        if (!string.IsNullOrWhiteSpace(
            request.downloadHandler?.text))
        {
            try
            {
                ErrorResponse error =
                    JsonUtility.FromJson<ErrorResponse>(
                        request.downloadHandler.text);

                if (!string.IsNullOrWhiteSpace(error?.message))
                    message = error.message;
            }
            catch (Exception)
            {
                // Dùng thông báo lỗi mặc định.
            }
        }

        if (request.responseCode == 401)
            message = "Token không hợp lệ hoặc đã hết hạn.";

        SetStatus(
            "HTTP " + request.responseCode + ": " + message);

        Debug.LogError(
            "[CharacterSelect] " +
            request.downloadHandler?.text);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log("[CharacterSelect] " + message);
    }
}
