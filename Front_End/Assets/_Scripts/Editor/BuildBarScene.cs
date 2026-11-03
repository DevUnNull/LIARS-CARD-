#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Menu Tools/Liar's Card/Build Bar Scene: dựng quán bar 3 bàn vào scene đang mở (MainScene). Chạy lại được nhiều lần.</summary>
public static class BuildBarScene
{
    private const string ModelDir = "Assets/_Models/Bar/";
    private const string ZonePath = "Assets/_Prefabs/TableZone.prefab";

    [MenuItem("Tools/Liar's Card/Build Bar Scene")]
    public static void Build()
    {
        // Prefab khu bàn: bỏ biển mặt sau, biển còn lại luôn quay về người nhìn
        var proot = PrefabUtility.LoadPrefabContents(ZonePath);
        var back = proot.transform.Find("TableSign1");
        if (back != null) Object.DestroyImmediate(back.gameObject);
        var front = proot.transform.Find("TableSign0");
        if (front != null)
        {
            if (front.GetComponent<BillboardY>() == null) front.gameObject.AddComponent<BillboardY>();
            front.localPosition = new Vector3(0, 4.3f, 0);
        }
        // Đèn điểm đặt ngay trong chụp đèn treo (đáy chụp ở y≈3.15) để bóng đèn thật sự toả sáng xuống bàn
        if (proot.transform.Find("BulbLight") == null)
        {
            var bulb = new GameObject("BulbLight"); bulb.transform.SetParent(proot.transform, false);
            bulb.transform.localPosition = new Vector3(0, 3.45f, 0);
            var bl = bulb.AddComponent<Light>();
            bl.type = LightType.Point; bl.color = new Color(1f, 0.74f, 0.45f); bl.intensity = 7f; bl.range = 8f; bl.shadows = LightShadows.None;
        }
        // Tối ưu: đèn rọi bàn không đổ bóng (mỗi đèn có bóng = thêm 1 lượt render shadow map)
        var tspot = proot.transform.Find("TableSpot");
        if (tspot != null) tspot.GetComponent<Light>().shadows = LightShadows.None;
        var lampMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/Bar_Lamp.mat");
        if (lampMat != null) { lampMat.EnableKeyword("_EMISSION"); EditorUtility.SetDirty(lampMat); }
        PrefabUtility.SaveAsPrefabAsset(proot, ZonePath);
        PrefabUtility.UnloadPrefabContents(proot);

        // Dọn đồ cũ của scene
        foreach (var n in new[] { "Plane", "Directional Light", "Table", "Chair", "Chair (1)", "Chair (2)", "Chair (3)", "Bar", "TableZone_1", "TableZone_2", "TableZone_3" })
        {
            var g = GameObject.Find(n);
            if (g != null) Object.DestroyImmediate(g);
        }

        var blue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/Bar_GlowBlue.mat");
        var warm = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/Bar_Lamp.mat");
        var bar = new GameObject("Bar");

        string[] mn = { "BarShell", "BarCounter", "BottleShelf", "BarStool", "BarStool", "BarStool", "BarStool", "BarStool", "Jukebox", "Sofa", "PoolTable", "Barrel", "Barrel", "Crate", "Crate", "Plant", "Plant", "Plant", "Plant", "Frames", "Frames", "Frames" };
        Vector3[] mp = { new Vector3(0, 0, 0), new Vector3(0, 0, 7.6f), new Vector3(0, 0, 9.65f), new Vector3(-3.2f, 0, 6.1f), new Vector3(-1.6f, 0, 6.1f), new Vector3(0, 0, 6.1f), new Vector3(1.6f, 0, 6.1f), new Vector3(3.2f, 0, 6.1f), new Vector3(-13.4f, 0, 3.8f), new Vector3(13.05f, 0, -7f), new Vector3(8.5f, 0, 6.8f), new Vector3(-5.6f, 0, 9.3f), new Vector3(5.6f, 0, 9.3f), new Vector3(13.4f, 0, 9.3f), new Vector3(13.4f, 0.8f, 9.3f), new Vector3(-13.3f, 0, 9.3f), new Vector3(13.3f, 0, -9.3f), new Vector3(-13.3f, 0, -9.3f), new Vector3(13.3f, 0, 5.5f), new Vector3(8f, 3.2f, -9.8f), new Vector3(-4f, 3.2f, -9.8f), new Vector3(-13.85f, 3.2f, -2f) };
        float[] mr = { 0, 180, 180, 0, 0, 0, 0, 0, 90, -90, 0, 0, 0, 0, 25, 0, 0, 0, 0, 0, 0, 90 };
        for (int i = 0; i < mn.Length; i++)
        {
            var pr = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + mn[i] + ".fbx");
            if (pr == null) { Debug.LogWarning("Thiếu model " + mn[i]); continue; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(pr);
            g.transform.SetParent(bar.transform, false);
            // FBX từ Blender về dạng Z-up: dựng đứng bằng -90° quanh X, rồi xoay hướng quanh trục Y thế giới
            g.transform.SetPositionAndRotation(mp[i], Quaternion.Euler(-90, mr[i], 0));
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials; bool ch = false;
                for (int k = 0; k < ms.Length; k++)
                {
                    if (ms[k] == null) continue;
                    if (ms[k].name == "GlowBlue") { ms[k] = blue; ch = true; }
                    else if (ms[k].name.StartsWith("Glow")) { ms[k] = warm; ch = true; }
                }
                if (ch) r.sharedMaterials = ms;
            }
            if (i != 0 && mn[i] != "Frames" && mn[i] != "Plant")
            {
                var mf = g.GetComponentInChildren<MeshFilter>();
                var bc = g.AddComponent<BoxCollider>();
                bc.center = mf.sharedMesh.bounds.center; bc.size = mf.sharedMesh.bounds.size;
            }
        }

        // Collider sàn + 4 tường
        var col = new GameObject("RoomColliders"); col.transform.SetParent(bar.transform, false);
        string[] cn = { "Floor", "WallN", "WallS", "WallW", "WallE" };
        Vector3[] cp = { new Vector3(0, -0.25f, 0), new Vector3(0, 3, 10.4f), new Vector3(0, 3, -10.4f), new Vector3(-14.4f, 3, 0), new Vector3(14.4f, 3, 0) };
        Vector3[] cs = { new Vector3(32, 0.5f, 24), new Vector3(32, 6, 1), new Vector3(32, 6, 1), new Vector3(1, 6, 24), new Vector3(1, 6, 24) };
        for (int i = 0; i < cn.Length; i++)
        {
            var g = new GameObject(cn[i]); g.transform.SetParent(col.transform, false); g.transform.position = cp[i];
            g.AddComponent<BoxCollider>().size = cs[i];
        }

        // 3 khu bàn
        var zp = AssetDatabase.LoadAssetAtPath<GameObject>(ZonePath);
        float[] zx = { -9f, 0f, 9f };
        Transform firstSpot = null;
        for (int z = 0; z < 3; z++)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(zp);
            inst.name = "TableZone_" + (z + 1);
            inst.transform.position = new Vector3(zx[z], 0, -1f);
            foreach (var ci in inst.GetComponentsInChildren<ChairItem>())
            {
                var so = new SerializedObject(ci);
                so.FindProperty("tableID").intValue = z + 1;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var tm in inst.GetComponentsInChildren<TextMeshPro>()) tm.text = "BÀN " + (z + 1);
            if (z == 0) firstSpot = inst.transform.Find("GunSpot");
        }
        var gun = GameObject.Find("Revolver");
        if (gun != null && firstSpot != null) gun.transform.SetPositionAndRotation(firstSpot.position, firstSpot.rotation);

        // Ánh sáng
        var lights = new GameObject("Lights"); lights.transform.SetParent(bar.transform, false);
        // 0 trên quầy, 1 sau biển neon, 2 trước jukebox, 3 trong chụp đèn bàn bi-a, 4 cạnh cửa
        Vector3[] lp = { new Vector3(0, 3.6f, 7.6f), new Vector3(0, 5.0f, 9.2f), new Vector3(-12.3f, 1.9f, 3.8f), new Vector3(8.5f, 3.45f, 6.8f), new Vector3(0, 3.2f, -8.5f) };
        Color[] lc = { new Color(1f, 0.65f, 0.35f), new Color(1f, 0.2f, 0.6f), new Color(0.3f, 0.7f, 1f), new Color(1f, 0.75f, 0.45f), new Color(1f, 0.7f, 0.4f) };
        float[] li = { 13f, 10f, 5f, 14f, 8f }, lr = { 10f, 7f, 4f, 7f, 8f };
        for (int i = 0; i < lp.Length; i++) AddPoint(lights.transform, "Light" + i, lp[i], lc[i], li[i], lr[i]);
        // Đèn treo + đèn điểm trong chụp đèn trên bàn bi-a (cùng kiểu với khu bàn)
        var lampPr = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "PendantLamp.fbx");
        if (lampPr != null)
        {
            var pl = (GameObject)PrefabUtility.InstantiatePrefab(lampPr);
            pl.name = "PoolLamp"; pl.transform.SetParent(bar.transform, false);
            pl.transform.SetPositionAndRotation(new Vector3(8.5f, 5.3f, 6.8f), Quaternion.Euler(-90, 0, 0));
            foreach (var r in pl.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int k = 0; k < ms.Length; k++) if (ms[k] != null && ms[k].name.StartsWith("Glow")) ms[k] = warm;
                r.sharedMaterials = ms;
            }
        }

        var neon = new GameObject("NeonText"); neon.transform.SetParent(bar.transform, false);
        neon.transform.position = new Vector3(0, 4.95f, 9.92f); // khoảng trống tường: trên đỉnh kệ (4.44), dưới xà ngang (5.4)
        var t = neon.AddComponent<TextMeshPro>();
        t.text = "LIAR'S CARD"; t.fontSize = 11; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center; t.color = new Color(1f, 0.35f, 0.75f);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.35f, 0.28f, 0.28f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.05f, 0.035f, 0.035f); RenderSettings.fogDensity = 0.012f;

        var player = GameObject.Find("Player");
        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(0, 1f, -8f), Quaternion.identity);
            if (cc != null) cc.enabled = true;
        }
        var vgo = GameObject.Find("Global Volume");
        var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Materials/BarVolume.asset");
        if (vgo != null && prof != null) vgo.GetComponent<Volume>().sharedProfile = prof;

        // Tối ưu: đồ trong quán không di chuyển -> static để Unity gộp batch
        foreach (var tr in bar.GetComponentsInChildren<Transform>()) tr.gameObject.isStatic = true;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Đã dựng quán bar. Nhớ Ctrl+S để lưu scene.");
    }

    private static void AddPoint(Transform parent, string name, Vector3 pos, Color c, float intensity, float range)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.position = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
    }
}
#endif
