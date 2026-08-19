using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>
    /// 발처럼 생긴 도형을 무작위로 만듭니다.
    ///
    /// <b>왜 따로 두는가</b> — <see cref="CustomShapes.Randomize"/>는 극좌표 반지름에
    /// 사인 물결을 얹는 방식이라 좌우대칭 얼룩이 나옵니다. 아무 도형이나 만들 때는
    /// 쓸모가 있지만, 발자국을 만들려고 누르면 아메바만 나옵니다.
    ///
    /// <b>문법</b> — 같이 실려 나가는 21개 발자국(오리·곰·고양이·개·새·말굽·맨발…)을
    /// 뜯어보면 전부 다섯 부품의 조합입니다. 실제 생흔학에서 발자국 화석을 가르는
    /// 축(발가락 수 · 벌어짐 각도 · 패드 형태 · 발톱 유무 · 대칭성)과 거의 같습니다.
    ///
    /// <list type="number">
    /// <item><b>패드</b> — 뒤쪽 살. 크기와 앞뒤 위치. 고양이는 크고 새는 없습니다.</item>
    /// <item><b>발가락</b> — 부챗살로 배치. 개수·각도·길이 분포·굵기.</item>
    /// <item><b>발톱</b> — 발가락 끝에서 더 뻗는 가는 것. 개는 있고 고양이는 없습니다.</item>
    /// <item><b>물갈퀴</b> — 발가락 사이를 오목하게 채움. 오리·개구리.</item>
    /// <item><b>갈라짐</b> — 가운데를 빼냄. 말굽·소굽.</item>
    /// </list>
    ///
    /// 이 매개변수 공간에서 뽑으면 <b>나오는 것이 전부 발</b>입니다.
    /// </summary>
    internal static class FootShapeGenerator
    {
        /// <summary>
        /// 쓸 만한 덮임 비율.
        ///
        /// 너무 성기면 앙상한 선 몇 개라 자국으로 안 읽히고, 너무 빽빽하면 그냥
        /// 덩어리입니다. 벗어나면 매개변수를 다시 뽑습니다.
        /// </summary>
        private const float MinCoverage = 0.12f;

        private const float MaxCoverage = 0.55f;

        /// <summary>이 횟수 안에 못 맞추면 마지막 결과를 그냥 씁니다.</summary>
        private const int MaxAttempts = 16;

        /// <summary>
        /// <paramref name="cells"/>를 발 모양으로 채웁니다.
        /// </summary>
        /// <param name="seed">같은 씨앗은 같은 결과를 냅니다.</param>
        /// <param name="size">격자 한 변의 칸 수.</param>
        internal static void Fill(int seed, int size, bool[] cells)
        {
            if (cells == null || size <= 0 || cells.Length < size * size)
                return;

            var rng = new System.Random(seed);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                Draw(rng, size, cells);

                int filled = 0;
                for (int i = 0; i < size * size; i++)
                {
                    if (cells[i])
                        filled++;
                }

                float coverage = filled / (float)(size * size);
                if (coverage >= MinCoverage && coverage <= MaxCoverage)
                    return;
            }
        }

        private static void Draw(System.Random rng, int size, bool[] cells)
        {
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            bool Chance(float p) => rng.NextDouble() < p;

            // ── 매개변수 뽑기 ────────────────────────────────────────

            // 3~4개가 가장 발처럼 읽힙니다. 2개는 굽, 5개는 곰·사람 쪽입니다.
            int[] toeChoices = { 2, 3, 3, 4, 4, 5 };
            int toes = toeChoices[rng.Next(toeChoices.Length)];

            float spread = Range(24f, 68f) * Mathf.Deg2Rad;
            float pivotY = Range(0.16f, 0.34f);

            // 길이 분포가 종을 가릅니다. 가운데가 길면 사람·개, 바깥이 길면 일부 조류.
            int profile = rng.Next(4);   // 0,1 = 가운데 김 / 2 = 바깥 김 / 3 = 고름

            float toeLength = Range(0.30f, 0.56f);
            float toeBase = Range(0.045f, 0.095f);
            float toeTip = toeBase * Range(0.45f, 0.95f);

            float padRx = Range(0.10f, 0.28f);
            float padRy = Range(0.09f, 0.24f);
            float padDy = Range(0.02f, 0.14f);

            float claw = Chance(0.45f) ? Range(0.06f, 0.16f) : 0f;
            float web = Chance(0.30f) ? Range(0.70f, 0.95f) : 0f;
            float webSag = Range(0.12f, 0.32f);
            float cleft = Chance(0.15f) ? Range(0.05f, 0.10f) : 0f;

            // 사람·곰처럼 뒤꿈치가 따로 찍히는 종(척행성).
            bool heel = Chance(0.30f);

            const float pivotX = 0.5f;

            // 발가락 끝 자리를 미리 구해 둡니다. 발톱이 여기서 이어집니다.
            var tipX = new float[toes];
            var tipY = new float[toes];
            var tipAngle = new float[toes];

            for (int i = 0; i < toes; i++)
            {
                float t = toes == 1 ? 0f : 2f * i / (toes - 1) - 1f;
                float angle = t * spread;

                float scale = profile switch
                {
                    2 => 0.78f + 0.22f * Mathf.Abs(t),   // 바깥이 김
                    3 => 1f,                              // 고름
                    _ => 1f - 0.28f * Mathf.Abs(t),      // 가운데가 김
                };

                float length = toeLength * scale;

                tipAngle[i] = angle;
                tipX[i] = pivotX + Mathf.Sin(angle) * length;
                tipY[i] = pivotY + Mathf.Cos(angle) * length;
            }

            // ── 칸마다 안팎 판정 ─────────────────────────────────────

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;

                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;

                    bool inside = false;

                    // 1) 패드
                    if (InEllipse(u, v, pivotX, pivotY + padDy, padRx, padRy))
                        inside = true;

                    if (!inside && heel &&
                        InEllipse(u, v, pivotX, pivotY - padRy * 0.85f, padRx * 0.72f, padRy * 0.75f))
                        inside = true;

                    // 2) 발가락과 3) 발톱
                    for (int i = 0; i < toes && !inside; i++)
                    {
                        if (InCapsule(u, v, pivotX, pivotY, tipX[i], tipY[i], toeBase, toeTip))
                        {
                            inside = true;
                            break;
                        }

                        if (claw <= 0f)
                            continue;

                        float clawX = tipX[i] + Mathf.Sin(tipAngle[i]) * claw;
                        float clawY = tipY[i] + Mathf.Cos(tipAngle[i]) * claw;

                        if (InCapsule(u, v, tipX[i], tipY[i], clawX, clawY, toeTip * 0.8f, 0.012f))
                            inside = true;
                    }

                    // 4) 물갈퀴 — 이웃한 발가락 사이를 채우되 가운데가 오목합니다.
                    if (!inside && web > 0f && toes >= 3)
                    {
                        float dx = u - pivotX;
                        float dy = v - pivotY;

                        if (dy > -0.02f)
                        {
                            float angle = Mathf.Atan2(dx, dy);

                            if (Mathf.Abs(angle) <= spread)
                            {
                                float s = Mathf.Clamp01((angle + spread) / (2f * spread));
                                float dip = Mathf.Sin(Mathf.PI * s * (toes - 1));
                                float bound = toeLength * web * (1f - webSag * dip * dip);

                                if (Mathf.Sqrt(dx * dx + dy * dy) <= bound)
                                    inside = true;
                            }
                        }
                    }

                    // 5) 갈라짐 — 마지막에 <b>빼냅니다</b>. 굽은 가운데가 솟아 있어서
                    //    바닥에 안 닿고, 그 자리가 자국에서 빠집니다.
                    if (inside && cleft > 0f &&
                        InCapsule(u, v, pivotX, 0f, pivotX, pivotY + toeLength * 0.8f, cleft, cleft * 1.2f))
                        inside = false;

                    cells[y * size + x] = inside;
                }
            }
        }

        private static bool InEllipse(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx;
            float dy = (v - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        /// <summary>끝으로 갈수록 굵기가 변하는 막대 안인지.</summary>
        private static bool InCapsule(
            float u, float v, float x0, float y0, float x1, float y1, float r0, float r1)
        {
            float vx = x1 - x0;
            float vy = y1 - y0;
            float lengthSq = Mathf.Max(vx * vx + vy * vy, 1e-9f);

            float t = Mathf.Clamp01(((u - x0) * vx + (v - y0) * vy) / lengthSq);

            float dx = u - (x0 + t * vx);
            float dy = v - (y0 + t * vy);
            float r = r0 + (r1 - r0) * t;

            return dx * dx + dy * dy <= r * r;
        }
    }
}
