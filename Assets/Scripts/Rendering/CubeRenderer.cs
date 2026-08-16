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

        readonly Dictionary<MeshRenderer, StickerArtworkPiece> _artworkPieces
            = new Dictionary<MeshRenderer, StickerArtworkPiece>();
        readonly List<Material> _artworkMaterials = new List<Material>();

        readonly struct StickerArtworkPiece
        {
            public readonly int ColorIndex;
            public readonly int Row;
            public readonly int Col;

            public StickerArtworkPiece(int colorIndex, int row, int col)
            {
                ColorIndex = colorIndex;
                Row = row;
                Col = col;
            }
        }

        readonly struct StickerArtworkKey : System.IEquatable<StickerArtworkKey>
        {
            public readonly int ColorIndex;
            public readonly int CubieColorMask;

            public StickerArtworkKey(int colorIndex, int cubieColorMask)
            {
                ColorIndex = colorIndex;
                CubieColorMask = cubieColorMask;
            }

            public bool Equals(StickerArtworkKey other)
                => ColorIndex == other.ColorIndex && CubieColorMask == other.CubieColorMask;

            public override bool Equals(object obj)
                => obj is StickerArtworkKey other && Equals(other);

            public override int GetHashCode() => (ColorIndex * 397) ^ CubieColorMask;
        }

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

        /// 색뿐 아니라 한 면 일러스트의 조각 위치와 상하좌우 방향까지 완성됐는지 본다.
        /// 일반 색상 스킨과 조각 반복 모드에서는 기존 색 완성 판정과 같다.
        public bool IsSolvedWithArtwork()
        {
            if (State == null || !State.IsSolved()) return false;

            var skin = SkinService.Current;
            if (SkinService.ArtworkLayout != SkinArtworkLayout.WholeFace
                || skin == null || skin.StickerTextures == null)
                return true;

            bool hasDirectionalArtwork = false;
            for (int f = 0; f < Faces.Count; f++)
            {
                if (f >= skin.StickerTextures.Length || skin.StickerTextures[f] == null) continue;
                hasDirectionalArtwork = true;

                for (int row = 0; row < _n; row++)
                    for (int col = 0; col < _n; col++)
                    {
                        var sticker = StickerAt((Face)f, row, col);
                        if (sticker == null
                            || !_artworkPieces.TryGetValue(sticker, out var piece)
                            || piece.ColorIndex != f || piece.Row != row || piece.Col != col)
                            return false;

                        var point = CubeCoords.ToPoint((Face)f, row, col, _n);
                        var expectedForward = new Vector3(point.NX, point.NY, -point.NZ);
                        var expectedUp = ArtworkUp((Face)f);
                        var marker = sticker.transform.parent.GetComponent<CubieMarker>();
                        if (marker == null) return false;
                        var logicalStickerRotation = marker.Orientation * sticker.transform.localRotation;
                        var actualForward = logicalStickerRotation * Vector3.forward;
                        var actualUp = logicalStickerRotation * Vector3.up;
                        if (Vector3.Dot(actualForward, expectedForward) < 0.999f
                            || Vector3.Dot(actualUp, expectedUp) < 0.999f)
                            return false;
                    }
            }

            return hasDirectionalArtwork || State.IsSolved();
        }

        public void Build(CubeState state)
        {
            Clear();
            State = state;
            _n = state.N;
            _grid = new Transform[_n, _n, _n];
            _stickers = new MeshRenderer[Faces.Count * _n * _n];

            var skin = SkinService.Current;

            // Resources의 머티리얼 애셋에서 복제한다. Shader.Find로 찾으면
            // 빌드에서 null이 나온다 — 어떤 애셋도 그 셰이더를 참조하지 않으면
            // 빌드에서 잘려나가기 때문이다. 에디터에서는 멀쩡해서 테스트로는 안 잡힌다.
            var template = Resources.Load<Material>("CubieMaterial");
            if (template == null)
                throw new MissingReferenceException(
                    "Assets/Resources/CubieMaterial.mat이 없다. ProjectSetup.CreateAssets를 돌릴 것");

            _bodyMaterial = new Material(template) { color = skin.CubeBody };
            _stickerMaterials = new Material[6];
            for (int i = 0; i < 6; i++)
            {
                _stickerMaterials[i] = new Material(template);
                ApplyStickerVisual(_stickerMaterials[i], skin, i);
            }

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
            // 큐비끼리 딱 붙여 한 덩어리로 보이게 한다. 0.96처럼 줄여 두면
            // 사이가 벌어져서, 층이 돌 때 아홉 조각이 따로 노는 것처럼 보인다.
            go.transform.localScale = Vector3.one * (CubieSize * 0.998f);
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
                        var face = (Face)f;
                        var p = CubeCoords.ToPoint(face, row, col, _n);
                        var parent = _grid[p.X, p.Y, p.Z];

                        // Quad가 아니라 납작한 정육면체를 쓴다.
                        // CreatePrimitive(Quad)는 MeshCollider를 붙이려 하는데
                        // 그 클래스가 빌드에서 잘려나가면 통째로 실패한다. 에디터에서는
                        // 멀쩡히 돌아서 실기기에서만 드러났다. 정육면체는 BoxCollider를
                        // 쓰므로 그 경로를 타지 않고, 양면이라 방향을 틀릴 일도 없다.
                        var sticker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        sticker.name = $"Sticker_{(Face)f}_{row}_{col}";
                        DestroyNow(sticker.GetComponent<Collider>());
                        sticker.transform.SetParent(parent, false);

                        // 법선도 Z를 뒤집어 Unity 방향으로 옮긴다.
                        var dir = new Vector3(p.NX, p.NY, -p.NZ);
                        // 몸통 표면(0.5)에 얇게 얹는다. 두께의 절반만큼 밖으로 밀어
                        // z-파이팅을 피하되, 떠 보이지 않을 만큼만 띄운다.
                        sticker.transform.localPosition = dir * 0.508f;
                        // 옆면은 화면 위가 Unity +Y지만, U/D면은 법선과 +Y가 나란해
                        // 기본 LookRotation의 위쪽 기준이 무너진다. CubeCoords 규칙대로
                        // U는 B쪽(Unity +Z), D는 F쪽(Unity -Z)을 그림의 위로 고정한다.
                        var artworkUp = ArtworkUp(face);
                        sticker.transform.localRotation = Quaternion.LookRotation(dir, artworkUp);
                        sticker.transform.localScale = new Vector3(0.9f, 0.9f, 0.02f);

                        var stickerRenderer = sticker.GetComponent<MeshRenderer>();
                        _stickers[(f * _n + row) * _n + col] = stickerRenderer;
                    }

            ReassignArtworkPiecesFromState();
        }

        /// 상태를 다시 읽어 스티커 색만 갱신한다. 오브젝트는 그대로 둔다.
        public void Refresh()
        {
            if (State == null) return;
            // 외부에서 CubeState만 직접 바꾼 뒤 Refresh를 호출하는 기존 경로도 지원한다.
            // 실제 회전은 CommitPermutation이 물리 스티커를 옮기므로 이 재구성이 필요 없다.
            ReassignArtworkPiecesFromState();
            RefreshStickerVisuals(SkinService.Current);
        }

        void ReassignArtworkPiecesFromState()
        {
            _artworkPieces.Clear();

            // 저장된 섞인 상태를 Build할 때 같은 색 스티커를 단순 발견 순서로
            // 0..8에 배정하면, 움직이지 않는 센터에도 그림의 모서리 조각이 붙는다.
            // 큐비가 가진 색 조합은 3x3에서 조각의 고향을 유일하게 알려 준다.
            // 예: 흰-초록-빨강 조합의 흰 스티커는 언제나 흰 면의 같은 모서리다.
            // 그 조합으로 원래 그림 좌표를 복구해야 저장 상태에서 시작해도
            // 색을 다 맞춘 순간 한 면짜리 일러스트까지 정확히 완성된다.
            var homePieces = BuildHomeArtworkPieces();
            var cubieColorMasks = new int[_n, _n, _n];

            for (int f = 0; f < Faces.Count; f++)
                for (int row = 0; row < _n; row++)
                    for (int col = 0; col < _n; col++)
                    {
                        int colorIndex = State.Get((Face)f, row, col);
                        if (colorIndex < 0 || colorIndex >= Faces.Count) continue;
                        var p = CubeCoords.ToPoint((Face)f, row, col, _n);
                        cubieColorMasks[p.X, p.Y, p.Z] |= 1 << colorIndex;
                    }

            var fallbackPiece = new int[Faces.Count];
            for (int f = 0; f < Faces.Count; f++)
                for (int row = 0; row < _n; row++)
                    for (int col = 0; col < _n; col++)
                    {
                        var sticker = _stickers[(f * _n + row) * _n + col];
                        int colorIndex = State.Get((Face)f, row, col);
                        if (colorIndex < 0 || colorIndex >= Faces.Count) continue;

                        var p = CubeCoords.ToPoint((Face)f, row, col, _n);
                        var key = new StickerArtworkKey(
                            colorIndex, cubieColorMasks[p.X, p.Y, p.Z]);

                        StickerArtworkPiece piece;
                        if (homePieces.TryGetValue(key, out var candidates)
                            && candidates.Count > 0)
                        {
                            piece = candidates.Dequeue();
                        }
                        else
                        {
                            // 사진 입력 중처럼 아직 유효하지 않은 임시 상태도 화면에는
                            // 그려야 하므로, 복구할 수 없는 조각만 예전 순서 방식으로 둔다.
                            int index = fallbackPiece[colorIndex]++;
                            piece = new StickerArtworkPiece(
                                colorIndex, index / _n, index % _n);
                        }

                        _artworkPieces[sticker] = piece;
                    }
        }

        Dictionary<StickerArtworkKey, Queue<StickerArtworkPiece>> BuildHomeArtworkPieces()
        {
            var result = new Dictionary<StickerArtworkKey, Queue<StickerArtworkPiece>>();
            for (int f = 0; f < Faces.Count; f++)
                for (int row = 0; row < _n; row++)
                    for (int col = 0; col < _n; col++)
                    {
                        var p = CubeCoords.ToPoint((Face)f, row, col, _n);
                        int mask = SolvedCubieColorMask(p.X, p.Y, p.Z);
                        var key = new StickerArtworkKey(f, mask);
                        if (!result.TryGetValue(key, out var pieces))
                        {
                            pieces = new Queue<StickerArtworkPiece>();
                            result.Add(key, pieces);
                        }
                        pieces.Enqueue(new StickerArtworkPiece(f, row, col));
                    }
            return result;
        }

        int SolvedCubieColorMask(int x, int y, int z)
        {
            int mask = 0;
            int last = _n - 1;
            if (y == last) mask |= 1 << (int)Face.U;
            if (y == 0) mask |= 1 << (int)Face.D;
            if (z == last) mask |= 1 << (int)Face.F;
            if (z == 0) mask |= 1 << (int)Face.B;
            if (x == 0) mask |= 1 << (int)Face.L;
            if (x == last) mask |= 1 << (int)Face.R;
            return mask;
        }

        static Vector3 ArtworkUp(Face face)
            => face == Face.U ? Vector3.forward
             : face == Face.D ? Vector3.back
             : Vector3.up;

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
                float degrees = m.Turns == 3 ? -90f : 90f * m.Turns;
                mk.Orientation = Quaternion.AngleAxis(degrees, UnityAxis(m.Axis)) * mk.Orientation;
                _grid[x, y, z] = mk.transform;
            }
        }

        // 스킨 화면에서 실시간 미리보기가 되려면 큐브가 보이는 동안은 항상 구독돼 있어야
        // 하고, 숨어 있다 다시 보일 때는 그사이 바뀐 스킨을 놓치지 않아야 한다.
        void OnEnable()
        {
            SkinService.Changed -= ApplySkin;
            SkinService.Changed += ApplySkin;
            ApplySkin(SkinService.Current);
        }

        void OnDisable() => SkinService.Changed -= ApplySkin;

        /// 큐비를 다시 짓지 않고 이미 만든 머티리얼 색만 바꾼다. Build보다 훨씬 가볍다.
        void ApplySkin(Skin skin)
        {
            if (_bodyMaterial == null || _stickerMaterials == null || skin == null) return;
            _bodyMaterial.color = skin.CubeBody;
            for (int i = 0; i < 6; i++)
                ApplyStickerVisual(_stickerMaterials[i], skin, i);
            RefreshStickerVisuals(skin);
        }

        void RefreshStickerVisuals(Skin skin)
        {
            if (skin == null || _stickerMaterials == null) return;
            ClearArtworkMaterials();

            bool wholeFace = SkinService.ArtworkLayout == SkinArtworkLayout.WholeFace;
            foreach (var pair in _artworkPieces)
            {
                var sticker = pair.Key;
                var piece = pair.Value;
                if (sticker == null || piece.ColorIndex < 0 || piece.ColorIndex >= 6) continue;

                var texture = skin.StickerTextures != null
                    && piece.ColorIndex < skin.StickerTextures.Length
                    ? skin.StickerTextures[piece.ColorIndex]
                    : null;

                if (!wholeFace || texture == null)
                {
                    sticker.sharedMaterial = _stickerMaterials[piece.ColorIndex];
                    continue;
                }

                var material = new Material(_stickerMaterials[piece.ColorIndex]);
                var scale = new Vector2(1f / _n, 1f / _n);
                var offset = new Vector2(piece.Col / (float)_n, 1f - (piece.Row + 1f) / _n);
                SetTextureTransform(material, scale, offset);
                sticker.sharedMaterial = material;
                _artworkMaterials.Add(material);
            }
        }

        static void SetTextureTransform(Material material, Vector2 scale, Vector2 offset)
        {
            // URP/Lit은 _BaseMap, 일부 테스트/구형 셰이더는 _MainTex를 쓴다.
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTextureScale("_BaseMap", scale);
                material.SetTextureOffset("_BaseMap", offset);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTextureScale("_MainTex", scale);
                material.SetTextureOffset("_MainTex", offset);
            }
        }

        void ClearArtworkMaterials()
        {
            for (int i = 0; i < _artworkMaterials.Count; i++)
                DestroyNow(_artworkMaterials[i]);
            _artworkMaterials.Clear();
        }

        /// 텍스처가 있는 면은 텍스처를 입히고 색은 흰색으로 둔다 — 틴트를 곱하면
        /// 대표색으로 한 번 더 물들어 실제 텍스처보다 탁하고 어둡게 보인다.
        /// 텍스처가 없는 면(기존 5종처럼 색만 있는 스킨)은 그대로 플랫 컬러를 쓴다.
        static void ApplyStickerVisual(Material mat, Skin skin, int i)
        {
            var tex = skin.StickerTextures != null && i < skin.StickerTextures.Length
                ? skin.StickerTextures[i] : null;
            if (tex != null)
            {
                mat.color = Color.white;
                mat.mainTexture = tex;
            }
            else
            {
                mat.color = skin.StickerColors[i];
                mat.mainTexture = null;
            }
        }

        public void Clear()
        {
            ClearArtworkMaterials();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                // 플레이 중 Destroy는 프레임 끝에 처리된다. 먼저 떼어내야
                // 곧바로 다시 Build할 때 옛 큐비가 한 프레임 겹쳐 보이지 않는다.
                child.transform.SetParent(null, false);
                DestroyNow(child);
            }
            _cubies.Clear();
            _artworkPieces.Clear();
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
        public Quaternion Orientation = Quaternion.identity;
    }
}
