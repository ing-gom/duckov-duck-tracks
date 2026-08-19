using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>
    /// 발자국 그림과 그것을 그리는 머티리얼.
    ///
    /// PNG는 <c>track_textures</c> 폴더에서 읽습니다. 하나도 못 찾아도 모드가
    /// 아무것도 안 그리는 상태로 떨어지면 안 되므로, 오리 물갈퀴 자국 하나는
    /// 코드로 그려서 항상 들고 있습니다.
    /// </summary>
    internal static class TrackTextures
    {
        internal const string TextureFolder = "track_textures";

        private static readonly Dictionary<string, Texture2D?> Cache = new();
        private static readonly Dictionary<string, Material> Materials = new();
        private static string[]? _names;
        private static string? _cachedModRoot;
        private static Texture2D? _builtinFoot;

        /// <summary>
        /// 이름으로 그림을 찾습니다. PNG를 못 찾으면 내장 물갈퀴 자국으로 떨어집니다.
        /// </summary>
        internal static Texture2D Resolve(string? textureName)
        {
            if (!string.IsNullOrEmpty(textureName))
            {
                // 직접 그린 도형이 먼저입니다. 같은 이름의 PNG가 있어도 사용자가
                // 방금 만든 쪽을 보여 주는 편이 덜 놀랍습니다.
                var drawn = CustomShapes.GetTexture(textureName);
                if (drawn != null)
                    return drawn;

                var loaded = Load(textureName!);
                if (loaded != null)
                    return loaded;
            }

            return BuiltinFoot();
        }

        /// <summary>
        /// 그림 + 합성 방식 조합마다 머티리얼 하나. 자국은 수백 개씩 나오므로
        /// 매번 새로 만들면 그대로 드로우콜과 GC 부담이 됩니다.
        /// </summary>
        internal static Material ResolveMaterial(string? textureName, TrackBlend blend)
        {
            string key = (textureName ?? "") + "|" + (int)blend;

            if (Materials.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var material = BuildMaterial(Resolve(textureName), blend);
            Materials[key] = material;
            return material;
        }

        /// <summary>
        /// 구워 둔 실제 발 실루엣용 머티리얼. 텍스처마다 하나씩 캐시합니다.
        ///
        /// 이름으로 캐시하는 <see cref="ResolveMaterial"/>과 통을 나눠야 합니다 —
        /// 구운 텍스처는 파일 이름이 없고, 좌우 발이 서로 다른 그림입니다.
        /// </summary>
        internal static Material ResolveBakedMaterial(Texture2D texture, TrackBlend blend)
        {
            string key = "baked:" + texture.GetInstanceID() + "|" + (int)blend;

            if (Materials.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var material = BuildMaterial(texture, blend);
            Materials[key] = material;
            return material;
        }

        /// <summary>구워 둔 텍스처로 바로 머티리얼을 만듭니다 (실제 발 실루엣용).</summary>
        internal static Material BuildMaterial(Texture2D texture, TrackBlend blend)
        {
            bool additive = blend == TrackBlend.Additive;

            var material = new Material(FindShader())
            {
                name = additive ? "DuckTracks_Add" : "DuckTracks_Blend",
                mainTexture = texture,
                hideFlags = HideFlags.HideAndDontSave,
            };

            // ── 여기부터가 핵심입니다. ──
            //
            // 셰이더만 찾아 놓고 아래를 빼먹으면 검은 사각형이 찍힙니다. URP의
            // Particles/Unlit은 기본 표면이 Opaque라서 알파를 통째로 무시하고,
            // 텍스처의 검은 배경이 그대로 그려집니다. 도형은 RGB 밝기로만 어렴풋이
            // 보이고요. 셰이더 이름을 찾은 것과 투명하게 그리는 것은 별개입니다.
            //
            // 어느 셰이더가 잡힐지는 빌드마다 다르므로 HasProperty로 하나씩 확인하고
            // 넣습니다. 없는 프로퍼티에 쓰면 경고만 쌓입니다.

            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);

            // 실제 합성을 정하는 것은 이 둘입니다. 셰이더가 Blend [_SrcBlend][_DstBlend]로
            // 읽어 갑니다.
            //   발광(가산)  : dst = src*a + dst        — 바닥에 빛을 더합니다
            //   덮어쓰기(알파): dst = src*a + dst*(1-a) — 바닥을 가립니다
            var src = UnityEngine.Rendering.BlendMode.SrcAlpha;
            var dst = additive
                ? UnityEngine.Rendering.BlendMode.One
                : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;

            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)src);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)dst);

            // 자국은 바닥에 겹쳐 눕습니다. 깊이를 쓰면 서로를 가려서 먼저 찍힌 것이
            // 사라집니다.
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);   // Transparent

            // URP의 _Blend는 0=알파, 1=미리곱함, 2=가산, 3=곱하기입니다.
            //
            // 여기에 1을 넣고 있었습니다 — 그건 가산이 아니라 "미리 곱한 알파"입니다.
            // 발광이 안 먹던 원인입니다. 런타임에서는 _SrcBlend/_DstBlend가 실제
            // 합성을 정하지만, URP는 _Blend를 보고 키워드를 다시 맞추는 경로가 있어서
            // 둘이 어긋나면 한쪽이 다른 쪽을 덮어씁니다.
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", additive ? 2f : 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            // 알파를 색에 미리 곱하는 모드가 켜져 있으면 가산에서 밝기가 이중으로
            // 깎입니다. 둘 다 꺼 두고 우리가 정한 블렌드만 쓰게 합니다.
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");

            if (additive)
                material.DisableKeyword("_ALPHABLEND_ON");
            else
                material.EnableKeyword("_ALPHABLEND_ON");

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;

            // 합성이 의심스러울 때 로그로 확인할 수 있게 남깁니다. 조합마다 한 번씩만
            // 찍히므로 시끄럽지 않습니다.
            UnityEngine.Debug.Log(
                $"[DuckTracks] 자국 재질 — {material.shader.name} / " +
                $"{(additive ? "발광" : "덮어쓰기")} / src={src} dst={dst} / " +
                $"큐={material.renderQueue}");

            return material;
        }

        /// <summary>
        /// 셰이더 후보를 순서대로 시도합니다.
        ///
        /// 가산/알파를 셰이더로 나누지 않습니다 — 어차피 BuildMaterial에서 블렌드
        /// 모드를 직접 지정하므로, 정점 색을 받는 파티클 계열이기만 하면 됩니다.
        /// 정점 색을 무시하는 셰이더를 쓰면 시간에 따라 사라지는 색이 안 먹습니다.
        /// </summary>
        private static Shader FindShader()
        {
            string[] candidates =
            {
                "Particles/Additive",
                "Legacy Shaders/Particles/Additive",
                "Universal Render Pipeline/Particles/Unlit",
                "Particles/Standard Unlit",
                "Sprites/Default",
            };

            foreach (string name in candidates)
            {
                var shader = Shader.Find(name);
                if (shader != null)
                    return shader;
            }

            return Shader.Find("Sprites/Default");
        }

        /// <summary>걸음 알갱이용 재질. 그림은 내장 점 하나뿐이라 합성 방식으로만 나뉩니다.</summary>
        internal static Material ResolveDotMaterial(TrackBlend blend)
        {
            string key = "dot|" + (int)blend;

            if (Materials.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var material = BuildMaterial(BuiltinDot(), blend);
            Materials[key] = material;
            return material;
        }

        private static Texture2D? _builtinDot;

        /// <summary>
        /// 가운데가 밝고 가장자리로 갈수록 사라지는 점.
        ///
        /// 흰 사각형을 쓰면 알갱이가 종잇조각처럼 보입니다. 감쇠를 제곱으로 두어
        /// 가운데를 또렷하게 남기면, 색과 합성 방식에 따라 먼지로도 반짝임으로도
        /// 읽힙니다.
        /// </summary>
        private static Texture2D BuiltinDot()
        {
            if (_builtinDot != null)
                return _builtinDot;

            const int size = 64;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "builtin_dot",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;

                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    falloff *= falloff;

                    byte level = (byte)Mathf.RoundToInt(falloff * 255f);
                    pixels[y * size + x] = new Color32(level, level, level, level);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            _builtinDot = texture;
            return texture;
        }

        /// <summary>고를 수 있는 그림 이름 목록. 첫 항목은 항상 내장 도형("")입니다.</summary>
        internal static string[] GetNames(bool refresh = false)
        {
            if (_names != null && !refresh)
                return _names;

            var names = new List<string> { "" };

            // 직접 그린 도형을 앞에 둡니다 — 목록에서 찾기 쉬운 자리입니다.
            foreach (var shape in CustomShapes.All)
            {
                if (!string.IsNullOrEmpty(shape.name) && !names.Contains(shape.name))
                    names.Add(shape.name);
            }

            try
            {
                foreach (string folder in GetFolders())
                {
                    if (!Directory.Exists(folder))
                        continue;

                    foreach (string file in Directory.GetFiles(folder))
                    {
                        string extension = Path.GetExtension(file).ToLowerInvariant();
                        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".tga")
                            continue;

                        string name = Path.GetFileNameWithoutExtension(file);
                        if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                            names.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 그림 목록 조회 실패: {ex.Message}");
            }

            if (refresh)
            {
                Cache.Clear();
                Materials.Clear();
            }

            _names = names.ToArray();
            return _names;
        }

        private static Texture2D? Load(string textureName)
        {
            if (Cache.TryGetValue(textureName, out var cached))
                return cached;

            Texture2D? result = null;

            try
            {
                foreach (string folder in GetFolders())
                {
                    if (result != null)
                        break;

                    foreach (string extension in new[] { ".png", ".jpg", ".jpeg", ".tga" })
                    {
                        string path = Path.Combine(folder, textureName + extension);
                        if (!File.Exists(path))
                            continue;

                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                        {
                            name = textureName,
                            wrapMode = TextureWrapMode.Clamp,
                            filterMode = FilterMode.Bilinear,
                            hideFlags = HideFlags.HideAndDontSave,
                        };

                        if (texture.LoadImage(File.ReadAllBytes(path)))
                            result = texture;
                        else
                            UnityEngine.Object.Destroy(texture);

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 그림 로드 실패({textureName}): {ex.Message}");
            }

            // 실패도 캐시합니다. 없는 이름을 매 걸음마다 디스크에서 찾으면 안 됩니다.
            Cache[textureName] = result;
            return result;
        }

        /// <summary>
        /// 그림을 찾을 폴더들 — <b>여러 곳</b>입니다. WeaponAura와 같은 규칙입니다.
        ///
        /// 사용자 데이터 폴더가 1순위인 이유는, 모드 폴더가 재설치·갱신 때 통째로
        /// 지워지기 때문입니다. 거기 넣은 그림은 업데이트에도 남습니다.
        ///
        /// 모드 폴더 안에서 자리가 둘인 이유는 빌드 방식마다 짐이 다르게 놓이기
        /// 때문입니다 — SDK가 게임에 바로 설치할 때는 <c>assets/</c> 안이 모드 루트로
        /// 펼쳐지고(<c>&lt;모드&gt;/track_textures</c>), 창작마당 묶음은 <c>assets/</c>를
        /// 그대로 들고 갑니다(<c>&lt;모드&gt;/assets/track_textures</c>).
        /// </summary>
        internal static List<string> GetFolders()
        {
            var folders = new List<string>();

            void Add(string? path)
            {
                if (!string.IsNullOrEmpty(path) && !folders.Contains(path!))
                    folders.Add(path!);
            }

            void AddModFolder(string? root)
            {
                if (string.IsNullOrEmpty(root))
                    return;

                Add(Path.Combine(root!, TextureFolder));
                Add(Path.Combine(Path.Combine(root!, "assets"), TextureFolder));
            }

            try
            {
                string user = Path.Combine(
                    Path.Combine(Application.persistentDataPath, "DuckTracks"), TextureFolder);

                Directory.CreateDirectory(user);
                Add(user);
            }
            catch
            {
                // 못 만들어도 아래 폴더들은 계속 봅니다.
            }

            AddModFolder(GetModRoot());

            try
            {
                string? dataPath = Application.dataPath;
                if (!string.IsNullOrEmpty(dataPath))
                {
                    AddModFolder(Path.Combine(Path.Combine(dataPath, "Mods"), "DuckTracks"));

                    string? gameRoot = Path.GetDirectoryName(dataPath);
                    if (!string.IsNullOrEmpty(gameRoot))
                        AddModFolder(Path.Combine(Path.Combine(gameRoot!, "Mods"), "DuckTracks"));
                }
            }
            catch
            {
                // 경로를 못 만들어도 나머지는 씁니다.
            }

            return folders;
        }

        /// <summary>사용자가 자기 그림을 넣을 폴더 (설정 창에서 안내용).</summary>
        internal static string? GetUserFolder()
        {
            var folders = GetFolders();
            return folders.Count > 0 ? folders[0] : null;
        }

        /// <summary>모드 루트 — 어셈블리 위치에서 assets 폴더를 가진 상위를 찾습니다.</summary>
        private static string? GetModRoot()
        {
            if (_cachedModRoot != null)
                return _cachedModRoot.Length == 0 ? null : _cachedModRoot;

            _cachedModRoot = "";

            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(assemblyPath))
                    return null;

                var dir = new DirectoryInfo(Path.GetDirectoryName(assemblyPath) ?? "");
                while (dir != null)
                {
                    if (Directory.Exists(Path.Combine(dir.FullName, "assets")))
                    {
                        _cachedModRoot = dir.FullName;
                        return _cachedModRoot;
                    }

                    dir = dir.Parent;
                }
            }
            catch
            {
                // 무시 — 다른 폴더 후보로 갑니다.
            }

            return null;
        }

        /// <summary>
        /// GLSL의 <c>smoothstep(edge0, edge1, x)</c>.
        ///
        /// <b>Unity의 <c>Mathf.SmoothStep</c>은 이것과 다른 함수입니다.</b> 그쪽은
        /// from과 to <i>사이를</i> t로 보간하므로, 경계 판정에 쓰면 0이나 1이 아니라
        /// 두 경계값 사이의 값만 돌려줍니다. 그대로 도형을 그리면 알파가 전체에
        /// 균일하게 깔려서 <b>도형이 아니라 사각형</b>이 나옵니다.
        /// </summary>
        private static float SmoothEdge(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>경계 안쪽이 1, 바깥이 0. 가장자리는 <paramref name="feather"/>만큼 부드럽게.</summary>
        private static float Solid(float distance, float radius, float feather)
        {
            return 1f - SmoothEdge(radius - feather, radius, distance);
        }

        /// <summary>점에서 선분까지의 거리와, 선분 위 투영 위치(0~1).</summary>
        private static float SegmentDistance(
            float px, float py, float ax, float ay, float bx, float by, out float t)
        {
            float vx = bx - ax;
            float vy = by - ay;
            float lengthSq = vx * vx + vy * vy;

            t = lengthSq < 1e-8f ? 0f : Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / lengthSq);

            float cx = ax + t * vx;
            float cy = ay + t * vy;

            return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        }

        /// <summary>
        /// 코드로 그린 오리 물갈퀴 자국.
        ///
        /// PNG가 하나도 없어도 모드가 눈에 보이게 하려고 둡니다. 발가락 셋이
        /// 뒤꿈치에서 뻗고, 그 사이를 물갈퀴가 오목하게 잇습니다.
        ///
        /// v가 진행 방향(1이 앞)입니다 — 자국을 진행 방향으로 회전시킬 때의 약속.
        /// </summary>
        private static Texture2D BuiltinFoot()
        {
            if (_builtinFoot != null)
                return _builtinFoot;

            const int size = 256;

            // 뒤꿈치. 발가락이 여기서 뻗어 나갑니다.
            const float heelU = 0.5f;
            const float heelV = 0.16f;

            // 부챗살이 벌어지는 반각. 물갈퀴 발은 사람 발보다 훨씬 넓습니다.
            const float halfSpread = 46f * Mathf.Deg2Rad;

            // 가운데 발가락이 가장 깁니다.
            const float midLength = 0.64f;
            const float outerLength = 0.56f;

            // 발가락 굵기 — 뿌리에서 끝으로 갈수록 가늘어집니다.
            const float toeBase = 0.068f;
            const float toeTip = 0.034f;

            // 물갈퀴가 발가락 끝까지 차는 비율과, 발가락 사이에서 안으로 패는 깊이.
            // 다 채우면(1, 0) 삼각형이 되고, 너무 파면 삼지창이 됩니다.
            const float webFill = 0.92f;
            const float webSag = 0.22f;

            // 가장자리를 부드럽게 만드는 폭. 이게 없으면 계단이 그대로 보입니다.
            const float feather = 0.018f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "builtin_duck_webbed",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];

            // 발가락 셋: (각도, 길이). 각도는 진행 방향(+v) 기준입니다.
            var toeAngles = new[] { -halfSpread, 0f, halfSpread };
            var toeLengths = new[] { outerLength, midLength, outerLength };

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;

                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;

                    float du = u - heelU;
                    float dv = v - heelV;
                    float r = Mathf.Sqrt(du * du + dv * dv);

                    float alpha = 0f;

                    // 1) 발가락 — 뒤꿈치에서 끝까지 이어지는, 끝으로 갈수록 가는 막대.
                    for (int i = 0; i < 3; i++)
                    {
                        float tipU = heelU + Mathf.Sin(toeAngles[i]) * toeLengths[i];
                        float tipV = heelV + Mathf.Cos(toeAngles[i]) * toeLengths[i];

                        float distance = SegmentDistance(u, v, heelU, heelV, tipU, tipV, out float along);
                        float radius = Mathf.Lerp(toeBase, toeTip, along);

                        alpha = Mathf.Max(alpha, Solid(distance, radius, feather));
                    }

                    // 2) 물갈퀴 — 이웃한 두 발가락 사이를 채우되, 바깥 경계를
                    //    가운데에서 가장 깊게 안으로 파냅니다.
                    if (dv > 0f && r > 1e-4f)
                    {
                        float angle = Mathf.Atan2(du, dv);

                        for (int i = 0; i < 2; i++)
                        {
                            float a0 = toeAngles[i];
                            float a1 = toeAngles[i + 1];

                            if (angle < a0 || angle > a1)
                                continue;

                            float s = Mathf.Clamp01((angle - a0) / (a1 - a0));
                            float reach = Mathf.Lerp(toeLengths[i], toeLengths[i + 1], s)
                                          * webFill
                                          * (1f - webSag * Mathf.Sin(Mathf.PI * s));

                            alpha = Mathf.Max(alpha, Solid(r, reach, feather));
                        }
                    }

                    // 3) 뒤꿈치 — 발가락이 모이는 자리를 메웁니다. 없으면 아래가 뾰족합니다.
                    alpha = Mathf.Max(alpha, Solid(r, toeBase * 1.45f, feather));

                    // RGB에도 같은 감쇠를 넣습니다. 가산 합성에서는 밝기가 곧 형상이고,
                    // 알파 합성에서는 알파가 형상을 냅니다 — 둘 다 맞추려면 같이 깎아야 합니다.
                    byte level = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
                    pixels[y * size + x] = new Color32(level, level, level, level);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            _builtinFoot = texture;
            return texture;
        }

        internal static void Dispose()
        {
            foreach (var material in Materials.Values)
            {
                if (material != null)
                    UnityEngine.Object.Destroy(material);
            }

            Materials.Clear();

            foreach (var texture in Cache.Values)
            {
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }

            Cache.Clear();
            _names = null;

            // 내장 도형은 남겨 둡니다. 모드를 껐다 켜는 동안 다시 그릴 이유가 없고,
            // 128x128 하나라 들고 있어도 부담이 없습니다.
        }
    }
}
