using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>
    /// 실제 발을 위에서 내려다본 실루엣으로 굽고, 발의 실제 크기를 잽니다.
    ///
    /// <b>정점을 읽을 수 없습니다.</b> 게임의 <c>DuckFoot</c> 메시는 Read/Write가 꺼진
    /// 채로 임포트돼 있어서 <c>mesh.vertices</c>가 통하지 않습니다. 그래서 CPU에서
    /// 삼각형을 채우는 길은 막혀 있고, <b>GPU로 한 번 그려서</b> 그 그림을 되읽는
    /// 방식으로 갑니다 — 메시가 안 읽혀도 그리는 것은 됩니다.
    ///
    /// 크기 재기는 정점이 필요 없습니다. <see cref="Renderer.localBounds"/>는
    /// Read/Write와 무관하게 항상 읽히므로, 실루엣이 아직 안 구워졌어도 자국 크기는
    /// 처음부터 실제 발과 같게 낼 수 있습니다.
    /// </summary>
    internal static class FootSilhouette
    {
        /// <summary>구운 발 하나.</summary>
        internal sealed class Baked
        {
            internal Texture2D Texture = null!;

            /// <summary>
            /// 이 텍스처가 월드에서 차지해야 하는 한 변의 길이(미터).
            ///
            /// 발 실제 크기에 여백까지 더한 값입니다. 자국을 이 크기로 찍으면
            /// 그림 안의 발이 실제 발과 정확히 같은 크기로 바닥에 놓입니다.
            /// </summary>
            internal float WorldSize;
        }

        /// <summary>최종 텍스처 한 변.</summary>
        private const int Size = 128;

        /// <summary>발 주위에 남기는 여백 비율. 0이면 발가락이 텍스처 모서리에 붙습니다.</summary>
        internal const float Padding = 0.08f;

        /// <summary>
        /// 구울 때 발을 옮겨 놓을 자리.
        ///
        /// 맵에서 아주 멀리 떨어뜨립니다. 카메라 앞에 발 말고 아무것도 없게 하려는
        /// 것입니다 — 레이어를 하나 빌리는 방법도 있지만, 다른 모드가 쓰고 있는
        /// 레이어를 건드릴 수 있어서 거리로 격리하는 편이 안전합니다.
        /// </summary>
        private static readonly Vector3 BakeOrigin = new Vector3(0f, -12000f, 0f);

        /// <summary>구운 결과. 렌더러마다 하나 — 좌우 발은 거울상이라 서로 다릅니다.</summary>
        private static readonly Dictionary<int, Baked?> Cache = new();

        /// <summary>지금 굽고 있는 것. GPU에 그림이 올라오려면 한 프레임이 필요합니다.</summary>
        private static Pending? _pending;

        private sealed class Pending
        {
            internal int Key;
            internal GameObject Subject = null!;
            internal Camera Camera = null!;
            internal RenderTexture Target = null!;
            internal float WorldSize;

            /// <summary>그리라고 시킨 프레임. 다음 프레임에 되읽습니다.</summary>
            internal int RequestedFrame;
        }

        private static Material? _whiteMaterial;

        /// <summary>
        /// 발의 실제 크기에 맞는 자국 한 변의 길이(미터).
        ///
        /// 실루엣이 아직 안 구워졌어도 씁니다 — 모양은 내장 도형이더라도 크기만은
        /// 처음부터 실제 발과 같아야 합니다.
        ///
        /// <see cref="Renderer.localBounds"/>의 여덟 꼭짓점을 발 기준 좌표계로 옮겨서
        /// 잽니다. 월드 AABB(<see cref="Renderer.bounds"/>)를 그냥 쓰면 캐릭터가
        /// 비스듬히 서 있을 때 최대 √2배까지 부풀어 오릅니다.
        /// </summary>
        internal static float MeasureWorldSize(Renderer? renderer, Transform footBone, Transform? tip)
        {
            if (renderer == null || footBone == null)
                return 0f;

            try
            {
                Quaternion align = Quaternion.Inverse(FootRotation(footBone, tip));

                var local = renderer.localBounds;
                var toWorld = renderer.transform.localToWorldMatrix;

                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;

                Vector3 center = local.center;
                Vector3 extents = local.extents;

                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        center.x + ((corner & 1) == 0 ? -extents.x : extents.x),
                        center.y + ((corner & 2) == 0 ? -extents.y : extents.y),
                        center.z + ((corner & 4) == 0 ? -extents.z : extents.z));

                    Vector3 inFoot = align * (toWorld.MultiplyPoint3x4(point) - footBone.position);

                    if (inFoot.x < minX) minX = inFoot.x;
                    if (inFoot.x > maxX) maxX = inFoot.x;
                    if (inFoot.z < minZ) minZ = inFoot.z;
                    if (inFoot.z > maxZ) maxZ = inFoot.z;
                }

                float extent = Mathf.Max(maxX - minX, maxZ - minZ);
                if (extent < 1e-4f)
                    return 0f;

                // 그림은 발 둘레에 여백을 두고 담기므로, 그림 전체가 차지하는 월드
                // 크기는 발보다 그만큼 큽니다.
                return extent / (1f - Padding * 2f);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 발 크기 재기 실패: {ex.Message}");
                return 0f;
            }
        }

        /// <summary>
        /// 구운 실루엣. 아직 없으면 굽기를 걸어 두고 <c>null</c>을 냅니다 —
        /// 부르는 쪽은 그 사이 내장 도형으로 찍으면 됩니다.
        /// </summary>
        internal static Baked? Get(Renderer? renderer, Transform footBone, Transform? tip)
        {
            if (renderer == null || footBone == null)
                return null;

            int key = renderer.GetInstanceID();

            if (Cache.TryGetValue(key, out var cached))
                return cached;

            Request(key, renderer, footBone, tip);
            return null;
        }

        /// <summary>발을 멀리 옮겨 놓고 위에서 내려다보는 카메라를 세웁니다.</summary>
        private static void Request(int key, Renderer renderer, Transform footBone, Transform? tip)
        {
            // 한 번에 하나만 굽습니다. 발은 둘뿐이라 줄을 설 일이 거의 없고,
            // 카메라를 여럿 띄우면 그 프레임에 눈에 띄게 걸립니다.
            if (_pending != null)
                return;

            var filter = renderer.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;

            if (mesh == null)
            {
                Cache[key] = null;
                return;
            }

            try
            {
                float worldSize = MeasureWorldSize(renderer, footBone, tip);
                if (worldSize <= 0f)
                {
                    Cache[key] = null;
                    return;
                }

                Quaternion align = Quaternion.Inverse(FootRotation(footBone, tip));

                // 발을 그대로 복제하지 않고 메시만 새 오브젝트에 얹습니다. Instantiate로
                // 통째로 복제하면 파티클·스크립트 같은 것이 딸려 와서 굽는 동안 제 할 일을
                // 하려 듭니다.
                var subject = new GameObject("DuckTracks_BakeSubject");
                subject.transform.position = BakeOrigin + align * (renderer.transform.position - footBone.position);
                subject.transform.rotation = align * renderer.transform.rotation;
                subject.transform.localScale = renderer.transform.lossyScale;

                subject.AddComponent<MeshFilter>().sharedMesh = mesh;

                var meshRenderer = subject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = WhiteMaterial();
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                var bounds = meshRenderer.bounds;

                var target = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32)
                {
                    name = "DuckTracks_BakeTarget",
                    hideFlags = HideFlags.HideAndDontSave,
                };

                var cameraGo = new GameObject("DuckTracks_BakeCamera");
                cameraGo.transform.position = new Vector3(bounds.center.x, bounds.max.y + 1f, bounds.center.z);

                // 똑바로 내려다보되, 카메라의 위쪽을 월드 +Z에 맞춥니다. 발의 진행
                // 방향을 +Z로 돌려 놓았으므로 이러면 그림의 위쪽이 곧 발 앞쪽입니다.
                cameraGo.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

                var camera = cameraGo.AddComponent<Camera>();
                camera.orthographic = true;

                // 렌더 타깃이 정사각이므로 종횡비도 1로 못 박습니다. 그냥 두면
                // 화면 종횡비가 새어 들어와 발이 한 축으로만 늘어납니다.
                camera.aspect = 1f;

                // orthographicSize는 세로 <b>반</b>지름입니다.
                camera.orthographicSize = worldSize * 0.5f;

                camera.clearFlags = CameraClearFlags.SolidColor;

                // 검은 바탕에 흰 발. 알파를 믿지 않는 이유는 렌더 파이프라인에 따라
                // 배경 알파가 1로 채워져 나오는 경우가 있기 때문입니다. 밝기로 읽으면
                // 어느 쪽이든 통합니다.
                camera.backgroundColor = Color.black;

                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 5f;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.useOcclusionCulling = false;
                camera.targetTexture = target;

                // 켜 둔 채로 한 프레임 흘려보냅니다. Camera.Render()를 직접 부르는 길은
                // URP에서 막혀 있습니다 — 이 게임이 URP입니다(로그의 셰이더 이름이 그
                // 증거입니다).
                camera.enabled = true;

                _pending = new Pending
                {
                    Key = key,
                    Subject = subject,
                    Camera = camera,
                    Target = target,
                    WorldSize = worldSize,
                    RequestedFrame = Time.frameCount,
                };
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 발 굽기 준비 실패: {ex.Message}");
                Cache[key] = null;
                Cleanup();
            }
        }

        /// <summary>
        /// 그려진 그림을 되읽습니다. 매 프레임 <c>LateUpdate</c>에서 부릅니다.
        /// </summary>
        internal static void LateTick()
        {
            var pending = _pending;
            if (pending == null)
                return;

            // 카메라가 실제로 그린 뒤에 읽어야 합니다.
            if (Time.frameCount <= pending.RequestedFrame)
                return;

            try
            {
                var texture = ReadBack(pending.Target);

                Cache[pending.Key] = texture != null
                    ? new Baked { Texture = texture, WorldSize = pending.WorldSize }
                    : null;

#if DEBUG
                UnityEngine.Debug.Log(
                    $"[DuckTracks] 발 실루엣 구움 — 월드 크기 {pending.WorldSize:F3}m" +
                    $" / 발 실제 {pending.WorldSize * (1f - Padding * 2f):F3}m" +
                    $" / {(texture != null ? "성공" : "빈 그림")}");
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 발 실루엣 되읽기 실패: {ex.Message}");
                Cache[pending.Key] = null;
            }
            finally
            {
                Cleanup();
            }
        }

        /// <summary>
        /// 렌더 텍스처를 읽어 자국용 그림으로 바꿉니다.
        ///
        /// 밝기를 그대로 RGB와 알파 양쪽에 넣습니다. 가산 합성에서는 밝기가 형상을
        /// 내고 알파 합성에서는 알파가 냅니다 — 둘 다 쓰려면 같이 넣어야 합니다.
        /// </summary>
        private static Texture2D? ReadBack(RenderTexture target)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;

            try
            {
                var raw = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                raw.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                raw.Apply(false, false);

                var source = raw.GetPixels32();
                var pixels = new Color32[source.Length];

                int lit = 0;

                for (int i = 0; i < source.Length; i++)
                {
                    // 흰 발 / 검은 바탕이므로 가장 밝은 채널이 곧 덮임 정도입니다.
                    byte level = Math.Max(source[i].r, Math.Max(source[i].g, source[i].b));
                    pixels[i] = new Color32(level, level, level, level);

                    if (level > 8)
                        lit++;
                }

                UnityEngine.Object.Destroy(raw);

                // 아무것도 안 그려졌으면 실패로 봅니다. 투명한 그림을 자국으로 쓰면
                // 발자국이 조용히 사라진 것처럼 보입니다.
                if (lit < 16)
                    return null;

                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                {
                    name = "foot_silhouette",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                texture.SetPixels32(pixels);
                texture.Apply(false, false);

                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static void Cleanup()
        {
            var pending = _pending;
            _pending = null;

            if (pending == null)
                return;

            if (pending.Camera != null)
            {
                pending.Camera.targetTexture = null;
                UnityEngine.Object.Destroy(pending.Camera.gameObject);
            }

            if (pending.Subject != null)
                UnityEngine.Object.Destroy(pending.Subject);

            if (pending.Target != null)
            {
                pending.Target.Release();
                UnityEngine.Object.Destroy(pending.Target);
            }
        }

        /// <summary>
        /// 발이 향한 쪽을 +Z로 삼는 회전.
        ///
        /// 뼈의 로컬 축은 리그를 만든 쪽 관례에 달려 있어 믿을 수 없지만, 뼈에서
        /// 발끝으로 가는 벡터는 언제나 실제 발이 향한 쪽입니다.
        /// </summary>
        private static Quaternion FootRotation(Transform footBone, Transform? tip)
        {
            Vector3 forward = tip != null ? tip.position - footBone.position : footBone.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        /// <summary>그림자도 빛도 받지 않는 순백. 실루엣만 뽑으면 되므로 이걸로 충분합니다.</summary>
        private static Material WhiteMaterial()
        {
            if (_whiteMaterial != null)
                return _whiteMaterial;

            Shader? shader = null;

            foreach (string name in new[]
                     {
                         "Unlit/Color",
                         "Universal Render Pipeline/Unlit",
                         "Sprites/Default",
                     })
            {
                shader = Shader.Find(name);
                if (shader != null)
                    break;
            }

            _whiteMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"))
            {
                name = "DuckTracks_BakeWhite",
                hideFlags = HideFlags.HideAndDontSave,
            };

            if (_whiteMaterial.HasProperty("_Color"))
                _whiteMaterial.SetColor("_Color", Color.white);
            if (_whiteMaterial.HasProperty("_BaseColor"))
                _whiteMaterial.SetColor("_BaseColor", Color.white);

            return _whiteMaterial;
        }

        internal static void Dispose()
        {
            Cleanup();

            foreach (var baked in Cache.Values)
            {
                if (baked != null && baked.Texture != null)
                    UnityEngine.Object.Destroy(baked.Texture);
            }

            Cache.Clear();
        }
    }
}
