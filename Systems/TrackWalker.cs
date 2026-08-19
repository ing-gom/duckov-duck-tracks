using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>
    /// 한 대상이 얼마나 걸었는지를 세는 자리.
    ///
    /// 자국을 놓는 기준은 <b>시간이 아니라 거리</b>입니다. 시간 기준으로 하면 제자리에
    /// 서 있거나 아주 느리게 움직일 때 같은 자리에 자국이 겹쳐 쌓이고, 빠르게 달릴 때는
    /// 자국 사이가 성기게 벌어집니다.
    /// </summary>
    internal sealed class TrackWalker
    {
        /// <summary>
        /// 이만큼 한 프레임에 움직였으면 순간이동으로 봅니다.
        ///
        /// 맵을 옮기거나 탈것에서 내릴 때 위치가 한 번에 크게 튑니다. 그대로 두면
        /// 그 사이를 걸어간 것으로 쳐서 지나오지도 않은 곳에 자국이 줄줄이 남습니다.
        /// </summary>
        private const float TeleportDistance = 4f;

        private Vector3 _lastPosition;
        private float _accumulated;
        private bool _primed;

        /// <summary>다음에 어느 쪽 발을 찍을지. 좌·우 번갈아 가는 데 씁니다.</summary>
        private bool _rightFoot;

        /// <summary>진행 방향. 멈춰 있을 때 마지막 방향을 유지하려고 들고 있습니다.</summary>
        private Vector3 _heading = Vector3.forward;

        /// <summary>대상이 사라졌는지 판단할 때 쓰는, 마지막으로 갱신된 시각.</summary>
        internal float LastSeenTime;

        /// <summary>
        /// 한 프레임 진행.
        /// </summary>
        /// <param name="position">대상의 현재 위치.</param>
        /// <param name="facing">
        /// 대상이 보는 방향. 실제로 움직이지 않은 프레임에만 씁니다.
        ///
        /// 평소에는 <b>이동 방향</b>이 우선입니다 — 조준하며 뒷걸음질할 때 캐릭터는
        /// 앞을 보지만 발자국은 뒤로 나 있어야 합니다.
        /// </param>
        /// <param name="running">달리는 중인지. 걸음 폭을 넓힙니다.</param>
        internal void Tick(TrackProfile profile, Vector3 position, Vector3 facing, bool running)
        {
            if (!_primed)
            {
                _lastPosition = position;
                _primed = true;
                return;
            }

            // 높이 변화는 걸음으로 세지 않습니다. 계단이나 경사에서 실제로 걸은
            // 거리보다 많이 걸은 것으로 잡히면 자국이 촘촘해집니다.
            Vector3 delta = position - _lastPosition;
            delta.y = 0f;

            float moved = delta.magnitude;
            _lastPosition = position;

            if (moved > TeleportDistance)
            {
                // 튄 만큼은 버리고 이번 자리에서 다시 셉니다.
                _accumulated = 0f;
                return;
            }

            if (moved > 0.0001f)
                _heading = delta / moved;
            else if (facing.sqrMagnitude > 0.0001f)
                _heading = new Vector3(facing.x, 0f, facing.z).normalized;

            _accumulated += moved;

            float stride = Mathf.Max(0.05f, profile.stride * (running ? Mathf.Max(0.1f, profile.runStrideScale) : 1f));

            // while입니다. 한 프레임에 stride보다 많이 움직였으면 그만큼 자국이
            // 여러 개 나와야 합니다 — if로 하나만 찍으면 빠를수록 자국이 성깁니다.
            // 다만 순간이동을 이미 걸렀어도 프레임이 크게 튈 수 있어서 상한을 둡니다.
            int guard = 0;
            while (_accumulated >= stride && guard++ < 16)
            {
                _accumulated -= stride;

                // 자국을 지금 위치가 아니라 걸음이 완성된 지점에 놓습니다. 프레임이
                // 길 때 자국이 한 프레임 뒤로 밀려 뭉치는 것을 막습니다.
                Vector3 stepPoint = position - _heading * _accumulated;

                Emit(profile, stepPoint, _heading);
            }
        }

        private void Emit(TrackProfile profile, Vector3 point, Vector3 heading)
        {
            Vector3 right = Vector3.Cross(Vector3.up, heading).normalized;
            float yaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;

            switch (profile.kind)
            {
                case TrackKind.Wheel:
                    // 바퀴는 번갈아가 아닙니다. 좌우 두 줄이 동시에 이어져야
                    // 점점이 찍힌 게 아니라 지나간 자국으로 읽힙니다.
                    Place(profile, point - right * profile.spread, heading, yaw, -1);
                    Place(profile, point + right * profile.spread, heading, yaw, +1);
                    break;

                case TrackKind.Hoof:
                {
                    int side = _rightFoot ? +1 : -1;
                    _rightFoot = !_rightFoot;

                    Vector3 lateral = right * profile.spread * side;

                    if (profile.pairGap > 0.01f)
                    {
                        // 앞발과 뒷발. 한 걸음에 둘 다 남습니다.
                        Place(profile, point + lateral + heading * (profile.pairGap * 0.5f), heading, yaw, side);
                        Place(profile, point + lateral - heading * (profile.pairGap * 0.5f), heading, yaw, side);
                    }
                    else
                    {
                        Place(profile, point + lateral, heading, yaw, side);
                    }

                    break;
                }

                default:
                {
                    int side = _rightFoot ? +1 : -1;
                    _rightFoot = !_rightFoot;

                    Place(profile, point + right * profile.spread * side, heading, yaw, side);
                    break;
                }
            }
        }

        /// <summary>
        /// 자국 하나를 실제로 놓습니다. 바닥을 못 찾으면 <b>찍지 않습니다</b> —
        /// 발밑 높이로 대충 찍으면 다리 위나 실내 2층에서 지형을 뚫고 아래층에 남습니다.
        /// </summary>
        private static void Place(TrackProfile profile, Vector3 point, Vector3 heading, float yaw, int side)
        {
            if (!GroundProbe.TryFind(point, out Vector3 ground, out _))
                return;

            float jitter = profile.angleJitter;
            if (jitter > 0.01f)
            {
                // 절반은 바깥으로 벌리는 고정값(팔자걸음), 절반은 무작위입니다.
                // 전부 무작위로 하면 방향이 흔들려 보이고, 전부 고정이면 도장 티가 납니다.
                yaw += jitter * 0.5f * side;
                yaw += UnityEngine.Random.Range(-jitter * 0.5f, jitter * 0.5f);
            }

            // 거리 누적 방식은 탈것 쪽이라 실제 발이라는 게 없습니다. 도형이나
            // PNG 중에서 고릅니다.
            string? name = profile.shapeSource == TrackShapeSource.Texture ? profile.textureName : null;
            var material = TrackTextures.ResolveMaterial(name, profile.blend);

            TrackStampSystem.Stamp(
                profile.kind.ToString(), profile, material, ground, yaw, profile.size);
        }

        /// <summary>탈것을 타고 내릴 때처럼 이어짐을 끊어야 할 때.</summary>
        internal void Reset()
        {
            _primed = false;
            _accumulated = 0f;
        }
    }
}
