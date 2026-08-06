using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Cube.App
{
    [Serializable]
    public class SolveRecord
    {
        public long UnixMs;
        public double DurationMs;
        public string Scramble;
        public int Moves;
    }

    [Serializable]
    public class SessionData
    {
        public int CubeSize;
        public List<SolveRecord> Records = new List<SolveRecord>();
    }

    [Serializable]
    public class StoreFile
    {
        public List<SessionData> Sessions = new List<SessionData>();
    }

    public static class SessionStats
    {
        /// 최근 count개에서 최고와 최저를 하나씩 빼고 평균. 모자라면 null.
        public static double? Average(IReadOnlyList<SolveRecord> records, int count)
        {
            if (records == null || records.Count < count || count < 3) return null;

            var recent = new List<double>(count);
            for (int i = records.Count - count; i < records.Count; i++)
                recent.Add(records[i].DurationMs);

            recent.Sort();
            double sum = 0d;
            for (int i = 1; i < recent.Count - 1; i++) sum += recent[i];
            return sum / (recent.Count - 2);
        }

        public static double? Best(IReadOnlyList<SolveRecord> records)
        {
            if (records == null || records.Count == 0) return null;
            double best = double.MaxValue;
            foreach (var r in records) if (r.DurationMs < best) best = r.DurationMs;
            return best;
        }

        public static string Format(double ms)
        {
            double seconds = ms / 1000d;
            if (seconds < 60d) return seconds.ToString("F2");
            int minutes = (int)(seconds / 60d);
            return $"{minutes}:{(seconds - minutes * 60d):00.00}";
        }
    }

    /// 기록을 JSON 파일 하나에 담는다. 저장에 실패해도 앱은 계속 돈다.
    public sealed class SessionStore
    {
        public string FilePath { get; }
        public bool LastSaveFailed { get; private set; }

        StoreFile _data = new StoreFile();

        public SessionStore(string filePath = null)
        {
            FilePath = string.IsNullOrEmpty(filePath)
                ? Path.Combine(Application.persistentDataPath, "records.json")
                : filePath;
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) { _data = new StoreFile(); return; }
                _data = JsonUtility.FromJson<StoreFile>(File.ReadAllText(FilePath)) ?? new StoreFile();
                if (_data.Sessions == null) _data.Sessions = new List<SessionData>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SessionStore] 기록을 읽지 못했다: {e.Message}");
                _data = new StoreFile();
            }
        }

        public bool Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, JsonUtility.ToJson(_data));
                LastSaveFailed = false;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SessionStore] 기록을 쓰지 못했다: {e.Message}");
                LastSaveFailed = true;
                return false;
            }
        }

        SessionData SessionFor(int cubeSize)
        {
            foreach (var s in _data.Sessions) if (s.CubeSize == cubeSize) return s;
            var created = new SessionData { CubeSize = cubeSize };
            _data.Sessions.Add(created);
            return created;
        }

        public void Add(int cubeSize, SolveRecord r) => SessionFor(cubeSize).Records.Add(r);

        public IReadOnlyList<SolveRecord> Records(int cubeSize) => SessionFor(cubeSize).Records;

        public void Delete(int cubeSize, int index)
        {
            var list = SessionFor(cubeSize).Records;
            if (index >= 0 && index < list.Count) list.RemoveAt(index);
        }

        public void ClearSession(int cubeSize) => SessionFor(cubeSize).Records.Clear();
    }
}
