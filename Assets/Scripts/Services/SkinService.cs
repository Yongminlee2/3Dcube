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
                _all = Resources.LoadAll<Skin>("Skins").OrderBy(s => s.name).ToArray();
                if (_all.Length == 0)
                    throw new MissingReferenceException(
                        "Assets/Resources/Skins에 스킨 에셋이 없다. ProjectSetup.CreateAssets를 돌릴 것");
            }
            if (_current == null)
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
    }
}
