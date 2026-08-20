using System;
using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>발자국을 남기는 주체의 종류. 종류마다 프로필이 따로입니다.</summary>
    public enum TrackKind
    {
        /// <summary>두 발로 걷는 것 — 좌·우 번갈아 찍습니다.</summary>
        Foot = 0,

        /// <summary>네 발 짐승 — 앞발·뒷발이 있어 한 걸음에 자국이 둘씩 납니다.</summary>
        Hoof = 1,

        /// <summary>바퀴 — 번갈아가 아니라 좌우 두 줄이 <b>동시에</b> 이어집니다.</summary>
        Wheel = 2,
    }

    /// <summary>자국 그림을 어디서 가져올지.</summary>
    public enum TrackShapeSource
    {
        /// <summary>
        /// 캐릭터가 지금 신고 있는 <b>실제 발</b>의 실루엣을 구워서 씁니다.
        ///
        /// 발이 소켓이라 신발을 갈아 신으면 자국도 따라 바뀝니다. 발 리그를 못 찾는
        /// 대상(탈것 등)에서는 자동으로 내장 도형으로 떨어집니다.
        /// </summary>
        ActualFoot = 0,

        /// <summary>코드로 그린 오리 물갈퀴 자국.</summary>
        Builtin = 1,

        /// <summary>track_textures 폴더의 PNG.</summary>
        Texture = 2,
    }

    /// <summary>합성 방식. 발광하는 자국과 물감처럼 얹히는 자국은 셰이더가 다릅니다.</summary>
    public enum TrackBlend
    {
        /// <summary>가산 — 어두운 바닥에서 빛납니다. 검은 부분은 저절로 투명해집니다.</summary>
        Additive = 0,

        /// <summary>알파 — 진흙·피처럼 바닥을 덮습니다. 밝은 바닥에서도 읽힙니다.</summary>
        AlphaBlend = 1,
    }

    /// <summary>
    /// 한 종류의 발자국이 어떻게 생겼는지.
    ///
    /// 값만 들고 있습니다 — 파티클을 만들거나 색을 계산하는 일은 하지 않습니다.
    /// 그래야 설정 창이 이 객체를 그대로 편집하고, 저장은 JSON 한 번으로 끝납니다.
    /// </summary>
    [Serializable]
    public sealed class TrackProfile
    {
        /// <summary>이 종류를 남길지.</summary>
        public bool enabled = true;

        /// <summary>어떻게 걷는 것으로 볼지. 자국을 놓는 규칙이 여기서 갈립니다.</summary>
        public TrackKind kind = TrackKind.Foot;

        /// <summary>그림을 어디서 가져올지.</summary>
        public TrackShapeSource shapeSource = TrackShapeSource.ActualFoot;

        /// <summary>track_textures 안의 파일 이름(확장자 제외). 비어 있으면 내장 도형.</summary>
        public string textureName = "";

        /// <summary>
        /// 실제 발 모양을 쓸 때 구운 크기에 곱하는 배수.
        ///
        /// 1이면 발과 같은 크기입니다. <see cref="size"/>는 이 방식에서 쓰지 않습니다 —
        /// 실제 발은 자기 크기가 이미 정해져 있고, 그걸 무시하면 "실제 발 모양"이라는
        /// 말이 무색해집니다.
        ///
        /// 기본이 1이 아니라 1.5인 이유: 발과 <b>정확히</b> 같은 크기면 탑다운 시점에서
        /// 캐릭터 발에 가려 잘 안 보입니다. 조금 크게 찍혀야 지나간 자리가 읽힙니다.
        /// </summary>
        public float autoSizeScale = 1.5f;

        /// <summary>찍힌 직후의 색. 기본은 흙 묻은 자국 같은 진한 회색입니다.</summary>
        public Color color = new Color(0.17f, 0.17f, 0.18f, 0.78f);

        /// <summary>사라지기 직전의 색. 보통 같은 색의 투명한 판입니다.</summary>
        public Color fadeColor = new Color(0.17f, 0.17f, 0.18f, 0f);

        /// <summary>
        /// 기본이 알파입니다.
        ///
        /// 진한 회색은 가산 합성과 상극입니다 — 가산은 바닥에 빛을 <b>더하는</b> 것이라
        /// 어두운 색을 넣으면 아무것도 안 더해져서 자국이 보이지 않습니다. 색을
        /// 밝게 바꾸는 사람은 가산으로 옮기면 발광하는 자국이 됩니다.
        /// </summary>
        public TrackBlend blend = TrackBlend.AlphaBlend;

        /// <summary>자국 하나의 크기 (미터).</summary>
        public float size = 0.34f;

        /// <summary>
        /// 한 걸음의 길이 (미터). 이만큼 움직일 때마다 자국을 하나 놓습니다.
        ///
        /// 시간이 아니라 <b>거리</b>가 기준입니다. 시간 기준이면 제자리걸음이나
        /// 느린 이동에서 자국이 한 자리에 겹쳐 쌓입니다.
        /// </summary>
        public float stride = 0.55f;

        /// <summary>중심선에서 좌·우로 벌리는 폭 (미터). 0이면 한 줄로 찍힙니다.</summary>
        public float spread = 0.12f;

        /// <summary>자국이 남아 있는 시간 (초).</summary>
        public float life = 6f;

        /// <summary>
        /// 사라지지 않고 계속 남을지.
        ///
        /// 켜면 <see cref="life"/>를 무시하고 자국 수 상한도 크게 풀립니다.
        /// 파티클 시스템에는 상한이 반드시 있어야 하므로 진짜 무한은 아니지만,
        /// 쉬지 않고 걸어도 두어 시간은 닿지 않는 값입니다.
        /// </summary>
        public bool infiniteLife;

        /// <summary>
        /// 진행 방향 대비 각도 흔들림 (도).
        ///
        /// 0이면 모든 자국이 완벽히 같은 각도라 도장으로 찍은 티가 납니다.
        /// 좌·우 자국을 바깥으로 살짝 벌리는 팔자걸음도 이 값으로 냅니다.
        /// </summary>
        public float angleJitter = 6f;

        /// <summary>달릴 때 걸음을 넓히는 배수. 1이면 걷기와 같습니다.</summary>
        public float runStrideScale = 1.45f;

        /// <summary>
        /// 앞뒤 자국 사이 거리 (미터). <see cref="TrackKind.Hoof"/>에서만 씁니다 —
        /// 네 발 짐승은 한 걸음에 앞발과 뒷발 자국이 같이 남기 때문입니다.
        /// 0이면 앞뒤 구분 없이 한 쌍만 찍습니다.
        /// </summary>
        public float pairGap = 0.6f;

        // ── 야광 ────────────────────────────────────────────────

        /// <summary>
        /// 색 밝기 배수. <see cref="TrackBlend.Additive"/>에서만 씁니다.
        ///
        /// 1을 넘으면 색이 1.0 위로 올라갑니다. 게임 화면 후처리에 블룸이 있으면
        /// 그 지점이 실제로 번져서 빛나 보입니다. 블룸이 없어도 손해는 없습니다 —
        /// 그냥 더 밝게 더해질 뿐입니다.
        /// </summary>
        public float glowIntensity = 2.2f;

        // ── 깜박임 ──────────────────────────────────────────────

        /// <summary>자국 밝기가 맥동할지.</summary>
        public bool pulse;

        /// <summary>1초에 몇 번 깜박일지.</summary>
        public float pulseSpeed = 1.1f;

        /// <summary>
        /// 얼마나 어두워졌다 돌아올지. 0이면 변화 없음, 1이면 완전히 꺼졌다 켜집니다.
        /// </summary>
        public float pulseDepth = 0.6f;

        /// <summary>색까지 무지개처럼 돌릴지.</summary>
        public bool cycleHue;

        /// <summary>색상환을 1초에 얼마나 돌릴지 (1 = 한 바퀴).</summary>
        public float hueSpeed = 0.12f;

        // ── 걸음 알갱이 ─────────────────────────────────────────

        /// <summary>발을 디딜 때 알갱이가 튀어오를지.</summary>
        public bool burst;

        /// <summary>한 걸음에 뿜는 개수.</summary>
        public int burstCount = 11;

        /// <summary>알갱이 하나의 크기 (미터).</summary>
        public float burstSize = 0.18f;

        /// <summary>튀어오르는 세기.</summary>
        public float burstSpeed = 2.1f;

        /// <summary>
        /// 중력 배수.
        ///
        /// 0이면 떠오르다 사라져서 연기처럼 보입니다. 1 근처면 튀었다 떨어져서
        /// "뿅" 하고 튄 것으로 읽힙니다.
        /// </summary>
        public float burstGravity = 1.21f;

        /// <summary>알갱이가 남아 있는 시간 (초).</summary>
        public float burstLife = 1.6f;

        /// <summary>
        /// 알갱이 모양. 비어 있으면 내장 점(가운데가 밝은 동그라미)을 씁니다.
        ///
        /// 자국과 같은 목록에서 고릅니다 — 하트를 뿌리거나 작은 발자국을 흩뿌릴 수
        /// 있게 하려는 것입니다.
        /// </summary>
        public string burstTextureName = "";

        /// <summary>
        /// 바닥에 남아 있는 자국에서 알갱이가 계속 떠오를지.
        ///
        /// 튀기기(<see cref="burst"/>)와 색·모양·수명을 같이 씁니다. 움직임만 다릅니다 —
        /// 튀는 것은 떨어지고 이쪽은 천천히 떠오릅니다.
        /// </summary>
        public bool drift;

        /// <summary>자국 하나가 1초에 흘리는 개수.</summary>
        public float driftRate = 1.5f;

        /// <summary>튀는 알갱이 대비 크기 배수. 1보다 크면 흘리는 쪽이 더 큽니다.</summary>
        public float driftScale = 1.2f;

        /// <summary>떠오르는 속도.</summary>
        public float driftRise = 0.5f;

        /// <summary>알갱이 색. 자국 색과 따로 둡니다.</summary>
        public Color burstColor = new Color(1f, 0.94f, 0.72f, 1f);

        public TrackProfile Clone()
        {
            return (TrackProfile)MemberwiseClone();
        }

        /// <summary>도보 기본값 — 오리 물갈퀴 자국.</summary>
        internal static TrackProfile DefaultFoot()
        {
            return new TrackProfile();
        }

        /// <summary>
        /// 탈것 기본값 — 바퀴 자국.
        ///
        /// 도보와 달리 자국이 촘촘하고(짧은 stride) 폭이 넓습니다. 그래야 점점이
        /// 찍힌 게 아니라 이어진 두 줄로 읽힙니다.
        /// </summary>
        internal static TrackProfile DefaultVehicle()
        {
            return new TrackProfile
            {
                // 설정 창에서 뺐으므로 기본은 꺼짐입니다. 끌 방법이 없는 기능이
                // 멋대로 자국을 남기면 곤란합니다. 창에 탭을 다시 넣을 때 켭니다.
                enabled = false,
                kind = TrackKind.Wheel,
                shapeSource = TrackShapeSource.Texture,
                textureName = "tire_tread",
                color = new Color(0.35f, 0.3f, 0.27f, 0.75f),
                fadeColor = new Color(0.35f, 0.3f, 0.27f, 0f),
                blend = TrackBlend.AlphaBlend,
                size = 0.3f,
                stride = 0.18f,
                spread = 0.55f,
                life = 9f,
                angleJitter = 1.5f,
                runStrideScale = 1f,
            };
        }

        /// <summary>탈것이되 바퀴가 아닌 것(말 등) 기본값 — 발굽 자국.</summary>
        internal static TrackProfile DefaultMount()
        {
            return new TrackProfile
            {
                enabled = false,
                kind = TrackKind.Hoof,
                shapeSource = TrackShapeSource.Texture,
                textureName = "hoof",
                color = new Color(0.42f, 0.33f, 0.24f, 0.8f),
                fadeColor = new Color(0.42f, 0.33f, 0.24f, 0f),
                blend = TrackBlend.AlphaBlend,
                size = 0.26f,
                stride = 0.75f,
                spread = 0.18f,
                life = 8f,
                angleJitter = 5f,
                runStrideScale = 1.6f,
            };
        }
    }
}
