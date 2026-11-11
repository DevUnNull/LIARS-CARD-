using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Tạo 2 clip ngồi cầm bài từ clip "sit": SitHold (bài trước ngực) và SitPeek (giơ bài lên ngang mặt xem).
/// Giữ nguyên chân / thân của sit, chỉ đặt lại xoay 2 cánh tay, rồi thêm state vào Character.controller.
/// </summary>
public static class BuildHoldCardsClips
{
    private const string ControllerPath = "Assets/_Models/Character.controller";
    private const string ModelPath = "Assets/_Models/Character.fbx";

    // Góc tay so với phương ngang (độ, + = giơ lên) và độ khép vào giữa ngực
    private const float HoldPitch = -25f, PeekPitch = 22f, Inward = 0.15f;

    [MenuItem("Tools/Liars Card/Build Hold Cards Clips")]
    public static string Run()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var sm = ctrl.layers[0].stateMachine;
        var sitClip = sm.states.First(s => s.state.name == "sit").state.motion as AnimationClip;

        string log = "";
        foreach (var (name, pitch) in new[] { ("sitHold", HoldPitch), ("sitPeek", PeekPitch) })
        {
            var clip = MakeClip(sitClip, name, pitch, out string info);
            log += info + "\n";
            var st = sm.states.FirstOrDefault(s => s.state.name == name).state ?? sm.AddState(name);
            st.motion = clip;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return log;
    }

    private static AnimationClip MakeClip(AnimationClip sit, string name, float pitch, out string info)
    {
        var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        try
        {
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            sit.SampleAnimation(go, 0f);

            var bones = go.GetComponentsInChildren<Transform>();
            Transform B(string n) => bones.First(t => t.name == n);
            Transform root = go.transform;

            var clip = new AnimationClip { name = name };
            foreach (var b in AnimationUtility.GetCurveBindings(sit))
                AnimationUtility.SetEditorCurve(clip, b, AnimationUtility.GetEditorCurve(sit, b));

            info = name + ":";
            foreach (var side in new[] { "left", "right" })
            {
                Transform arm = B("arm-" + side);
                // Hướng ngang của vai so với tâm thân: tay trái / phải khép vào giữa ngực
                Vector3 lateral = Vector3.ProjectOnPlane(arm.position - root.position, Vector3.up);
                lateral = Vector3.Project(lateral, root.right).normalized;
                // Model Kenney quay mặt về +Z (xương *_end chĩa lệch nên không dùng để đoán hướng)
                Vector3 fwd = root.forward;
                float rad = pitch * Mathf.Deg2Rad;
                Vector3 dir = (fwd * Mathf.Cos(rad) + Vector3.up * Mathf.Sin(rad) - lateral * Inward).normalized;
                // Xương arm→arm_end của Kenney chĩa ngang, còn cánh tay nhìn thấy buông xuống:
                // lấy hướng thật từ tâm các đỉnh mesh gắn vào xương tay
                Vector3 visual = arm.TransformPoint(CharacterAnimator.ArmTipLocal(go.transform, arm)) - arm.position;
                arm.rotation = Quaternion.FromToRotation(visual, dir) * arm.rotation;

                string path = AnimationUtility.CalculateTransformPath(arm, root);
                Quaternion q = arm.localRotation;
                float[] v = { q.x, q.y, q.z, q.w };
                string[] c = { "x", "y", "z", "w" };
                for (int i = 0; i < 4; i++)
                {
                    var bind = EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + c[i]);
                    AnimationUtility.SetEditorCurve(clip, bind, AnimationCurve.Constant(0f, sit.length, v[i]));
                }
                info += $" {side} hand={root.InverseTransformPoint(arm.TransformPoint(CharacterAnimator.ArmTipLocal(go.transform, arm))):F2}";
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.EnsureQuaternionContinuity();

            string outPath = $"Assets/_Models/{char.ToUpper(name[0])}{name.Substring(1)}.anim";
            AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(clip, outPath);
            return clip;
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
