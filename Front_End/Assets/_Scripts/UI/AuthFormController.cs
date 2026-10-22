
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    public void OnLoginClicked()
    {
        if (isBusy) return;

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

        yield return authApi.Login(
            loginEmail.text,
            loginPassword.text,
            (success, message) =>
            {
                loginStatus.text = message;
                if (success)
                    Debug.Log("Login thành công!");
            });

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
