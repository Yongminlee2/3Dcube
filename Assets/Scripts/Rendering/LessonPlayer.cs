using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cube.Core;

namespace Cube.App
{
    /// 공식을 큐브에서 시연한다. 회전은 Phase A의 LayerRotator를 그대로 쓴다 —
    /// 새 회전 경로를 만들면 상태와 화면이 어긋날 자리가 하나 더 생긴다.
    public sealed class LessonPlayer : MonoBehaviour
    {
        /// 재생이나 되돌리기가 끝났을 때.
        public event Action Finished;

        CubeRenderer _renderer;
        LayerRotator _rotator;
        TouchController _touch;

        readonly List<Move> _sequence = new List<Move>();
        readonly List<Move> _played = new List<Move>();
        int _cursor;
        Coroutine _running;

        public bool IsPlaying => _running != null;
        public int PlayedCount => _played.Count;
        public int SequenceLength => _sequence.Count;
        public bool HasMoreSteps => _cursor < _sequence.Count;

        public void Init(CubeRenderer renderer, LayerRotator rotator, TouchController touch)
        {
            _renderer = renderer; _rotator = rotator; _touch = touch;
        }

        /// 공식을 읽어 들이되 아직 돌리지는 않는다. 한 수씩 보기 전에 쓴다.
        public void Load(string notation)
        {
            if (_renderer == null || _renderer.State == null) return;
            _sequence.Clear();
            _sequence.AddRange(MoveNotation.Parse(notation, _renderer.State.N));
            _cursor = 0;
        }

        /// 공식 전체를 이어서 돌린다.
        public void Play(string notation)
        {
            Load(notation);
            if (_sequence.Count == 0) return;

            BlockTouch();
            _rotator.EnqueueRange(_sequence);
            _played.AddRange(_sequence);
            _cursor = _sequence.Count;
            StartWatching();
        }

        /// 읽어 들인 공식에서 다음 한 수만 돌린다.
        public void StepOnce()
        {
            if (!HasMoreSteps) return;

            BlockTouch();
            var m = _sequence[_cursor++];
            _rotator.Enqueue(m);
            _played.Add(m);
            StartWatching();
        }

        /// 지금까지 돌린 것을 역순으로 되돌려 시연 전 상태로 돌아간다.
        public void Rewind()
        {
            if (_played.Count == 0) return;

            BlockTouch();
            for (int i = _played.Count - 1; i >= 0; i--)
                _rotator.Enqueue(_played[i].Inverse);

            _played.Clear();
            _cursor = 0;
            StartWatching();
        }

        void StartWatching()
        {
            if (_running == null && isActiveAndEnabled) _running = StartCoroutine(WaitUntilIdle());
            else if (!isActiveAndEnabled) { _running = null; ReleaseTouch(); Finished?.Invoke(); }
        }

        IEnumerator WaitUntilIdle()
        {
            // 애니메이션이 꺼져 있어도 한 프레임은 넘겨서 호출자가
            // IsPlaying을 관찰할 수 있게 한다.
            yield return null;
            while (_rotator.IsAnimating) yield return null;

            _running = null;
            ReleaseTouch();
            Finished?.Invoke();
        }

        void BlockTouch() { if (_touch != null) _touch.Enabled = false; }
        void ReleaseTouch() { if (_touch != null) _touch.Enabled = true; }

        void OnDisable()
        {
            _running = null;
            ReleaseTouch();
        }
    }
}
