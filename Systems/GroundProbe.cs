using System;
using Duckov.Utilities;
using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>
    /// 발자국을 놓을 바닥을 찾습니다.
    ///
    /// 캐릭터의 <c>transform.position.y</c>를 그대로 쓰면 안 됩니다. 캐릭터 원점은
    /// 발바닥이 아니고, 계단·경사·다리 위에서는 서 있는 면과 어긋납니다. 어긋나면
    /// 발자국이 공중에 뜨거나 바닥에 파묻혀 아예 안 보입니다.
    ///
    /// 게임이 이동·하차 판정에 쓰는 것과 같은 마스크(<see cref="GameplayDataSettings"/>의
    /// <c>Layers.groundLayerMask</c>)로 쏩니다. 우리가 레이어를 따로 추측하면 맵마다
    /// 다르게 틀립니다.
    /// </summary>
    internal static class GroundProbe
    {
        /// <summary>레이를 시작할 높이 (캐릭터 원점 기준 위쪽).</summary>
        private const float RayUp = 1.2f;

        /// <summary>레이 길이. 위로 올린 만큼에 여유를 더합니다.</summary>
        private const float RayLength = RayUp + 2.5f;

        /// <summary>
        /// 바닥에 살짝 띄우는 높이.
        ///
        /// 정확히 바닥면에 놓으면 지형 텍스처와 깊이가 같아져서 z-fighting으로
        /// 지글거립니다. 탑다운 시점이라 이 정도 띄워도 떠 보이지 않습니다.
        /// </summary>
        internal const float SurfaceLift = 0.02f;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        /// <summary>
        /// <paramref name="origin"/> 아래의 바닥을 찾습니다.
        ///
        /// 못 찾으면 <paramref name="origin"/>을 그대로 돌려주고 <c>false</c>를 냅니다.
        /// 부르는 쪽에서 "그래도 찍을지"를 정하게 하려고 실패를 감추지 않습니다 —
        /// 예를 들어 다리 위 같은 곳에서 바닥을 못 잡았는데 발밑에 찍어 버리면
        /// 발자국이 지형을 뚫고 아래층에 남습니다.
        /// </summary>
        internal static bool TryFind(Vector3 origin, out Vector3 point, out Vector3 normal)
        {
            point = origin;
            normal = Vector3.up;

            try
            {
                var ray = new Ray(origin + Vector3.up * RayUp, Vector3.down);
                int mask = GameplayDataSettings.Layers.groundLayerMask;

                // NonAlloc + 직접 최근접 선택. Physics.Raycast 한 방으로도 되지만,
                // 캐릭터 자신의 콜라이더가 마스크에 걸리는 맵이 있어서 여러 개를
                // 받아 두고 고르는 편이 안전합니다.
                int count = Physics.RaycastNonAlloc(
                    ray, Hits, RayLength, mask, QueryTriggerInteraction.Ignore);

                if (count <= 0)
                    return false;

                float best = float.MaxValue;
                bool found = false;

                for (int i = 0; i < count; i++)
                {
                    if (Hits[i].distance >= best)
                        continue;

                    best = Hits[i].distance;
                    point = Hits[i].point;
                    normal = Hits[i].normal;
                    found = true;
                }

                if (!found)
                    return false;

                point += Vector3.up * SurfaceLift;
                return true;
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[DuckTracks] 바닥 탐색 실패: {ex.Message}");
#endif
                return false;
            }
        }
    }
}
