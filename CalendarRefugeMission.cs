using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Opens the refuge as an isolated mission.  It deliberately does not
    /// create a Settlement, Location, encounter, or map party, so native
    /// campaign ownership and save graphs remain untouched.
    /// </summary>
    internal static class CalendarRefugeMission
    {
        internal static bool TryOpen(
            string sceneId,
            RefugeSceneClimate climate,
            bool isWinter,
            RefugeUpgrade upgrades,
            out string failure)
        {
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(sceneId))
            {
                failure = "The refuge has no valid scene assigned.";
                return false;
            }

            if (Mission.Current != null)
            {
                failure = "Another mission is already active.";
                return false;
            }

            try
            {
                MissionInitializerRecord initializer = new MissionInitializerRecord(sceneId)
                {
                    PlayingInCampaignMode = true
                };
                ApplyCampaignEnvironment(ref initializer);

                MissionState.OpenNew(
                    "RealisticCalendarRefuge",
                    initializer,
                    delegate(Mission mission)
                    {
                        return new MissionBehavior[]
                        {
                            new MissionOptionsComponent(),
                            new CalendarRefugeMissionController(climate, isWinter, upgrades),
                            // Supplies Bannerlord's normal leave-mission request
                            // path, including the Tab key used in peaceful scenes.
                            new BasicLeaveMissionLogic(false, 0),
                            new EquipmentControllerLeaveLogic()
                        };
                    },
                    true,
                    true);

                Diagnostics.Info("Opened isolated refuge mission. Scene=" + sceneId + ".");
                return true;
            }
            catch (Exception exception)
            {
                failure = "Bannerlord could not open the refuge scene.";
                Diagnostics.Error("Opening the isolated refuge mission failed safely. Scene=" + sceneId + ".", exception);
                return false;
            }
        }

        private static void ApplyCampaignEnvironment(ref MissionInitializerRecord initializer)
        {
            try
            {
                Campaign campaign = Campaign.Current;
                MobileParty mainParty = MobileParty.MainParty;
                if (campaign == null || mainParty == null)
                {
                    return;
                }

                if (campaign.Models != null && campaign.Models.MapWeatherModel != null)
                {
                    initializer.AtmosphereOnCampaign = campaign.Models.MapWeatherModel
                        .GetAtmosphereModel(mainParty.Position);
                }

                if (campaign.MapSceneWrapper != null)
                {
                    initializer.TerrainType = (int)campaign.MapSceneWrapper
                        .GetFaceTerrainType(mainParty.CurrentNavigationFace);
                }
            }
            catch (Exception exception)
            {
                // Scene loading remains safe even if another mod replaces the
                // campaign weather or map model with an incompatible version.
                Diagnostics.Error("Refuge campaign environment could not be transferred to the scene.", exception);
            }
        }
    }

    internal sealed class CalendarRefugeMissionController : MissionLogic
    {
        private const string TentPrefabId = "tent_vlandia_a";
        // These are stationary architectural props with their own collision.
        // Do not substitute a siege-machine prefab here: mobile siege entities
        // bring wheels, deployment scripts, and mission-only assumptions.
        private const string PalisadePrefabId = "castle_plank_wall_a";
        private const string WatchTowerPrefabId = "battania_arena_tower";
        private const string BarracksPrefabId = "tents_pict_a";
        private const string StaffTentPrefabId = "tents_pict_b";
        private const string QuartersPrefabId = "sturgia_village_tent_a";
        private const string StoragePrefabId = "wood_storage_a";
        private const float TentHalfWidth = 6f;
        private const float TentHalfDepth = 7f;
        private const float TentGroundClearance = 0.12f;
        private const float PropGroundClearance = 0.08f;

        private readonly RefugeSceneClimate _climate;
        private readonly bool _isWinter;
        private readonly RefugeUpgrade _upgrades;
        private bool _leaveRequested;

        internal CalendarRefugeMissionController(
            RefugeSceneClimate climate,
            bool isWinter,
            RefugeUpgrade upgrades)
        {
            _climate = climate;
            _isWinter = isWinter;
            _upgrades = upgrades;
        }

        private static readonly string[] PlayerSpawnTags =
        {
            "spawnpoint_player",
            "player_spawn_frame",
            "sp_player"
        };

        public override void EarlyStart()
        {
            base.EarlyStart();

            // Native raid/hideout scene mission objects are not needed by
            // this peaceful isolated visit and could otherwise run logic for
            // an encounter that does not exist.
            foreach (MissionObject missionObject in Mission.ActiveMissionObjects.ToList())
            {
                missionObject.SetDisabled(true);
            }
        }

        public override void AfterStart()
        {
            base.AfterStart();

            try
            {
                ConfigureSceneClimate();
                MatrixFrame spawnFrame = FindPlayerSpawnFrame();
                SpawnPlayerOnFoot(spawnFrame);
                PlaceRefugeLayout(spawnFrame);
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Refuge scene initialization failed; ending the isolated mission safely.", exception);
                InformationManager.DisplayMessage(
                    new InformationMessage("The refuge scene could not be initialized. Returning to the campaign map."));
                Mission.EndMission();
            }
        }

        private void ConfigureSceneClimate()
        {
            bool useSnow = _climate == RefugeSceneClimate.Snow
                || (_climate == RefugeSceneClimate.Temperate && _isWinter);

            // Desert profiles never snow. Temperate profiles snow only in
            // calendar winter, while the northern profile stays snowy all year.
            Mission.Scene.SetWinterTimeFactor(useSnow ? 1f : 0f);
            Mission.Scene.SetForcedSnow(useSnow);
            if (!useSnow)
            {
                Mission.Scene.SetSnowDensity(0f);
            }

            Diagnostics.Info(
                "Configured refuge scene climate. Climate=" + _climate
                + "; CalendarWinter=" + _isWinter
                + "; Snow=" + useSnow + ".");
        }

        public override bool MissionEnded(ref MissionResult missionResult)
        {
            return _leaveRequested;
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_leaveRequested || Mission.InputManager == null)
            {
                return;
            }

            // Isolated missions do not always have Bannerlord's regular
            // campaign leave state. End this peaceful scene directly on Tab
            // so a scene configuration can never trap the player inside it.
            if (Mission.InputManager.IsKeyPressed(InputKey.Tab)
                || Mission.InputManager.IsGameKeyPressed(4))
            {
                _leaveRequested = true;
                Mission.EndMission();
            }
        }

        private MatrixFrame FindPlayerSpawnFrame()
        {
            Scene scene = Mission.Scene;
            for (int index = 0; index < PlayerSpawnTags.Length; index++)
            {
                WeakGameEntity entity = scene.FindWeakEntityWithTag(PlayerSpawnTags[index]);
                if (entity.IsValid)
                {
                    return entity.GetGlobalFrame();
                }
            }

            MatrixFrame fallback = MatrixFrame.Identity;
            fallback.origin.z = scene.GetTerrainHeight(Vec2.Zero);
            Diagnostics.Info("Refuge scene had no known player spawn tag; using terrain origin fallback.");
            return fallback;
        }

        private void SpawnPlayerOnFoot(MatrixFrame spawnFrame)
        {
            CharacterObject playerCharacter = CharacterObject.PlayerCharacter;
            if (playerCharacter == null)
            {
                throw new InvalidOperationException("The campaign player character is unavailable.");
            }

            Vec3 position = spawnFrame.origin;
            Vec2 direction = spawnFrame.rotation.f.AsVec2;
            if (direction.LengthSquared < 0.001f)
            {
                direction = new Vec2(0f, 1f);
            }
            else
            {
                direction.Normalize();
            }

            AgentBuildData buildData = new AgentBuildData(new BasicBattleAgentOrigin(playerCharacter))
                .InitialPosition(position)
                .InitialDirection(direction)
                .NoHorses(true)
                .Controller(AgentControllerType.Player);

            Agent playerAgent = Mission.SpawnAgent(buildData, false);
            if (playerAgent == null)
            {
                throw new InvalidOperationException("Bannerlord did not create the refuge player agent.");
            }
        }

        private void PlaceRefugeLayout(MatrixFrame spawnFrame)
        {
            Vec2 forward = spawnFrame.rotation.f.AsVec2;
            if (forward.LengthSquared < 0.001f)
            {
                forward = new Vec2(0f, 1f);
            }
            else
            {
                forward.Normalize();
            }

            Vec2 side = new Vec2(forward.y, -forward.x);
            // The gate is near the player spawn and the compound extends
            // forward from it.  Keeping this footprint fixed lets every
            // climate and water variant use the same safe upgrade sockets.
            Vec3 idealCenter = spawnFrame.origin + new Vec3(forward.x * 18f, forward.y * 18f, 0f);
            Vec3 center = FindBestElevatedRefugeCenter(idealCenter, side, forward);

            // The main tent is the permanent clan-leader tent. It stays level
            // and is raised to the highest point beneath its footprint so the
            // terrain cannot cut through it.
            center.z = FindHighestTerrainUnderTent(center, side, forward) + TentGroundClearance;
            TryPlaceLevelPrefab(TentPrefabId, center, forward);

            PlaceStarterPalisade(center, side, forward);
            PlaceUpgradeSockets(center, side, forward);

            Diagnostics.Info("Placed refuge starter layout. Upgrades=" + _upgrades + ".");
        }

        private Vec3 FindBestElevatedRefugeCenter(Vec3 idealCenter, Vec2 side, Vec2 forward)
        {
            Vec3 best = idealCenter;
            float bestScore = float.MinValue;
            float bestRoughness = float.MaxValue;
            float bestAverageHeight = Mission.Scene.GetTerrainHeight(idealCenter.AsVec2);

            // Survey nearby terrain rather than editing its heightmap at
            // runtime. This preserves the authored navigation mesh while
            // preferring an elevated, naturally level refuge site.
            for (int forwardStep = 0; forwardStep <= 4; forwardStep++)
            {
                for (int sideStep = -3; sideStep <= 3; sideStep++)
                {
                    float sideOffset = sideStep * 8f;
                    float forwardOffset = forwardStep * 8f;
                    Vec3 candidate = Offset(idealCenter, side, forward, sideOffset, forwardOffset);
                    float minimum = float.MaxValue;
                    float maximum = float.MinValue;
                    float total = 0f;
                    int samples = 0;

                    for (int sampleForward = -2; sampleForward <= 2; sampleForward++)
                    {
                        for (int sampleSide = -2; sampleSide <= 2; sampleSide++)
                        {
                            Vec3 sample = Offset(
                                candidate,
                                side,
                                forward,
                                sampleSide * 7f,
                                sampleForward * 8f);
                            float height = Mission.Scene.GetTerrainHeight(sample.AsVec2);
                            minimum = Math.Min(minimum, height);
                            maximum = Math.Max(maximum, height);
                            total += height;
                            samples++;
                        }
                    }

                    float average = samples == 0 ? 0f : total / samples;
                    float roughness = maximum - minimum;
                    float distancePenalty = Math.Abs(sideOffset) * 0.03f + forwardOffset * 0.02f;
                    float score = average - roughness * 12f - distancePenalty;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                        bestRoughness = roughness;
                        bestAverageHeight = average;
                    }
                }
            }

            best.z = bestAverageHeight;
            Diagnostics.Info(
                "Selected elevated refuge site. AverageHeight=" + bestAverageHeight.ToString("F2")
                + "; HeightVariation=" + bestRoughness.ToString("F2") + ".");
            return best;
        }

        private void PlaceStarterPalisade(Vec3 center, Vec2 side, Vec2 forward)
        {
            const float halfWidth = 15f;
            const float halfDepth = 17f;
            const float segment = 6f;

            // Rear wall, three pieces per side wall, and two short front
            // sections form a complete protected compound with a deliberate
            // central gate opening. Every wall section is grounded separately
            // instead of being tilted into uneven terrain.
            for (float offset = -halfWidth + segment * 0.5f; offset < halfWidth; offset += segment)
            {
                PlaceGroundPrefab(
                    PalisadePrefabId,
                    Offset(center, side, forward, offset, halfDepth),
                    side);
            }

            for (float offset = -halfDepth + segment * 0.5f; offset < halfDepth; offset += segment)
            {
                PlaceGroundPrefab(
                    PalisadePrefabId,
                    Offset(center, side, forward, -halfWidth, offset),
                    forward);
                PlaceGroundPrefab(
                    PalisadePrefabId,
                    Offset(center, side, forward, halfWidth, offset),
                    forward);
            }

            PlaceGroundPrefab(
                PalisadePrefabId,
                Offset(center, side, forward, -10f, -halfDepth),
                side);
            PlaceGroundPrefab(
                PalisadePrefabId,
                Offset(center, side, forward, 10f, -halfDepth),
                side);
        }

        private void PlaceUpgradeSockets(Vec3 center, Vec2 side, Vec2 forward)
        {
            // All locations are inside the palisade. The protected stash is
            // specifically positioned just right of the gate, never outside.
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.Barracks,
                BarracksPrefabId,
                Offset(center, side, forward, -9f, 6f),
                forward);
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.Tavern,
                TentPrefabId,
                Offset(center, side, forward, 0f, 11f),
                new Vec2(-forward.x, -forward.y));
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.StaffTents,
                StaffTentPrefabId,
                Offset(center, side, forward, 9f, 7f),
                new Vec2(-forward.x, -forward.y));
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.SleepingQuarters,
                QuartersPrefabId,
                Offset(center, side, forward, -10f, -4f),
                forward);
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.Blacksmith,
                StoragePrefabId,
                Offset(center, side, forward, 10f, -3f),
                forward);
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.Stash,
                StoragePrefabId,
                Offset(center, side, forward, 8f, -11f),
                forward);

            Vec3 northWest = Offset(center, side, forward, -14f, 15f);
            Vec3 northEast = Offset(center, side, forward, 14f, 15f);
            Vec3 southWest = Offset(center, side, forward, -14f, -14f);
            Vec3 southEast = Offset(center, side, forward, 14f, -14f);
            if ((_upgrades & RefugeUpgrade.GuardTowers) == RefugeUpgrade.GuardTowers)
            {
                PlaceGuardTower(northWest, forward);
                PlaceGuardTower(northEast, forward);
                PlaceGuardTower(southWest, forward);
                PlaceGuardTower(southEast, forward);
            }
            else
            {
                // One clear visible construction plot marks the next tower;
                // material piles at the other corners keep the design readable
                // without blocking the compound's paths.
                PlaceGroundPrefab(StoragePrefabId, southEast, forward);
                PlaceGroundPrefab(StoragePrefabId, northWest, forward);
            }
        }

        private void PlaceUpgradeOrConstruction(
            RefugeUpgrade upgrade,
            string completedPrefab,
            Vec3 position,
            Vec2 direction)
        {
            if ((_upgrades & upgrade) == upgrade)
            {
                PlaceGroundPrefab(completedPrefab, position, direction);
                if (upgrade == RefugeUpgrade.Blacksmith)
                {
                    PlaceGroundPrefab(TentPrefabId, Offset(position, direction, new Vec2(direction.y, -direction.x), 3f, 0f), direction);
                }

                return;
            }

            // Native timber/storage props make unbuilt sockets visible while
            // retaining an open, navigable courtyard.
            PlaceGroundPrefab(StoragePrefabId, position, direction);
        }

        private void PlaceGuardTower(Vec3 position, Vec2 direction)
        {
            // This is a fixed architectural tower, not a mobile siege engine.
            // A static wooden platform is a safe visual fallback if a future
            // Bannerlord version removes the arena prop.
            if (!TryPlaceGroundPrefab(WatchTowerPrefabId, position, direction))
            {
                PlaceGroundPrefab("wooden_platform_2_a", position, direction);
            }
        }

        private static Vec3 Offset(Vec3 origin, Vec2 side, Vec2 forward, float sideOffset, float forwardOffset)
        {
            return origin + new Vec3(
                side.x * sideOffset + forward.x * forwardOffset,
                side.y * sideOffset + forward.y * forwardOffset,
                0f);
        }

        private float FindHighestTerrainUnderTent(Vec3 center, Vec2 side, Vec2 forward)
        {
            float highest = float.MinValue;
            for (int sideStep = -2; sideStep <= 2; sideStep++)
            {
                for (int forwardStep = -2; forwardStep <= 2; forwardStep++)
                {
                    float sideOffset = TentHalfWidth * sideStep / 2f;
                    float forwardOffset = TentHalfDepth * forwardStep / 2f;
                    Vec2 sample = new Vec2(
                        center.x + side.x * sideOffset + forward.x * forwardOffset,
                        center.y + side.y * sideOffset + forward.y * forwardOffset);
                    float height = Mission.Scene.GetTerrainHeight(sample);
                    if (height > highest)
                    {
                        highest = height;
                    }
                }
            }

            return highest == float.MinValue
                ? Mission.Scene.GetGroundHeightAtPosition(center)
                : highest;
        }

        private void PlaceGroundPrefab(string prefabId, Vec3 position, Vec2 forward)
        {
            position.z = Mission.Scene.GetTerrainHeight(position.AsVec2) + PropGroundClearance;
            TryPlaceLevelPrefab(prefabId, position, forward);
        }

        private bool TryPlaceGroundPrefab(string prefabId, Vec3 position, Vec2 forward)
        {
            position.z = Mission.Scene.GetTerrainHeight(position.AsVec2) + PropGroundClearance;
            return TryPlaceLevelPrefab(prefabId, position, forward);
        }

        private bool TryPlaceLevelPrefab(string prefabId, Vec3 position, Vec2 forward)
        {
            try
            {
                MatrixFrame frame = MatrixFrame.Identity;
                frame.rotation = Mat3.CreateMat3WithForward(new Vec3(forward.x, forward.y, 0f));
                frame.origin = position;
                GameEntity entity = GameEntity.Instantiate(Mission.Scene, prefabId, frame, false);
                return entity != null;
            }
            catch (Exception exception)
            {
                // A missing prop in a later game version must not prevent the
                // player from entering or leaving the refuge scene.
                Diagnostics.Error("Optional refuge scene prop could not be placed: " + prefabId + ".", exception);
                return false;
            }
        }
    }
}
