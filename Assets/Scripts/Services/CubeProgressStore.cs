using System;
using UnityEngine;
using Cube.Core;

namespace Cube.App
{
    [Serializable]
    public sealed class PracticeProgressSnapshot
    {
        public int Version = 2;
        public int CubeSize;
        public string FaceletsBase64;
        public string Scramble;
        public string HistoryNotation;
        public int MovesSinceScramble;
        public bool Armed;
        public bool FromRealCube;
        public int TimerPhase;
        public double TimerElapsedMs;
        public double InspectionRemainingMs;
        public bool ArtworkPending;

        public CubeState ToState() => CubeProgressStore.DecodeState(CubeSize, FaceletsBase64);
    }

    [Serializable]
    public sealed class LessonProgressSnapshot
    {
        public int Version = 1;
        public int Stage;
        public string FaceletsBase64;
        public int Page;
        public bool InPractice;

        public CubeState ToState() => CubeProgressStore.DecodeState(3, FaceletsBase64);
    }

    /// 연습·실물 큐브·단계 학습의 미완료 상태를 PlayerPrefs에 작게 저장한다.
    /// 큐브 한 칸은 byte 하나라 4×4도 96바이트뿐이므로 별도 파일이 필요 없다.
    public static class CubeProgressStore
    {
        const string PracticePrefix = "cube.progress.practice.";
        const string LessonPrefix = "cube.progress.lesson.";

        public static PracticeProgressSnapshot LoadPractice(int cubeSize)
            => Load<PracticeProgressSnapshot>(PracticePrefix + cubeSize,
                snapshot => snapshot.CubeSize == cubeSize && snapshot.ToState() != null);

        public static void SavePractice(PracticeProgressSnapshot snapshot)
        {
            if (snapshot == null || snapshot.CubeSize < 2 || snapshot.CubeSize > 4) return;
            Save(PracticePrefix + snapshot.CubeSize, snapshot);
        }

        public static bool HasUnfinishedPractice(int cubeSize)
        {
            var snapshot = LoadPractice(cubeSize);
            var state = snapshot?.ToState();
            return state != null && (!state.IsSolved() || snapshot.ArtworkPending);
        }

        public static void ClearPractice(int cubeSize)
            => Delete(PracticePrefix + cubeSize);

        public static LessonProgressSnapshot LoadLesson(int stage)
            => Load<LessonProgressSnapshot>(LessonPrefix + stage,
                snapshot => snapshot.Stage == stage && snapshot.ToState() != null);

        public static void SaveLesson(LessonProgressSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Stage < 1 || snapshot.Stage > StageChecker.LastStage) return;
            Save(LessonPrefix + snapshot.Stage, snapshot);
        }

        public static void ClearLesson(int stage)
            => Delete(LessonPrefix + stage);

        public static string EncodeState(CubeState state)
            => state == null ? "" : Convert.ToBase64String(state.Facelets);

        public static CubeState DecodeState(int cubeSize, string encoded)
        {
            if (cubeSize < 2 || cubeSize > 4 || string.IsNullOrEmpty(encoded)) return null;
            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                if (bytes.Length != Faces.Count * cubeSize * cubeSize) return null;
                for (int i = 0; i < bytes.Length; i++) if (bytes[i] >= Faces.Count) return null;

                var state = CubeState.Solved(cubeSize);
                Array.Copy(bytes, state.Facelets, bytes.Length);
                return state;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        public static void ClearAll()
        {
            for (int n = 2; n <= 4; n++) ClearPractice(n);
            for (int stage = 1; stage <= StageChecker.LastStage; stage++) ClearLesson(stage);
        }

        static T Load<T>(string key, Func<T, bool> validate) where T : class
        {
            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var snapshot = JsonUtility.FromJson<T>(json);
                if (snapshot != null && validate(snapshot)) return snapshot;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CubeProgressStore] 저장 상태를 읽지 못했다: {e.Message}");
            }
            Delete(key);
            return null;
        }

        static void Save<T>(string key, T snapshot)
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(snapshot));
            PlayerPrefs.Save();
        }

        static void Delete(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return;
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
