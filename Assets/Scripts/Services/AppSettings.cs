using UnityEngine;

namespace Cube.App
{
    public enum CubeSoundMode
    {
        Classic = 0,
        Realistic = 1,
        Off = 2,
        Cute = 3,
    }

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
        const string KeySkinArtworkLayout = "cube.skinArtworkLayout";
        const string KeyBackgroundMusic = "cube.backgroundMusic";
        const string KeySoundEffects = "cube.soundEffects";
        const string KeyCubeSoundMode = "cube.cubeSoundMode";

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

        /// 그림 스킨을 각 조각에 반복할지, 한 장을 NxN으로 나눌지 기억한다.
        /// 새 설치의 기본값은 사용자가 요청한 '한 면 전체' 방식이다.
        public static SkinArtworkLayout SkinArtworkLayout
        {
            get => (SkinArtworkLayout)Mathf.Clamp(
                PlayerPrefs.GetInt(KeySkinArtworkLayout, (int)Cube.App.SkinArtworkLayout.WholeFace), 0, 1);
            set
            {
                PlayerPrefs.SetInt(KeySkinArtworkLayout, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static bool BackgroundMusic
        {
            get => PlayerPrefs.GetInt(KeyBackgroundMusic, 1) != 0;
            set { PlayerPrefs.SetInt(KeyBackgroundMusic, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool SoundEffects
        {
            get => CubeSound != CubeSoundMode.Off;
            set
            {
                if (!value) CubeSound = CubeSoundMode.Off;
                else if (CubeSound == CubeSoundMode.Off) CubeSound = CubeSoundMode.Classic;
            }
        }

        /// 큐브를 돌릴 때 쓸 소리. 예전 켬/끔 설정도 그대로 마이그레이션한다.
        public static CubeSoundMode CubeSound
        {
            get
            {
                int legacyDefault = PlayerPrefs.GetInt(KeySoundEffects, 1) != 0
                    ? (int)CubeSoundMode.Classic
                    : (int)CubeSoundMode.Off;
                return (CubeSoundMode)Mathf.Clamp(
                    PlayerPrefs.GetInt(KeyCubeSoundMode, legacyDefault),
                    (int)CubeSoundMode.Classic, (int)CubeSoundMode.Cute);
            }
            set
            {
                int mode = Mathf.Clamp((int)value,
                    (int)CubeSoundMode.Classic, (int)CubeSoundMode.Cute);
                PlayerPrefs.SetInt(KeyCubeSoundMode, mode);
                PlayerPrefs.SetInt(KeySoundEffects,
                    mode == (int)CubeSoundMode.Off ? 0 : 1);
                PlayerPrefs.Save();
            }
        }
    }
}
