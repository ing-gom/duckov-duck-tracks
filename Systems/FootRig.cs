using System;
using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>
    /// 캐릭터의 두 발을 찾아 놓고, <b>실제로 디디는 순간</b>에 자국을 냅니다.
    ///
    /// 거리 누적 방식(<see cref="TrackWalker"/>)은 애니메이션과 무관하게 일정 간격으로
    /// 찍기 때문에 발이 공중에 떠 있는 순간에도 자국이 남습니다. 리그를 직접 보면
    /// 그런 어긋남이 없습니다.
    ///
    /// 모델 구조는 로그로 확인한 것입니다:
    /// <code>
    /// Thigh.L → Foot.L → Foot.L_end          (뼈. 디딜 때 바닥 높이 0)
    ///                  → FootLSocket → … → DuckFoot   (실제 발 메시, 정점 191)
    /// </code>
    /// 발이 <b>소켓</b>이라 신발을 갈아 신으면 그 아래 메시가 바뀝니다.
    /// </summary>
    internal sealed class FootRig
    {
        /// <summary>발 뼈. 이 높이로 디딤을 판단합니다.</summary>
        private Transform? _left;
        private Transform? _right;

        /// <summary>발끝. 뼈에서 이쪽을 향하는 방향이 곧 발이 가리키는 방향입니다.</summary>
        private Transform? _leftTip;
        private Transform? _rightTip;

        /// <summary>실제 발 메시. 실루엣을 구울 때 씁니다.</summary>
        internal Renderer? LeftMesh { get; private set; }
        internal Renderer? RightMesh { get; private set; }

        /// <summary>실루엣을 구울 때 기준이 되는 뼈와 발끝.</summary>
        internal Transform? LeftBone => _left;
        internal Transform? RightBone => _right;
        internal Transform? LeftTip => _leftTip;
        internal Transform? RightTip => _rightTip;

        /// <summary>높이를 재는 기준. 캐릭터 원점이 바닥에 있습니다.</summary>
        private Transform? _root;

        private FootState _leftState;
        private FootState _rightState;

        /// <summary>리그를 못 찾았으면 거리 누적으로 돌아갑니다.</summary>
        internal bool Found => _left != null && _right != null;

        /// <summary>발이 어느 쪽인지. 자국을 좌우로 뒤집는 데 씁니다.</summary>
        internal enum Side
        {
            Left,
            Right,
        }

        private struct FootState
        {
            /// <summary>지금 바닥에 닿아 있는 것으로 보는지.</summary>
            internal bool Planted;

            /// <summary>
            /// 최근에 관찰한 최대 들림 높이.
            ///
            /// 디딤 문턱을 여기서 끌어냅니다. 고정값으로 박으면 애니메이션이 발을
            /// 조금만 드는 캐릭터에서는 영원히 "닿아 있음"이 되어 자국이 안 나오고,
            /// 크게 드는 캐릭터에서는 너무 늦게 찍힙니다.
            /// </summary>
            internal float Lift;

            /// <summary>마지막으로 자국을 낸 시각. 같은 프레임에 두 번 찍는 것 방지.</summary>
            internal float LastStampTime;
        }

        /// <summary>
        /// 리그를 찾습니다. 캐릭터 모델이 바뀌면(장비 교체 등) 다시 부릅니다.
        /// </summary>
        internal void Bind(Transform modelRoot)
        {
            _root = modelRoot;
            _left = _right = null;
            _leftTip = _rightTip = null;
            LeftMesh = RightMesh = null;

            try
            {
                foreach (var node in modelRoot.GetComponentsInChildren<Transform>(true))
                {
                    string name = node.name;

                    // 이름을 정확히 맞춥니다. "Foot"이 들어간 것을 전부 집으면
                    // FootLSocket이나 0_FootDefault_L 같은 것까지 딸려 옵니다.
                    if (_left == null && Matches(name, "Foot.L", "Foot_L", "foot.l"))
                        _left = node;
                    else if (_right == null && Matches(name, "Foot.R", "Foot_R", "foot.r"))
                        _right = node;
                }

                _leftTip = FindTip(_left, "Foot.L_end", "Foot_L_end");
                _rightTip = FindTip(_right, "Foot.R_end", "Foot_R_end");

                LeftMesh = FindFootMesh(_left);
                RightMesh = FindFootMesh(_right);

                _leftState = default;
                _rightState = default;

#if DEBUG
                UnityEngine.Debug.Log(
                    $"[DuckTracks] 발 리그 — 왼쪽 {(_left != null ? _left.name : "없음")}" +
                    $" / 오른쪽 {(_right != null ? _right.name : "없음")}" +
                    $" / 메시 {(LeftMesh != null ? LeftMesh.name : "없음")}");
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 발 리그 탐색 실패: {ex.Message}");
                _left = _right = null;
            }
        }

        private static bool Matches(string name, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>발끝 뼈. 없으면 자식 중 아무거나 — 방향만 알면 됩니다.</summary>
        private static Transform? FindTip(Transform? foot, params string[] names)
        {
            if (foot == null)
                return null;

            foreach (var node in foot.GetComponentsInChildren<Transform>(true))
            {
                if (Matches(node.name, names))
                    return node;
            }

            return null;
        }

        /// <summary>
        /// 소켓 아래의 실제 발 메시.
        ///
        /// 뼈 아래를 통째로 뒤져서 <see cref="MeshFilter"/>를 가진 것 중 가장 큰 것을
        /// 고릅니다. 이름(DuckFoot)으로 찾지 않는 이유는 신발을 갈아 신으면 이름이
        /// 바뀌기 때문입니다 — 소켓 구조라는 게 그런 뜻입니다.
        /// </summary>
        private static Renderer? FindFootMesh(Transform? foot)
        {
            if (foot == null)
                return null;

            Renderer? best = null;
            float bestArea = 0f;

            foreach (var filter in foot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;

                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null)
                    continue;

                // 바닥에 닿는 넓이로 고릅니다. 발목의 작은 구(Sphere)보다 발이 넓습니다.
                var size = renderer.bounds.size;
                float area = size.x * size.z;

                if (area <= bestArea)
                    continue;

                bestArea = area;
                best = renderer;
            }

            return best;
        }

        /// <summary>
        /// 한 프레임 진행. 발이 새로 바닥에 닿았으면 <paramref name="onStep"/>을 부릅니다.
        /// </summary>
        /// <param name="onStep">(어느 발, 월드 위치, 진행 각도)</param>
        internal void Tick(Action<Side, Vector3, float> onStep)
        {
            if (_root == null || _left == null || _right == null)
                return;

            float groundY = _root.position.y;

            Step(Side.Left, _left!, _leftTip, ref _leftState, groundY, onStep);
            Step(Side.Right, _right!, _rightTip, ref _rightState, groundY, onStep);
        }

        private static void Step(
            Side side,
            Transform foot,
            Transform? tip,
            ref FootState state,
            float groundY,
            Action<Side, Vector3, float> onStep)
        {
            float height = foot.position.y - groundY;

            // 관찰한 들림 높이를 천천히 줄입니다. 늘 때는 즉시, 줄 때는 서서히 —
            // 걷다 멈추면 문턱이 옛날 값에 붙잡혀 있지 않게 하려는 것입니다.
            state.Lift = Mathf.Max(height, state.Lift - Time.deltaTime * 0.35f);

            // 최고로 든 높이의 35% 아래로 내려오면 디딘 것으로 봅니다. 위아래로
            // 여유를 둬서 문턱 근처에서 떨렸다 붙었다 하는 것을 막습니다.
            float threshold = Mathf.Clamp(state.Lift * 0.35f, 0.012f, 0.09f);
            float release = threshold * 1.8f;

            if (!state.Planted && height <= threshold)
            {
                state.Planted = true;

                // 같은 발이 아주 짧은 간격으로 두 번 찍히는 것을 막습니다.
                if (Time.time - state.LastStampTime > 0.08f)
                {
                    state.LastStampTime = Time.time;
                    onStep(side, foot.position, YawOf(foot, tip));
                }
            }
            else if (state.Planted && height > release)
            {
                state.Planted = false;
            }
        }

        /// <summary>
        /// 발이 가리키는 방향.
        ///
        /// 뼈에서 발끝으로 가는 벡터를 씁니다. 뼈의 로컬 축(forward/up)은 리그를
        /// 만든 쪽 관례에 달려 있어서 믿을 수 없지만, 두 뼈의 위치 차이는 언제나
        /// 실제 발이 향한 쪽입니다.
        /// </summary>
        private static float YawOf(Transform foot, Transform? tip)
        {
            Vector3 direction = tip != null
                ? tip.position - foot.position
                : foot.forward;

            direction.y = 0f;

            if (direction.sqrMagnitude < 1e-6f)
                direction = Vector3.forward;

            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }
    }
}
