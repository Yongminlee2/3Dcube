using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cube.App
{
    public sealed class HomeScreen : MonoBehaviour
    {
        int _size = 3;
        Button[] _sizeButtons;
        Palette _p;
        Text _notice;

        Action _onLearn;

        public void Build(RectTransform parent, Action<int> onPractice, Action onLearn,
                          Action onRecords, Action onSettings)
        {
            _onLearn = onLearn;
            _p = ThemeService.Current;
            _size = AppSettings.CubeSize;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            var title = UiKit.Label(transform, "Title", "큐브", 72, _p.TextPrimary);
            UiKit.Stretch((RectTransform)title.transform, new Vector2(0f, 0.78f), new Vector2(1f, 0.90f), Vector4.zero);

            var sizes = UiKit.Panel(transform, "Sizes", new Color(0, 0, 0, 0));
            UiKit.Stretch(sizes, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.73f), Vector4.zero);
            _sizeButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int size = i + 2;
                var btn = UiKit.Button(sizes, $"Size{size}", $"{size}×{size}", _p, () => SelectSize(size));
                var rt = (RectTransform)btn.transform;
                rt.anchorMin = new Vector2(i / 3f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 3f, 1f);
                rt.offsetMin = new Vector2(6f, 0f);
                rt.offsetMax = new Vector2(-6f, 0f);
                _sizeButtons[i] = btn;
            }

            MakeMenuButton("연습 시작", 0.48f, () => onPractice?.Invoke(_size));

            var learn = UiKit.Button(transform, "Learn", "배우기", _p, OpenLearn);
            UiKit.Stretch((RectTransform)learn.transform, new Vector2(0.08f, 0.37f), new Vector2(0.92f, 0.46f), Vector4.zero);

            MakeMenuButton("기록", 0.26f, () => onRecords?.Invoke());
            MakeMenuButton("설정", 0.15f, () => onSettings?.Invoke());

            _notice = UiKit.Label(transform, "Notice", "", 26, _p.TextSecondary);
            UiKit.Stretch((RectTransform)_notice.transform, new Vector2(0f, 0.07f), new Vector2(1f, 0.13f), Vector4.zero);

            RefreshSizeButtons();
        }

        void MakeMenuButton(string label, float yMin, Action action)
        {
            var btn = UiKit.Button(transform, $"Menu_{label}", label, _p, () => action());
            UiKit.Stretch((RectTransform)btn.transform,
                new Vector2(0.08f, yMin), new Vector2(0.92f, yMin + 0.09f), Vector4.zero);
        }

        /// 학습 코스는 3×3만 있다. 다른 크기를 고른 상태면 안내만 하고 넘기지 않는다.
        void OpenLearn()
        {
            if (_size != 3)
            {
                if (_notice != null) _notice.text = "배우기는 3×3부터 시작합니다. 3×3을 골라 주세요.";
                return;
            }
            if (_notice != null) _notice.text = "";
            _onLearn?.Invoke();
        }

        void SelectSize(int size)
        {
            _size = size;
            AppSettings.CubeSize = size;
            RefreshSizeButtons();
        }

        void RefreshSizeButtons()
        {
            for (int i = 0; i < _sizeButtons.Length; i++)
                _sizeButtons[i].image.color = (i + 2 == _size) ? _p.Accent : _p.Surface;
        }
    }
}
