using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Dựng bartender sau quầy + khung hội thoại + anchor camera cho màn đăng nhập trong MainScene. Chạy lại được (xoá bản cũ).</summary>
public static class SetupBartenderLogin
{
    private static GameObject Root(Scene s, string n)
    {
        foreach (var r in s.GetRootGameObjects()) if (r.name == n) return r;
        return null;
    }

    private static void Kill(Scene s, string n)
    {
        var g = Root(s, n);
        if (g != null) Object.DestroyImmediate(g);
    }

    private static Material Mat(string name, Color c)
    {
        string path = "Assets/_Materials/Bartender_" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = c;
        m.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static GameObject Part(Transform parent, PrimitiveType type, string n, Vector3 localPos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = n;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    private static TextMeshProUGUI Label(Transform parent, string n, Vector2 min, Vector2 max, float size, Color c, FontStyles style)
    {
        var g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var t = g.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.color = c; t.fontStyle = style; t.enableWordWrapping = true; t.raycastTarget = false;
        return t;
    }

    [MenuItem("Tools/Liar's Card/Setup Bartender Login")]
    public static void Run()
    {
        var ms = SceneManager.GetActiveScene();
        var lrGo = Root(ms, "LoginRoom");
        if (lrGo == null) { Debug.LogError("[SetupBartenderLogin] Chưa có LoginRoom: chạy 'Setup Login In Bar' trước."); return; }

        Kill(ms, "LoginPC"); Kill(ms, "LoginAnchor"); Kill(ms, "LoginOverviewAnchor");
        Kill(ms, "LoginTalkAnchor"); Kill(ms, "Bartender");

        // ---- Bartender (mặt quay về -z, về phía người chơi) ----
        var skin = Mat("Skin", new Color(0.93f, 0.74f, 0.6f));
        var vest = Mat("Vest", new Color(0.45f, 0.08f, 0.1f));
        var shirt = Mat("Shirt", new Color(0.95f, 0.95f, 0.92f));
        var pants = Mat("Pants", new Color(0.1f, 0.1f, 0.12f));
        var hat = Mat("Hat", new Color(0.12f, 0.1f, 0.09f));
        var hair = Mat("Hair", new Color(0.18f, 0.1f, 0.06f));
        var dark = Mat("Dark", new Color(0.02f, 0.02f, 0.02f));
        var glass = Mat("Glass", new Color(0.7f, 0.85f, 0.95f));

        var root = new GameObject("Bartender");
        root.transform.position = new Vector3(0.4f, 0f, 8.85f);
        root.transform.localScale = Vector3.one * 1.12f;
        root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        Part(root.transform, PrimitiveType.Cube, "Legs", new Vector3(0f, 0.4f, 0f), new Vector3(0.42f, 0.8f, 0.26f), pants);
        var body = Part(root.transform, PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.15f, 0f), new Vector3(0.52f, 0.4f, 0.32f), vest);
        Part(body.transform, PrimitiveType.Cube, "Shirt", new Vector3(0f, 0.1f, 0.5f), new Vector3(0.45f, 0.6f, 0.05f), shirt);
        var tie = Part(root.transform, PrimitiveType.Cube, "BowTie", new Vector3(0f, 1.46f, 0.17f), new Vector3(0.16f, 0.06f, 0.03f), vest);
        tie.transform.SetParent(body.transform, true);

        var head = new GameObject("Head").transform;
        head.SetParent(root.transform, false);
        head.localPosition = new Vector3(0f, 1.66f, 0f);
        Part(head, PrimitiveType.Sphere, "Face", Vector3.zero, new Vector3(0.3f, 0.34f, 0.3f), skin);
        Part(head, PrimitiveType.Sphere, "Nose", new Vector3(0f, -0.01f, 0.15f), new Vector3(0.05f, 0.05f, 0.05f), skin);
        Part(head, PrimitiveType.Sphere, "EyeL", new Vector3(-0.06f, 0.05f, 0.135f), new Vector3(0.04f, 0.04f, 0.03f), dark);
        Part(head, PrimitiveType.Sphere, "EyeR", new Vector3(0.06f, 0.05f, 0.135f), new Vector3(0.04f, 0.04f, 0.03f), dark);
        Part(head, PrimitiveType.Cube, "Brows", new Vector3(0f, 0.095f, 0.14f), new Vector3(0.2f, 0.018f, 0.02f), hair);
        Part(head, PrimitiveType.Cube, "Moustache", new Vector3(0f, -0.045f, 0.145f), new Vector3(0.16f, 0.03f, 0.03f), hair);
        Part(head, PrimitiveType.Cylinder, "HatBrim", new Vector3(0f, 0.15f, 0f), new Vector3(0.46f, 0.012f, 0.46f), hat);
        Part(head, PrimitiveType.Cylinder, "HatCrown", new Vector3(0f, 0.21f, 0f), new Vector3(0.28f, 0.06f, 0.28f), hat);

        // Tay trái buông, tay phải cầm ly lau (tự đung đưa nhờ BartenderIdle)
        var armL = new GameObject("ArmL").transform;
        armL.SetParent(root.transform, false);
        armL.localPosition = new Vector3(-0.31f, 1.42f, 0f);
        armL.localRotation = Quaternion.Euler(0f, 0f, 8f);
        Part(armL, PrimitiveType.Capsule, "Arm", new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.3f, 0.14f), vest);
        Part(armL, PrimitiveType.Sphere, "Hand", new Vector3(0f, -0.6f, 0f), new Vector3(0.12f, 0.12f, 0.12f), skin);

        var armR = new GameObject("ArmR").transform;
        armR.SetParent(root.transform, false);
        armR.localPosition = new Vector3(0.31f, 1.42f, 0f);
        armR.localRotation = Quaternion.Euler(-55f, 0f, -8f);
        Part(armR, PrimitiveType.Capsule, "Arm", new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.3f, 0.14f), vest);
        Part(armR, PrimitiveType.Sphere, "Hand", new Vector3(0f, -0.6f, 0f), new Vector3(0.12f, 0.12f, 0.12f), skin);
        Part(armR, PrimitiveType.Cylinder, "Glass", new Vector3(0f, -0.68f, 0.02f), new Vector3(0.1f, 0.08f, 0.1f), glass);

        var idle = root.AddComponent<BartenderIdle>();
        var so = new SerializedObject(idle);
        so.FindProperty("head").objectReferenceValue = head;
        so.FindProperty("body").objectReferenceValue = body.transform;
        so.FindProperty("wipeArm").objectReferenceValue = armR;
        so.ApplyModifiedProperties();

        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 1f, 0f);
        col.size = new Vector3(0.9f, 2.1f, 0.6f);

        // ---- Anchor camera ----
        var ov = new GameObject("LoginOverviewAnchor");
        ov.transform.position = new Vector3(0.9f, 1.9f, 3.0f);
        ov.transform.rotation = Quaternion.LookRotation(new Vector3(0.2f, 2.0f, 9.2f) - ov.transform.position);
        var ta = new GameObject("LoginTalkAnchor");
        ta.transform.position = new Vector3(1.1f, 1.75f, 5.6f);
        ta.transform.rotation = Quaternion.LookRotation(new Vector3(1.1f, 1.7f, 8.85f) - ta.transform.position);

        // ---- Form đăng nhập dời sang phải để không che bartender ----
        var canvas = Root(ms, "Canvas");
        foreach (var rt in canvas.GetComponentsInChildren<RectTransform>(true))
            if (rt.name == "LoginCard" || rt.name == "RegisterCard") rt.anchoredPosition = new Vector2(560f, 0f);

        // ---- Khung hội thoại dưới màn hình ----
        var hint = Root(ms, "RoomHintCanvas");
        var old = hint.transform.Find("DialoguePanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var panel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panel.transform.SetParent(hint.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(-240f, 70f);
        prt.sizeDelta = new Vector2(1100f, 190f);
        panel.GetComponent<Image>().color = new Color(0.06f, 0.03f, 0.03f, 0.88f);
        panel.GetComponent<Image>().raycastTarget = false;
        var nameT = Label(panel.transform, "NameText", new Vector2(0.03f, 0.74f), new Vector2(0.6f, 0.97f), 34f, new Color(1f, 0.78f, 0.25f), FontStyles.Bold);
        var textT = Label(panel.transform, "DialogueText", new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.74f), 38f, Color.white, FontStyles.Normal);
        nameT.text = "Bartender";
        textT.text = "";
        panel.GetComponent<CanvasGroup>().alpha = 0f;

        // ---- Nối vào controller ----
        var lr = lrGo.GetComponent<LoginRoomController>();
        var lso = new SerializedObject(lr);
        lso.FindProperty("overviewAnchor").objectReferenceValue = ov.transform;
        lso.FindProperty("talkAnchor").objectReferenceValue = ta.transform;
        lso.FindProperty("bartenderCollider").objectReferenceValue = col;
        lso.FindProperty("dialogueGroup").objectReferenceValue = panel.GetComponent<CanvasGroup>();
        lso.FindProperty("nameText").objectReferenceValue = nameT;
        lso.FindProperty("dialogueText").objectReferenceValue = textT;
        lso.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(ms);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupBartenderLogin] Xong. Nhớ Ctrl+S.");
    }
}
