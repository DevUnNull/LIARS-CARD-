using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Màn hình vào game: nhập tên rồi vào quán bar (phòng Photon chung).</summary>
public class AuthFormController : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private AuthApiClient authApi;

    [Header("Vào Game")]
    [Tooltip("Ô nhập tên hiển thị (trước đây là ô email đăng nhập).")]
    [SerializeField] private TMP_InputField loginEmail;
    [Tooltip("Ô mật khẩu cũ: không dùng nữa, tự ẩn khi chạy.")]
    [SerializeField] private TMP_InputField loginPassword;
    [SerializeField] private Button loginButton;
    [SerializeField] private TMP_Text loginStatus;

    private bool isBusy;

    private void Start()
    {
        if (loginPassword != null) loginPassword.gameObject.SetActive(false);
        if (loginEmail != null)
        {
            loginEmail.characterLimit = AuthApiClient.MaxNameLength;
            loginEmail.contentType = TMP_InputField.ContentType.Standard;
            if (loginEmail.placeholder is TMP_Text ph) ph.text = "Tên của bạn";
            loginEmail.text = PlayerPrefs.GetString("PlayerName", "");
            loginEmail.onSubmit.AddListener(_ => OnLoginClicked());
        }
    }

    public void OnLoginClicked()
    {
        if (isBusy) return;
        string error = authApi.SignIn(loginEmail.text);
        if (error != null)
        {
            loginStatus.text = error;
            return;
        }
        PlayerPrefs.SetString("PlayerName", authApi.DisplayName);
        StartCoroutine(LoginRoutine());
    }

    // Nút Đăng ký cũ (nếu scene còn giữ) cũng chỉ là vào game
    public void OnRegisterClicked() => OnLoginClicked();

    private IEnumerator LoginRoutine()
    {
        isBusy = true;
        loginButton.interactable = false;
        loginStatus.text = "Đang vào quán...";

        Task connectTask = NetworkManager.Instance.ConnectAsync(authApi.CurrentUserId, authApi.DisplayName);
        yield return new WaitUntil(() => connectTask.IsCompleted);

        loginButton.interactable = true;
        isBusy = false;

        if (connectTask.IsFaulted)
        {
            loginStatus.text = connectTask.Exception.GetBaseException().Message;
            yield break;
        }

        loginStatus.text = "Đã vào quán!";
        // Bartender chào, camera lùi ra chỗ spawn rồi trao điều khiển; không có controller thì giữ hành vi cũ
        var room = FindFirstObjectByType<LoginRoomController>();
        if (room != null) room.Welcome();
        else SceneManager.LoadScene("MainScene");
    }
}
