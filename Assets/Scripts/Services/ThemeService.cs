using System;
using UnityEngine;

namespace Cube.App
{
    /// 지금 쓰는 팔레트를 들고 있고, 바뀌면 알린다.
    public static class ThemeService
    {
        static Palette _dark, _light, _current;

        public static Palette Current
        {
            get { if (_current == null) Init(); return _current; }
        }

        public static event Action<Palette> Changed;

        public static void Init()
        {
            if (_dark == null) _dark = Resources.Load<Palette>("DarkPalette");
            if (_light == null) _light = Resources.Load<Palette>("LightPalette");
            if (_current == null) _current = AppSettings.DarkTheme ? _dark : _light;
        }

        public static void Apply(bool dark)
        {
            Init();
            AppSettings.DarkTheme = dark;
            _current = dark ? _dark : _light;
            Changed?.Invoke(_current);
        }
    }
}
