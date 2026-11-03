using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Chuyển màn đăng nhập từ LoginScene vào MainScene (quán bar) và dựng PC + anchor camera. Chạy một lần.</summary>
public static class SetupLoginInBar
{
    private const string MainPath = "Assets/_Scenes/MainScene.unity";
    private const string LoginPath = "Assets/_Scenes/LoginScene.unity";

    private static GameObject Root(Scene s, string n)
    {
        foreach (var r in s.GetRootGameObjects()) if (r.name == n) return r;
        return null;
    }

    private static GameObject Box(Transform parent, string n, Vector3 p, Vector3 s, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = n;
        g.transform.SetParent(parent);
        g.transform.position = p;
        g.transform.localScale = s;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    private static Material MakeMat(string name, Color c)
    {
        string path = "Assets/_Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = c;
        EditorUtility.SetDirty(m);
        return m;
    }

    [MenuItem("Tools/Liar's Card/Setup Login In Bar")]
    public static void Run()
    {
        var ms = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        if (Root(ms, "LoginRoom") != null) { Debug.LogWarning("[SetupLoginInBar] MainScene đã có LoginRoom, bỏ qua."); return; }

        var ls = EditorSceneManager.OpenScene(LoginPath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(ms);
        string[] move = { "Canvas", "AuthForm", "AuthAPI", "NetworkManager", "RoomHintCanvas", "LoginRoom" };
        foreach (var n in move)
        {
            var g = Root(ls, n);
            if (g != null) SceneManager.MoveGameObjectToScene(g, ms);
        }
        var fb = Root(ms, "AuthApiClient_Fallback");
        if (fb != null) Object.DestroyImmediate(fb);

        // PC trên quầy bar, màn hình quay về phía người chơi (-z)
        var dark = MakeMat("LoginPC_Dark", new Color(0.08f, 0.08f, 0.1f));
        var green = MakeMat("LoginPC_Screen", new Color(0.03f, 0.12f, 0.05f));
        var pc = new GameObject("LoginPC");
        Box(pc.transform, "Stand", new Vector3(-2f, 1.17f, 7.7f), new Vector3(0.28f, 0.04f, 0.22f), dark);
        Box(pc.transform, "Neck", new Vector3(-2f, 1.29f, 7.7f), new Vector3(0.06f, 0.22f, 0.06f), dark);
        var body = Box(pc.transform, "Monitor", new Vector3(-2f, 1.62f, 7.7f), new Vector3(0.72f, 0.46f, 0.05f), dark);
        var scr = Box(pc.transform, "Screen", new Vector3(-2f, 1.62f, 7.67f), new Vector3(0.64f, 0.38f, 0.01f), green);
        Object.DestroyImmediate(scr.GetComponent<Collider>());
        Box(pc.transform, "Keyboard", new Vector3(-2f, 1.165f, 7.4f), new Vector3(0.46f, 0.02f, 0.16f), dark);

        var ov = new GameObject("LoginOverviewAnchor");
        ov.transform.position = new Vector3(0.4f, 2.1f, 3.6f);
        ov.transform.rotation = Quaternion.LookRotation(new Vector3(-0.9f, 2.4f, 9.6f) - ov.transform.position);
        var la = new GameObject("LoginAnchor");
        la.transform.position = new Vector3(-2f, 1.62f, 6.95f);

        var player = Root(ms, "Player");
        var lr = Root(ms, "LoginRoom").GetComponent<LoginRoomController>();
        var so = new SerializedObject(lr);
        so.FindProperty("cameraTransform").objectReferenceValue = player.transform.Find("PlayerCamera");
        so.FindProperty("overviewAnchor").objectReferenceValue = ov.transform;
        so.FindProperty("loginAnchor").objectReferenceValue = la.transform;
        so.FindProperty("pcCollider").objectReferenceValue = body.GetComponent<Collider>();
        so.FindProperty("screenRenderer").objectReferenceValue = scr.GetComponent<Renderer>();
        so.FindProperty("player").objectReferenceValue = player.transform;
        var h = so.FindProperty("hideWhenInGame");
        h.arraySize = 1;
        h.GetArrayElementAtIndex(0).objectReferenceValue = Root(ms, "RoomHintCanvas");
        so.ApplyModifiedProperties();

        player.transform.position = new Vector3(0f, 1f, -6.5f);

        EditorSceneManager.CloseScene(ls, true);
        EditorSceneManager.MarkSceneDirty(ms);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupLoginInBar] Xong. Nhớ Ctrl+S.");
    }
}
