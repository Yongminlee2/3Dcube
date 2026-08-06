using UnityEngine;
using Cube.Core;

namespace Cube.App
{
    /// 어디까지 배웠는지 기억한다.
    /// 1단계는 항상 열려 있고, N단계는 N-1단계를 마쳐야 열린다.
    public static class LearnProgress
    {
        const string Key = "cube.learn.completed";

        /// 마친 마지막 단계. 아무것도 안 했으면 0.
        public static int Completed
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, StageChecker.LastStage);
            set
            {
                PlayerPrefs.SetInt(Key, Mathf.Clamp(value, 0, StageChecker.LastStage));
                PlayerPrefs.Save();
            }
        }

        public static bool IsUnlocked(int stage) => stage >= 1 && stage <= Completed + 1;

        public static bool IsDone(int stage) => stage <= Completed;

        /// 뒤로 돌아가 다시 해도 진도가 깎이지 않는다.
        public static void MarkDone(int stage)
        {
            if (stage > Completed) Completed = stage;
        }

        public static void Reset() => Completed = 0;
    }
}
