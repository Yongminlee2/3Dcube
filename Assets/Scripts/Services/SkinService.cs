using System;
using System.Linq;
using UnityEngine;

namespace Cube.App
{
    /// 지금 쓰는 큐브 스킨을 들고 있고, 바뀌면 알린다. ThemeService와 짝이지만
    /// 다크/라이트 둘 중 하나가 아니라 이름 있는 여러 스킨 중 하나를 고른다.
    public static class SkinService
    {
        const string DefaultName = "Skin_Classic";

        static Skin[] _all;
        static Skin _current;

        public static Skin[] All
        {
            get { Init(); return _all; }
        }

        public static Skin Current
        {
            get { Init(); return _current; }
        }

        public static event Action<Skin> Changed;

        public static void Init()
        {
            if (_all == null || _all.Length == 0)
            {
                // Hidden인 것은 아예 걸러 낸다. 파일은 남아 있지만 목록에도 없고
                // 고를 수도 없다 — 되살리려면 에셋의 Hidden만 끄면 된다.
                _all = Resources.LoadAll<Skin>("Skins")
                    .Where(s => !s.Hidden)
                    .OrderBy(s => s.CharacterArtwork ? 1 : 0)
                    .ThenBy(s => s.name)
                    .ToArray();
                if (_all.Length == 0)
                    throw new MissingReferenceException(
                        "Assets/Resources/Skins에 스킨 에셋이 없다. ProjectSetup.CreateAssets를 돌릴 것");
            }
            if (_current == null)
                // 감춘 스킨을 이미 고른 채로 업데이트를 받았다면 기본 스킨으로 돌린다.
                // 그러지 않으면 목록에 없는 스킨이 계속 큐브에 입혀진 채로 남는다.
                _current = _all.FirstOrDefault(s => s.name == AppSettings.SkinName)
                        ?? _all.FirstOrDefault(s => s.name == DefaultName)
                        ?? _all[0];
        }

        public static void Apply(Skin skin)
        {
            Init();
            if (skin == null || skin == _current) return;
            _current = skin;
            AppSettings.SkinName = skin.name;
            Changed?.Invoke(_current);
        }

        public static SkinArtworkLayout ArtworkLayout => AppSettings.SkinArtworkLayout;

        public static void SetArtworkLayout(SkinArtworkLayout layout)
        {
            Init();
            if (AppSettings.SkinArtworkLayout == layout) return;
            AppSettings.SkinArtworkLayout = layout;
            Changed?.Invoke(_current);
        }
    }
}
