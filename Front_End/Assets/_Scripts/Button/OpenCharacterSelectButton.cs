
using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenCharacterSelectButton : MonoBehaviour
{
    public void OpenCharacterSelect()
    {
        if (AuthApiClient.Instance == null ||
            string.IsNullOrWhiteSpace(
                AuthApiClient.Instance.AccessToken))
        {
            Debug.LogWarning("Bạn cần đăng nhập trước.");
            return;
        }

        SceneManager.LoadScene("CharacterScene");
    }
}
