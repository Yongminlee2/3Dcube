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

            public Pending(Move move, IReadOnlyList<Transform> cubies)
            {
                Move = move; Cubies = cubies;
            }
        }

        CubeRenderer _renderer;
        readonly Queue<Pending> _pending = new Queue<Pending>();
        Coroutine _running;

        public bool IsAnimating => _running != null || _pending.Count > 0;
        public int QueueLength => _pending.Count;

        public void Init(CubeRenderer renderer) { _renderer = renderer; }

        public void EnqueueRange(IEnumerable<Move> moves)
        {
            foreach (var m in moves) Enqueue(m);
        }

        /// 애니메이션 없이 한꺼번에 적용한다.
        ///
        /// 섞기처럼 스무 수를 밀어 넣을 때는 이걸 쓴다. 큐에 넣으면 큐 넘침과
        /// 중간 중단이 얽혀 큐비가 흩어진 채 남는다 — 실기기에서 정확히 그랬다.
        /// 어차피 섞는 과정을 애니메이션으로 보여줄 이유도 없다.
        public void ApplyInstant(IEnumerable<Move> moves)
        {
            if (_renderer == null || _renderer.State == null) return;

            FinishAllImmediately();

            foreach (var m in moves)
            {
                var cubies = _renderer.CubiesInLayer(m);
                _renderer.State.Apply(m);
                _renderer.CommitPermutation(m);

                // 애니메이션만 생략하고 큐비와 스티커 자체는 정확히 회전시킨다.
                // 상태만 바꾼 뒤 Build하면 한 면 일러스트의 조각 정체성이 사라져,
                // 색은 맞아도 그림은 다시 이어지지 않는다.
                var rotation = Quaternion.AngleAxis(TotalAngle(m), CubeRenderer.UnityAxis(m.Axis));
                foreach (var cubie in cubies)
                {
                    if (cubie == null) continue;
                    cubie.localPosition = rotation * cubie.localPosition;
                    cubie.localRotation = rotation * cubie.localRotation;
                }
                MoveApplied?.Invoke(m);
            }

            SnapAll();
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
            AudioService.PlayMove();

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

            _pending.Enqueue(new Pending(m, cubies));
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
            float total = TotalAngle(p.Move);

            foreach (var t in p.Cubies) if (t != null) t.SetParent(pivot, true);

            // 90도 한 번에 설정값만큼 쓴다. 손가락이든 버튼이든 같은 시간에 돈다.
            float duration = Mathf.Max(0.001f, AppSettings.AnimationMs / 1000f) * (Mathf.Abs(total) / 90f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - k) * (1f - k);          // 끝에서 부드럽게 멈춘다
                pivot.localRotation = Quaternion.AngleAxis(total * eased, axis);
                yield return null;
            }

            pivot.localRotation = Quaternion.AngleAxis(total, axis);
            foreach (var t in p.Cubies) if (t != null) t.SetParent(_renderer.transform, true);
            pivot.SetParent(null, false);
            Destroy(pivot.gameObject);
        }

        /// 눈에 보이는 회전각. 반시계(turns=3)는 +270°가 아니라 −90°로 돌린다.
        ///
        /// 둘은 끝 자세가 같지만 가는 길이 다르다. +270°로 돌리면 손가락이 끌던
        /// 방향과 반대로 먼 길을 돌아가서, 손을 떼는 순간 큐브가 뒤로 튄다.
        /// 실기기에서 "돌다가 멈추고 이상하다"로 나타났다.
        public static float TotalAngle(Move m) => m.Turns == 3 ? -90f : 90f * m.Turns;

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
            Vector3 f = NearestAxis(q * Vector3.forward, -1);
            int forwardAxis = DominantAxis(f);
            Vector3 u = NearestAxis(q * Vector3.up, forwardAxis);
            if (f == Vector3.zero || u == Vector3.zero) return Quaternion.identity;
            return Quaternion.LookRotation(f, u);
        }

        /// 방향을 가장 가까운 축(±X, ±Y, ±Z) 하나로 붙인다.
        ///
        /// 성분별로 반올림하면 안 된다. 30도쯤 돌아간 방향 (0.5, 0, 0.87)은
        /// (1, 0, 1)이 되어 축이 아닌 대각선이 나오고, 큐비가 45도 틀어진 채 굳는다.
        /// 드래그를 중간에 멈췄을 때 큐브가 깨져 보이던 원인이다.
        ///
        /// exclude에 축 번호를 주면 그 축은 고르지 않는다 —
        /// 앞 방향과 위 방향이 같은 축으로 붙으면 자세를 만들 수 없다.
        static Vector3 NearestAxis(Vector3 v, int exclude)
        {
            float best = -1f;
            int axis = -1;
            for (int i = 0; i < 3; i++)
            {
                if (i == exclude) continue;
                float m = Mathf.Abs(v[i]);
                if (m > best) { best = m; axis = i; }
            }
            if (axis < 0) return Vector3.zero;

            var result = Vector3.zero;
            result[axis] = v[axis] >= 0f ? 1f : -1f;
            return result;
        }

        static int DominantAxis(Vector3 axisVector)
        {
            if (axisVector.x != 0f) return 0;
            if (axisVector.y != 0f) return 1;
            if (axisVector.z != 0f) return 2;
            return -1;
        }

        public void FinishAllImmediately()
        {
            if (_running != null) { StopCoroutine(_running); _running = null; }
            _pending.Clear();

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
