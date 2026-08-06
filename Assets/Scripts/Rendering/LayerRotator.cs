using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cube.Core;

namespace Cube.App
{
    /// 회전 애니메이션만 맡는다. 어떤 무브인지 판정하지 않는다.
    ///
    /// 무브가 들어오는 순간 논리 상태와 배열을 전부 커밋하고, 트랜스폼만 뒤따라간다.
    /// 애니메이션이 끝나기를 기다렸다가 커밋하면 빠른 연속 조작에서 화면과 상태가 어긋난다.
    public sealed class LayerRotator : MonoBehaviour
    {
        public const int MaxQueue = 8;

        public event Action<Move> MoveApplied;

        /// 큐에 쌓아 두는 한 건. 돌릴 큐비를 그때 잡아 둬야 한다 —
        /// 마커는 이미 커밋된 뒷 무브까지 반영하고 있어서 나중에 조회하면 엉뚱한 큐비가 나온다.
        readonly struct Pending
        {
            public readonly Move Move;
            public readonly IReadOnlyList<Transform> Cubies;
            public readonly float StartAngle;

            public Pending(Move move, IReadOnlyList<Transform> cubies, float startAngle)
            {
                Move = move; Cubies = cubies; StartAngle = startAngle;
            }
        }

        CubeRenderer _renderer;
        readonly Queue<Pending> _pending = new Queue<Pending>();
        Coroutine _running;
        float _startAngle;

        public bool IsAnimating => _running != null || _pending.Count > 0;
        public int QueueLength => _pending.Count;

        public void Init(CubeRenderer renderer) { _renderer = renderer; }

        public void EnqueueRange(IEnumerable<Move> moves)
        {
            foreach (var m in moves) Enqueue(m);
        }

        /// 손가락이 이미 돌려 놓은 각도에서 이어서 애니메이션한다.
        /// 0부터 다시 그리면 손을 떼는 순간 화면이 튄다.
        public void EnqueueFromAngle(Move m, float startAngleDeg)
        {
            _startAngle = startAngleDeg;
            Enqueue(m);
        }

        public void Enqueue(Move m)
        {
            if (_renderer == null || _renderer.State == null) return;

            // 돌릴 큐비를 커밋 전에 붙잡아 둔다.
            var cubies = _renderer.CubiesInLayer(m);

            // 논리부터 커밋한다.
            _renderer.State.Apply(m);
            _renderer.CommitPermutation(m);
            MoveApplied?.Invoke(m);

            float start = _startAngle;
            _startAngle = 0f;

            if (AppSettings.AnimationMs <= 0 || !isActiveAndEnabled)
            {
                SnapAll();
                return;
            }

            if (_pending.Count >= MaxQueue)
            {
                // 밀린 애니메이션이 끝없이 재생되지 않도록 전부 건너뛰고 최종 자세로 붙인다.
                FinishAllImmediately();
                return;
            }

            _pending.Enqueue(new Pending(m, cubies, start));
            if (_running == null) _running = StartCoroutine(RunQueue());
        }

        IEnumerator RunQueue()
        {
            while (_pending.Count > 0)
                yield return Animate(_pending.Dequeue());

            _running = null;
            // 큐를 다 비운 뒤에만 정렬한다. 중간에 맞추면 마커가 이미 마지막 수까지
            // 반영하고 있어서 아직 재생하지 않은 회전만큼 화면이 앞질러 간다.
            SnapAll();
        }

        IEnumerator Animate(Pending p)
        {
            var pivot = new GameObject("RotatePivot").transform;
            pivot.SetParent(_renderer.transform, false);

            Vector3 axis = CubeRenderer.UnityAxis(p.Move.Axis);
            float total = 90f * p.Move.Turns;

            pivot.localRotation = Quaternion.AngleAxis(p.StartAngle, axis);
            foreach (var t in p.Cubies) if (t != null) t.SetParent(pivot, true);

            float remaining = Mathf.Abs(total - p.StartAngle);
            float duration = Mathf.Max(0.001f, AppSettings.AnimationMs / 1000f) * (remaining / 90f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - k) * (1f - k);          // 끝에서 부드럽게 멈춘다
                pivot.localRotation = Quaternion.AngleAxis(Mathf.Lerp(p.StartAngle, total, eased), axis);
                yield return null;
            }

            pivot.localRotation = Quaternion.AngleAxis(total, axis);
            foreach (var t in p.Cubies) if (t != null) t.SetParent(_renderer.transform, true);
            pivot.SetParent(null, false);
            Destroy(pivot.gameObject);
        }

        /// 트랜스폼을 마커가 가리키는 정확한 자리와 직각 자세로 붙인다.
        /// 90° 회전을 거듭하면 부동소수점 오차가 쌓이므로 매번 눌러준다.
        public void SnapAll()
        {
            if (_renderer == null) return;
            foreach (var t in _renderer.Cubies)
            {
                if (t == null) continue;
                var mk = t.GetComponent<CubieMarker>();
                if (mk == null) continue;
                if (t.parent != _renderer.transform) t.SetParent(_renderer.transform, false);
                t.localPosition = _renderer.GridToLocal(mk.X, mk.Y, mk.Z);
                t.localRotation = SnapToRightAngle(t.localRotation);
            }
        }

        static Quaternion SnapToRightAngle(Quaternion q)
        {
            Vector3 f = RoundAxis(q * Vector3.forward);
            Vector3 u = RoundAxis(q * Vector3.up);
            if (f == Vector3.zero || u == Vector3.zero) return Quaternion.identity;
            return Quaternion.LookRotation(f, u);
        }

        static Vector3 RoundAxis(Vector3 v)
            => new Vector3(Mathf.Round(v.x), Mathf.Round(v.y), Mathf.Round(v.z));

        public void FinishAllImmediately()
        {
            if (_running != null) { StopCoroutine(_running); _running = null; }
            _pending.Clear();
            _startAngle = 0f;

            // 애니메이션 도중이면 큐비가 피벗에 붙어 있을 수 있다. 전부 떼어낸다.
            if (_renderer != null)
                for (int i = _renderer.transform.childCount - 1; i >= 0; i--)
                {
                    var child = _renderer.transform.GetChild(i);
                    if (child.name != "RotatePivot") continue;
                    for (int k = child.childCount - 1; k >= 0; k--)
                        child.GetChild(k).SetParent(_renderer.transform, true);
                    child.SetParent(null, false);
                    Destroy(child.gameObject);
                }

            SnapAll();
        }
    }
}
