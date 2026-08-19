using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DuckTracks.Helpers
{
    /// <summary>
    /// 캐릭터 모델이 실제로 어떻게 생겼는지 로그로 뽑습니다.
    ///
    /// <b>왜 필요한가</b> — "캐릭터의 실제 발 모양으로 자국을 찍는다"를 하려면 모델
    /// 계층에서 발을 집어내야 하는데, <see cref="CharacterModel"/>이 내주는 소켓은
    /// 손·방어구·헬멧·얼굴·가방·근접무기뿐이고 <b>발 소켓이 없습니다</b>. 그래서
    /// 뼈 이름이나 메시 이름을 짐작해서 찾아야 하는데, 짐작으로 짜면 캐릭터 종류가
    /// 바뀔 때 조용히 실패합니다. 한 번 찍어 보고 사실 위에서 짭니다.
    ///
    /// 진단용이라 DEBUG 빌드에만 들어갑니다.
    /// </summary>
    internal static class CharacterModelProbe
    {
        /// <summary>이름에 이게 들어 있으면 발 후보로 표시합니다.</summary>
        private static readonly string[] FootHints =
        {
            "foot", "feet", "toe", "ankle", "leg", "shoe", "boot", "paw", "claw",
            "발", "다리",
        };

        /// <summary>
        /// 플레이어 모델 계층을 통째로 로그에 씁니다.
        ///
        /// 각 줄에 이름 · 로컬 Y · 렌더러 종류 · 메시 정점 수를 같이 냅니다.
        /// 발을 찾는 데 필요한 건 결국 "바닥에 가장 가까운, 메시를 가진 것"이라서
        /// 높이와 메시 유무가 이름만큼 중요합니다.
        /// </summary>
        internal static void Dump()
        {
            try
            {
                var level = LevelManager.Instance;
                if (level == null || level.MainCharacter == null)
                {
                    UnityEngine.Debug.Log("[DuckTracks] 덤프 불가 — 판에 들어간 뒤에 눌러 주세요.");
                    return;
                }

                var character = level.MainCharacter;
                var model = character.characterModel;

                if (model == null)
                {
                    UnityEngine.Debug.Log("[DuckTracks] 덤프 불가 — characterModel이 없습니다.");
                    return;
                }

                var root = model.transform;
                float rootY = root.position.y;

                var sb = new StringBuilder();
                sb.AppendLine("[DuckTracks] ===== 캐릭터 모델 덤프 시작 =====");
                sb.AppendLine($"루트: {root.name}  (월드 Y {rootY:F3})");
                sb.AppendLine("표기: [발후보]  들여쓰기=계층  (바닥높이 / 렌더러 / 정점수)");

                int count = Walk(root, root, 0, sb);

                sb.AppendLine($"[DuckTracks] 트랜스폼 {count}개");
                sb.AppendLine("[DuckTracks] ===== 덤프 끝 =====");

                UnityEngine.Debug.Log(sb.ToString());
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckTracks] 모델 덤프 실패: {ex}");
            }
        }

        /// <summary>
        /// 계층을 훑으며 한 줄씩 씁니다.
        ///
        /// 깊이 제한을 둡니다 — 리그에 따라 뼈가 아주 깊게 들어가는데, 그 끝까지
        /// 다 찍으면 로그가 수천 줄이 되어 오히려 못 읽습니다.
        /// </summary>
        private static int Walk(Transform node, Transform root, int depth, StringBuilder sb)
        {
            const int maxDepth = 12;

            int count = 1;

            string indent = new string(' ', depth * 2);
            string hint = LooksLikeFoot(node.name) ? "[발후보] " : "         ";

            // 루트 기준 높이. 발은 이 값이 0에 가장 가깝습니다.
            float localHeight = node.position.y - root.position.y;

            string renderer = DescribeRenderer(node);

            sb.AppendLine($"{hint}{indent}{node.name}  ({localHeight:F3}{renderer})");

            if (depth >= maxDepth)
            {
                if (node.childCount > 0)
                    sb.AppendLine($"{indent}  … 자식 {node.childCount}개 생략(깊이 제한)");

                return count;
            }

            for (int i = 0; i < node.childCount; i++)
                count += Walk(node.GetChild(i), root, depth + 1, sb);

            return count;
        }

        /// <summary>
        /// 이 트랜스폼에 붙은 렌더러와 메시 정보.
        ///
        /// 자국 모양을 뽑으려면 결국 메시의 정점이 필요하므로, 어떤 종류의 렌더러인지와
        /// 정점이 몇 개인지가 핵심입니다. SkinnedMeshRenderer면 뼈에 물려 있어서
        /// 발만 따로 떼려면 본 웨이트를 봐야 하고, MeshRenderer면 그냥 떼면 됩니다.
        /// </summary>
        private static string DescribeRenderer(Transform node)
        {
            var skinned = node.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null)
            {
                var mesh = skinned.sharedMesh;
                int vertices = mesh != null ? mesh.vertexCount : 0;
                int bones = skinned.bones != null ? skinned.bones.Length : 0;
                return $" / SkinnedMesh '{(mesh != null ? mesh.name : "없음")}' / 정점 {vertices} / 본 {bones}";
            }

            var filter = node.GetComponent<MeshFilter>();
            if (filter != null)
            {
                var mesh = filter.sharedMesh;
                int vertices = mesh != null ? mesh.vertexCount : 0;
                return $" / Mesh '{(mesh != null ? mesh.name : "없음")}' / 정점 {vertices}";
            }

            var any = node.GetComponent<Renderer>();
            if (any != null)
                return $" / {any.GetType().Name}";

            return "";
        }

        private static bool LooksLikeFoot(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            string lower = name.ToLowerInvariant();

            foreach (string hint in FootHints)
            {
                if (lower.Contains(hint))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 바닥에 가장 가까운 메시들만 추려서 따로 냅니다.
        ///
        /// 이름으로 찾는 것이 실패했을 때의 대안입니다 — 서 있는 캐릭터에서 가장
        /// 낮은 메시는 정의상 발입니다. 이름 규칙과 무관하게 통하는 길이라
        /// 이쪽이 오히려 본선일 수 있습니다.
        /// </summary>
        internal static void DumpLowest()
        {
            try
            {
                var level = LevelManager.Instance;
                var character = level != null ? level.MainCharacter : null;
                var model = character != null ? character.characterModel : null;

                if (model == null)
                {
                    UnityEngine.Debug.Log("[DuckTracks] 덤프 불가 — 판에 들어간 뒤에 눌러 주세요.");
                    return;
                }

                float rootY = model.transform.position.y;
                var rows = new List<(float bottom, string line)>();

                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null)
                        continue;

                    var bounds = renderer.bounds;

                    rows.Add((
                        bounds.min.y - rootY,
                        $"{bounds.min.y - rootY,7:F3} ~ {bounds.max.y - rootY,7:F3}  " +
                        $"가로 {bounds.size.x:F3} x {bounds.size.z:F3}  " +
                        $"{renderer.GetType().Name,-22} {renderer.name}"));
                }

                rows.Sort((a, b) => a.bottom.CompareTo(b.bottom));

                var sb = new StringBuilder();
                sb.AppendLine("[DuckTracks] ===== 낮은 순 렌더러 =====");
                sb.AppendLine("바닥높이 ~ 천장높이   가로크기   종류   이름");

                int shown = 0;
                foreach (var row in rows)
                {
                    sb.AppendLine(row.line);
                    if (++shown >= 30)
                    {
                        sb.AppendLine($"… 그 외 {rows.Count - shown}개 생략");
                        break;
                    }
                }

                sb.AppendLine("[DuckTracks] ===== 끝 =====");
                UnityEngine.Debug.Log(sb.ToString());
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DuckTracks] 낮은 순 덤프 실패: {ex}");
            }
        }
    }
}
