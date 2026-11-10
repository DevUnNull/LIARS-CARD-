using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiarsCard.Cards3D
{
    /// <summary>
    /// Hệ thống âm thanh bài và gameplay đồng bộ qua server.
    /// Hỗ trợ cả file âm thanh thực tế trong project (Assets/Audio/Card/) và âm thanh thủ tục (Procedural SFX).
    /// Hỗ trợ Spatial 3D Audio tại tọa độ thế giới của người chơi trên bàn đấu.
    /// </summary>
    public class CardAudioSystem : MonoBehaviour
    {
        public static CardAudioSystem Instance { get; private set; }

        [Header("Audio Clips (Tự động load từ Assets/Audio/Card hoặc gán qua Inspector)")]
        public AudioClip clipDanh1;
        public AudioClip clipDanh2;
        public AudioClip clipDanh3;
        public AudioClip clipTo;

        private AudioSource audioSource2D;
        private readonly Dictionary<string, AudioClip> loadedClips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

        // Procedural clips
        private AudioClip clipShuffle;
        private AudioClip clipSnap;
        private AudioClip clipThrow;
        private AudioClip clipFeltImpact;
        private AudioClip clipGunshot;
        private AudioClip clipDryFire;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            audioSource2D = gameObject.AddComponent<AudioSource>();
            audioSource2D.playOnAwake = false;
            audioSource2D.spatialBlend = 0f; // 2D clean audio

            GenerateProceduralAudioClips();
        }

        private void Start()
        {
            // Đăng ký các clip đã gán từ trước nếu có
            RegisterInspectorClips();

            // Phải nằm trong Resources mới có trong bản build (đọc file từ Assets/ chỉ chạy được trong Editor)
            foreach (var n in new[] { "Danh_1", "Danh_2", "Dnah_3", "To" })
                if (!loadedClips.ContainsKey(n))
                {
                    var clip = Resources.Load<AudioClip>("Audio/Card/" + n);
                    if (clip != null) loadedClips[n] = clip;
                }
        }

        private void RegisterInspectorClips()
        {
            if (clipDanh1 != null) loadedClips["Danh_1"] = clipDanh1;
            if (clipDanh2 != null) loadedClips["Danh_2"] = clipDanh2;
            if (clipDanh3 != null)
            {
                loadedClips["Danh_3"] = clipDanh3;
                loadedClips["Dnah_3"] = clipDanh3;
            }
            if (clipTo != null) loadedClips["To"] = clipTo;
        }

        /// <summary>
        /// Phát âm thanh Spatial 3D Audio tại tọa độ vị trí người chơi trên bàn đấu.
        /// </summary>
        public void PlaySpatialSfx(string soundName, Vector3 worldPosition)
        {
            AudioClip clip = GetClip(soundName);

            if (clip != null)
            {
                GameObject sfxObj = new GameObject($"SpatialSFX_{soundName}");
                sfxObj.transform.position = worldPosition;

                var src = sfxObj.AddComponent<AudioSource>();
                src.clip = clip;
                src.spatialBlend = 1f; // 100% 3D Audio
                src.minDistance = 1.5f;
                src.maxDistance = 30f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.volume = 1f;
                src.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
                src.Play();

                Destroy(sfxObj, clip.length + 0.3f);
            }
            else if (GameAudio.Has(soundName))
            {
                // File thật trong Resources/Audio/Sfx (súng thuốc đen, lên cò, cò hụt...); tiếng súng vang khắp quán
                bool loud = soundName.Equals("Gunshot", StringComparison.OrdinalIgnoreCase);
                GameAudio.PlayAt(soundName, worldPosition, 1f, 0f, loud ? 50f : 25f);
            }
            else
            {
                // Fallback procedural sounds
                if (soundName.Equals("Gunshot", StringComparison.OrdinalIgnoreCase))
                {
                    PlayGunshotSpatial(worldPosition);
                }
                else if (soundName.Equals("DryFire", StringComparison.OrdinalIgnoreCase))
                {
                    PlayDryFireSpatial(worldPosition);
                }
                else
                {
                    // Fallback to table impact
                    PlayTableImpactSpatial(worldPosition);
                }
            }
        }

        private AudioClip GetClip(string soundName)
        {
            if (string.IsNullOrEmpty(soundName)) return null;

            if (loadedClips.TryGetValue(soundName, out var clip) && clip != null)
            {
                return clip;
            }

            if (soundName.Equals("Danh_3", StringComparison.OrdinalIgnoreCase) && loadedClips.TryGetValue("Dnah_3", out var dnah))
            {
                return dnah;
            }
            if (soundName.Equals("Dnah_3", StringComparison.OrdinalIgnoreCase) && loadedClips.TryGetValue("Danh_3", out var danh))
            {
                return danh;
            }

            return null;
        }

        #region Procedural Fallbacks & Legacy Calls

        private void GenerateProceduralAudioClips()
        {
            clipShuffle = CreateNoiseClip("Card_Shuffle", 0.12f, 0.45f, 1800f, true);
            clipSnap = CreateSnapClip("Card_Snap", 0.05f, 0.7f, 2600f);
            clipThrow = CreateNoiseClip("Card_Throw", 0.22f, 0.5f, 900f, false);
            clipFeltImpact = CreateImpactClip("Card_FeltImpact", 0.14f, 0.85f, 320f);
            clipGunshot = CreateGunshotClip("Gunshot_Bang", 0.8f, 1f);
            clipDryFire = CreateSnapClip("Gun_DryFire", 0.06f, 0.9f, 3200f);
        }

        public void PlayShuffle()
        {
            if (GameAudio.Has("CardShuffle")) { GameAudio.Play2D("CardShuffle", 0.6f, 0.03f); return; }

            if (audioSource2D != null && clipShuffle != null)
            {
                audioSource2D.pitch = UnityEngine.Random.Range(0.95f, 1.08f);
                audioSource2D.PlayOneShot(clipShuffle, 0.6f);
            }
        }

        public void PlaySnap()
        {
            if (GameAudio.Has("CardSnap")) { GameAudio.Play2D("CardSnap", 0.7f, 0.03f); return; }

            if (audioSource2D != null && clipSnap != null)
            {
                audioSource2D.pitch = UnityEngine.Random.Range(0.92f, 1.12f);
                audioSource2D.PlayOneShot(clipSnap, 0.7f);
            }
        }

        public void PlayThrow()
        {
            if (GameAudio.Has("CardThrow")) { GameAudio.Play2D("CardThrow", 0.75f, 0.03f); return; }

            if (audioSource2D != null && clipThrow != null)
            {
                audioSource2D.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                audioSource2D.PlayOneShot(clipThrow, 0.75f);
            }
        }

        public void PlayTableImpact()
        {
            if (GameAudio.Has("CardImpact")) { GameAudio.Play2D("CardImpact", 0.85f, 0.03f); return; }

            if (audioSource2D != null && clipFeltImpact != null)
            {
                audioSource2D.pitch = UnityEngine.Random.Range(0.9f, 1.05f);
                audioSource2D.PlayOneShot(clipFeltImpact, 0.85f);
            }
        }

        private void PlayGunshotSpatial(Vector3 worldPos)
        {
            if (clipGunshot == null) return;
            GameObject sfx = new GameObject("Procedural_Gunshot");
            sfx.transform.position = worldPos;
            var src = sfx.AddComponent<AudioSource>();
            src.clip = clipGunshot;
            src.spatialBlend = 1f;
            src.minDistance = 2f;
            src.maxDistance = 50f;
            src.volume = 1f;
            src.Play();
            Destroy(sfx, clipGunshot.length + 0.2f);
        }

        private void PlayDryFireSpatial(Vector3 worldPos)
        {
            if (clipDryFire == null) return;
            GameObject sfx = new GameObject("Procedural_DryFire");
            sfx.transform.position = worldPos;
            var src = sfx.AddComponent<AudioSource>();
            src.clip = clipDryFire;
            src.spatialBlend = 1f;
            src.minDistance = 1.5f;
            src.maxDistance = 25f;
            src.volume = 1f;
            src.Play();
            Destroy(sfx, clipDryFire.length + 0.2f);
        }

        private void PlayTableImpactSpatial(Vector3 worldPos)
        {
            if (clipFeltImpact == null) return;
            GameObject sfx = new GameObject("Procedural_Impact");
            sfx.transform.position = worldPos;
            var src = sfx.AddComponent<AudioSource>();
            src.clip = clipFeltImpact;
            src.spatialBlend = 1f;
            src.minDistance = 1.5f;
            src.maxDistance = 25f;
            src.volume = 0.8f;
            src.Play();
            Destroy(sfx, clipFeltImpact.length + 0.2f);
        }

        private AudioClip CreateNoiseClip(string name, float duration, float volume, float filterFreq, bool isFlutter)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float lastSample = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float env = Mathf.Sin(t * Mathf.PI);
                if (isFlutter) env *= (0.6f + 0.4f * Mathf.Sin(t * 80f));

                float white = (UnityEngine.Random.value * 2f - 1f);
                float alpha = Mathf.Clamp01(filterFreq / sampleRate);
                lastSample = Mathf.Lerp(lastSample, white, alpha);

                samples[i] = lastSample * env * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateSnapClip(string name, float duration, float volume, float freq)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float env = Mathf.Exp(-t * 30f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)sampleRate));
                float click = (UnityEngine.Random.value * 2f - 1f) * 0.4f;

                samples[i] = (wave * 0.6f + click) * env * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateImpactClip(string name, float duration, float volume, float freq)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float env = Mathf.Exp(-t * 18f);
                float sub = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)sampleRate));
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.25f;

                samples[i] = (sub + noise) * env * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateGunshotClip(string name, float duration, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float sharpBlast = Mathf.Exp(-t * 60f) * (UnityEngine.Random.value * 2f - 1f);
                float lowBoom = Mathf.Exp(-t * 8f) * Mathf.Sin(2f * Mathf.PI * 70f * (i / (float)sampleRate));
                float tail = Mathf.Exp(-t * 4f) * (UnityEngine.Random.value * 2f - 1f) * 0.3f;

                samples[i] = (sharpBlast * 0.7f + lowBoom * 0.8f + tail) * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
