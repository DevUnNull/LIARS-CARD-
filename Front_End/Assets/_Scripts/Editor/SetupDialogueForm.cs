using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Chuyển ô nhập đăng nhập/đăng ký từ Canvas cũ vào thẳng khung hội thoại bartender (nhân bản input/nút cũ rồi nối lại
/// AuthFormController + AuthPanelSwitcher), xoá Canvas cũ. Cần chạy sau 'Setup Bartender Login'.
/// </summary>
public static class SetupDialogueForm
{
    private static GameObject Root(Scene s, string n)
    {
        foreach (var r in s.GetRootGameObjects()) if (r.name == n) return r;
        return null;
    }

    private static T Dup<T>(Transform src, string path, Transform parent, Vector2 pos, float width) where T : Component
    {
        var s = src.Find(path);
        var g = Object.Instantiate(s.gameObject, parent, false);
        g.name = s.name;
        var rt = (RectTransform)g.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
        return g.GetComponent<T>();
    }

    private static RectTransform Stretch(string n, Transform parent)
    {
        var g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return rt;
    }

    [MenuItem("Tools/Liar's Card/Setup Dialogue Form")]
    public static void Run()
    {
        var ms = SceneManager.GetActiveScene();
        var canvas = Root(ms, "Canvas");
        var hint = Root(ms, "RoomHintCanvas");
        var authGo = Root(ms, "AuthForm");
        if (canvas == null || hint == null || authGo == null) { Debug.LogError("[SetupDialogueForm] Thiếu Canvas cũ / RoomHintCanvas / AuthForm (đã chạy rồi?)."); return; }
        var panel = hint.transform.Find("DialoguePanel") as RectTransform;
        if (panel == null) { Debug.LogError("[SetupDialogueForm] Chạy 'Setup Bartender Login' trước."); return; }

        var loginCard = canvas.transform.Find("LoginPanel/LoginCard");
        var regCard = canvas.transform.Find("RegisterPanel/RegisterCard");

        // Khung to hơn: chữ ở trên, ô nhập ở dưới
        panel.anchoredPosition = new Vector2(0f, 50f);
        panel.sizeDelta = new Vector2(1500f, 340f);
        var nameT = panel.Find("NameText") as RectTransform;
        nameT.anchorMin = new Vector2(0.03f, 0.88f); nameT.anchorMax = new Vector2(0.6f, 0.99f);
        var textT = panel.Find("DialogueText") as RectTransform;
        textT.anchorMin = new Vector2(0.03f, 0.56f); textT.anchorMax = new Vector2(0.97f, 0.88f);

        var old = panel.Find("FormArea");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var area = new GameObject("FormArea", typeof(RectTransform), typeof(CanvasGroup));
        area.transform.SetParent(panel, false);
        var art = (RectTransform)area.transform;
        art.anchorMin = Vector2.zero; art.anchorMax = new Vector2(1f, 0.56f); art.offsetMin = Vector2.zero; art.offsetMax = Vector2.zero;
        var cg = area.GetComponent<CanvasGroup>();
        cg.alpha = 0f;

        // ---- Đăng nhập ----
        var loginRow = Stretch("LoginRow", art);
        var lEmail = Dup<TMP_InputField>(loginCard, "Email_Input", loginRow, new Vector2(-470f, 25f), 400f);
        var lPass = Dup<TMP_InputField>(loginCard, "Password_Input", loginRow, new Vector2(-40f, 25f), 400f);
        var lBtn = Dup<Button>(loginCard, "LoginButton", loginRow, new Vector2(370f, 25f), 240f);
        var toReg = Dup<Button>(loginCard, "BackToRegisterButton", loginRow, new Vector2(370f, -55f), 300f);
        var lStatus = Dup<TMP_Text>(loginCard, "StatusText", loginRow, new Vector2(-250f, -55f), 800f);

        // ---- Đăng ký ----
        var regRow = Stretch("RegisterRow", art);
        var rEmail = Dup<TMP_InputField>(regCard, "Email_Input", regRow, new Vector2(-560f, 25f), 340f);
        var rName = Dup<TMP_InputField>(regCard, "DisplayName_Input", regRow, new Vector2(-215f, 25f), 300f);
        var rPass = Dup<TMP_InputField>(regCard, "Password_Input", regRow, new Vector2(100f, 25f), 300f);
        var rBtn = Dup<Button>(regCard, "RegisterButton", regRow, new Vector2(400f, 25f), 240f);
        var toLogin = Dup<Button>(regCard, "BackToLoginButton", regRow, new Vector2(400f, -55f), 300f);
        var rStatus = Dup<TMP_Text>(regCard, "StatusText", regRow, new Vector2(-250f, -55f), 800f);
        regRow.gameObject.SetActive(false);

        foreach (var st in new[] { lStatus, rStatus })
        {
            st.fontSize = 24f;
            st.color = new Color(1f, 0.85f, 0.4f);
            st.alignment = TextAlignmentOptions.MidlineLeft;
            st.text = "";
        }

        // ---- Nối lại logic cũ ----
        var form = authGo.GetComponent<AuthFormController>();
        var so = new SerializedObject(form);
        so.FindProperty("loginEmail").objectReferenceValue = lEmail;
        so.FindProperty("loginPassword").objectReferenceValue = lPass;
        so.FindProperty("loginButton").objectReferenceValue = lBtn;
        so.FindProperty("loginStatus").objectReferenceValue = lStatus;
        so.FindProperty("registerEmail").objectReferenceValue = rEmail;
        so.FindProperty("displayName").objectReferenceValue = rName;
        so.FindProperty("registerPassword").objectReferenceValue = rPass;
        so.FindProperty("registerButton").objectReferenceValue = rBtn;
        so.FindProperty("registerStatus").objectReferenceValue = rStatus;
        so.ApplyModifiedProperties();

        var sw = authGo.GetComponent<AuthPanelSwitcher>();
        var sso = new SerializedObject(sw);
        sso.FindProperty("loginPanel").objectReferenceValue = loginRow.gameObject;
        sso.FindProperty("registerPanel").objectReferenceValue = regRow.gameObject;
        sso.ApplyModifiedProperties();

        // Nút chuyển/gửi: gỡ listener cũ (còn trỏ vào Canvas cũ) rồi gắn lại
        Rewire(lBtn, form, "OnLoginClicked");
        Rewire(rBtn, form, "OnRegisterClicked");
        Rewire(toReg, sw, "ShowRegister");
        Rewire(toLogin, sw, "ShowLogin");

        // Form của controller: nằm trong khung hội thoại
        var lr = Root(ms, "LoginRoom").GetComponent<LoginRoomController>();
        var lso = new SerializedObject(lr);
        lso.FindProperty("formGroup").objectReferenceValue = cg;
        lso.ApplyModifiedProperties();

        Object.DestroyImmediate(canvas);
        EditorSceneManager.MarkSceneDirty(ms);
        Debug.Log("[SetupDialogueForm] Xong. Nhớ Ctrl+S.");
    }

    private static void Rewire(Button b, Object target, string method)
    {
        var so = new SerializedObject(b);
        var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        calls.arraySize = 0;
        so.ApplyModifiedProperties();
        var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, method);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(b.onClick, action);
    }
}
