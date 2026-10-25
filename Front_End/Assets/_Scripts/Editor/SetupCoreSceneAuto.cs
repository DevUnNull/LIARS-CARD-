using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LiarsCard.Network;

namespace LiarsCard.Editor
{
    [InitializeOnLoad]
    public static class SetupCoreSceneAuto
    {
        static SetupCoreSceneAuto()
        {
            EditorApplication.delayCall += SetupScene;
        }

        [MenuItem("Liars Card/Setup CoreScene Demo Now")]
        public static void SetupScene()
        {
            string scenePath = "Assets/_Scenes/CoreScene.unity";
            var currentScene = EditorSceneManager.GetActiveScene();

            if (currentScene.path != scenePath)
            {
                if (System.IO.File.Exists(scenePath))
                {
                    currentScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
            }

            // Find or Create CardBattleDemo GameObject
#if UNITY_2023_1_OR_NEWER
            var existingDemo = Object.FindAnyObjectByType<CardBattleDemoUI>();
#else
            var existingDemo = Object.FindObjectOfType<CardBattleDemoUI>();
#endif
            if (existingDemo == null)
            {
                GameObject demoObj = new GameObject("CardBattleDemo");
                demoObj.AddComponent<MatchApiClient>();
                demoObj.AddComponent<CardBattleDemoUI>();

                EditorSceneManager.MarkSceneDirty(currentScene);
                EditorSceneManager.SaveScene(currentScene);
                Debug.Log("<color=green>[LiarsCard] Da tu dong tao 'CardBattleDemo' trong CoreScene thanh cong!</color>");
            }
        }
    }
}