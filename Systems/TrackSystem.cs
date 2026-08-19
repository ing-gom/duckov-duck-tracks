using System;
using System.Collections.Generic;
using DuckTracks.Settings;
using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>
    /// 누가 자국을 남기는지 정하고 매 프레임 걸음을 먹입니다.
    ///
    /// 자국을 놓는 방식이 <b>둘</b>입니다.
    ///
    /// <list type="number">
    /// <item>
    /// 발 리그를 찾은 경우 — 발 뼈의 높이를 보고 <b>실제로 디디는 순간</b>에 그 발이
    /// 있는 자리에 찍습니다. 걸음걸이와 정확히 맞습니다.
    /// </item>
    /// <item>
    /// 못 찾은 경우(탈것 등) — 걸은 거리를 세어 일정 간격으로 찍습니다
    /// (<see cref="TrackWalker"/>). 바퀴에는 애초에 디딤이라는 게 없으므로
    /// 이쪽이 오히려 맞습니다.
    /// </item>
    /// </list>
    ///
    /// 탈것은 별도 시스템이 아닙니다 — 게임에서 탈것도 그냥
    /// <see cref="CharacterMainControl"/>이고, 타면 플레이어의 현재 행동이
    /// <c>CA_ControlOtherCharacter</c>가 되면서 그 안의 <c>targetCharacter</c>가
    /// 탈것을 가리킵니다.
    /// </summary>
    internal static class TrackSystem
    {
        private static readonly Dictionary<int, TrackWalker> Walkers = new();

        /// <summary>플레이어의 발 리그. 모델이 바뀌면 다시 묶습니다.</summary>
        private static readonly FootRig Rig = new();

        /// <summary>지금 리그가 물려 있는 모델. 이게 바뀌면 다시 묶어야 합니다.</summary>
        private static Transform? _boundModel;

        /// <summary>지난 프레임에 자국을 남기던 대상. 바뀌면 이어짐을 끊습니다.</summary>
        private static int _lastSubjectId;

        /// <summary>탈것 판별에 <c>isVehicle</c>을 못 읽었을 때 한 번만 경고하려고.</summary>
        private static bool _warnedVehicleField;

        /// <summary>
        /// 지금 발자국을 찍을 프로필과, 그것을 쓰는 콜백.
        ///
        /// 람다로 프로필을 잡아 넘기면 <b>매 프레임</b> 클로저가 하나씩 생깁니다.
        /// Update에서 도는 코드라 그대로 두면 쓰레기가 계속 쌓입니다.
        /// </summary>
        private static TrackProfile? _activeProfile;

        private static readonly Action<FootRig.Side, Vector3, float> StepHandler = OnFootStep;

        internal static void Tick()
        {
            if (!TrackSettings.Enabled)
                return;

            var level = LevelManager.Instance;
            if (level == null || !LevelManager.LevelInited)
                return;

            var player = level.MainCharacter;
            if (player == null)
                return;

            // 죽은 뒤에는 남기지 않습니다. 시체가 밀려나면서 자국이 이어집니다.
            if (player.Health != null && player.Health.IsDead)
                return;

            var subject = ResolveSubject(player, out TrackProfile profile, out bool riding);
            if (subject == null || !profile.enabled)
                return;

            int id = subject.GetInstanceID();

            // 타고 내릴 때는 이어짐을 끊습니다. 안 끊으면 탈것에서 내린 자리와
            // 플레이어가 서 있는 자리 사이가 걸어간 것으로 잡힙니다.
            if (id != _lastSubjectId)
            {
                if (Walkers.TryGetValue(id, out var switched))
                    switched.Reset();

                _lastSubjectId = id;
            }

            // 공중에서는 찍지 않습니다. 대시나 낙하 중에 바닥에 자국이 남으면
            // 발이 닿지도 않은 자리에 찍힙니다.
            if (!subject.IsOnGround)
                return;

            // 타고 있지 않을 때만 발 리그를 씁니다. 탈것에는 발 뼈가 없고,
            // 플레이어의 발은 타는 동안 발판에 얹혀 있어서 디딤이 나오지 않습니다.
            if (!riding && TickFootRig(subject, profile))
                return;

            TickWalker(id, subject, profile);
        }

        /// <summary>
        /// 발 리그로 자국을 찍습니다. 리그를 못 찾았으면 <c>false</c> —
        /// 부르는 쪽이 거리 누적으로 넘어갑니다.
        /// </summary>
        private static bool TickFootRig(CharacterMainControl subject, TrackProfile profile)
        {
            var model = subject.characterModel;
            if (model == null)
                return false;

            var modelRoot = model.transform;

            // 모델이 통째로 갈릴 수 있습니다(씬 전환·외형 변경). 그때 다시 묶습니다.
            if (_boundModel != modelRoot)
            {
                Rig.Bind(modelRoot);
                _boundModel = modelRoot;
            }

            if (!Rig.Found)
                return false;

            _activeProfile = profile;
            Rig.Tick(StepHandler);
            return true;
        }

        /// <summary>발이 새로 바닥에 닿았을 때 자국 하나.</summary>
        private static void OnFootStep(FootRig.Side side, Vector3 position, float yaw)
        {
            var profile = _activeProfile;
            if (profile == null)
                return;

            // 발 위치에서 바로 아래 바닥을 찾습니다. 발 높이를 그대로 쓰면
            // 경사에서 살짝 뜨거나 묻힙니다.
            if (!GroundProbe.TryFind(position, out Vector3 ground, out _))
                return;

            var renderer = side == FootRig.Side.Left ? Rig.LeftMesh : Rig.RightMesh;
            var bone = side == FootRig.Side.Left ? Rig.LeftBone : Rig.RightBone;
            var tip = side == FootRig.Side.Left ? Rig.LeftTip : Rig.RightTip;

            bool useActual = profile.shapeSource == TrackShapeSource.ActualFoot && bone != null;

            Material material;
            float size;
            string key;

            var baked = useActual ? FootSilhouette.Get(renderer, bone!, tip) : null;

            if (baked != null)
            {
                material = TrackTextures.ResolveBakedMaterial(baked.Texture, profile.blend);
                size = baked.WorldSize;

                // 좌우가 서로 다른 그림이므로 통을 나눕니다.
                key = "foot_" + side;
            }
            else
            {
                // 실제 발을 아직 못 구웠거나(굽는 데 한 프레임 걸립니다) 다른 모양을
                // 고른 경우입니다.
                string? name = profile.shapeSource == TrackShapeSource.Texture ? profile.textureName : null;
                material = TrackTextures.ResolveMaterial(name, profile.blend);
                key = "foot";

                // 모양이 내장 도형이더라도 <b>크기는</b> 실제 발에 맞춥니다.
                // localBounds는 Read/Write와 무관하게 읽히므로 굽기 성공 여부와
                // 상관없이 항상 잽니다.
                size = useActual ? FootSilhouette.MeasureWorldSize(renderer, bone!, tip) : 0f;

                if (size <= 0f)
                    size = profile.size;
            }

            TrackStampSystem.Stamp(key, profile, material, ground, yaw, size * Mathf.Max(0.05f, profile.autoSizeScale));

            // 자국과 같은 자리에서 알갱이가 튑니다.
            StepBurstSystem.Burst(profile, ground);
        }

        /// <summary>거리 누적 방식. 탈것과, 발 리그를 못 찾은 대상에 씁니다.</summary>
        private static void TickWalker(int id, CharacterMainControl subject, TrackProfile profile)
        {
            if (!Walkers.TryGetValue(id, out var walker))
            {
                walker = new TrackWalker();
                Walkers[id] = walker;
            }

            walker.LastSeenTime = Time.time;

            var transform = subject.transform;
            walker.Tick(profile, transform.position, transform.forward, subject.Running);

            PruneStale();
        }

        /// <summary>
        /// 지금 자국을 남겨야 하는 대상과 그 프로필.
        ///
        /// 탈것에 타고 있으면 플레이어가 아니라 탈것을 따라갑니다. 플레이어는 타는 동안
        /// 탈것에 붙어 같이 움직이므로, 플레이어를 따라가면 탈것 한가운데에 사람 발자국이
        /// 찍힙니다.
        /// </summary>
        private static CharacterMainControl? ResolveSubject(
            CharacterMainControl player, out TrackProfile profile, out bool riding)
        {
            var ride = player.CurrentAction as CA_ControlOtherCharacter;
            var mount = ride != null ? ride.targetCharacter : null;

            if (mount != null && mount.Health != null && !mount.Health.IsDead)
            {
                profile = IsVehicle(mount) ? TrackSettings.Vehicle : TrackSettings.Mount;
                riding = true;
                return mount;
            }

            profile = TrackSettings.Foot;
            riding = false;
            return player;
        }

        /// <summary>
        /// 바퀴 달린 것인지.
        ///
        /// <c>isVehicle</c>은 게임이 하차 지점을 계산할 때 쓰는 것과 같은 값입니다.
        /// SDK 버전에 따라 이 필드가 없을 수 있어서, 없으면 바퀴가 아닌 것으로 보고
        /// 계속 갑니다 — 발굽 자국이 나올 뿐 모드가 죽지는 않습니다.
        /// </summary>
        private static bool IsVehicle(CharacterMainControl mount)
        {
            try
            {
                return mount.isVehicle;
            }
            catch (Exception ex)
            {
                if (!_warnedVehicleField)
                {
                    _warnedVehicleField = true;
                    UnityEngine.Debug.LogWarning(
                        $"[DuckTracks] 탈것 종류를 못 읽어 발굽 자국으로 갑니다: {ex.Message}");
                }

                return false;
            }
        }

        /// <summary>
        /// 오래 안 보인 대상의 걸음 기록을 걷어냅니다.
        ///
        /// 파괴된 캐릭터의 InstanceID로 계속 쌓이면 판을 거듭할수록 늘어납니다.
        /// </summary>
        private static void PruneStale()
        {
            const float staleAfter = 30f;

            if (Walkers.Count < 8)
                return;

            List<int>? drop = null;

            foreach (var pair in Walkers)
            {
                if (Time.time - pair.Value.LastSeenTime < staleAfter)
                    continue;

                drop ??= new List<int>();
                drop.Add(pair.Key);
            }

            if (drop == null)
                return;

            foreach (int id in drop)
                Walkers.Remove(id);
        }

        /// <summary>씬이 바뀔 때. 지난 판의 걸음 기록을 들고 있으면 안 됩니다.</summary>
        internal static void ResetAll()
        {
            Walkers.Clear();
            _lastSubjectId = 0;
            _boundModel = null;
        }

        /// <summary>
        /// 구운 발을 되읽습니다. 반드시 LateUpdate에서 불러야 합니다 — 카메라가
        /// 그린 뒤여야 읽을 게 있습니다.
        /// </summary>
        internal static void LateTick()
        {
            FootSilhouette.LateTick();
        }

        internal static void Dispose()
        {
            ResetAll();
            TrackStampSystem.Dispose();
            StepBurstSystem.Dispose();
            FootSilhouette.Dispose();
            TrackTextures.Dispose();
        }
    }
}
