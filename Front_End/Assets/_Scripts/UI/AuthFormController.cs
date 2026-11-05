
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class AuthFormController : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private AuthApiClient authApi;

    [Header("Login")]
    [SerializeField] private TMP_InputField loginEmail;
    [SerializeField] private TMP_InputField loginPassword;
    [SerializeField] private Button loginButton;
    [SerializeField] private TMP_Text loginStatus;

    [Header("Register")]
    [SerializeField] private TMP_InputField registerEmail;
    [SerializeField] private TMP_InputField displayName;
    [SerializeField] private TMP_InputField registerPassword;
    [SerializeField] private Button registerButton;
    [SerializeField] private TMP_Text registerStatus;

    private bool isBusy;

    private const string DefaultDomain = "@gmail.com";
    private TMP_InputField[] loginOrder, registerOrder;

    private void Start()
    {
        loginOrder = new[] { loginEmail, loginPassword };
        registerOrder = new[] { registerEmail, displayName, registerPassword };

        // Enter ở ô mật khẩu = gửi đi; Enter ở ô khác = nhảy sang ô kế tiếp
        if (loginPassword != null) loginPassword.onSubmit.AddListener(_ => OnLoginClicked());
        if (registerPassword != null) registerPassword.onSubmit.AddListener(_ => OnRegisterClicked());
        if (loginEmail != null) loginEmail.onSubmit.AddListener(_ => FocusNext(loginOrder, loginEmail, 1));
        if (registerEmail != null) registerEmail.onSubmit.AddListener(_ => FocusNext(registerOrder, registerEmail, 1));
        if (displayName != null) displayName.onSubmit.AddListener(_ => FocusNext(registerOrder, displayName, 1));

        // Rời ô email mà chưa có @ thì tự thêm @gmail.com
        if (loginEmail != null) loginEmail.onEndEdit.AddListener(_ => loginEmail.text = WithDomain(loginEmail.text));
        if (registerEmail != null) registerEmail.onEndEdit.AddListener(_ => registerEmail.text = WithDomain(registerEmail.text));
    }

    // Tab / Shift+Tab chuyển ô trong form đang gõ
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.tabKey.wasPressedThisFrame) return;

        int dir = kb.shiftKey.isPressed ? -1 : 1;
        foreach (var order in new[] { loginOrder, registerOrder })
        {
            if (order == null) continue;
            foreach (var f in order)
            {
                if (f != null && f.isFocused) { FocusNext(order, f, dir); return; }
            }
        }
    }

    private static void FocusNext(TMP_InputField[] order, TMP_InputField current, int dir)
    {
        int i = System.Array.IndexOf(order, current);
        var next = order[(i + dir + order.Length) % order.Length];
        if (next == null) return;
        current.text = current.text.Replace("\t", ""); // TMP có thể đã chèn ký tự Tab
        next.Select();
        next.ActivateInputField();
    }

    // Chưa có @ thì thêm @gmail.com; đã có @ thì giữ nguyên
    private static string WithDomain(string email)
    {
        email = email?.Trim() ?? "";
        return email.Length == 0 || email.Contains("@") ? email : email + DefaultDomain;
    }

    public void OnLoginClicked()
    {
        if (isBusy) return;

        loginEmail.text = WithDomain(loginEmail.text);
        if (string.IsNullOrWhiteSpace(loginEmail.text) ||
            string.IsNullOrEmpty(loginPassword.text))
        {
            loginStatus.text = "Nhập email và mật khẩu.";
            return;
        }

        StartCoroutine(LoginRoutine());
    }

    public void OnRegisterClicked()
    {
        if (isBusy) return;

        registerEmail.text = WithDomain(registerEmail.text);
        if (string.IsNullOrWhiteSpace(registerEmail.text) ||
            string.IsNullOrWhiteSpace(displayName.text) ||
            string.IsNullOrEmpty(registerPassword.text))
        {
            registerStatus.text =
                "Nhập email, display name và mật khẩu.";
            return;
        }

        StartCoroutine(RegisterRoutine());
    }

    private IEnumerator LoginRoutine()
    {
        isBusy = true;
        loginButton.interactable = false;
        loginStatus.text = "Đang đăng nhập...";

        Task connectTask = null;

        yield return authApi.Login(
            loginEmail.text,
            loginPassword.text,
            (success, message) =>
            {
                loginStatus.text = message;

                if (success)
                {
                    if (NetworkManager.Instance == null)
                    {
                        loginStatus.text = "Chưa có NetworkManager trong scene.";
                        return;
                    }

                    loginStatus.text = "Đang kết nối vào game...";

                    connectTask = NetworkManager.Instance.ConnectAsync(
                        authApi.AccessToken);
                }
            });

        if (connectTask != null)
        {
            yield return new WaitUntil(() => connectTask.IsCompleted);

            if (connectTask.IsCanceled)
            {
                loginStatus.text = "Đã hủy kết nối.";
            }
            else if (connectTask.IsFaulted)
            {
                loginStatus.text =
                    connectTask.Exception.GetBaseException().Message;
            }
            else
            {
                loginStatus.text = "Đã kết nối vào game!";
                loginButton.interactable = true;
                isBusy = false;

                // Cùng scene: bartender chào, camera lùi ra chỗ spawn rồi trao điều khiển; không có controller thì giữ hành vi cũ
                var room = FindFirstObjectByType<LoginRoomController>();
                if (room != null) room.Welcome();
                else SceneManager.LoadScene("MainScene");
                yield break;
            }
        }

        loginButton.interactable = true;
        isBusy = false;
    }

    private IEnumerator RegisterRoutine()
    {
        isBusy = true;
        registerButton.interactable = false;
        registerStatus.text = "Đang đăng ký...";

        yield return authApi.Register(
            registerEmail.text,
            displayName.text,
            registerPassword.text,
            (success, message) =>
            {
                registerStatus.text = message;

                if (success)
                {
                    loginEmail.text = registerEmail.text;
                    Debug.Log("Register thành công!");
                }
            });

        registerButton.interactable = true;
        isBusy = false;
    }
}
