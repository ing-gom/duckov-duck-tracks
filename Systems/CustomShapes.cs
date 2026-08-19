using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>
    /// 사용자가 설정 창에서 직접 칠해 만든 도형 하나.
    ///
    /// 칸을 문자열('0'/'1')로 들고 있습니다. bool 배열은 JsonUtility가 다루기 번거롭고,
    /// 문자열이면 저장 파일을 열었을 때 사람이 모양을 눈으로 읽을 수 있습니다.
    /// </summary>
    [Serializable]
    public class CustomShape
    {
        public string name = "";

        /// <summary>한 변의 칸 수</summary>
        public int size = CustomShapes.GridSize;

        /// <summary>size × size 개의 '0'/'1'. 첫 글자가 아래쪽 첫 칸입니다.</summary>
        public string cells = "";

        public bool IsValid => size > 0 && !string.IsNullOrEmpty(cells) && cells.Length >= size * size;

        public bool Get(int x, int y)
        {
            if (!IsValid || x < 0 || y < 0 || x >= size || y >= size)
                return false;

            return cells[y * size + x] == '1';
        }
    }

    /// <summary>
    /// 직접 그린 도형 보관소.
    ///
    /// 칸 정보를 그대로 쓰면 24칸짜리 계단이 그대로 보입니다. 출력 텍스처를 구울 때
    /// 칸 격자를 이중선형으로 보간한 뒤 문턱값을 부드럽게 넘겨서 가장자리를 다듬습니다.
    /// 그래서 24×24로 그려도 하트 정도는 매끄럽게 나옵니다.
    /// </summary>
    public static class CustomShapes
    {
        /// <summary>편집 격자 한 변의 칸 수</summary>
        public const int GridSize = 24;

        /// <summary>구워 낼 텍스처 한 변의 픽셀 수</summary>
        private const int BakeSize = 128;

        public const string FileName = "duck_tracks_custom_shapes.json";

        /// <summary>한 사람이 관리할 수 있는 정도. 목록을 ◀ ▶로 넘기기 때문에 무한정은 곤란합니다.</summary>
        public const int MaxShapes = 24;

        [Serializable]
        private class ShapeSetData
        {
            public CustomShape[] shapes = Array.Empty<CustomShape>();
        }

        private static List<CustomShape>? _shapes;

        private static readonly Dictionary<string, Texture2D> _textures =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        public static List<CustomShape> All
        {
            get
            {
                if (_shapes == null)
                {
                    _shapes = new List<CustomShape>();
                    Load();
                }

                return _shapes;
            }
        }

        public static CustomShape? Find(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            foreach (var shape in All)
            {
                if (shape != null && string.Equals(shape.name, name, StringComparison.OrdinalIgnoreCase))
                    return shape;
            }

            return null;
        }

        /// <summary>이름에 해당하는 텍스처. 없으면 null (그러면 내장 도형으로 넘어갑니다).</summary>
        public static Texture2D? GetTexture(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (_textures.TryGetValue(name!, out var cached) && cached != null)
                return cached;

            var shape = Find(name);
            if (shape == null || !shape.IsValid)
                return null;

            var texture = Bake(shape);
            _textures[name!] = texture;
            return texture;
        }

        /// <summary>도형을 새로 넣거나 같은 이름을 덮어씁니다.</summary>
        public static bool Save(string name, bool[] cells, int size, out string reason)
        {
            reason = "";

            if (string.IsNullOrWhiteSpace(name))
            {
                reason = "name";
                return false;
            }

            bool anyFilled = false;
            foreach (bool cell in cells)
            {
                if (cell)
                {
                    anyFilled = true;
                    break;
                }
            }

            if (!anyFilled)
            {
                reason = "empty";
                return false;
            }

            var builder = new StringBuilder(size * size);
            for (int i = 0; i < size * size; i++)
                builder.Append(i < cells.Length && cells[i] ? '1' : '0');

            name = name.Trim();

            var existing = Find(name);
            if (existing != null)
            {
                existing.size = size;
                existing.cells = builder.ToString();
            }
            else
            {
                if (All.Count >= MaxShapes)
                {
                    reason = "full";
                    return false;
                }

                All.Add(new CustomShape { name = name, size = size, cells = builder.ToString() });
            }

            // 같은 이름으로 다시 구워야 하므로 캐시를 버립니다.
            Forget(name);
            Write();
            return true;
        }

        public static bool Delete(string? name)
        {
            var shape = Find(name);
            if (shape == null)
                return false;

            All.Remove(shape);
            Forget(shape.name);
            Write();
            return true;
        }

        private static void Forget(string name)
        {
            if (_textures.TryGetValue(name, out var texture))
            {
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);

                _textures.Remove(name);
            }
        }

        // ── 무작위 생성 ──────────────────────────────────────────────

        /// <summary>
        /// 칸을 무작위로 채웁니다.
        ///
        /// 칸마다 동전을 던지면 알아볼 수 없는 노이즈만 나옵니다. 대신 중심에서의 반지름을
        /// 각도의 함수로 만들고(하모닉 두세 개를 겹칩니다) 그 안쪽을 채웁니다 —
        /// 꽃잎·별·물방울 같은 "의도적으로 보이는" 모양이 나옵니다.
        /// 왼쪽 절반만 계산해서 오른쪽에 거울처럼 붙입니다. 대칭이면 훨씬 그럴듯해 보입니다.
        /// </summary>
        public static void Randomize(int seed, int size, bool[] cells)
        {
            if (cells == null || size <= 0)
                return;

            var rng = new System.Random(seed);
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            bool Chance(float p) => rng.NextDouble() < p;

            float half = size * 0.5f;

            // 기본 반지름과, 거기에 얹을 물결 두세 개.
            float baseRadius = Range(0.5f, 0.8f);
            int harmonics = rng.Next(2, 4);

            var orders = new int[harmonics];
            var amplitudes = new float[harmonics];
            var phases = new float[harmonics];

            for (int i = 0; i < harmonics; i++)
            {
                orders[i] = rng.Next(2, 8);
                amplitudes[i] = Range(0.06f, 0.26f);
                phases[i] = Range(0f, Mathf.PI * 2f);
            }

            // 가끔 가운데를 비워 고리 모양을 만듭니다.
            float hole = Chance(0.25f) ? Range(0.2f, 0.45f) : 0f;

            int mid = (size + 1) / 2;

            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f - half) / half;

                for (int x = 0; x < mid; x++)
                {
                    float nx = (x + 0.5f - half) / half;

                    float distance = Mathf.Sqrt(nx * nx + ny * ny);
                    float angle = Mathf.Atan2(ny, nx);

                    float radius = baseRadius;
                    for (int i = 0; i < harmonics; i++)
                        radius += amplitudes[i] * Mathf.Cos(orders[i] * angle + phases[i]);

                    bool filled = distance <= radius && distance >= hole;

                    cells[y * size + x] = filled;
                    cells[y * size + (size - 1 - x)] = filled;   // 거울
                }
            }
        }

        // ── 굽기 ─────────────────────────────────────────────────────

        /// <summary>
        /// 칸 격자를 텍스처로 굽습니다.
        ///
        /// 칸 값을 그대로 확대하면 24칸짜리 계단이 됩니다. 이중선형으로 보간해서
        /// 0~1 사이 값을 만든 뒤 0.35~0.65 구간을 부드럽게 넘기면, 모양은 유지하면서
        /// 가장자리만 한 픽셀 폭으로 깎입니다.
        /// </summary>
        private static Texture2D Bake(CustomShape shape)
        {
            var texture = new Texture2D(BakeSize, BakeSize, TextureFormat.RGBA32, false)
            {
                name = "DuckTracks_CustomShape_" + shape.name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            int size = shape.size;
            var pixels = new Color32[BakeSize * BakeSize];

            for (int py = 0; py < BakeSize; py++)
            {
                float gy = (py + 0.5f) / BakeSize * size - 0.5f;

                for (int px = 0; px < BakeSize; px++)
                {
                    float gx = (px + 0.5f) / BakeSize * size - 0.5f;

                    float value = SampleBilinear(shape, gx, gy);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, value));

                    pixels[py * BakeSize + px] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static float SampleBilinear(CustomShape shape, float x, float y)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);

            float fx = x - x0;
            float fy = y - y0;

            float v00 = shape.Get(x0, y0) ? 1f : 0f;
            float v10 = shape.Get(x0 + 1, y0) ? 1f : 0f;
            float v01 = shape.Get(x0, y0 + 1) ? 1f : 0f;
            float v11 = shape.Get(x0 + 1, y0 + 1) ? 1f : 0f;

            return Mathf.Lerp(Mathf.Lerp(v00, v10, fx), Mathf.Lerp(v01, v11, fx), fy);
        }

        // ── 파일 ─────────────────────────────────────────────────────

        public static string GetPath()
        {
            // 프로필과 같은 자리에 둡니다. 모드 폴더는 재설치 때 통째로 지워지므로
            // 직접 그린 도형도 거기 있으면 업데이트마다 날아갑니다.
            return Path.Combine(
                Path.Combine(Application.persistentDataPath, "DuckTracks"), FileName);
        }

        private static void Load()
        {
            try
            {
                string path = GetPath();
                if (!File.Exists(path))
                    return;

                var data = JsonUtility.FromJson<ShapeSetData>(File.ReadAllText(path, Encoding.UTF8));
                if (data == null || data.shapes == null)
                    return;

                foreach (var shape in data.shapes)
                {
                    if (shape != null && shape.IsValid)
                        _shapes!.Add(shape);
                }

                UnityEngine.Debug.Log($"[DuckTracks] 직접 그린 도형 {_shapes!.Count}개를 불러왔습니다.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 도형 불러오기 실패: {ex.Message}");
            }
        }

        private static void Write()
        {
            try
            {
                var data = new ShapeSetData { shapes = All.ToArray() };
                File.WriteAllText(GetPath(), JsonUtility.ToJson(data, true), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DuckTracks] 도형 저장 실패: {ex.Message}");
            }
        }
    }
}
