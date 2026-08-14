using System;
using System.Diagnostics;

namespace Cube.App
{
    public enum TimerPhase { Idle, Inspection, Running, Stopped }

    /// 인스펙션과 계측만 맡는다. 완성 판정은 하지 않는다.
    /// 시각을 밖에서 주입받으므로 테스트가 시간을 기다리지 않아도 된다.
    public sealed class TimerService
    {
        public const double InspectionMs = 15000d;

        readonly Func<double> _now;
        double _inspectionStart, _solveStart, _stoppedAt;

        public TimerPhase Phase { get; private set; } = TimerPhase.Idle;

        public TimerService(Func<double> nowMs = null)
        {
            if (nowMs != null) { _now = nowMs; return; }
            var sw = Stopwatch.StartNew();
            _now = () => sw.Elapsed.TotalMilliseconds;
        }

        public void BeginInspection()
        {
            _inspectionStart = _now();
            Phase = TimerPhase.Inspection;
        }

        public void BeginSolve()
        {
            if (Phase == TimerPhase.Running) return;
            _solveStart = _now();
            Phase = TimerPhase.Running;
        }

        public void Stop()
        {
            if (Phase != TimerPhase.Running) return;
            _stoppedAt = _now();
            Phase = TimerPhase.Stopped;
        }

        public void Reset()
        {
            Phase = TimerPhase.Idle;
            _inspectionStart = _solveStart = _stoppedAt = 0d;
        }

        public void Restore(TimerPhase phase, double elapsedMs, double inspectionRemainingMs)
        {
            Reset();
            elapsedMs = Math.Max(0d, elapsedMs);
            switch (phase)
            {
                case TimerPhase.Inspection:
                    inspectionRemainingMs = Math.Max(0d, Math.Min(InspectionMs, inspectionRemainingMs));
                    _inspectionStart = _now() - (InspectionMs - inspectionRemainingMs);
                    Phase = TimerPhase.Inspection;
                    break;
                case TimerPhase.Running:
                    _solveStart = _now() - elapsedMs;
                    Phase = TimerPhase.Running;
                    break;
                case TimerPhase.Stopped:
                    _solveStart = _now() - elapsedMs;
                    _stoppedAt = _now();
                    Phase = TimerPhase.Stopped;
                    break;
            }
        }

        /// 15초를 넘기면 음수가 된다. Phase A에서는 벌점을 매기지 않는다.
        public double InspectionRemainingMs
            => Phase == TimerPhase.Inspection ? InspectionMs - (_now() - _inspectionStart) : 0d;

        public double ElapsedMs
        {
            get
            {
                if (Phase == TimerPhase.Running) return _now() - _solveStart;
                if (Phase == TimerPhase.Stopped) return _stoppedAt - _solveStart;
                return 0d;
            }
        }
    }
}
