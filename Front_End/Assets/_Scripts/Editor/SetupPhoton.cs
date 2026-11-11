using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Tạo prefab PhotonHub / NetPlayer và gắn PhotonLink vào NetworkManager trong scene đang mở.</summary>
public static class SetupPhoton
{
    private const string Folder = "Assets/_Prefabs/Network";

    [MenuItem("Tools/Liars Card/Setup Photon")]
    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Prefabs", "Network");

        var hub = MakePrefab<PhotonHub>("PhotonHub", NetworkObjectFlags.MasterClientObject);
        var player = MakePrefab<NetPlayer>("NetPlayer", NetworkObjectFlags.None);

        var nm = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
        if (nm == null) return "Không thấy NetworkManager trong scene.";
        var link = nm.GetComponent<PhotonLink>() ?? Undo.AddComponent<PhotonLink>(nm.gameObject);
        var so = new SerializedObject(link);
        so.FindProperty("hubPrefab").objectReferenceValue = hub;
        so.FindProperty("playerPrefab").objectReferenceValue = player;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(nm.gameObject.scene);
        EditorSceneManager.SaveScene(nm.gameObject.scene);
        return $"OK: PhotonLink gắn vào '{nm.name}', hub flags = {hub.Flags}";
    }

    private static NetworkObject MakePrefab<T>(string name, NetworkObjectFlags flags) where T : Component
    {
        string path = $"{Folder}/{name}.prefab";
        var go = new GameObject(name);
        var no = go.AddComponent<NetworkObject>();
        go.AddComponent<T>();
        no.Flags |= flags;
        // Hub phải sống qua lúc chủ phòng thoát để quyền chuyển sang người kế
        if ((flags & NetworkObjectFlags.MasterClientObject) != 0) no.Flags &= ~NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<NetworkObject>();
    }
}
