using System;
using UnityEngine;

namespace Cube.App
{
    /// 앱 안에서 짧은 효과음과 잔잔한 배경음을 만든다. 외부 음원 파일을 묶지 않아
    /// 라이선스와 기기별 압축 차이 없이 같은 소리를 낸다.
    public sealed class AudioService : MonoBehaviour
    {
        const int SampleRate = 24000;

        static AudioService _instance;
        static float _lastMoveAt = -10f;

        AudioSource _music;
        AudioSource _effects;
        AudioClip _click;
        AudioClip _move;
        AudioClip _success;

        public static void Init(Transform parent)
        {
            if (_instance == null)
            {
                var go = new GameObject("AudioService");
                if (parent != null) go.transform.SetParent(parent, false);
                _instance = go.AddComponent<AudioService>();
                _instance.Build();
            }
            Refresh();
        }

        void Build()
        {
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = 0.075f;
            _music.ignoreListenerPause = true;
            _music.clip = CreateMusic();

            _effects = gameObject.AddComponent<AudioSource>();
            _effects.playOnAwake = false;
            _effects.volume = 1f;
            _effects.ignoreListenerPause = true;

            _click = CreateClip("SoftClick", 0.075f, t =>
            {
                float env = Mathf.Exp(-34f * t);
                return env * (0.65f * Mathf.Sin(2f * Mathf.PI * 520f * t)
                              + 0.25f * Mathf.Sin(2f * Mathf.PI * 780f * t));
            });
            _move = CreateClip("CubeMove", 0.11f, t =>
            {
                float env = Mathf.Exp(-26f * t);
                float f = Mathf.Lerp(310f, 185f, t / 0.11f);
                return env * (0.7f * Mathf.Sin(2f * Mathf.PI * f * t)
                              + 0.22f * Mathf.Sin(2f * Mathf.PI * f * 2.03f * t));
            });
            _success = CreateClip("Solved", 0.62f, t =>
            {
                float attack = Mathf.Clamp01(t / 0.025f);
                float release = Mathf.Clamp01((0.62f - t) / 0.28f);
                float env = attack * release;
                return env * (Mathf.Sin(2f * Mathf.PI * 523.25f * t)
                              + Mathf.Sin(2f * Mathf.PI * 659.25f * t)
                              + Mathf.Sin(2f * Mathf.PI * 783.99f * t)) / 3f;
            });
        }

        static AudioClip CreateMusic()
        {
            float[] notes =
            {
                261.63f, 329.63f, 392.00f, 329.63f,
                220.00f, 261.63f, 329.63f, 392.00f,
                246.94f, 293.66f, 369.99f, 293.66f,
                196.00f, 246.94f, 293.66f, 392.00f,
            };
            const float step = 0.5f;
            float duration = notes.Length * step;
            return CreateClip("FocusLoop", duration, t =>
            {
                int index = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / step));
                float local = t - index * step;
                float env = Mathf.Sin(Mathf.PI * local / step);
                env *= env;
                float f = notes[index];
                float tone = Mathf.Sin(2f * Mathf.PI * f * t)
                           + 0.22f * Mathf.Sin(2f * Mathf.PI * f * 2f * t);
                return tone * env * 0.62f;
            });
        }

        static AudioClip CreateClip(string name, float duration, Func<float, float> sample)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
                data[i] = Mathf.Clamp(sample(i / (float)SampleRate), -1f, 1f);
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void Refresh()
        {
            if (_instance == null) return;
            if (AppSettings.BackgroundMusic)
            {
                if (!_instance._music.isPlaying) _instance._music.Play();
            }
            else
            {
                _instance._music.Stop();
            }
            _instance._effects.mute = !AppSettings.SoundEffects;
        }

        public static void PlayClick()
        {
            if (_instance == null || !AppSettings.SoundEffects) return;
            if (Time.unscaledTime - _lastMoveAt < 0.06f) return;
            _instance._effects.PlayOneShot(_instance._click, 0.16f);
        }

        public static void PlayMove()
        {
            _lastMoveAt = Time.unscaledTime;
            if (_instance == null || !AppSettings.SoundEffects) return;
            _instance._effects.PlayOneShot(_instance._move, 0.12f);
        }

        public static void PlaySuccess()
        {
            if (_instance == null || !AppSettings.SoundEffects) return;
            _instance._effects.PlayOneShot(_instance._success, 0.22f);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
