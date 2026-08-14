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
        AudioClip[] _cuteMoves = Array.Empty<AudioClip>();
        AudioClip[] _realMoves = Array.Empty<AudioClip>();
        int _lastCuteMove = -1;
        int _lastRealMove = -1;

        public static int CuteMoveClipCount
            => _instance != null && _instance._cuteMoves != null
                ? _instance._cuteMoves.Length
                : 0;

        public static int RealisticMoveClipCount
            => _instance != null && _instance._realMoves != null
                ? _instance._realMoves.Length
                : 0;

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

            _cuteMoves = new[]
            {
                CreateCuteMove("MallowPop1", 510f, 741),
                CreateCuteMove("MallowPop2", 565f, 902),
                CreateCuteMove("MallowPop3", 625f, 1187),
            };

            _realMoves = Resources.LoadAll<AudioClip>("Audio/CubeTurns");
            Array.Sort(_realMoves, (a, b) => string.CompareOrdinal(a.name, b.name));
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

        static AudioClip CreateCuteMove(string name, float baseFrequency, int seed)
        {
            const float duration = 0.115f;
            var random = new System.Random(seed);
            return CreateClip(name, duration, t =>
            {
                float progress = t / duration;
                float attack = Mathf.Clamp01(t / 0.004f);
                float roundEnvelope = attack * Mathf.Exp(-29f * t);
                float frequency = Mathf.Lerp(baseFrequency, baseFrequency * 0.64f, progress);
                float roundTone = Mathf.Sin(2f * Mathf.PI * frequency * t)
                                + 0.24f * Mathf.Sin(2f * Mathf.PI * frequency * 1.5f * t);

                float sparkleTime = Mathf.Max(0f, t - 0.018f);
                float sparkleEnvelope = Mathf.Clamp01(sparkleTime / 0.007f)
                                      * Mathf.Exp(-42f * sparkleTime);
                float sparkle = Mathf.Sin(2f * Mathf.PI * baseFrequency * 2.02f * t);

                float softTick = t < 0.01f
                    ? (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-190f * t)
                    : 0f;
                return 0.52f * roundTone * roundEnvelope
                     + 0.10f * sparkle * sparkleEnvelope
                     + 0.025f * softTick;
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
            _instance._effects.mute = AppSettings.CubeSound == CubeSoundMode.Off;
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
            if (_instance == null || AppSettings.CubeSound == CubeSoundMode.Off) return;

            if (AppSettings.CubeSound == CubeSoundMode.Cute
                && _instance._cuteMoves.Length > 0)
            {
                int index = UnityEngine.Random.Range(0, _instance._cuteMoves.Length);
                if (_instance._cuteMoves.Length > 1 && index == _instance._lastCuteMove)
                    index = (index + 1) % _instance._cuteMoves.Length;
                _instance._lastCuteMove = index;
                _instance._effects.PlayOneShot(_instance._cuteMoves[index], 0.18f);
                return;
            }

            if (AppSettings.CubeSound == CubeSoundMode.Realistic
                && _instance._realMoves.Length > 0)
            {
                int index = UnityEngine.Random.Range(0, _instance._realMoves.Length);
                if (_instance._realMoves.Length > 1 && index == _instance._lastRealMove)
                    index = (index + 1) % _instance._realMoves.Length;
                _instance._lastRealMove = index;
                _instance._effects.PlayOneShot(_instance._realMoves[index], 0.34f);
                return;
            }

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
