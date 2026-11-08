using System.Collections;
using UnityEngine;

namespace LiarsCard.Cards3D
{
    public enum GunStage : byte { OnTable = 0, Lifting = 1, Aiming = 2, Fired = 3 }

    /// <summary>
    /// Khẩu súng Nga đặt trên bàn. Khi có người bị phạt (gói 11) súng được người đó nhấc lên, lên đạn rồi dí vào thái dương;
    /// mọi client tự chạy cùng một animation và súng đi theo đầu người cầm (tư thế đầu người cầm lấy từ avatar + gói 13).
    /// Khi có kết quả (gói 12) súng "đoàng" hoặc "tạch" rồi được đặt lại lên bàn.
    /// Quy ước model: nòng hướng +Z, đỉnh súng +Y (prefab đã xoay model cho khớp).
    /// </summary>
    public class RevolverGun : MonoBehaviour
    {
        public static RevolverGun Instance { get; private set; }

        [Header("Bộ phận (tự tìm theo tên nếu để trống)")]
        public Transform hammer;
        public Transform cylinder;
        public Transform trigger;
        public Transform muzzle;

        [Header("Dí vào đầu (tọa độ tương đối đầu người cầm: X phải, Y lên, Z trước)")]
        public float heldScale = 1.5f;
        public Vector3 templeLocal = new Vector3(0.09f, 0.03f, 0f);
        public Vector3 barrelDirLocal = new Vector3(-0.55f, -0.05f, -0.83f);
        public float headAboveRoot = 0.6f;     // camera cách gốc nhân vật 0.6m (giống Player)
        [Tooltip("Nửa bề rộng đầu model nhân vật (mét): súng của người chơi khác được dí vào mặt bên đầu model.")]
        public float avatarHeadHalfWidth = 0.4f;

        [Header("Thời gian (giây) và góc")]
        public float liftDuration = 0.9f;
        public float cockDuration = 0.35f;
        public float returnDelay = 1.5f;
        public float returnDuration = 0.8f;
        public float cockAngle = -45f;         // búa ngửa ra sau
        public float cylinderStep = 60f;       // mỗi lần lên đạn / bóp cò, ổ đạn xoay 1/6 vòng

        private enum Mode { Rest, Lift, Hold, Return }

        public GunStage Stage { get; private set; } = GunStage.OnTable;
        /// <summary>Người chơi này đang cầm súng, súng đã lên đạn và sẵn sàng bóp cò.</summary>
        public bool IsReadyToFire => holderIsMe && Stage == GunStage.Aiming;

        private Mode mode = Mode.Rest;
        private bool busy;
        private string holderId;
        private bool holderIsMe;
        private bool? pendingHit;
        private int pendingShot;
        private float ignoreBeginUntil;

        private Vector3 restPos;
        private Quaternion restRot;
        private float restScale;
        private Vector3 muzzleLocal;           // vị trí đầu nòng trong không gian local của súng
        private Quaternion hammerBase, cylBase, trigBase;

        private float liftT, returnT;
        private float hammerAngle, cylAngle, triggerAngle, recoil;
        private Vector3 returnFromPos; private Quaternion returnFromRot; private float returnFromScale;

        private float remotePitch, remotePitchCur;
        private float nextSend;
        private float savedNear = -1f;
        private GameObject flash;

        private void Awake()
        {
            Instance = this;

            if (hammer == null) hammer = FindPart("Hammer");
            if (cylinder == null) cylinder = FindPart("Cylinder");
            if (trigger == null) trigger = FindPart("Trigger");
            if (muzzle == null) muzzle = FindPart("Muzzle");

            restPos = transform.position;
            restRot = transform.rotation;
            restScale = transform.lossyScale.x;
            muzzleLocal = muzzle != null ? transform.InverseTransformPoint(muzzle.position) : new Vector3(0f, 0.05f, 0.24f);

            if (hammer != null) hammerBase = Quaternion.Inverse(transform.rotation) * hammer.rotation;
            if (cylinder != null) cylBase = Quaternion.Inverse(transform.rotation) * cylinder.rotation;
            if (trigger != null) trigBase = Quaternion.Inverse(transform.rotation) * trigger.rotation;

            BuildMuzzleFlash();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private Transform FindPart(string partName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == partName) return t;
            return null;
        }

        #region Public API (gọi từ CardBattleUI / NetworkManager)

        /// <summary>Có người bị phạt: súng được người đó nhấc lên. An toàn khi gọi nhiều lần.</summary>
        public void BeginPenalty(string holderPlayerId, bool force = false)
        {
            // ignoreBeginUntil: polling có thể còn báo "đang chờ bóp cò" vài giây sau khi đã bắn; không nhấc súng lại.
            // force = true khi đây là lần tố cáo mới (server/người tố báo trực tiếp), không phải polling cũ.
            if (busy || Stage != GunStage.OnTable || (!force && Time.time < ignoreBeginUntil) || string.IsNullOrEmpty(holderPlayerId)) return;

            holderId = holderPlayerId;
            holderIsMe = IsMe(holderPlayerId);
            remotePitch = remotePitchCur = 0f;
            StartCoroutine(PenaltyRoutine());
        }

        /// <summary>Kết quả bóp cò từ server (đoàng / tạch). Gọi trùng thì bỏ qua.</summary>
        public void ResolveShot(string victimId, bool bulletHit, int shotNumber = 0)
        {
            if (Stage == GunStage.Fired) return;

            if (Stage == GunStage.OnTable)
            {
                BeginPenalty(victimId);   // lỡ không nhận gói bắt đầu phạt: vẫn chạy đủ animation
                if (Stage == GunStage.OnTable) return;
            }

            if (Stage == GunStage.Aiming) StartCoroutine(FireRoutine(bulletHit, shotNumber));
            else { pendingHit = bulletHit; pendingShot = shotNumber; }  // súng còn đang nhấc/lên đạn: bắn ngay khi sẵn sàng
        }

        /// <summary>Tư thế đầu người cầm súng do server chuyển tiếp (gói 13).</summary>
        public void OnRemoteGunPose(string holderPlayerId, byte stage, float pitch)
        {
            if (IsMe(holderPlayerId)) return;

            remotePitch = pitch;
            if (Stage == GunStage.OnTable && stage >= 1) BeginPenalty(holderPlayerId);
        }

        /// <summary>Bắt đầu/kết thúc trận: đặt súng về lại bàn ngay lập tức.</summary>
        /// <summary>Đổi chỗ súng nằm chờ (khi người chơi ngồi vào bàn khác trong quán).</summary>
        public void SetRestPose(Vector3 pos, Quaternion rot)
        {
            restPos = pos;
            restRot = rot;
            if (Stage == GunStage.OnTable) transform.SetPositionAndRotation(pos, rot);
        }

        public void ResetToTable()
        {
            StopAllCoroutines();
            mode = Mode.Rest;
            busy = false;
            pendingHit = null;
            holderId = null;
            holderIsMe = false;
            Stage = GunStage.OnTable;
            hammerAngle = triggerAngle = recoil = 0f;
            SetNearClip(false);
            if (flash != null) flash.SetActive(false);

            transform.SetPositionAndRotation(restPos, restRot);
            transform.localScale = Vector3.one * restScale;
            ApplyParts();
        }

        #endregion

        #region Animation

        private IEnumerator PenaltyRoutine()
        {
            busy = true;
            Stage = GunStage.Lifting;
            if (holderIsMe) SetNearClip(true);

            // 1. Nhấc súng từ bàn lên tới thái dương người bị phạt
            mode = Mode.Lift;
            for (float t = 0f; t < liftDuration; t += Time.deltaTime)
            {
                liftT = t / liftDuration;
                yield return null;
            }
            liftT = 1f;
            mode = Mode.Hold;

            // 2. Lên đạn: búa ngửa ra sau, ổ đạn xoay 1 nấc, "cạch"
            PlaySfx("DryFire");
            for (float t = 0f; t < cockDuration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / cockDuration);
                hammerAngle = Mathf.Lerp(0f, cockAngle, k);
                cylAngle += cylinderStep * Time.deltaTime / cockDuration;
                yield return null;
            }
            hammerAngle = cockAngle;

            Stage = GunStage.Aiming;

            if (pendingHit.HasValue)
                yield return FireRoutine(pendingHit.Value, pendingShot);
        }

        private IEnumerator FireRoutine(bool bulletHit, int shotNumber)
        {
            Stage = GunStage.Fired;
            pendingHit = null;
            ShowShotText(shotNumber, bulletHit);

            // Bóp cò: cò kéo vào, búa đập xuống
            triggerAngle = 20f;
            for (float t = 0f; t < 0.05f; t += Time.deltaTime)
            {
                hammerAngle = Mathf.Lerp(cockAngle, 0f, t / 0.05f);
                yield return null;
            }
            hammerAngle = 0f;

            if (bulletHit)
            {
                PlaySfx("Gunshot");
                if (flash != null) flash.SetActive(true);

                for (float t = 0f; t < 0.07f; t += Time.deltaTime)
                {
                    recoil = t / 0.07f;
                    yield return null;
                }
                recoil = 1f;
                if (flash != null) flash.SetActive(false);

                for (float t = 0f; t < 0.4f; t += Time.deltaTime)
                {
                    recoil = 1f - t / 0.4f;
                    yield return null;
                }
                recoil = 0f;
            }
            else
            {
                PlaySfx("DryFire");   // tạch!
                recoil = 0.12f;
                yield return new WaitForSeconds(0.12f);
                recoil = 0f;
            }
            triggerAngle = 0f;

            // 3. Giữ súng một lúc rồi đặt lại lên bàn
            yield return new WaitForSeconds(returnDelay);

            returnFromPos = transform.position;
            returnFromRot = transform.rotation;
            returnFromScale = transform.lossyScale.x;
            mode = Mode.Return;
            for (float t = 0f; t < returnDuration; t += Time.deltaTime)
            {
                returnT = t / returnDuration;
                yield return null;
            }

            ResetToTable();
            ignoreBeginUntil = Time.time + 4f;
        }

        #endregion

        #region Pose

        private void LateUpdate()
        {
            if (mode != Mode.Rest)
            {
                remotePitchCur = Mathf.Lerp(remotePitchCur, remotePitch, 1f - Mathf.Exp(-12f * Time.deltaTime));

                Vector3 pos; Quaternion rot; float scale;
                if (mode == Mode.Return)
                {
                    float e = Mathf.SmoothStep(0f, 1f, returnT);
                    pos = Vector3.Lerp(returnFromPos, restPos, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.2f);
                    rot = Quaternion.Slerp(returnFromRot, restRot, e);
                    scale = Mathf.Lerp(returnFromScale, restScale, e);
                    transform.SetPositionAndRotation(pos, rot);
                    transform.localScale = Vector3.one * scale;
                }
                else if (TryGetAimPose(out var aimPos, out var aimRot))
                {
                    if (mode == Mode.Lift)
                    {
                        float e = Mathf.SmoothStep(0f, 1f, liftT);
                        pos = Vector3.Lerp(restPos, aimPos, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.25f);
                        rot = Quaternion.Slerp(restRot, aimRot, e);
                        scale = Mathf.Lerp(restScale, heldScale, e);
                    }
                    else
                    {
                        // Giật nòng khi bắn + rung nhẹ cho tay run
                        Vector3 back = -(aimRot * Vector3.forward) * (0.10f * heldScale * recoil);
                        float s = Time.time;
                        Vector3 sway = (aimRot * Vector3.right) * (Mathf.Sin(s * 1.3f) * 0.002f)
                                     + (aimRot * Vector3.up) * (Mathf.Sin(s * 1.7f) * 0.002f);
                        pos = aimPos + back + sway;
                        rot = aimRot * Quaternion.AngleAxis(-14f * recoil, Vector3.right);
                        scale = heldScale;
                    }
                    transform.SetPositionAndRotation(pos, rot);
                    transform.localScale = Vector3.one * scale;
                }
            }

            ApplyParts();
        }

        // Nhãn "X/6" luôn hiện trên đầu do RemoteHands vẽ; ở đây chỉ báo phát vừa bóp để nhãn nháy đúng số đó
        private void ShowShotText(int shotNumber, bool hit)
        {
            RemoteHands.Instance.FlashShot(holderId, shotNumber, hit, returnDelay + 1.5f);
        }

        private void ApplyParts()
        {
            Quaternion r = transform.rotation;
            if (hammer != null) hammer.rotation = r * Quaternion.AngleAxis(hammerAngle, Vector3.right) * hammerBase;
            if (cylinder != null) cylinder.rotation = r * Quaternion.AngleAxis(cylAngle, Vector3.forward) * cylBase;
            if (trigger != null) trigger.rotation = r * Quaternion.AngleAxis(triggerAngle, Vector3.right) * trigBase;
        }

        // Tư thế súng khi dí vào thái dương phải của người cầm: đầu nòng chạm thái dương, nòng chếch từ phía trước-phải.
        private bool TryGetAimPose(out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = Quaternion.identity;
            if (!TryGetHeadFrame(out var head, out var headRot)) return false;

            rot = headRot * Quaternion.LookRotation(barrelDirLocal.normalized, Vector3.up);
            Vector3 temple = head + headRot * templeLocal;
            pos = temple - rot * (muzzleLocal * heldScale);
            return true;
        }

        private bool TryGetHeadFrame(out Vector3 head, out Quaternion headRot)
        {
            head = default;
            headRot = Quaternion.identity;

            if (holderIsMe)
            {
                var cam = LocalCamera();
                if (cam == null) return false;
                head = cam.transform.position;
                headRot = cam.transform.rotation;
                return true;
            }

            var remotes = RemotePlayerManager.Instance;
            if (remotes != null && remotes.TryGetRemote(holderId, out var avatar))
            {
                headRot = Quaternion.Euler(remotePitchCur, avatar.eulerAngles.y, 0f);
                // Model nhân vật đầu to: dí súng vào mặt bên của đầu model (chỗ nắm tay ở clip gun-to-head)
                if (avatar.TryGetComponent<CharacterAnimator>(out var anim) && anim.TryGetHeadCenter(out var center))
                    head = center + avatar.right * avatarHeadHalfWidth;
                else
                    head = avatar.position + Vector3.up * headAboveRoot;
                return true;
            }
            return false;
        }

        /// <summary>Người chơi có avatar này đang nhấc súng dí vào đầu (để CharacterAnimator chơi clip gun-to-head).</summary>
        public bool IsHeldBy(Transform avatar)
        {
            if (string.IsNullOrEmpty(holderId) || (mode != Mode.Lift && mode != Mode.Hold)) return false;
            if (holderIsMe) return avatar.GetComponent<PlayerController>() != null;
            var remotes = RemotePlayerManager.Instance;
            return remotes != null && remotes.TryGetRemote(holderId, out var a) && a == avatar;
        }

        private static Camera LocalCamera()
        {
            var pc = PlayerController.Instance;
            if (pc != null && pc.firstPersonLook != null && pc.firstPersonLook.playerCamera != null)
                return pc.firstPersonLook.playerCamera;
            return Camera.main;
        }

        // Súng gần mắt hơn mặt phẳng cắt gần mặc định (0.3m) nên hạ near clip khi mình cầm súng
        private void SetNearClip(bool on)
        {
            var cam = LocalCamera();
            if (cam == null) return;

            if (on)
            {
                if (savedNear < 0f) savedNear = cam.nearClipPlane;
                cam.nearClipPlane = 0.03f;
            }
            else if (savedNear >= 0f)
            {
                cam.nearClipPlane = savedNear;
                savedNear = -1f;
            }
        }

        #endregion

        #region Network + helpers

        private void Update()
        {
            // Người cầm súng gửi tư thế đầu (10 lần/giây) để server chuyển tiếp cho cả bàn
            if (!holderIsMe || (Stage != GunStage.Lifting && Stage != GunStage.Aiming)) return;
            if (Time.unscaledTime < nextSend) return;
            nextSend = Time.unscaledTime + 0.1f;

            var cam = LocalCamera();
            var net = NetworkManager.Instance;
            if (cam == null || net == null) return;

            float pitch = cam.transform.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            net.SendGunPose((byte)Stage, pitch);
        }

        private static bool IsMe(string playerId)
        {
            var session = TableSessionManager.Instance;
            string me = session != null ? session.MyPlayerId : null;
            return !string.IsNullOrEmpty(me) && string.Equals(me, playerId, System.StringComparison.OrdinalIgnoreCase);
        }

        private void PlaySfx(string sound)
        {
            var audio = CardAudioSystem.Instance;
            if (audio != null) audio.PlaySpatialSfx(sound, muzzle != null ? muzzle.position : transform.position);
        }

        private void BuildMuzzleFlash()
        {
            if (muzzle == null) return;

            flash = new GameObject("MuzzleFlash");
            flash.transform.SetParent(muzzle, false);

            var l = flash.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.7f, 0.3f);
            l.intensity = 4f;
            l.range = 4f;

            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.transform.SetParent(flash.transform, false);
            orb.transform.localScale = Vector3.one * 0.08f;
            Destroy(orb.GetComponent<Collider>());
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var r = orb.GetComponent<MeshRenderer>();
            r.sharedMaterial = new Material(shader) { color = new Color(1f, 0.85f, 0.35f) };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            flash.SetActive(false);
        }

        #endregion
    }
}
