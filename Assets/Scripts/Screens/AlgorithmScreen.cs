using System;
using UnityEngine;
using UnityEngine.UI;
using Cube.Core;

namespace Cube.App
{
    /// 공식 모아보기. 카드를 누르면 큐브에서 시연한다.
    public sealed class AlgorithmScreen : MonoBehaviour
    {
        Palette _p;
        CubeRenderer _renderer;
        LayerRotator _rotator;
        TouchController _touch;
        LessonPlayer _player;
        Text _status;

        public void Build(RectTransform parent, Action onBack)
        {
            _p = ThemeService.Current;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            var cubeRoot = AppBootstrap.Instance != null
                ? AppBootstrap.Instance.CubeRoot
                : new GameObject("CubeRoot").transform;
            _renderer = GetOrAdd<CubeRenderer>(cubeRoot.gameObject);
            _rotator = GetOrAdd<LayerRotator>(cubeRoot.gameObject);
            _touch = GetOrAdd<TouchController>(cubeRoot.gameObject);
            _player = GetOrAdd<LessonPlayer>(cubeRoot.gameObject);

            var title = UiKit.Label(transform, "Title", "공식 모아보기", 40, _p.TextPrimary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)title.transform, new Vector2(0f, 0.91f), new Vector2(1f, 0.97f), new Vector4(48, 0, 48, 0));

            var list = UiKit.Panel(transform, "List", new Color(0, 0, 0, 0));
            UiKit.Stretch(list, new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.55f), Vector4.zero);

            var library = LessonData.Library;
            for (int i = 0; i < library.Count; i++)
            {
                var alg = library[i];
                var btn = UiKit.Button(list, $"Alg{i}", "", _p, () => Play(alg));
                var rt = (RectTransform)btn.transform;
                rt.anchorMin = new Vector2(0f, 1f - (i + 1f) / library.Count);
                rt.anchorMax = new Vector2(1f, 1f - i / (float)library.Count);
                rt.offsetMin = new Vector2(0f, 2f);
                rt.offsetMax = new Vector2(0f, -2f);

                var label = btn.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 24;
                label.text = $"▶  {alg.Name}      {alg.Notation}";
                UiKit.Stretch((RectTransform)label.transform, Vector2.zero, Vector2.one, new Vector4(20, 0, 20, 0));
            }

            _status = UiKit.Label(transform, "Status", "카드를 누르면 큐브에서 보여줍니다", 24,
                _p.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.Stretch((RectTransform)_status.transform, new Vector2(0f, 0.09f), new Vector2(1f, 0.135f), Vector4.zero);

            var reset = UiKit.Button(transform, "Reset", "큐브 되돌리기", _p, ResetCube);
            UiKit.Stretch((RectTransform)reset.transform, new Vector2(0.06f, 0.015f), new Vector2(0.48f, 0.08f), Vector4.zero);

            var back = UiKit.Button(transform, "Back", "돌아가기", _p, () => onBack?.Invoke());
            UiKit.Stretch((RectTransform)back.transform, new Vector2(0.52f, 0.015f), new Vector2(0.94f, 0.08f), Vector4.zero);

            ResetCube();
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        public void ResetCube()
        {
            _rotator.FinishAllImmediately();
            _renderer.Build(CubeState.Solved(3));
            _rotator.Init(_renderer);

            var cam = AppBootstrap.Instance != null ? AppBootstrap.Instance.CubeCamera : Camera.main;
            var orbit = GetOrAdd<OrbitCamera>(_renderer.gameObject);
            if (cam != null) _touch.Init(cam, _renderer, _rotator, orbit);
            _player.Init(_renderer, _rotator, _touch);

            if (_status != null) _status.text = "카드를 누르면 큐브에서 보여줍니다";
        }

        void Play(Algorithm alg)
        {
            _status.text = $"{alg.Name} — {alg.When}";
            _player.Play(alg.Notation);
        }
    }
}
