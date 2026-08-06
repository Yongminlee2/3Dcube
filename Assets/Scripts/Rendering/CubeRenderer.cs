using System.Collections.Generic;
using UnityEngine;
using Cube.Core;

namespace Cube.App
{
    /// 상태를 그림으로 바꾼다. 입력도 애니메이션도 알지 못한다.
    public sealed class CubeRenderer : MonoBehaviour
    {
        public CubeState State { get; private set; }
        public float CubieSize => 1f;

        readonly List<Transform> _cubies = new List<Transform>();
        public IReadOnlyList<Transform> Cubies => _cubies;

        Transform[,,] _grid;
        MeshRenderer[] _stickers;      // face * n * n + row * n + col
        Material[] _stickerMaterials;  // 색마다 하나씩 공유한다
        Material _bodyMaterial;
        int _n;

        public Vector3 GridToLocal(int x, int y, int z)
        {
            float c = (_n - 1) * 0.5f;
            // Core는 Z+가 F면 바깥인 오른손 좌표계, Unity는 왼손 좌표계다.
            // 두 좌표계가 만나는 유일한 지점이 이 Z 부호 반전이다.
            return new Vector3(x - c, y - c, -(z - c));
        }

        public Transform CubieAt(int x, int y, int z) => _grid[x, y, z];

        public MeshRenderer StickerAt(Face face, int row, int col)
            => _stickers[((int)face * _n + row) * _n + col];

        public void Build(CubeState state)
        {
            Clear();
            State = state;
            _n = state.N;
            _grid = new Transform[_n, _n, _n];
            _stickers = new MeshRenderer[Faces.Count * _n * _n];

            var palette = ThemeService.Current;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            _bodyMaterial = new Material(shader) { color = palette.CubeBody };
            _stickerMaterials = new Material[6];
            for (int i = 0; i < 6; i++)
                _stickerMaterials[i] = new Material(shader) { color = palette.StickerColors[i] };

            for (int x = 0; x < _n; x++)
                for (int y = 0; y < _n; y++)
                    for (int z = 0; z < _n; z++)
                        _grid[x, y, z] = BuildCubie(x, y, z);

            BuildStickers();
            Refresh();
        }

        Transform BuildCubie(int x, int y, int z)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Cubie_{x}_{y}_{z}";
            DestroyNow(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = GridToLocal(x, y, z);
            go.transform.localScale = Vector3.one * (CubieSize * 0.96f);
            go.GetComponent<MeshRenderer>().sharedMaterial = _bodyMaterial;

            // 손가락 판정은 큐비 단위로 한다. 스티커마다 콜라이더를 두지 않는다.
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one;

            var marker = go.AddComponent<CubieMarker>();
            marker.X = x; marker.Y = y; marker.Z = z;

            _cubies.Add(go.transform);
            return go.transform;
        }

        void BuildStickers()
        {
            for (int f = 0; f < Faces.Count; f++)
                for (int row = 0; row < _n; row++)
                    for (int col = 0; col < _n; col++)
                    {
                        var p = CubeCoords.ToPoint((Face)f, row, col, _n);
                        var parent = _grid[p.X, p.Y, p.Z];

                        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        quad.name = $"Sticker_{(Face)f}_{row}_{col}";
                        DestroyNow(quad.GetComponent<Collider>());
                        quad.transform.SetParent(parent, false);

                        // 법선도 Z를 뒤집어 Unity 방향으로 옮긴다.
                        var dir = new Vector3(p.NX, p.NY, -p.NZ);
                        quad.transform.localPosition = dir * 0.52f;
                        quad.transform.localRotation = Quaternion.LookRotation(dir);
                        quad.transform.localScale = new Vector3(0.86f, 0.86f, 1f);

                        _stickers[(f * _n + row) * _n + col] = quad.GetComponent<MeshRenderer>();
                    }
        }

        /// 상태를 다시 읽어 스티커 색만 갱신한다. 오브젝트는 그대로 둔다.
        public void Refresh()
        {
            if (State == null) return;
            for (int f = 0; f < Faces.Count; f++)
                for (int row = 0; row < _n; row++)
                    for (int col = 0; col < _n; col++)
                    {
                        byte color = State.Get((Face)f, row, col);
                        _stickers[(f * _n + row) * _n + col].sharedMaterial = _stickerMaterials[color];
                    }
        }

        /// Core 축을 Unity 회전축으로 옮긴다. GridToLocal의 Z 반전과 짝을 이룬다.
        public static Vector3 UnityAxis(Axis axis)
            => axis == Axis.X ? Vector3.right
             : axis == Axis.Y ? Vector3.up
             : Vector3.back;

        public IReadOnlyList<Transform> CubiesInLayer(Move m)
        {
            int target = _n - 1 - m.Layer;
            var list = new List<Transform>();
            foreach (var t in _cubies)
            {
                var mk = t.GetComponent<CubieMarker>();
                int c = m.Axis == Axis.X ? mk.X : m.Axis == Axis.Y ? mk.Y : mk.Z;
                if (c == target) list.Add(t);
            }
            return list;
        }

        /// 무브에 맞춰 스티커 배열·격자·마커를 재배치한다.
        /// 트랜스폼은 건드리지 않는다 — 그쪽은 LayerRotator가 애니메이션으로 따라붙인다.
        public void CommitPermutation(Move m)
        {
            int[] perm = MovePermutation.For(m, _n);
            var newStickers = new MeshRenderer[_stickers.Length];
            for (int i = 0; i < perm.Length; i++)
                newStickers[perm[i] >= 0 ? perm[i] : i] = _stickers[i];
            _stickers = newStickers;

            int target = _n - 1 - m.Layer;
            var moved = new List<(CubieMarker mk, int x, int y, int z)>();
            foreach (var t in _cubies)
            {
                var mk = t.GetComponent<CubieMarker>();
                int c = m.Axis == Axis.X ? mk.X : m.Axis == Axis.Y ? mk.Y : mk.Z;
                if (c != target) continue;

                int nx = mk.X, ny = mk.Y, nz = mk.Z;
                for (int i = 0; i < m.Turns; i++)
                    CubeCoords.RotateGridCW(nx, ny, nz, m.Axis, _n, out nx, out ny, out nz);
                moved.Add((mk, nx, ny, nz));
            }
            foreach (var (mk, x, y, z) in moved)
            {
                mk.X = x; mk.Y = y; mk.Z = z;
                _grid[x, y, z] = mk.transform;
            }
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                // 플레이 중 Destroy는 프레임 끝에 처리된다. 먼저 떼어내야
                // 곧바로 다시 Build할 때 옛 큐비가 한 프레임 겹쳐 보이지 않는다.
                child.transform.SetParent(null, false);
                DestroyNow(child);
            }
            _cubies.Clear();
            State = null;
        }

        static void DestroyNow(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }

    /// 큐비가 격자 어디에 있는지 기억한다. 회전이 끝날 때마다 갱신된다.
    public sealed class CubieMarker : MonoBehaviour
    {
        public int X, Y, Z;
    }
}
