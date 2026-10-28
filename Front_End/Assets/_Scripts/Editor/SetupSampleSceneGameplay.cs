using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LiarsCard.Network;
using LiarsCard.Cards3D;

namespace LiarsCard.Editor
{
    public static class SetupSampleSceneGameplay
    {
        [MenuItem("Liars Card/Setup SampleScene Full Gameplay Now")]
        public static void SetupScene()
        {
            string scenePath = "Assets/_Scenes/SampleScene.unity";
            var currentScene = EditorSceneManager.GetActiveScene();

            if (currentScene.path != scenePath)
            {
                if (System.IO.File.Exists(scenePath))
                {
                    currentScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
            }

            // Tìm hoặc tạo GameObject [Gameplay_Managers]
            GameObject managersObj = GameObject.Find("[Gameplay_Managers]");
            if (managersObj == null)
            {
                managersObj = new GameObject("[Gameplay_Managers]");
            }

            // Đảm bảo có đầy đủ các Service & UI Components
            if (managersObj.GetComponent<MatchApiClient>() == null)
            {
                managersObj.AddComponent<MatchApiClient>();
            }

            if (managersObj.GetComponent<TableSessionManager>() == null)
            {
                managersObj.AddComponent<TableSessionManager>();
            }

            if (managersObj.GetComponent<TableLobbyUI>() == null)
            {
                managersObj.AddComponent<TableLobbyUI>();
            }

            if (managersObj.GetComponent<CardBattleUI>() == null)
            {
                managersObj.AddComponent<CardBattleUI>();
            }

            // Thêm các thành phần 3D Card FPP (Liar's Bar style)
            if (managersObj.GetComponent<CardHand3D>() == null)
            {
                managersObj.AddComponent<CardHand3D>();
            }

            if (managersObj.GetComponent<CardAudioSystem>() == null)
            {
                managersObj.AddComponent<CardAudioSystem>();
            }

            if (managersObj.GetComponent<TableCenterPile3D>() == null)
            {
                managersObj.AddComponent<TableCenterPile3D>();
            }

            // Kiểm tra AuthApiClient fallback
            if (Object.FindAnyObjectByType<AuthApiClient>() == null)
            {
                GameObject authObj = new GameObject("AuthApiClient_Fallback");
                authObj.AddComponent<AuthApiClient>();
            }

            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);
            Debug.Log("<color=green>[LiarsCard] ĐÃ CÀI ĐẶT HOÀN HẢO TOÀN BỘ GAMEPLAY VÀ 3D CARD SYSTEM VÀO SAMPLESCENE!</color>");
        }
    }
}
