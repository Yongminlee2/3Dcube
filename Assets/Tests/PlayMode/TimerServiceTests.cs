using NUnit.Framework;
using Cube.App;

namespace Cube.App.Tests
{
    public class TimerServiceTests
    {
        double _now;
        TimerService _t;

        [SetUp]
        public void SetUp() { _now = 0d; _t = new TimerService(() => _now); }

        [Test]
        public void 처음에는_대기_상태다()
        {
            Assert.AreEqual(TimerPhase.Idle, _t.Phase);
            Assert.AreEqual(0d, _t.ElapsedMs);
        }

        [Test]
        public void 인스펙션은_십오초부터_줄어든다()
        {
            _t.BeginInspection();
            Assert.AreEqual(TimerPhase.Inspection, _t.Phase);
            Assert.AreEqual(15000d, _t.InspectionRemainingMs, 1d);
            _now += 4000d;
            Assert.AreEqual(11000d, _t.InspectionRemainingMs, 1d);
        }

        [Test]
        public void 인스펙션을_넘겨도_계측이_저절로_시작되지_않는다()
        {
            _t.BeginInspection();
            _now += 20000d;
            Assert.AreEqual(TimerPhase.Inspection, _t.Phase);
            Assert.Less(_t.InspectionRemainingMs, 0d);   // 음수로 계속 흐른다
            Assert.AreEqual(0d, _t.ElapsedMs);
        }

        [Test]
        public void 첫_회전에서_계측이_시작된다()
        {
            _t.BeginInspection();
            _now += 3000d;
            _t.BeginSolve();
            Assert.AreEqual(TimerPhase.Running, _t.Phase);
            _now += 12480d;
            Assert.AreEqual(12480d, _t.ElapsedMs, 1d);
        }

        [Test]
        public void 멈추면_시간이_고정된다()
        {
            _t.BeginSolve();
            _now += 8000d;
            _t.Stop();
            _now += 5000d;
            Assert.AreEqual(TimerPhase.Stopped, _t.Phase);
            Assert.AreEqual(8000d, _t.ElapsedMs, 1d);
        }

        [Test]
        public void 초기화하면_대기로_돌아간다()
        {
            _t.BeginSolve(); _now += 3000d; _t.Stop(); _t.Reset();
            Assert.AreEqual(TimerPhase.Idle, _t.Phase);
            Assert.AreEqual(0d, _t.ElapsedMs);
        }
    }
}
