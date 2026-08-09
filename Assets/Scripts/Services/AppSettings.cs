using UnityEngine;

namespace Cube.App
{
    /// PlayerPrefs 얇은 껍데기. 키 문자열이 흩어지지 않게 한곳에 모은다.
    public static class AppSettings
    {
        const string KeyDark = "cube.darkTheme";
        const string KeyInspection = "cube.inspection";
        const string KeyShowPad = "cube.showPad";
        const string KeyAnimMs = "cube.animMs";
        const string KeySize = "cube.size";
        const string KeyShowNet = "cube.showNet";
        const string KeySkin = "cube.skin";

        public static bool DarkTheme
        {
            get => PlayerPrefs.GetInt(KeyDark, 1) != 0;
            set { PlayerPrefs.SetInt(KeyDark, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Inspection
        {
            get => PlayerPrefs.GetInt(KeyInspection, 0) != 0;
            set { PlayerPrefs.SetInt(KeyInspection, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool ShowPad
        {
            get => PlayerPrefs.GetInt(KeyShowPad, 1) != 0;
            set { PlayerPrefs.SetInt(KeyShowPad, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int AnimationMs
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyAnimMs, 220), 0, 400);
            set { PlayerPrefs.SetInt(KeyAnimMs, Mathf.Clamp(value, 0, 400)); PlayerPrefs.Save(); }
        }

        /// 전개도 미니맵을 보여줄지. 기본은 접힘이다 — 큐브가 화면 가운데를
        /// 넓게 차지하고, 필요할 때만 펴서 본다.
        public static bool ShowNet
        {
            get => PlayerPrefs.GetInt(KeyShowNet, 0) != 0;
            set { PlayerPrefs.SetInt(KeyShowNet, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int CubeSize
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeySize, 3), 2, 4);
            set { PlayerPrefs.SetInt(KeySize, Mathf.Clamp(value, 2, 4)); PlayerPrefs.Save(); }
        }

        /// 고른 스킨의 에셋 이름. 빈 문자열이면 SkinService가 기본값을 고른다.
        public static string SkinName
        {
            get => PlayerPrefs.GetString(KeySkin, "");
            set { PlayerPrefs.SetString(KeySkin, value); PlayerPrefs.Save(); }
        }
    }
}
