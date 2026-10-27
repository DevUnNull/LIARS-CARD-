using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LiarsCard.Network;

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

            // Tim hoac tao GameObject [Gameplay_Managers]
            GameObject managersObj = GameObject.Find("[Gameplay_Managers]");
            if (managersObj == null)
            {
                managersObj = new GameObject("[Gameplay_Managers]");
            }

            // Dam bao co day du cac Service & UI Components
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

            // Kiem tra AuthApiClient fallback
            if (Object.FindAnyObjectByType<AuthApiClient>() == null)
            {
                GameObject authObj = new GameObject("AuthApiClient_Fallback");
                authObj.AddComponent<AuthApiClient>();
            }

            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);
            Debug.Log("<color=green>[LiarsCard] ĐÃ CÀI ĐẶT HOÀN HẢO TOÀN BỘ GAMEPLAY VÀO SAMPLESCENE!</color>");
        }
    }
}