using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cube.App;

namespace Cube.App.Tests
{
    public class SessionStoreTests
    {
        string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Application.temporaryCachePath, "records-test.json");
            if (File.Exists(_path)) File.Delete(_path);
        }

        [TearDown]
        public void TearDown() { if (File.Exists(_path)) File.Delete(_path); }

        static SolveRecord R(double ms) => new SolveRecord { UnixMs = 0, DurationMs = ms, Scramble = "R U", Moves = 2 };

        [Test]
        public void 다섯_개가_모이면_ao5가_나온다()
        {
            var list = new List<SolveRecord> { R(10000), R(12000), R(11000), R(30000), R(9000) };
            // 최고 9000과 최저 30000을 빼고 10000·12000·11000의 평균 = 11000
            Assert.AreEqual(11000d, SessionStats.Average(list, 5).Value, 0.5d);
        }

        [Test]
        public void 개수가_모자라면_평균이_없다()
        {
            var list = new List<SolveRecord> { R(1000), R(2000), R(3000), R(4000) };
            Assert.IsNull(SessionStats.Average(list, 5));
            Assert.IsNull(SessionStats.Average(list, 12));
        }

        [Test]
        public void 평균은_가장_최근_것들만_본다()
        {
            var list = new List<SolveRecord> { R(99000), R(10000), R(12000), R(11000), R(30000), R(9000) };
            Assert.AreEqual(11000d, SessionStats.Average(list, 5).Value, 0.5d);
        }

        [Test]
        public void 최고_기록은_가장_짧은_시간이다()
        {
            var list = new List<SolveRecord> { R(10000), R(8000), R(12000) };
            Assert.AreEqual(8000d, SessionStats.Best(list).Value, 0.5d);
            Assert.IsNull(SessionStats.Best(new List<SolveRecord>()));
        }

        [Test]
        public void 시간_표기는_분과_초를_가른다()
        {
            Assert.AreEqual("12.48", SessionStats.Format(12480d));
            Assert.AreEqual("1:05.30", SessionStats.Format(65300d));
        }

        [Test]
        public void 저장했다_다시_읽으면_그대로다()
        {
            var a = new SessionStore(_path);
            a.Add(3, R(12480));
            a.Add(3, R(11000));
            a.Add(4, R(90000));
            Assert.IsTrue(a.Save());

            var b = new SessionStore(_path);
            b.Load();
            Assert.AreEqual(2, b.Records(3).Count);
            Assert.AreEqual(1, b.Records(4).Count);
            Assert.AreEqual(12480d, b.Records(3)[0].DurationMs, 0.5d);
            Assert.AreEqual(0, b.Records(2).Count);
        }

        [Test]
        public void 파일이_없으면_빈_기록으로_시작한다()
        {
            var s = new SessionStore(_path);
            s.Load();
            Assert.AreEqual(0, s.Records(3).Count);
            Assert.IsFalse(s.LastSaveFailed);
        }

        [Test]
        public void 하나만_지우거나_세션을_통째로_비울_수_있다()
        {
            var s = new SessionStore(_path);
            s.Add(3, R(1000)); s.Add(3, R(2000)); s.Add(3, R(3000));
            s.Delete(3, 1);
            Assert.AreEqual(2, s.Records(3).Count);
            Assert.AreEqual(3000d, s.Records(3)[1].DurationMs, 0.5d);
            s.ClearSession(3);
            Assert.AreEqual(0, s.Records(3).Count);
        }

        [Test]
        public void 망가진_파일을_만나도_앱이_죽지_않는다()
        {
            File.WriteAllText(_path, "{ 이건 JSON이 아니다");
            var s = new SessionStore(_path);
            LogAssert.ignoreFailingMessages = true;
            s.Load();
            LogAssert.ignoreFailingMessages = false;
            Assert.AreEqual(0, s.Records(3).Count);
        }
    }
}
