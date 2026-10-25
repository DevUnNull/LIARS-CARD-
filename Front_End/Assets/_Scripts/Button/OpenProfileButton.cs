
using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenProfileButton : MonoBehaviour
{
    public void OpenProfile()
    {
        if (AuthApiClient.Instance == null ||
            string.IsNullOrEmpty(AuthApiClient.Instance.AccessToken))
        {
            Debug.LogWarning("Bạn cần đăng nhập trước khi xem hồ sơ.");
            return;
        }

        SceneManager.LoadScene("ProfileScene");
    }
}
