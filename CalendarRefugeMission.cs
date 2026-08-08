using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
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
        /// <summary>
        /// Returns true only for a complete module-owned refuge scene. A
        /// partial editor export must not silently replace the stable native
        /// fallback, because the mission requires authored terrain, a linked
        /// fort, markers, and baked pedestrian navigation.
        /// </summary>
        internal static bool IsModuleOwnedSceneReady(string sceneId)
        {
            string reason;
            bool ready = RefugeSceneProfileCatalog.IsReady(sceneId, out reason);
            if (!ready && RefugeSceneProfileCatalog.TryGetProfile(sceneId, out _))
            {
                Diagnostics.Info("Module-owned refuge scene is not ready. Scene="
                    + sceneId + "; Reason=" + reason + ".");
            }
            return ready;
        }

        internal static bool TryOpen(
            string sceneId,
            string fortPrefabId,
            bool isCampOnly,
            RefugeSceneClimate climate,
            RefugeWaterAccessType waterAccess,
            bool isWinter,
            Hero stewardHero,
            Hero cookHero,
            Hero guardCaptainHero,
            Hero healerHero,
            RefugeUpgrade upgrades,
            RefugeUpgrade activeUpgrade,
            float activeUpgradeProgress,
            bool hasPortableAnchor,
            Vec3 portableAnchor,
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
                bool needsRandomTerrain = sceneId.StartsWith(
                    "battle_terrain_biome_",
                    StringComparison.Ordinal);
                MissionInitializerRecord initializer = new MissionInitializerRecord(sceneId)
                {
                    PlayingInCampaignMode = true,
                    NeedsRandomTerrain = needsRandomTerrain,
                    // A stable seed keeps the refuge terrain unchanged across
                    // repeated visits while still allowing Bannerlord to build
                    // the biome's real land heightmap instead of its water/base
                    // fallback layer.
                    RandomTerrainSeed = needsRandomTerrain ? 10840415 : 0
                };
                ApplyCampaignEnvironment(ref initializer);
                if (needsRandomTerrain)
                {
                    // The refuge builder's proven workspace was the generated
                    // plains variant of biome 130. Do not inherit a forest,
                    // river, or coastal campaign-face type here: in an
                    // isolated mission that can leave only the biome's water
                    // base visible instead of generating its land heightmap.
                    initializer.TerrainType = (int)TerrainType.Plain;
                }

                MissionState.OpenNew(
                    "RealisticCalendarRefuge",
                    initializer,
                    delegate(Mission mission)
                    {
                        CalendarRefugeMissionController controller = new CalendarRefugeMissionController(
                            sceneId,
                            fortPrefabId,
                            isCampOnly,
                            climate,
                            waterAccess,
                            isWinter,
                            stewardHero,
                            cookHero,
                            guardCaptainHero,
                            healerHero,
                            upgrades,
                            activeUpgrade,
                            activeUpgradeProgress,
                            hasPortableAnchor,
                            portableAnchor);
                        CalendarRefugeFlyoverView flyover = new CalendarRefugeFlyoverView(sceneId);
                        List<MissionBehavior> behaviors = new List<MissionBehavior>
                        {
                            new MissionOptionsComponent(),
                            controller,
                            flyover,
                            // Supplies Bannerlord's normal leave-mission request
                            // path, including the Tab key used in peaceful scenes.
                            new BasicLeaveMissionLogic(false, 0),
                            new EquipmentControllerLeaveLogic()
                        };

                        // A ready profile is a complete fixed scene. Do not even
                        // construct the legacy freeform builder there: loading a
                        // saved draft would create independent child props and
                        // undermine the linked prefab, collision, and navmesh.
                        if (!isCampOnly && !IsModuleOwnedSceneReady(sceneId))
                        {
                            CalendarRefugeLayoutBuilderBehavior builder =
                                new CalendarRefugeLayoutBuilderBehavior(sceneId, controller);
                            behaviors.Add(builder);
                            behaviors.Add(new CalendarRefugeBuilderHudView(builder, flyover));
                        }

                        return behaviors.ToArray();
                    },
                    true,
                    true);

                Diagnostics.Info(
                    "PortableCampDiagnostic MissionOpened; Scene=" + sceneId
                    + "; Fort=" + fortPrefabId
                    + "; Climate=" + climate
                    + "; Access=" + waterAccess
                    + "; NeedsRandomTerrain=" + needsRandomTerrain
                    + "; RandomTerrainSeed=" + initializer.RandomTerrainSeed + ".");
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

        internal static string GetModuleDirectoryPath()
        {
            string assemblyDirectory = System.IO.Path.GetDirectoryName(typeof(CalendarRefugeMission).Assembly.Location);
            DirectoryInfo binaryDirectory = string.IsNullOrWhiteSpace(assemblyDirectory)
                ? null
                : Directory.GetParent(assemblyDirectory);
            DirectoryInfo moduleDirectory = binaryDirectory == null ? null : binaryDirectory.Parent;
            if (moduleDirectory == null)
            {
                throw new InvalidOperationException("The Realistic Calendar Tweaks module directory could not be resolved.");
            }

            return moduleDirectory.FullName;
        }
    }

    /// <summary>
    /// A refuge-only inspection camera for laying out runtime props. It never
    /// changes campaign state and is not added to ordinary battles or towns.
    /// </summary>
    internal sealed class CalendarRefugeFlyoverView : MissionView
    {
        private const float NormalSpeed = 14f;
        private const float FastSpeed = 48f;
        private const float VerticalSpeed = 12f;
        private const float MouseTurnSpeed = 0.003f;
        private const float MinimumTerrainClearance = 1.5f;

        private bool _isActive;
        private bool _toggleWasDown;
        private bool _cameraOverrideConfirmed;
        private bool _movementInputConfirmed;
        private bool _mouseLookCaptured;
        private bool _mouseWasVisibleBeforeLook;
        private bool _anchorWasDown;
        private MatrixFrame _cameraFrame;
        private readonly string _sceneId;

        internal CalendarRefugeFlyoverView(string sceneId)
        {
            _sceneId = sceneId ?? string.Empty;
        }

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            ViewOrderPriority = 1;
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (Mission == null)
            {
                return;
            }

            IInputContext editorInput = GetEditorInput();

            bool toggleIsDown = editorInput.IsKeyDown(InputKey.F9);
            if (toggleIsDown && !_toggleWasDown)
            {
                ToggleFlyover();
            }
            _toggleWasDown = toggleIsDown;

            bool anchorIsDown = editorInput.IsKeyDown(InputKey.F8);
            if (_isActive && anchorIsDown && !_anchorWasDown)
            {
                CalendarRefugeBehavior behavior = CalendarRefugeBehavior.Active;
                if (behavior != null)
                {
                    Vec3 anchorPosition;
                    string anchorFailure;
                    if (TryResolveSolidAnchorSurface(out anchorPosition, out anchorFailure))
                    {
                        behavior.SavePortableSceneAnchor(_sceneId, anchorPosition);
                        Diagnostics.Info("PortableCampDiagnostic SolidSurfaceAnchorSaved; Scene=" + _sceneId
                            + "; CameraZ=" + _cameraFrame.origin.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                            + "; SurfaceZ=" + anchorPosition.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".");
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Camp anchor saved on solid ground. Press F9, then F7 and re-enter the camp."));
                    }
                    else
                    {
                        Diagnostics.Info("PortableCampDiagnostic AnchorRejected; Scene=" + _sceneId
                            + "; Reason=" + anchorFailure + ".");
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Camp anchor was not saved: " + anchorFailure));
                    }
                }
            }
            _anchorWasDown = anchorIsDown;
        }

        private bool TryResolveSolidAnchorSurface(out Vec3 anchorPosition, out string failure)
        {
            anchorPosition = Vec3.Invalid;
            failure = "no solid surface was found below the camera";
            if (Mission == null || Mission.Scene == null)
            {
                return false;
            }

            Vec3 source = _cameraFrame.origin + Vec3.Up;
            Vec3 target = new Vec3(source.x, source.y, source.z - 2000f);
            float collisionDistance;
            Vec3 hitPoint;
            WeakGameEntity collidedEntity;
            bool rayHit = Mission.Scene.RayCastForClosestEntityOrTerrain(
                source,
                target,
                out collisionDistance,
                out hitPoint,
                out collidedEntity,
                0.05f);
            if (!rayHit || !hitPoint.IsValid)
            {
                return false;
            }

            float terrainHeight = Mission.Scene.GetTerrainHeight(hitPoint.AsVec2);
            bool hitPlacedLandMesh = collidedEntity.IsValid;
            bool hitTerrain = !float.IsNaN(terrainHeight)
                && Math.Abs(hitPoint.z - terrainHeight) <= 0.75f;
            if (!hitPlacedLandMesh && !hitTerrain)
            {
                failure = "the camera is above water; move directly above visible land and press F8 again";
                return false;
            }

            anchorPosition = hitPoint;
            return true;
        }

        public override bool UpdateOverridenCamera(float dt)
        {
            if (!_isActive || Mission == null)
            {
                return base.UpdateOverridenCamera(dt);
            }

            // Borderless Bannerlord can keep receiving held keys while the
            // player is typing in another application. Never move a builder
            // camera unless this process owns the foreground window.
            if (!HasForegroundWindow())
            {
                ReleaseMouseLookCapture();
                return true;
            }

            IInputContext editorInput = GetEditorInput();

            // Mission camera callbacks can receive a zero delta while the
            // campaign/mission transition is paused. The camera is an editor
            // utility, so retain a usable real-time step in that state.
            float movementDt = dt > 0.0001f ? Math.Min(dt, 0.1f) : 1f / 60f;

            if (!_cameraOverrideConfirmed)
            {
                _cameraOverrideConfirmed = true;
                Diagnostics.Info("Refuge flyover camera override is receiving engine updates.");
            }

            // Hold right mouse to look around. Keeping rotation deliberate
            // leaves the cursor available for future builder UI controls.
            bool rightMouseDown = IsKeyDown(editorInput, InputKey.RightMouseButton);
            UpdateMouseLookCapture(rightMouseDown);
            if (rightMouseDown)
            {
                float yaw = -TaleWorlds.InputSystem.Input.GetMouseMoveX() * MouseTurnSpeed;
                float pitch = -TaleWorlds.InputSystem.Input.GetMouseMoveY() * MouseTurnSpeed;
                Vec3 worldUp = Vec3.Up;
                _cameraFrame.rotation.RotateAboutAnArbitraryVector(worldUp, yaw);
                _cameraFrame.rotation.RotateAboutSide(pitch);
                _cameraFrame.rotation.Orthonormalize();
            }

            float speed = IsKeyDown(editorInput, InputKey.LeftShift) ? FastSpeed : NormalSpeed;
            Vec3 movement = Vec3.Zero;

            // Bannerlord camera frames look down their negative U axis. This
            // differs from ordinary agent/entity transforms, whose F axis is
            // normally treated as forward. Using F here makes forward motion
            // mostly vertical and the terrain clamp appears to freeze it.
            Vec2 horizontalForward = -_cameraFrame.rotation.u.AsVec2;
            Vec2 horizontalSide = _cameraFrame.rotation.s.AsVec2;
            if (horizontalForward.LengthSquared > 0.001f)
            {
                horizontalForward.Normalize();
            }
            if (horizontalSide.LengthSquared > 0.001f)
            {
                horizontalSide.Normalize();
            }
            bool forwardDown = IsGameOrPhysicalKeyDown(editorInput, 0, InputKey.W);
            bool backwardDown = IsGameOrPhysicalKeyDown(editorInput, 1, InputKey.S);
            bool leftDown = IsGameOrPhysicalKeyDown(editorInput, 2, InputKey.A);
            bool rightDown = IsGameOrPhysicalKeyDown(editorInput, 3, InputKey.D);
            bool upDown = IsKeyDown(editorInput, InputKey.Space);
            bool downDown = IsKeyDown(editorInput, InputKey.LeftAlt);
            if (forwardDown)
            {
                movement += horizontalForward.ToVec3(0f);
            }
            if (backwardDown)
            {
                movement -= horizontalForward.ToVec3(0f);
            }
            if (rightDown)
            {
                movement += horizontalSide.ToVec3(0f);
            }
            if (leftDown)
            {
                movement -= horizontalSide.ToVec3(0f);
            }
            if (upDown)
            {
                _cameraFrame.origin += Vec3.Up * VerticalSpeed * movementDt;
            }
            if (downDown)
            {
                _cameraFrame.origin -= Vec3.Up * VerticalSpeed * movementDt;
            }

            if (movement.LengthSquared > 0.001f)
            {
                movement.Normalize();
                _cameraFrame.origin += movement * speed * movementDt;
            }

            if (!_movementInputConfirmed
                && (forwardDown || backwardDown || leftDown || rightDown || upDown || downDown))
            {
                _movementInputConfirmed = true;
                Diagnostics.Info(
                    "Refuge flyover movement input confirmed. Delta="
                    + dt.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)
                    + "; Position="
                    + _cameraFrame.origin.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    + ","
                    + _cameraFrame.origin.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    + ","
                    + _cameraFrame.origin.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            }

            float terrainHeight = Mission.Scene.GetTerrainHeight(_cameraFrame.origin.AsVec2);
            if (!float.IsNaN(terrainHeight))
            {
                _cameraFrame.origin.z = Math.Max(
                    _cameraFrame.origin.z,
                    terrainHeight + MinimumTerrainClearance);
            }

            if (MissionScreen != null && MissionScreen.CombatCamera != null)
            {
                MissionScreen.CombatCamera.Frame = _cameraFrame;
                // Updating CombatCamera.Frame alone changes the mission's
                // internal camera state, but does not necessarily replace the
                // camera already bound to the rendered scene. Native/RTS
                // free-camera implementations explicitly bind it each frame.
                if (MissionScreen.SceneView != null)
                {
                    MissionScreen.SceneView.SetCamera(MissionScreen.CombatCamera);
                }
            }
            Mission.SetCameraFrame(ref _cameraFrame, 1f);
            return true;
        }

        public override void OnMissionScreenFinalize()
        {
            if (_isActive && Mission != null)
            {
                ReleaseMouseLookCapture();
                Mission.SetCustomCameraIgnoreCollision(false);
                Mission.ResetFirstThirdPersonView();
            }
            _isActive = false;
            base.OnMissionScreenFinalize();
        }

        private void ToggleFlyover()
        {
            if (Mission == null)
            {
                return;
            }

            _isActive = !_isActive;
            _cameraOverrideConfirmed = false;
            _movementInputConfirmed = false;
            if (_isActive)
            {
                _cameraFrame = MissionScreen != null && MissionScreen.CombatCamera != null
                    ? MissionScreen.CombatCamera.Frame
                    : Mission.GetCameraFrame();
                Mission.SetCustomCameraIgnoreCollision(true);
                Diagnostics.Info("Refuge flyover view enabled.");
                InformationManager.DisplayMessage(new InformationMessage(
                    "Refuge flyover: WASD move, Space/Alt height, hold RMB to look, Shift fast, F8 save camp anchor, F9 exit."));
            }
            else
            {
                ReleaseMouseLookCapture();
                Mission.SetCustomCameraIgnoreCollision(false);
                Mission.ResetFirstThirdPersonView();
                Diagnostics.Info("Refuge flyover view disabled.");
                InformationManager.DisplayMessage(new InformationMessage("Refuge flyover off."));
            }
        }

        internal void StartCoastCalibrationOverview()
        {
            if (!_isActive)
            {
                ToggleFlyover();
            }
            if (!_isActive)
            {
                return;
            }

            // Coast fallback navmesh can exist beneath a visual mountain or
            // shoreline shell. Start calibration from a true overhead view so
            // the player can immediately see land and water instead of having
            // to navigate blindly out of the hidden layer.
            _cameraFrame.origin.z += 180f;
            _cameraFrame.rotation = Mat3.Identity;
            _cameraFrame.rotation.Orthonormalize();
            Diagnostics.Info("PortableCampDiagnostic CoastCalibrationOverview"
                + "; Scene=" + _sceneId
                + "; Position=" + _cameraFrame.origin.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + "," + _cameraFrame.origin.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + "," + _cameraFrame.origin.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".");
            InformationManager.DisplayMessage(new InformationMessage(
                "Coast overview active: look for flat visible land, move above it with WASD, then press F8."));
        }

        internal void SetBuilderMode(bool active)
        {
            if (_isActive != active)
            {
                ToggleFlyover();
            }
        }

        private IInputContext GetEditorInput()
        {
            return MissionScreen != null && MissionScreen.SceneLayer != null
                ? MissionScreen.SceneLayer.Input
                : Input;
        }

        private static bool IsKeyDown(IInputContext context, InputKey key)
        {
            return (context != null && context.IsKeyDown(key))
                || TaleWorlds.InputSystem.Input.IsKeyDown(key)
                || TaleWorlds.InputSystem.Input.IsKeyDownImmediate(key);
        }

        private static bool IsGameOrPhysicalKeyDown(
            IInputContext context,
            int gameKeyId,
            InputKey physicalKey)
        {
            return (context != null && context.IsGameKeyDown(gameKeyId))
                || IsKeyDown(context, physicalKey);
        }

        private void UpdateMouseLookCapture(bool wantsLook)
        {
            if (MissionScreen == null || wantsLook == _mouseLookCaptured)
            {
                return;
            }

            _mouseLookCaptured = wantsLook;
            if (wantsLook)
            {
                _mouseWasVisibleBeforeLook = MissionScreen.MouseVisible;
                MissionScreen.MouseVisible = false;
            }
            else
            {
                MissionScreen.MouseVisible = _mouseWasVisibleBeforeLook;
            }
        }

        private void ReleaseMouseLookCapture()
        {
            if (!_mouseLookCaptured)
            {
                return;
            }
            _mouseLookCaptured = false;
            if (MissionScreen != null)
            {
                MissionScreen.MouseVisible = _mouseWasVisibleBeforeLook;
            }
        }

        private static bool HasForegroundWindow()
        {
            try
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                {
                    return false;
                }
                uint processId;
                GetWindowThreadProcessId(foregroundWindow, out processId);
                return processId == (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            }
            catch
            {
                // The mod is Windows-only with the retail game. If a platform
                // layer blocks this query, retain normal in-game controls.
                return true;
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }

    internal sealed class CalendarRefugeMissionController : MissionLogic
    {
        private readonly List<RefugePrefabPlacement> _runtimeLayoutPlacements =
            new List<RefugePrefabPlacement>();
        private const string TentPrefabId = "tent_vlandia_a";
        // These are stationary architectural props with their own collision.
        // Do not substitute a siege-machine prefab here: mobile siege entities
        // bring wheels, deployment scripts, and mission-only assumptions.
        // The long native section used by the starter fort layout. It joins
        // end-to-end cleanly; the previous _c screen section left gaps.
        private const string PalisadePrefabId = "castle_plank_wall_a";
        // This compact native corner tower is the piece used by the authored
        // fort scene. It fits the palisade much better than arena towers.
        private const string WatchTowerPrefabId = "battania_castle_corner_a_l1";
        private const string GateStairsPrefabId = "battania_castle_stairs_a_l1";
        private const string WatchTowerFoundationPrefabId = "rct_watchtower_foundation";
        private const string WatchTowerScaffoldPrefabId = "rct_watchtower_scaffold";
        private const string BarracksPrefabId = "tents_pict_a";
        private const string StaffTentPrefabId = "tents_pict_b";
        private const string QuartersPrefabId = "sturgia_village_tent_a";
        private const string StoragePrefabId = "wood_storage_a";
        private const float TentHalfWidth = 6f;
        private const float TentHalfDepth = 7f;
        // Native tent meshes sit visually high when their origin is placed
        // exactly on terrain. Sink every runtime tent a little below the
        // ground line; player-spawn clearance remains separate and positive.
        private const float TentGroundClearance = -0.28f;
        private const float PropGroundClearance = 0.08f;
        private const float PalisadeBurialDepth = 7f;
        // A regular 24-sided ring: each six-metre native wall section meets
        // its neighbours end-to-end. One omitted side remains the only gate.
        private const int PalisadeSegmentCount = 24;
        private const float PalisadeSideRadius = 23f;
        private const float PalisadeForwardRadius = 23f;
        private const float PalisadeHalfSegmentLength = 3.2f;
        // The complete editor fort reaches roughly 27 metres laterally and
        // 29 metres fore/aft from its runtime anchor. Include a small margin
        // so every wall, tower, and ground prop is evaluated before we choose
        // the procedural-terrain anchor.
        private const float SurveyHalfWidth = 31f;
        private const float SurveyHalfDepth = 33f;

        private const string RefugeAnchorTag = "rct_refuge_anchor";
        private const string RefugeLayoutTag = "rct_refuge_layout";
        // Every authored refuge scene should contain this marker. A temporary
        // fallback near the anchor keeps older testing scenes usable until
        // their marker is added in the scene editor.
        private const string RefugeStewardSpawnTag = "rct_refuge_steward_spawn";
        private const string RefugeCookSpawnTag = "rct_refuge_cook_spawn";
        private const string RefugeGuardCaptainSpawnTag = "rct_refuge_guard_captain_spawn";
        private const string RefugeHealerSpawnTag = "rct_refuge_healer_spawn";
        private const float StewardInteractionDistance = 2.6f;
        // Native scene entities may not be queryable during AfterStart. Give
        // the scene a few ticks to finish loading, then fail once with a
        // useful message instead of trapping the player in a half-made scene.
        private const float SceneInitializationDelaySeconds = 0.20f;
        private const float SceneInitializationTimeoutSeconds = 5.0f;
        private const float NativeSpawnToGateDistance = 28f;
        private const int MaximumOpenTerrainCandidates = 192;
        // A portable camp only needs a clear patch for the player's tent, not
        // the 30 by 34 metre footprint required by the completed fort.
        // Keeping this separate prevents river/coast scenes from being
        // rejected merely because they cannot accommodate the full prefab.
        private const int MaximumPortableCampCandidates = 384;
        private const float PortableCampHalfExtent = 5f;
        private const float MaximumPortableCampHeightVariation = 1.25f;
        private const float MaximumOpenTerrainHeightVariation = 0.75f;
        // If no almost-level plateau exists, choose the least-uneven dry,
        // connected footprint instead of falling back to a fixed coordinate
        // whose slope may be much worse on this generated terrain seed.
        private const float MaximumFallbackTerrainHeightVariation = 4.00f;
        // Root pieces below this authored elevation sit on the terrain. Higher
        // records are tower-top/barrel/wall-walk details and must retain their
        // authored locked height rather than being dropped to the ground.
        private const float TerrainFollowingLayoutMaximumLocalZ = 3.0f;
        private const float PlacementRayHeight = 60f;
        private const float PlacementRayDepth = 20f;
        // On coastal and river battle scenes the raycast reports the water
        // plane as terrain.  Its height is above the actual terrain height
        // at the same X/Y location, which lets us reject water before it can
        // be selected as a spawn point or compound footprint.
        // A terrain ray can differ slightly from the terrain-height query on
        // slopes. One metre distinguishes that normal mesh variation from a
        // river or sea surface without rejecting dry uneven ground.
        private const float MaximumWaterSurfaceHeightDifference = 1.00f;
        private const float AgentSpawnGroundClearance = 0.35f;

        // These are calibration starting points, not a terrain search. Each
        // index is a fixed native-navmesh face chosen per native profile and
        // must be accepted visually before a profile is marked production
        // ready. The first river profile is the current calibration target.
        private struct NativeSceneProfile
        {
            internal readonly string SceneId;
            internal readonly int AnchorNavMeshFace;
            internal readonly float HeadingRadians;
            // Read from the scene's authored water_properties. NaN means the
            // profile has no water-level constraint.
            internal readonly float WaterLevel;
            // Temporary dry camp anchors taken from each scene's saved editor
            // camera position. They keep camps out of NavalDLC's water
            // navmesh until the module-owned scene copies are authored with
            // permanent rct_refuge_anchor entities and baked navigation.
            internal readonly Vec2 PortableCampAnchor;

            internal NativeSceneProfile(
                string sceneId,
                int anchorNavMeshFace,
                float headingRadians,
                float waterLevel = float.NaN,
                float portableCampAnchorX = float.NaN,
                float portableCampAnchorY = float.NaN)
            {
                SceneId = sceneId;
                AnchorNavMeshFace = anchorNavMeshFace;
                HeadingRadians = headingRadians;
                WaterLevel = waterLevel;
                PortableCampAnchor = new Vec2(portableCampAnchorX, portableCampAnchorY);
            }
        }

        private static readonly NativeSceneProfile[] NativeSceneProfiles =
        {
            new NativeSceneProfile("battle_terrain_biome_130", 0, 0f),
            new NativeSceneProfile("battle_terrain_001", 0, 0f),
            new NativeSceneProfile("battle_terrain_015", 0, 0f),
            new NativeSceneProfile("river_bt_empirewest_01_4x4km", 0, 0f, -1.000f, 2182.116f, 897.935f),
            new NativeSceneProfile("battle_terrain_coastal_02", 0, 0f, 210.000f, 4152.515f, 4093.655f),
            new NativeSceneProfile("battle_terrain_009", 0, 0f),
            new NativeSceneProfile("river_bt_aserai_01_4x4km", 0, 0f, 27.000f, 500.974f, 1492.877f),
            new NativeSceneProfile("battle_terrain_coastal_01", 0, 0f, 120.000f, 2754.377f, 4533.451f),
            new NativeSceneProfile("battle_terrain_006", 0, 0f),
            new NativeSceneProfile("river_bt_nord_01_4x4km", 0, 0f, 16.200f, 1492.826f, 2239.266f),
            new NativeSceneProfile("coastal_terrain_north_of_the_north_sea_01", 0, 0f, 90.000f, 3624.025f, 5436.145f)
            ,new NativeSceneProfile("forest_hideout_003", 0, 0f)
            ,new NativeSceneProfile("empire_village_e_navalraid", 0, 0f)
            ,new NativeSceneProfile("sea_bandit_b", 0, 0f)
            ,new NativeSceneProfile("sturgia_village_c", 0, 0f)
            ,new NativeSceneProfile("sturgia_village_a", 0, 0f)
            ,new NativeSceneProfile("sturgia_village_g_navalraid_v2", 0, 0f)
            ,new NativeSceneProfile("desert_hideout_002", 0, 0f)
            ,new NativeSceneProfile("aserai_village_c", 0, 0f)
            ,new NativeSceneProfile("aserai_village_k_navalraid", 0, 0f)
        };
        private const float WaterProximityRadius = 320f;
        private const float WaterProximitySampleStep = 16f;
        private const int WaterProximityDirectionCount = 16;

        private readonly RefugeSceneClimate _climate;
        private readonly string _sceneId;
        private readonly string _fortPrefabId;
        private readonly bool _isCampOnly;
        private readonly RefugeWaterAccessType _waterAccess;
        private readonly bool _isWinter;
        private readonly Hero _stewardHero;
        private readonly Hero _cookHero;
        private readonly Hero _guardCaptainHero;
        private readonly Hero _healerHero;
        private readonly RefugeUpgrade _upgrades;
        private readonly RefugeUpgrade _activeUpgrade;
        private readonly float _activeUpgradeProgress;
        private readonly bool _hasPortableAnchor;
        private readonly Vec3 _portableAnchor;
        private bool _leaveRequested;
        private Agent _playerAgent;
        private Agent _stewardAgent;
        private Agent _cookAgent;
        private Agent _guardCaptainAgent;
        private Agent _healerAgent;
        private RefugeStaffRole? _promptedStaffRole;
        private float _staffInteractionCooldown;
        private bool _sceneInitializationComplete;
        private float _sceneInitializationElapsed;
        private Exception _lastSceneInitializationException;
        private bool _hasCachedPlayerSpawnFrame;
        private MatrixFrame _cachedPlayerSpawnFrame;
        private bool _hasCachedNativeAnchorFrame;
        private MatrixFrame _cachedNativeAnchorFrame;
        private bool _collisionInstantiationUnavailable;
        private bool _initializationDiagnosticsStarted;
        private int _initializationAttemptCount;
        private float _nextInitializationRetryLogTime;
        private string _initializationPhase = "WaitingForScene";

        internal CalendarRefugeMissionController(
            string sceneId,
            string fortPrefabId,
            bool isCampOnly,
            RefugeSceneClimate climate,
            RefugeWaterAccessType waterAccess,
            bool isWinter,
            Hero stewardHero,
            Hero cookHero,
            Hero guardCaptainHero,
            Hero healerHero,
            RefugeUpgrade upgrades,
            RefugeUpgrade activeUpgrade,
            float activeUpgradeProgress,
            bool hasPortableAnchor,
            Vec3 portableAnchor)
        {
            _sceneId = sceneId ?? string.Empty;
            _fortPrefabId = string.IsNullOrWhiteSpace(fortPrefabId)
                ? RefugeFortPrefabCatalog.DefaultFortPrefabId
                : fortPrefabId;
            _isCampOnly = isCampOnly;
            _climate = climate;
            _waterAccess = waterAccess;
            _isWinter = isWinter;
            _stewardHero = stewardHero;
            _cookHero = cookHero;
            _guardCaptainHero = guardCaptainHero;
            _healerHero = healerHero;
            _upgrades = upgrades;
            _activeUpgrade = activeUpgrade;
            _activeUpgradeProgress = Math.Max(0f, Math.Min(1f, activeUpgradeProgress));
            _hasPortableAnchor = hasPortableAnchor && portableAnchor.IsValid;
            _portableAnchor = portableAnchor;
        }

        private const string PlayerSpawnTag = "spawnpoint_player";

        public override void AfterStart()
        {
            base.AfterStart();
            Mission.SetMissionMode(MissionMode.StartUp, atStart: true);
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
            if (_leaveRequested)
            {
                return;
            }

            // The player must always be able to leave, even if an authored
            // scene is still waiting for its tags to finish loading.
            if (Mission.InputManager != null
                && (Mission.InputManager.IsKeyPressed(InputKey.Tab)
                    || Mission.InputManager.IsGameKeyPressed(4)))
            {
                _leaveRequested = true;
                Mission.EndMission();
                return;
            }

            if (!_sceneInitializationComplete)
            {
                TickSceneInitialization(dt);
                return;
            }

            if (Mission.InputManager == null)
            {
                return;
            }

            TickStewardInteraction(dt);
        }

        private void TickSceneInitialization(float dt)
        {
            _sceneInitializationElapsed += Math.Max(0f, dt);
            if (_sceneInitializationElapsed < SceneInitializationDelaySeconds)
            {
                return;
            }

            _initializationAttemptCount++;
            if (!_initializationDiagnosticsStarted)
            {
                _initializationDiagnosticsStarted = true;
                Diagnostics.Info(
                    "Refuge initialization diagnostics started. Scene=" + _sceneId
                    + "; Attempt=" + _initializationAttemptCount
                    + "; DelaySeconds=" + SceneInitializationDelaySeconds.ToString("F2", CultureInfo.InvariantCulture)
                    + "; TimeoutSeconds=" + SceneInitializationTimeoutSeconds.ToString("F2", CultureInfo.InvariantCulture)
                    + "; NavMeshFaces=" + Mission.Scene.GetNavMeshFaceCount()
                    + "; WaterAccess=" + _waterAccess + ".");
            }

            try
            {
                _initializationPhase = "ValidateScenePrerequisites";
                ValidateScenePrerequisites();
                _initializationPhase = "ConfigureSceneClimate";
                ConfigureSceneClimate();
                _initializationPhase = "ResolvePlayerSpawn";
                MatrixFrame spawnFrame = FindPlayerSpawnFrame();
                _initializationPhase = "ConfigureAuthoredRefugeLayout";
                ConfigureAuthoredRefugeLayout();
                _initializationPhase = "SpawnPlayer";
                SpawnPlayerOnFoot(spawnFrame);
                EnableCoastAnchorCalibrationIfNeeded();
                if (_isCampOnly)
                {
                    _initializationPhase = "SpawnCampSteward";
                    SpawnCampSteward();
                }
                else
                {
                    _initializationPhase = "SpawnStaff";
                    SpawnRefugeStaff();
                }
                _sceneInitializationComplete = true;
                Diagnostics.Info("Refuge scene initialization completed after "
                    + _sceneInitializationElapsed.ToString("0.00") + " seconds. Attempts="
                    + _initializationAttemptCount + ".");
            }
            catch (Exception exception)
            {
                _lastSceneInitializationException = exception;
                // A portable camp that has already created the player is
                // usable even if an optional follow-up step (flyover setup,
                // visual tent placement, or a native coast callback) fails.
                // Do not discard a valid coast entry and force the player
                // back to the campaign map after they have visibly spawned.
                if (_isCampOnly && _playerAgent != null)
                {
                    _sceneInitializationComplete = true;
                    Diagnostics.Error(
                        "Portable camp completed with a non-critical post-spawn initialization failure. Phase="
                        + _initializationPhase + "; Scene=" + _sceneId + ".",
                        exception);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Camp entry recovered after optional coast setup failed. F9 enables flyover; F8 saves this scene anchor."));
                    return;
                }
                if (_initializationAttemptCount == 1
                    || _sceneInitializationElapsed >= _nextInitializationRetryLogTime)
                {
                    _nextInitializationRetryLogTime = _sceneInitializationElapsed + 1f;
                    Diagnostics.Error(
                        "Refuge initialization attempt failed. Phase=" + _initializationPhase
                        + "; Attempt=" + _initializationAttemptCount
                        + "; Elapsed=" + _sceneInitializationElapsed.ToString("F2", CultureInfo.InvariantCulture)
                        + "; WillRetry=" + (_sceneInitializationElapsed < SceneInitializationTimeoutSeconds) + ".",
                        exception);
                }
                if (_sceneInitializationElapsed < SceneInitializationTimeoutSeconds)
                {
                    return;
                }

                Diagnostics.Error("Refuge scene initialization timed out; ending the isolated mission safely.",
                    _lastSceneInitializationException);
                string failureDetail = _lastSceneInitializationException == null
                    ? "No exception detail was recorded."
                    : _lastSceneInitializationException.Message;
                InformationManager.DisplayMessage(new InformationMessage(
                    "Refuge camp diagnostic: scene=" + _sceneId
                    + "; climate=" + _climate
                    + "; access=" + _waterAccess
                    + "; phase=" + _initializationPhase
                    + "; " + failureDetail
                    + " Returning to the campaign map."));
                _leaveRequested = true;
                Mission.EndMission();
            }
        }

        private void EnableCoastAnchorCalibrationIfNeeded()
        {
            // Coast scenes have water navigation that can report a valid
            // position below the visible water surface. Until a player saves
            // a dry F8 anchor, enter flyover automatically so they can reach
            // shore without drowning or being trapped below the map.
            if (!_isCampOnly
                || _hasPortableAnchor
                || _waterAccess != RefugeWaterAccessType.Coast)
            {
                return;
            }

            CalendarRefugeFlyoverView flyover = Mission.GetMissionBehavior<CalendarRefugeFlyoverView>();
            if (flyover == null)
            {
                Diagnostics.Info("Coast anchor calibration could not find the refuge flyover view.");
                return;
            }

            flyover.StartCoastCalibrationOverview();
            Diagnostics.Info("PortableCampDiagnostic CoastAnchorCalibrationEnabled; Scene=" + _sceneId + ".");
            InformationManager.DisplayMessage(new InformationMessage(
                "Coast calibration: fly to dry land, press F8 to save this camp anchor, then F9 and F7 to re-enter."));
        }

        private void ValidateScenePrerequisites()
        {
            // Portable camps may be bound to any campaign-patch scene. They
            // never require the module-authored refuge markers; those belong
            // only to completed fixed refuge scenes. This also applies after
            // a test camp is upgraded: a campaign-bound fallback is still
            // not an authored rct_refuge_* scene.
            if (_isCampOnly || !CalendarRefugeMission.IsModuleOwnedSceneReady(_sceneId))
            {
                FindPlayerSpawnFrame();
                return;
            }

            NativeSceneProfile nativeProfile;
            if (TryGetNativeSceneProfile(out nativeProfile))
            {
                FindPlayerSpawnFrame();
                ValidateStaffHero(_stewardHero, "Refuge Steward");
                ValidateStaffHero(_cookHero, "Refuge Cook");
                ValidateStaffHero(_guardCaptainHero, "Refuge Guard Captain");
                ValidateStaffHero(_healerHero, "Refuge Healer");
                return;
            }

            // Every module-owned profile has fixed authored transforms. This
            // deliberately rejects arbitrary native scenes instead of trying
            // to infer a safe 50+ metre compound footprint at runtime.
            RequireSceneMarker(RefugeAnchorTag);
            RequireSceneMarker(RefugeLayoutTag);
            RequireSceneMarker(PlayerSpawnTag);
            RequireSceneMarker(RefugeStewardSpawnTag);
            RequireSceneMarker(RefugeCookSpawnTag);
            RequireSceneMarker(RefugeGuardCaptainSpawnTag);
            RequireSceneMarker(RefugeHealerSpawnTag);
            FindPlayerSpawnFrame();

            ValidateStaffHero(_stewardHero, "Refuge Steward");
            ValidateStaffHero(_cookHero, "Refuge Cook");
            ValidateStaffHero(_guardCaptainHero, "Refuge Guard Captain");
            ValidateStaffHero(_healerHero, "Refuge Healer");
        }

        private static void ValidateStaffHero(Hero staffHero, string staffTitle)
        {
            if (staffHero == null || !staffHero.IsAlive || staffHero.CharacterObject == null)
            {
                throw new InvalidOperationException("The persistent " + staffTitle + " hero is unavailable.");
            }
        }

        private static void RequireSceneMarker(string tag)
        {
            if (!Mission.Current.Scene.FindWeakEntityWithTag(tag).IsValid)
            {
                throw new InvalidOperationException("The refuge scene is missing required marker '" + tag + "'.");
            }
        }

        private MatrixFrame FindPlayerSpawnFrame()
        {
            if (_hasCachedPlayerSpawnFrame)
            {
                return _cachedPlayerSpawnFrame;
            }

            // A player-saved anchor is authoritative for every portable camp,
            // including campaign-patch coast scenes that are not registered
            // in the fixed native-scene profile catalog.
            if (_isCampOnly && _hasPortableAnchor)
            {
                MatrixFrame savedCampFrame = MatrixFrame.Identity;
                savedCampFrame.rotation = Mat3.CreateMat3WithForward(new Vec3(0f, 1f, 0f));
                float terrainHeight = Mission.Scene.GetTerrainHeight(_portableAnchor.AsVec2);
                float savedSurfaceHeight = Math.Abs(_portableAnchor.z) > 0.001f
                    ? _portableAnchor.z
                    : terrainHeight;
                savedCampFrame.origin = new Vec3(
                    _portableAnchor.x,
                    _portableAnchor.y,
                    savedSurfaceHeight + AgentSpawnGroundClearance);
                _cachedNativeAnchorFrame = savedCampFrame;
                _hasCachedNativeAnchorFrame = true;
                Diagnostics.Info("PortableCampDiagnostic PlayerAnchorUsed; Scene=" + _sceneId
                    + "; Anchor=" + FormatDiagnosticVector(
                        savedCampFrame.origin.x,
                        savedCampFrame.origin.y,
                        savedCampFrame.origin.z)
                    + "; SavedSurfaceZ=" + FormatDiagnosticFloat(savedSurfaceHeight)
                    + "; TerrainZ=" + FormatDiagnosticFloat(terrainHeight) + ".");
                return CachePlayerSpawnFrame(savedCampFrame);
            }

            NativeSceneProfile nativeProfile;
            if (TryGetNativeSceneProfile(out nativeProfile))
            {
                if (_isCampOnly)
                {
                    MatrixFrame campFrame;
                    if (_hasPortableAnchor)
                    {
                        campFrame = MatrixFrame.Identity;
                        campFrame.rotation = Mat3.CreateMat3WithForward(new Vec3(0f, 1f, 0f));
                        campFrame.origin = new Vec3(
                            _portableAnchor.x,
                            _portableAnchor.y,
                            Mission.Scene.GetTerrainHeight(_portableAnchor.AsVec2) + AgentSpawnGroundClearance);
                        Diagnostics.Info("PortableCampDiagnostic PlayerAnchorUsed; Scene=" + nativeProfile.SceneId
                            + "; Anchor=" + FormatDiagnosticVector(campFrame.origin.x, campFrame.origin.y, campFrame.origin.z) + ".");
                    }
                    // Coast camera/reference coordinates can belong to the
                    // water volume or the scene's under-map space. On a
                    // first coast visit, survey native terrain/navmesh for a
                    // real dry clearing instead; F8 then records the exact
                    // player-selected camp anchor for later entries.
                    else if (_waterAccess == RefugeWaterAccessType.Coast
                        && TryFindPortableCampSpawnFrame(out campFrame))
                    {
                        Diagnostics.Info("PortableCampDiagnostic CoastLandSurveyAnchor; Scene=" + nativeProfile.SceneId
                            + "; Anchor=" + FormatDiagnosticVector(campFrame.origin.x, campFrame.origin.y, campFrame.origin.z) + ".");
                    }
                    else if (_waterAccess != RefugeWaterAccessType.Coast
                        && TryGetCalibratedPortableCampFrame(nativeProfile, out campFrame))
                    {
                        Diagnostics.Info("PortableCampDiagnostic NativeTerrainAnchor; Scene=" + nativeProfile.SceneId
                            + "; Anchor=" + FormatDiagnosticVector(campFrame.origin.x, campFrame.origin.y, campFrame.origin.z)
                            + "; Mode=CalibratedDrySceneAnchor.");
                    }
                    else if (!TryFindPortableCampSpawnFrame(out campFrame))
                    {
                        throw new InvalidOperationException(
                            "No dry, walkable clearing could be found for the portable camp in this terrain scene.");
                    }

                    _cachedNativeAnchorFrame = campFrame;
                    _hasCachedNativeAnchorFrame = true;
                    return CachePlayerSpawnFrame(campFrame);
                }

                // battle_terrain_015 is the previously selected open-plains
                // refuge scene. Its native tactical Opening region has a
                // 60-metre radius centered here, which safely contains the
                // authored fort without relying on the over-strict generic
                // footprint scanner that rejected this scene in older builds.
                if (string.Equals(nativeProfile.SceneId, "battle_terrain_015", StringComparison.Ordinal))
                {
                    MatrixFrame plainsAnchor = MatrixFrame.Identity;
                    plainsAnchor.rotation = Mat3.CreateMat3WithForward(new Vec3(0f, 1f, 0f));
                    plainsAnchor.origin = new Vec3(575.087f, 496.165f, 14.231f);

                    Vec3 anchorGround;
                    if (!TryProjectToClearTerrain(plainsAnchor.origin, out anchorGround))
                    {
                        throw new InvalidOperationException("The calibrated plains refuge anchor could not be projected to terrain.");
                    }

                    _cachedNativeAnchorFrame = plainsAnchor;
                    _cachedNativeAnchorFrame.origin = anchorGround + Vec3.Up * PropGroundClearance;
                    _hasCachedNativeAnchorFrame = true;

                    MatrixFrame plainsGate = plainsAnchor;
                    plainsGate.origin = anchorGround
                        - plainsAnchor.rotation.f * NativeSpawnToGateDistance
                        + Vec3.Up * AgentSpawnGroundClearance;
                    MatrixFrame walkableGate;
                    if (!TryGetDryWalkableSpawnFrame(plainsGate, out walkableGate))
                    {
                        throw new InvalidOperationException("The calibrated plains refuge entrance is not on connected navmesh.");
                    }

                    Diagnostics.Info("Using calibrated battle_terrain_015 plains refuge anchor.");
                    return CachePlayerSpawnFrame(walkableGate);
                }

                // The old calibrated coordinate is dry, but it can land the
                // full fort across a substantial procedural slope. The editor
                // reference was authored on level ground, so first seek a
                // dry, navmesh-connected, nearly level footprint covering the
                // complete fort. Keep the calibrated point only as a logged
                // last-resort fallback for unusual generated terrain.
                if (string.Equals(nativeProfile.SceneId, "battle_terrain_biome_130", StringComparison.Ordinal))
                {
                    MatrixFrame selectedGateFrame;
                    if (TryFindOpenTerrainSpawnFrame(out selectedGateFrame))
                    {
                        Vec3 selectedCenter = selectedGateFrame.origin
                            + selectedGateFrame.rotation.f * NativeSpawnToGateDistance;
                        Vec3 selectedGround;
                        if (TryProjectToClearTerrain(selectedCenter, out selectedGround))
                        {
                            _cachedNativeAnchorFrame = selectedGateFrame;
                            _cachedNativeAnchorFrame.origin = selectedGround + Vec3.Up * PropGroundClearance;
                            _hasCachedNativeAnchorFrame = true;
                            LogTerrainPlacementProbe("Biome130SurveyedFlatAnchor", _cachedNativeAnchorFrame.origin);
                            Diagnostics.Info(
                                "Using surveyed flat open-plains refuge layout anchor. Scene="
                                + nativeProfile.SceneId + ".");
                            return CachePlayerSpawnFrame(selectedGateFrame);
                        }
                    }

                    MatrixFrame anchorFrame = GetNativeProfileAnchorFrame(nativeProfile);
                    Diagnostics.Info(
                        "No full flat biome-130 footprint was found; falling back to the calibrated anchor."
                        + " The placement diagnostics record its terrain variation.");
                    LogTerrainPlacementProbe("Biome130FallbackAnchorCandidate", anchorFrame.origin);
                    Vec3 anchorGround;
                    if (!TryProjectToClearTerrain(anchorFrame.origin, out anchorGround))
                    {
                        throw new InvalidOperationException("The open-plains refuge anchor could not be projected to terrain.");
                    }

                    _cachedNativeAnchorFrame = anchorFrame;
                    _cachedNativeAnchorFrame.origin = anchorGround + Vec3.Up * PropGroundClearance;
                    _hasCachedNativeAnchorFrame = true;
                    LogTerrainPlacementProbe("Biome130ResolvedAnchor", _cachedNativeAnchorFrame.origin);

                    Vec3 gateCandidate = anchorGround - anchorFrame.rotation.f * NativeSpawnToGateDistance;
                    LogTerrainPlacementProbe("Biome130GateCandidate", gateCandidate);
                    Vec3 gateGround;
                    if (!TryProjectToClearTerrain(gateCandidate, out gateGround))
                    {
                        throw new InvalidOperationException("The open-plains refuge entry point could not be projected to terrain.");
                    }

                    MatrixFrame calibratedGateFrame = anchorFrame;
                    calibratedGateFrame.origin = gateGround + Vec3.Up * AgentSpawnGroundClearance;
                    MatrixFrame walkableGateFrame;
                    if (!TryFindDryWalkableSpawnNear(calibratedGateFrame, out walkableGateFrame))
                    {
                        throw new InvalidOperationException(
                            "No dry navigation mesh was found near the calibrated open-plains refuge entrance.");
                    }

                    float gateAdjustmentDistance = walkableGateFrame.origin.Distance(
                        calibratedGateFrame.origin);
                    Diagnostics.Info(
                        "Resolved open-plains refuge entrance on dry navigation mesh. Candidate="
                        + FormatDiagnosticVector(
                            calibratedGateFrame.origin.x,
                            calibratedGateFrame.origin.y,
                            calibratedGateFrame.origin.z)
                        + "; Resolved=" + FormatDiagnosticVector(
                            walkableGateFrame.origin.x,
                            walkableGateFrame.origin.y,
                            walkableGateFrame.origin.z)
                        + "; AdjustmentDistance="
                        + FormatDiagnosticFloat(gateAdjustmentDistance) + ".");
                    LogTerrainPlacementProbe("Biome130ResolvedPlayerSpawn", walkableGateFrame.origin);
                    Diagnostics.Info("Using calibrated open-plains refuge layout anchor. Scene=" + nativeProfile.SceneId + ".");
                    return CachePlayerSpawnFrame(walkableGateFrame);
                }

                MatrixFrame gateFrame;
                if (!TryFindOpenTerrainSpawnFrame(out gateFrame))
                {
                    throw new InvalidOperationException(
                        "No dry, level, obstacle-free refuge footprint was found in the native scene.");
                }

                Vec3 forward = gateFrame.rotation.f;
                Vec3 centerCandidate = gateFrame.origin
                    + forward * NativeSpawnToGateDistance
                    - Vec3.Up * AgentSpawnGroundClearance;
                Vec3 centerGround;
                if (!TryProjectToClearTerrain(centerCandidate, out centerGround))
                {
                    throw new InvalidOperationException("The selected refuge center could not be projected to terrain.");
                }

                _cachedNativeAnchorFrame = gateFrame;
                _cachedNativeAnchorFrame.origin = centerGround + Vec3.Up * PropGroundClearance;
                _hasCachedNativeAnchorFrame = true;
                Diagnostics.Info(
                    "Selected clear native refuge footprint. Scene=" + nativeProfile.SceneId
                    + "; Gate=" + gateFrame.origin.x.ToString("F2") + ","
                    + gateFrame.origin.y.ToString("F2")
                    + "; Center=" + _cachedNativeAnchorFrame.origin.x.ToString("F2") + ","
                    + _cachedNativeAnchorFrame.origin.y.ToString("F2") + ".");
                return CachePlayerSpawnFrame(gateFrame);
            }

            // Campaign-patch scenes are intentionally not part of the fixed
            // native profile list. A portable camp may use one before an
            // authored refuge scene exists; select a clearing rather than
            // demanding module-only marker entities.
            if (_isCampOnly || !CalendarRefugeMission.IsModuleOwnedSceneReady(_sceneId))
            {
                MatrixFrame campaignPatchFrame;
                if (!TryFindPortableCampSpawnFrame(out campaignPatchFrame))
                {
                    throw new InvalidOperationException(
                        "The campaign-selected terrain scene has no usable portable-camp clearing.");
                }

                _cachedNativeAnchorFrame = campaignPatchFrame;
                _hasCachedNativeAnchorFrame = true;
                Diagnostics.Info("PortableCampDiagnostic CampaignPatchClearingSelected; Scene=" + _sceneId
                    + "; Anchor=" + FormatDiagnosticVector(
                        campaignPatchFrame.origin.x,
                        campaignPatchFrame.origin.y,
                        campaignPatchFrame.origin.z) + ".");
                return CachePlayerSpawnFrame(campaignPatchFrame);
            }

            WeakGameEntity spawnMarker = Mission.Scene.FindWeakEntityWithTag(PlayerSpawnTag);
            if (!spawnMarker.IsValid)
            {
                throw new InvalidOperationException("The refuge scene is missing its player spawn marker.");
            }

            MatrixFrame authoredFrame = spawnMarker.GetGlobalFrame();
            Vec3 spawnPosition = authoredFrame.origin;
            if (Mission.Scene.GetNavigationMeshForPosition(
                    in spawnPosition,
                    out int _,
                    1.5f,
                    true) == UIntPtr.Zero)
            {
                throw new InvalidOperationException("The authored refuge player spawn is not on connected navmesh.");
            }

            return CachePlayerSpawnFrame(authoredFrame);
        }

        private MatrixFrame CachePlayerSpawnFrame(MatrixFrame frame)
        {
            _cachedPlayerSpawnFrame = frame;
            _hasCachedPlayerSpawnFrame = true;
            LogTerrainPlacementProbe("CachedPlayerSpawn", frame.origin);
            return frame;
        }

        private bool TryGetCalibratedPortableCampFrame(
            NativeSceneProfile profile,
            out MatrixFrame result)
        {
            result = MatrixFrame.Identity;
            if (float.IsNaN(profile.PortableCampAnchor.x)
                || float.IsNaN(profile.PortableCampAnchor.y))
            {
                return false;
            }

            Vec2 anchor = profile.PortableCampAnchor;
            float terrainHeight = Mission.Scene.GetTerrainHeight(anchor);
            result.rotation = Mat3.CreateMat3WithForward(new Vec3(0f, 1f, 0f));
            // A scene's reference camera can be above water. Until the
            // player saves a real dry anchor with F8, keep that provisional
            // spawn well above the waterline so it is never below the map and
            // the flyover can be used to choose land safely.
            float spawnHeight = terrainHeight + AgentSpawnGroundClearance;
            if (!float.IsNaN(profile.WaterLevel))
            {
                spawnHeight = Math.Max(spawnHeight, profile.WaterLevel + 3f);
            }
            result.origin = new Vec3(anchor.x, anchor.y, spawnHeight);
            Diagnostics.Info("PortableCampDiagnostic ProvisionalWaterAnchor; Scene=" + profile.SceneId
                + "; TerrainZ=" + FormatDiagnosticFloat(terrainHeight)
                + "; WaterLevel=" + FormatDiagnosticFloat(profile.WaterLevel)
                + "; SpawnZ=" + FormatDiagnosticFloat(spawnHeight) + ".");
            return true;
        }

        private void LogTerrainPlacementProbe(string label, Vec3 position)
        {
            Scene scene = Mission.Scene;
            float terrainHeight = scene.GetTerrainHeight(position.AsVec2);
            Vec3 source = new Vec3(position.x, position.y, terrainHeight + PlacementRayHeight);
            Vec3 target = new Vec3(position.x, position.y, terrainHeight - PlacementRayDepth);
            float collisionDistance;
            Vec3 hitPoint;
            WeakGameEntity collidedEntity;
            bool rayHit = scene.RayCastForClosestEntityOrTerrain(
                source,
                target,
                out collisionDistance,
                out hitPoint,
                out collidedEntity,
                0.05f);
            float hitDelta = rayHit && hitPoint.IsValid
                ? hitPoint.z - terrainHeight
                : float.NaN;
            int faceGroupId;
            UIntPtr navigationMesh = scene.GetNavigationMeshForPosition(
                in position,
                out faceGroupId,
                2.5f,
                true);
            bool detectedWater = rayHit
                && !collidedEntity.IsValid
                && hitPoint.IsValid
                && hitDelta > MaximumWaterSurfaceHeightDifference;

            Diagnostics.Info(
                "Refuge terrain probe. Label=" + label
                + "; Position=" + FormatDiagnosticVector(position.x, position.y, position.z)
                + "; TerrainZ=" + FormatDiagnosticFloat(terrainHeight)
                + "; RayHit=" + rayHit
                + "; RayHitPoint=" + (rayHit && hitPoint.IsValid
                    ? FormatDiagnosticVector(hitPoint.x, hitPoint.y, hitPoint.z)
                    : "Invalid")
                + "; RayHitDelta=" + FormatDiagnosticFloat(hitDelta)
                + "; HitEntity=" + collidedEntity.IsValid
                + "; DetectedWater=" + detectedWater
                + "; NavMesh=" + (navigationMesh != UIntPtr.Zero)
                + "; FaceGroup=" + faceGroupId + ".");
        }

        private bool TryFindOpenTerrainSpawnFrame(out MatrixFrame result)
        {
            result = MatrixFrame.Identity;
            Scene scene = Mission.Scene;
            int faceCount = scene.GetNavMeshFaceCount();
            if (faceCount <= 0)
            {
                return false;
            }

            int stride = Math.Max(1, faceCount / MaximumOpenTerrainCandidates);
            Vec2[] directions =
            {
                new Vec2(0f, 1f),
                new Vec2(1f, 0f),
                new Vec2(0f, -1f),
                new Vec2(-1f, 0f)
            };

            float[] variationLimits =
            {
                MaximumOpenTerrainHeightVariation,
                MaximumFallbackTerrainHeightVariation
            };
            Vec3 bestGate = Vec3.Invalid;
            Vec2 bestForward = new Vec2(0f, 1f);
            for (int pass = 0; pass < variationLimits.Length && !bestGate.IsValid; pass++)
            {
                float bestScore = float.MaxValue;
                for (int faceIndex = 0; faceIndex < faceCount; faceIndex += stride)
                {
                    Vec3 gate = Vec3.Zero;
                    scene.GetNavMeshCenterPosition(faceIndex, ref gate);
                    for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
                    {
                        float score;
                        if (TryScoreOpenTerrainPlacement(
                                gate,
                                directions[directionIndex],
                                variationLimits[pass],
                                out score)
                            && score < bestScore)
                        {
                            bestScore = score;
                            bestGate = gate;
                            bestForward = directions[directionIndex];
                        }
                    }
                }
            }

            if (!bestGate.IsValid)
            {
                return false;
            }

            Vec3 projectedGate;
            if (!TryProjectToClearTerrain(bestGate, out projectedGate))
            {
                return false;
            }

            bestGate = projectedGate + Vec3.Up * PropGroundClearance;
            result.rotation = Mat3.CreateMat3WithForward(new Vec3(bestForward.x, bestForward.y, 0f));
            result.origin = bestGate + Vec3.Up * (AgentSpawnGroundClearance - PropGroundClearance);
            return true;
        }

        private bool TryFindPortableCampSpawnFrame(out MatrixFrame result)
        {
            result = MatrixFrame.Identity;
            Scene scene = Mission.Scene;
            int faceCount = scene.GetNavMeshFaceCount();
            if (faceCount <= 0)
            {
                Diagnostics.Info("PortableCampDiagnostic ClearingSurveyFailed; Reason=NoNavMeshFaces.");
                return false;
            }

            int stride = Math.Max(1, faceCount / MaximumPortableCampCandidates);
            Vec3 bestPoint = Vec3.Invalid;
            float bestScore = float.MaxValue;
            Vec3 highestNavMeshPoint = Vec3.Invalid;
            float highestNavMeshTerrain = float.MinValue;
            int checkedCandidates = 0;
            int acceptedCandidates = 0;
            for (int faceIndex = 0; faceIndex < faceCount; faceIndex += stride)
            {
                checkedCandidates++;
                Vec3 candidate = Vec3.Zero;
                scene.GetNavMeshCenterPosition(faceIndex, ref candidate);
                if (candidate.IsValid)
                {
                    float terrainHeight = scene.GetTerrainHeight(candidate.AsVec2);
                    if (terrainHeight > highestNavMeshTerrain)
                    {
                        highestNavMeshTerrain = terrainHeight;
                        highestNavMeshPoint = candidate;
                    }
                }

                float score;
                if (!TryScorePortableCampClearing(candidate, out score))
                {
                    continue;
                }

                acceptedCandidates++;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPoint = candidate;
                }
            }

            if (!bestPoint.IsValid)
            {
                // Never use the highest navmesh point as a coast fallback.
                // Naval scenes commonly place that point on an outer cliff or
                // terrain seam, which is technically walkable but completely
                // unsuitable for a player camp.
                Diagnostics.Info("PortableCampDiagnostic ClearingSurveyFailed; Scene=" + scene.GetName()
                    + "; Faces=" + faceCount + "; Checked=" + checkedCandidates
                    + "; Accepted=0; Fallback=RejectedMapEdge; HighestTerrain="
                    + FormatDiagnosticFloat(highestNavMeshTerrain) + ".");
                return false;
            }

            MatrixFrame intended = MatrixFrame.Identity;
            intended.rotation = Mat3.CreateMat3WithForward(new Vec3(0f, 1f, 0f));
            // GetNavMeshCenterPosition is the engine's authoritative source
            // for a walkable face. Do not send it back through the terrain
            // raycast used by completed-fort placement: native foliage is
            // frequently reported as a hit and incorrectly rejects every
            // otherwise valid camp clearing.
            intended.origin = new Vec3(
                bestPoint.x,
                bestPoint.y,
                scene.GetTerrainHeight(bestPoint.AsVec2) + AgentSpawnGroundClearance);
            result = intended;

            Diagnostics.Info("PortableCampDiagnostic ClearingSurveySelected; Scene=" + scene.GetName()
                + "; Faces=" + faceCount + "; Checked=" + checkedCandidates
                + "; Accepted=" + acceptedCandidates + "; Score=" + FormatDiagnosticFloat(bestScore)
                + "; Position=" + FormatDiagnosticVector(result.origin.x, result.origin.y, result.origin.z) + ".");
            LogTerrainPlacementProbe("PortableCampSurveyedClearing", result.origin);
            return true;
        }

        private bool TryScorePortableCampClearing(Vec3 center, out float score)
        {
            score = float.MaxValue;
            if (!center.IsValid)
            {
                return false;
            }

            float waterLevel;
            bool hasAuthoredWaterLevel = TryGetNativeWaterLevel(out waterLevel);

            float minimumHeight = float.MaxValue;
            float maximumHeight = float.MinValue;
            float[,] offsets =
            {
                { 0f, 0f },
                { -PortableCampHalfExtent, -PortableCampHalfExtent },
                { -PortableCampHalfExtent, PortableCampHalfExtent },
                { PortableCampHalfExtent, -PortableCampHalfExtent },
                { PortableCampHalfExtent, PortableCampHalfExtent }
            };

            for (int index = 0; index < offsets.GetLength(0); index++)
            {
                Vec3 sample = center + new Vec3(offsets[index, 0], offsets[index, 1], 0f);
                // Naval river scenes include navigable water faces. A camp
                // must be dry across its entire tent footprint, not merely
                // at its player-spawn center.
                float terrainHeight = Mission.Scene.GetTerrainHeight(sample.AsVec2);
                // Water navmesh faces have terrain beneath the visible water
                // plane. The scene's authored water level is stable even
                // when RayCastForClosestEntityOrTerrain reports the plane as
                // terrain, so it is the authoritative dry-land boundary.
                if (hasAuthoredWaterLevel && terrainHeight <= waterLevel + 0.75f)
                {
                    return false;
                }

                // Some coast scenes contain navigation mesh beneath a large
                // decorative mountain/shore shell. The navmesh alone appears
                // walkable, but spawning there places the player under the
                // visible world. Require open vertical clearance across the
                // whole tent footprint before accepting a candidate.
                if (!HasPortableCampOverheadClearance(sample, terrainHeight))
                {
                    return false;
                }

                minimumHeight = Math.Min(minimumHeight, terrainHeight);
                maximumHeight = Math.Max(maximumHeight, terrainHeight);
            }

            float variation = maximumHeight - minimumHeight;
            // Coastal scenes use naturally uneven shore terrain. Allow a
            // modest slope across the single-tent footprint, while land and
            // river camps retain their stricter flat-ground requirement.
            float maximumVariation = _waterAccess == RefugeWaterAccessType.Coast
                ? 5.5f
                : MaximumPortableCampHeightVariation;
            if (variation > maximumVariation)
            {
                return false;
            }

            // For river/coast profiles retain the terrain identity without
            // placing the tent on water: favour dry clearings close to a
            // water feature, but do not make that a hard requirement because
            // some native battle scenes expose no raycastable water plane.
            float waterPenalty = 0f;
            float nearestWaterDistance = GetNearestWaterDistance(center);
            if (_waterAccess != RefugeWaterAccessType.Land)
            {
                if (float.IsPositiveInfinity(nearestWaterDistance))
                {
                    waterPenalty = _waterAccess == RefugeWaterAccessType.Coast ? 10000f : 2f;
                }
                else if (_waterAccess == RefugeWaterAccessType.Coast)
                {
                    // A coast camp should be visibly on the shore, not merely
                    // somewhere inland in a scene that contains water.
                    waterPenalty = nearestWaterDistance * 25f;
                }
            }

            score = variation * 100f + waterPenalty - minimumHeight * 0.01f;
            Diagnostics.Info("PortableCampDiagnostic ClearingScore; Scene=" + Mission.Scene.GetName()
                + "; Candidate=" + FormatDiagnosticVector(center.x, center.y, center.z)
                + "; Variation=" + FormatDiagnosticFloat(variation)
                + "; NearestWater=" + (float.IsPositiveInfinity(nearestWaterDistance)
                    ? "none"
                    : FormatDiagnosticFloat(nearestWaterDistance))
                + "; Score=" + FormatDiagnosticFloat(score) + ".");
            return true;
        }

        private bool HasPortableCampOverheadClearance(Vec3 sample, float groundHeight)
        {
            // Probe from well above the rendered scene down through the
            // candidate. A short upward ray misses large coast mountain
            // shells whose underside may be dozens of metres above hidden
            // navmesh. The first top-down hit reveals any visible surface
            // covering that navmesh point.
            const float skyProbeHeight = 500f;
            const float surfaceTolerance = 1.5f;
            Vec3 source = new Vec3(sample.x, sample.y, groundHeight + skyProbeHeight);
            Vec3 target = new Vec3(sample.x, sample.y, groundHeight - 2f);
            float collisionDistance;
            Vec3 hitPoint;
            WeakGameEntity collidedEntity;
            bool rayHit = Mission.Scene.RayCastForClosestEntityOrTerrain(
                source,
                target,
                out collisionDistance,
                out hitPoint,
                out collidedEntity,
                0.05f);
            bool blocked = rayHit
                && hitPoint.IsValid
                && hitPoint.z > groundHeight + surfaceTolerance;
            if (blocked)
            {
                Diagnostics.Info("PortableCampDiagnostic ClearingRejectedOverhead"
                    + "; Scene=" + Mission.Scene.GetName()
                    + "; Position=" + FormatDiagnosticVector(sample.x, sample.y, groundHeight)
                    + "; Hit=" + (hitPoint.IsValid
                        ? FormatDiagnosticVector(hitPoint.x, hitPoint.y, hitPoint.z)
                        : "Invalid")
                    + "; HitEntity=" + collidedEntity.IsValid + ".");
            }
            return !blocked;
        }

        private bool TryGetNativeWaterLevel(out float waterLevel)
        {
            waterLevel = 0f;
            NativeSceneProfile profile;
            if (!TryGetNativeSceneProfile(out profile) || float.IsNaN(profile.WaterLevel))
            {
                return false;
            }

            waterLevel = profile.WaterLevel;
            return true;
        }

        private bool TryGetDryWalkableSpawnFrame(MatrixFrame candidate, out MatrixFrame result)
        {
            result = MatrixFrame.Identity;
            Vec3 groundPoint;
            if (!TryProjectToClearTerrain(candidate.origin, out groundPoint))
            {
                return false;
            }

            Vec3 walkablePoint = groundPoint + Vec3.Up * AgentSpawnGroundClearance;
            if (Mission.Scene.GetNavigationMeshForPosition(
                    in walkablePoint,
                    out int _,
                    2.5f,
                    true) == UIntPtr.Zero)
            {
                return false;
            }

            result = candidate;
            result.origin = walkablePoint;
            return true;
        }

        private bool TryFindDryWalkableSpawnNear(MatrixFrame intendedFrame, out MatrixFrame result)
        {
            if (TryGetDryWalkableSpawnFrame(intendedFrame, out result))
            {
                return true;
            }

            // Keep the entrance tied to the authored fort, but tolerate a
            // small procedural-navmesh gap at the exact gate coordinate. The
            // first successful ring is the nearest dry walkable correction.
            const float searchStep = 2f;
            const float maximumSearchRadius = 16f;
            const int directionsPerRing = 16;
            for (float radius = searchStep; radius <= maximumSearchRadius; radius += searchStep)
            {
                for (int directionIndex = 0; directionIndex < directionsPerRing; directionIndex++)
                {
                    float angle = (float)(Math.PI * 2.0 * directionIndex / directionsPerRing);
                    MatrixFrame candidate = intendedFrame;
                    candidate.origin = new Vec3(
                        intendedFrame.origin.x + (float)Math.Cos(angle) * radius,
                        intendedFrame.origin.y + (float)Math.Sin(angle) * radius,
                        intendedFrame.origin.z);
                    if (TryGetDryWalkableSpawnFrame(candidate, out result))
                    {
                        return true;
                    }
                }
            }

            result = MatrixFrame.Identity;
            return false;
        }

        private bool TryScoreOpenTerrainPlacement(
            Vec3 gate,
            Vec2 forward,
            float maximumHeightVariation,
            out float score)
        {
            score = float.MaxValue;
            Scene scene = Mission.Scene;
            Vec2 side = new Vec2(forward.y, -forward.x);
            Vec3 center = gate + new Vec3(
                forward.x * NativeSpawnToGateDistance,
                forward.y * NativeSpawnToGateDistance,
                0f);
            float minimumHeight = float.MaxValue;
            float maximumHeight = float.MinValue;

            // Survey a dense grid across the complete 30 by 34 metre compound.
            // A sparse five-point grid can miss a tree trunk or shallow ditch
            // between samples, so native scenes use nine samples per axis.
            const int footprintSteps = 10;
            for (int sideIndex = 0; sideIndex <= footprintSteps; sideIndex++)
            {
                float sideOffset = -SurveyHalfWidth
                    + SurveyHalfWidth * 2f * sideIndex / footprintSteps;
                for (int forwardIndex = 0; forwardIndex <= footprintSteps; forwardIndex++)
                {
                    float forwardOffset = -SurveyHalfDepth
                        + SurveyHalfDepth * 2f * forwardIndex / footprintSteps;
                    Vec3 sample = Offset(
                        center,
                        side,
                        forward,
                        sideOffset,
                        forwardOffset);
                    Vec3 groundPoint;
                    if (!TryProjectToClearTerrain(sample, out groundPoint))
                    {
                        return false;
                    }

                    sample = groundPoint + Vec3.Up * PropGroundClearance;
                    int faceGroupId;
                    if (scene.GetNavigationMeshForPosition(
                            in sample,
                            out faceGroupId,
                            2.5f,
                            true) == UIntPtr.Zero)
                    {
                        return false;
                    }

                    minimumHeight = Math.Min(minimumHeight, sample.z);
                    maximumHeight = Math.Max(maximumHeight, sample.z);
                }
            }

            float variation = maximumHeight - minimumHeight;
            if (variation > maximumHeightVariation)
            {
                return false;
            }

            // River/coastal refuges must actually look and feel like water
            // sites. They are placed on dry terrain only, with real water
            // nearby; land refuges deliberately do not require this.
            if (_waterAccess != RefugeWaterAccessType.Land
                && !HasWaterNearCompound(center))
            {
                return false;
            }

            if (!AreCriticalRefugePointsConnected(center, side, forward))
            {
                return false;
            }

            // Prefer the flattest valid candidate. Height is a small tie-breaker
            // so a dry rise wins over a low depression on river/coastal maps.
            score = variation * 100f - minimumHeight * 0.01f;
            return true;
        }

        private bool HasWaterNearCompound(Vec3 center)
        {
            return !float.IsPositiveInfinity(GetNearestWaterDistance(center));
        }

        private float GetNearestWaterDistance(Vec3 center)
        {
            for (float radius = WaterProximitySampleStep;
                 radius <= WaterProximityRadius;
                 radius += WaterProximitySampleStep)
            {
                for (int directionIndex = 0; directionIndex < WaterProximityDirectionCount; directionIndex++)
                {
                    float angle = (float)(Math.PI * 2.0 * directionIndex / WaterProximityDirectionCount);
                    Vec3 sample = center + new Vec3(
                        (float)Math.Cos(angle) * radius,
                        (float)Math.Sin(angle) * radius,
                        0f);
                    if (IsWaterSurface(sample))
                    {
                        return radius;
                    }
                }
            }

            return float.PositiveInfinity;
        }

        private bool IsWaterSurface(Vec3 position)
        {
            Scene scene = Mission.Scene;
            float terrainHeight = scene.GetTerrainHeight(position.AsVec2);
            Vec3 source = new Vec3(position.x, position.y, terrainHeight + PlacementRayHeight);
            Vec3 target = new Vec3(position.x, position.y, terrainHeight - PlacementRayDepth);
            float collisionDistance;
            Vec3 hitPoint;
            WeakGameEntity collidedEntity;
            if (!scene.RayCastForClosestEntityOrTerrain(
                    source,
                    target,
                    out collisionDistance,
                    out hitPoint,
                    out collidedEntity,
                    0.05f))
            {
                return false;
            }

            return !collidedEntity.IsValid
                && hitPoint.IsValid
                && hitPoint.z - terrainHeight > MaximumWaterSurfaceHeightDifference;
        }

        private bool AreCriticalRefugePointsConnected(Vec3 center, Vec2 side, Vec2 forward)
        {
            WorldPosition gatePosition;
            if (!TryCreateWalkableWorldPosition(
                    Offset(center, side, forward, 0f, -PalisadeForwardRadius),
                    out gatePosition))
            {
                return false;
            }

            float[,] criticalOffsets =
            {
                { 0f, 0f },
                { -17f, -17f },
                { 17f, -17f },
                { -17f, 17f },
                { 17f, 17f }
            };
            for (int index = 0; index < criticalOffsets.GetLength(0); index++)
            {
                WorldPosition destination;
                if (!TryCreateWalkableWorldPosition(
                        Offset(
                            center,
                            side,
                            forward,
                            criticalOffsets[index, 0],
                            criticalOffsets[index, 1]),
                        out destination))
                {
                    return false;
                }

                float pathDistance;
                if (!Mission.Scene.GetPathDistanceBetweenPositions(
                        ref gatePosition,
                        ref destination,
                        0.35f,
                        out pathDistance))
                {
                    return false;
                }

                float straightDistance = gatePosition.AsVec2.Distance(destination.AsVec2);
                if (pathDistance > straightDistance * 2f + 2f)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryCreateWalkableWorldPosition(Vec3 position, out WorldPosition worldPosition)
        {
            worldPosition = default(WorldPosition);
            Vec3 groundPoint;
            if (!TryProjectToClearTerrain(position, out groundPoint))
            {
                return false;
            }

            UIntPtr navMesh = Mission.Scene.GetNavigationMeshForPosition(
                in groundPoint,
                out int _,
                2.5f,
                true);
            if (navMesh == UIntPtr.Zero)
            {
                return false;
            }

            worldPosition = new WorldPosition(Mission.Scene, navMesh, groundPoint, hasValidZ: true);
            return true;
        }

        private bool TryProjectToClearTerrain(Vec3 position, out Vec3 groundPoint)
        {
            Scene scene = Mission.Scene;
            float terrainHeight = scene.GetTerrainHeight(position.AsVec2);
            Vec3 source = new Vec3(position.x, position.y, terrainHeight + PlacementRayHeight);
            Vec3 target = new Vec3(position.x, position.y, terrainHeight - PlacementRayDepth);
            float collisionDistance;
            WeakGameEntity collidedEntity;
            if (!scene.RayCastForClosestEntityOrTerrain(
                    source,
                    target,
                    out collisionDistance,
                    out groundPoint,
                    out collidedEntity,
                    0.05f))
            {
                groundPoint = Vec3.Invalid;
                return false;
            }

            // A valid entity means the ray struck a tree, rock, building,
            // bridge, or another physical prop before reaching terrain. Such a
            // candidate cannot safely contain the fixed refuge footprint.
            if (collidedEntity.IsValid || !groundPoint.IsValid)
            {
                groundPoint = Vec3.Invalid;
                return false;
            }

            // Water surfaces are returned by this engine call without an
            // entity. Their collision point sits above the real terrain at
            // this location (the riverbed or seabed), so accepting them would
            // place the player and the entire refuge in water.
            if (groundPoint.z - terrainHeight > MaximumWaterSurfaceHeightDifference)
            {
                groundPoint = Vec3.Invalid;
                return false;
            }

            return true;
        }

        private void SpawnPlayerOnFoot(MatrixFrame spawnFrame)
        {
            CharacterObject playerCharacter = CharacterObject.PlayerCharacter;
            if (playerCharacter == null)
            {
                throw new InvalidOperationException("The campaign player character is unavailable.");
            }

            Vec3 position = spawnFrame.origin;
            LogTerrainPlacementProbe("PlayerSpawnRequested", position);
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

            _playerAgent = Mission.SpawnAgent(buildData, false);
            if (_playerAgent == null)
            {
                throw new InvalidOperationException("Bannerlord did not create the refuge player agent.");
            }

            Vec3 engineSpawnPosition = _playerAgent.Position;
            float engineRelocationDistance = engineSpawnPosition.Distance(position);
            Diagnostics.Info(
                "Refuge player spawned. Requested="
                + FormatDiagnosticVector(position.x, position.y, position.z)
                + "; Actual=" + FormatDiagnosticVector(
                    engineSpawnPosition.x,
                    engineSpawnPosition.y,
                    engineSpawnPosition.z)
                + "; RelocationDistance="
                + FormatDiagnosticFloat(engineRelocationDistance) + ".");
            LogTerrainPlacementProbe("PlayerSpawnEngineResult", engineSpawnPosition);

            // Mission.SpawnAgent may silently relocate a requested position
            // to a different navigation face. Pin a relocated player back to
            // the dry, walkable terrain point that was validated above.
            const float maximumAcceptedSpawnRelocation = 0.5f;
            if (engineRelocationDistance > maximumAcceptedSpawnRelocation)
            {
                _playerAgent.TeleportToPosition(position);
                Diagnostics.Info(
                    "Corrected refuge player spawn relocation. EnginePosition="
                    + FormatDiagnosticVector(
                        engineSpawnPosition.x,
                        engineSpawnPosition.y,
                        engineSpawnPosition.z)
                    + "; CorrectedPosition=" + FormatDiagnosticVector(
                        _playerAgent.Position.x,
                        _playerAgent.Position.y,
                        _playerAgent.Position.z)
                    + "; RelocationDistance="
                    + FormatDiagnosticFloat(engineRelocationDistance) + ".");
            }

            LogTerrainPlacementProbe("PlayerSpawnActual", _playerAgent.Position);
        }

        private void SpawnRefugeStaff()
        {
            _stewardAgent = SpawnStaffMember(
                _stewardHero,
                RefugeStewardSpawnTag,
                "Refuge Steward",
                0f,
                -5f);

            // Staff arrive as the refuge becomes capable of supporting their
            // role. The Guard Captain follows the completed towers, the Cook
            // follows the Barracks, and the Healer is the final arrival after
            // the Tavern has been built.
            if ((_upgrades & RefugeUpgrade.GuardTowers) == RefugeUpgrade.GuardTowers)
            {
                _guardCaptainAgent = SpawnStaffMember(
                    _guardCaptainHero,
                    RefugeGuardCaptainSpawnTag,
                    "Refuge Guard Captain",
                    7f,
                    6f);
            }

            if ((_upgrades & RefugeUpgrade.Barracks) == RefugeUpgrade.Barracks)
            {
                _cookAgent = SpawnStaffMember(
                    _cookHero,
                    RefugeCookSpawnTag,
                    "Refuge Cook",
                    -7f,
                    6f);
            }

            if ((_upgrades & RefugeUpgrade.Tavern) == RefugeUpgrade.Tavern)
            {
                _healerAgent = SpawnStaffMember(
                    _healerHero,
                    RefugeHealerSpawnTag,
                    "Refuge Healer",
                    -7f,
                    -6f);
            }

            Diagnostics.Info("Refuge staff progression spawned. Towers="
                + ((_upgrades & RefugeUpgrade.GuardTowers) == RefugeUpgrade.GuardTowers)
                + "; Barracks=" + ((_upgrades & RefugeUpgrade.Barracks) == RefugeUpgrade.Barracks)
                + "; Tavern=" + ((_upgrades & RefugeUpgrade.Tavern) == RefugeUpgrade.Tavern) + ".");
        }

        private void SpawnCampSteward()
        {
            // The Steward is the first persistent person attached to a new
            // camp. The specialist staff arrive only after the palisade
            // refuge has been completed.
            _stewardAgent = SpawnStaffMember(
                _stewardHero,
                RefugeStewardSpawnTag,
                "Refuge Steward",
                0f,
                -5f);
            Diagnostics.Info("PortableCampDiagnostic CampStewardSpawned; Scene=" + _sceneId
                + "; Spawned=" + (_stewardAgent != null) + ".");
        }

        private Agent SpawnStaffMember(
            Hero staffHero,
            string spawnTag,
            string staffTitle,
            float fallbackSideOffset,
            float fallbackForwardOffset)
        {
            CharacterObject staffCharacter = staffHero == null ? null : staffHero.CharacterObject;
            if (staffCharacter == null)
            {
                throw new InvalidOperationException("The persistent " + staffTitle + " hero is unavailable.");
            }

            MatrixFrame frame = FindStaffSpawnFrame(
                spawnTag,
                fallbackSideOffset,
                fallbackForwardOffset,
                staffTitle);
            MatrixFrame validatedFrame;
            if (!TryFindDryWalkableSpawnNear(frame, out validatedFrame))
            {
                throw new InvalidOperationException(
                    "No dry connected navigation mesh was found for " + staffTitle + ".");
            }
            frame = validatedFrame;
            Vec2 direction = frame.rotation.f.AsVec2;
            if (direction.LengthSquared < 0.001f)
            {
                direction = new Vec2(0f, 1f);
            }
            else
            {
                direction.Normalize();
            }

            Vec3 position = frame.origin;
            LogTerrainPlacementProbe(staffTitle + "SpawnRequested", position);
            AgentBuildData buildData = new AgentBuildData(new BasicBattleAgentOrigin(staffCharacter))
                .InitialPosition(position)
                .InitialDirection(direction)
                .NoHorses(true)
                .NoWeapons(true)
                .Controller(AgentControllerType.AI);
            Agent agent = Mission.SpawnAgent(buildData, false);
            if (agent == null)
            {
                throw new InvalidOperationException("Bannerlord did not create the " + staffTitle + ".");
            }
            float relocationDistance = agent.Position.Distance(position);
            if (relocationDistance > 0.5f)
            {
                agent.TeleportToPosition(position);
            }
            Diagnostics.Info(
                "Refuge staff spawned. Role=" + staffTitle
                + "; Requested=" + FormatDiagnosticVector(position.x, position.y, position.z)
                + "; Actual=" + FormatDiagnosticVector(agent.Position.x, agent.Position.y, agent.Position.z)
                + "; RelocationDistance=" + FormatDiagnosticFloat(relocationDistance)
                + "; Corrected=" + (relocationDistance > 0.5f) + ".");
            LogTerrainPlacementProbe(staffTitle + "SpawnActual", agent.Position);
            return agent;
        }

        private MatrixFrame FindStaffSpawnFrame(
            string spawnTag,
            float fallbackSideOffset,
            float fallbackForwardOffset,
            string staffTitle)
        {
            WeakGameEntity staffMarker = Mission.Scene.FindWeakEntityWithTag(spawnTag);
            if (staffMarker.IsValid)
            {
                return staffMarker.GetGlobalFrame();
            }

            NativeSceneProfile nativeProfile;
            if (TryGetNativeSceneProfile(out nativeProfile))
            {
                // The generated scene's anchor is selected at runtime. Never
                // return to the static profile coordinate after the fort has
                // been surveyed and moved to a dry, level footprint.
                MatrixFrame fallback = _hasCachedNativeAnchorFrame
                    ? _cachedNativeAnchorFrame
                    : GetNativeProfileAnchorFrame(nativeProfile);
                fallback.origin += fallback.rotation.s * fallbackSideOffset;
                fallback.origin += fallback.rotation.f * fallbackForwardOffset;
                return fallback;
            }

            throw new InvalidOperationException(
                "The refuge scene is missing required " + spawnTag + " marker for " + staffTitle + ".");
        }

        private void TickStewardInteraction(float dt)
        {
            if (_playerAgent == null)
            {
                return;
            }

            _staffInteractionCooldown = Math.Max(0f, _staffInteractionCooldown - dt);
            RefugeStaffRole nearbyRole;
            if (!TryGetNearbyStaffRole(out nearbyRole))
            {
                _promptedStaffRole = null;
                return;
            }

            if (!_promptedStaffRole.HasValue || _promptedStaffRole.Value != nearbyRole)
            {
                _promptedStaffRole = nearbyRole;
                InformationManager.DisplayMessage(new InformationMessage(
                    "Press F to speak with the " + GetStaffDisplayName(nearbyRole) + "."));
            }

            if (_staffInteractionCooldown <= 0f
                && !CalendarRefugeLayoutBuilderBehavior.IsEditing
                && Mission.InputManager.IsKeyPressed(InputKey.F))
            {
                _staffInteractionCooldown = 0.5f;
                CalendarRefugeStewardInteraction.Show(nearbyRole);
            }
        }

        private bool TryGetNearbyStaffRole(out RefugeStaffRole role)
        {
            role = RefugeStaffRole.Steward;
            Agent nearestAgent = null;
            float nearestDistanceSquared = StewardInteractionDistance * StewardInteractionDistance;
            ConsiderNearbyStaff(_stewardAgent, RefugeStaffRole.Steward, ref nearestAgent, ref nearestDistanceSquared, ref role);
            ConsiderNearbyStaff(_cookAgent, RefugeStaffRole.Cook, ref nearestAgent, ref nearestDistanceSquared, ref role);
            ConsiderNearbyStaff(_guardCaptainAgent, RefugeStaffRole.GuardCaptain, ref nearestAgent, ref nearestDistanceSquared, ref role);
            ConsiderNearbyStaff(_healerAgent, RefugeStaffRole.Healer, ref nearestAgent, ref nearestDistanceSquared, ref role);
            return nearestAgent != null;
        }

        private void ConsiderNearbyStaff(
            Agent staffAgent,
            RefugeStaffRole staffRole,
            ref Agent nearestAgent,
            ref float nearestDistanceSquared,
            ref RefugeStaffRole nearestRole)
        {
            if (staffAgent == null || !staffAgent.IsActive())
            {
                return;
            }

            float distanceSquared = (_playerAgent.Position - staffAgent.Position).LengthSquared;
            if (distanceSquared <= nearestDistanceSquared)
            {
                nearestAgent = staffAgent;
                nearestDistanceSquared = distanceSquared;
                nearestRole = staffRole;
            }
        }

        private static string GetStaffDisplayName(RefugeStaffRole role)
        {
            switch (role)
            {
                case RefugeStaffRole.Cook:
                    return "Refuge Cook";
                case RefugeStaffRole.GuardCaptain:
                    return "Refuge Guard Captain";
                case RefugeStaffRole.Healer:
                    return "Refuge Healer";
                default:
                    return "Refuge Steward";
            }
        }

        private void ConfigureAuthoredRefugeLayout()
        {
            if (CalendarRefugeMission.IsModuleOwnedSceneReady(_sceneId))
            {
                // Module-owned scenes contain the linked fort and its baked
                // collision/navmesh. Never generate a second palisade, tent,
                // tower set, or upgrade layout on top of them.
                Diagnostics.Info("Using embedded authored refuge layout; runtime fort generation skipped. Scene="
                    + _sceneId + ".");
                return;
            }

            MatrixFrame anchorFrame = FindRefugeAnchorFrame();
            Vec2 forward = anchorFrame.rotation.f.AsVec2;
            if (forward.LengthSquared < 0.001f)
            {
                forward = new Vec2(0f, 1f);
            }
            else
            {
                forward.Normalize();
            }

            Vec2 side = new Vec2(forward.y, -forward.x);
            Vec3 center = anchorFrame.origin;

            if (string.Equals(_sceneId, "battle_terrain_biome_130", StringComparison.Ordinal))
            {
                // Bannerlord v1.4.7 raises an AccessViolationException while
                // creating this 192-entity flattened root even when embedded
                // physics is disabled. Do not call that unsafe native path.
                // Recreate only its top-level registered native prefabs, but
                // preserve every complete authored local frame with the
                // engine's own Euler convention and rigid anchor transform.
                // Use the complete editor-derived export. Its curved walls,
                // towers/platforms, and interior are authored in one frame;
                // do not mix it with the stripped perimeter-only export.
                int placed = PlaceAuthoredFortOnTerrain(anchorFrame, side, forward);
                if (placed == 0)
                {
                    throw new InvalidOperationException(
                        "The complete authored refuge components could not be instantiated on the open-plains scene.");
                }

                float anchorTerrainHeight = Mission.Scene.GetTerrainHeight(center.AsVec2);
                Diagnostics.Info(
                    "Placed complete authored refuge with rigid full engine transforms on biome-130."
                    + " Components=" + placed
                    + " Anchor=" + FormatDiagnosticVector(center.x, center.y, center.z)
                    + "; TerrainZ=" + FormatDiagnosticFloat(anchorTerrainHeight)
                    + "; GroundDelta=" + FormatDiagnosticFloat(center.z - anchorTerrainHeight)
                    + ".");
                return;
            }

            NativeSceneProfile portableCampProfile;
            bool hasPortableCampProfile = TryGetNativeSceneProfile(out portableCampProfile);
            if (_isCampOnly)
            {
                // This is deliberately a small first-stage camp. It proves
                // the selected climate/access terrain and anchor before a
                // player upgrades it into one of the larger fort blueprints.
                PlaceMainTent(center, side, forward);
                float terrainHeight = Mission.Scene.GetTerrainHeight(center.AsVec2);
                Diagnostics.Info("PortableCampDiagnostic CampPlaced"
                    + "; Scene=" + _sceneId
                    + "; Climate=" + _climate
                    + "; Access=" + _waterAccess
                    + "; Anchor=" + FormatDiagnosticVector(center.x, center.y, center.z)
                    + "; TerrainZ=" + FormatDiagnosticFloat(terrainHeight)
                    + "; GroundDelta=" + FormatDiagnosticFloat(center.z - terrainHeight)
                    + "; NavMeshFace=" + (hasPortableCampProfile
                        ? portableCampProfile.AnchorNavMeshFace.ToString()
                        : "campaign-patch")
                    + "; Layout=SingleTent.");
                return;
            }

            PlaceStarterPalisade(center, side, forward);
            PlaceMainTent(center, side, forward);

            PlaceUpgradeSockets(center, side, forward);

            Diagnostics.Info(
                "Loaded authored refuge layout. Upgrades=" + _upgrades
                + "; Center=" + center.x.ToString("F2") + "," + center.y.ToString("F2") + ".");
        }

        private int PlaceAuthoredFortOnTerrain(
            MatrixFrame anchorFrame,
            Vec2 side,
            Vec2 forward)
        {
            RefugeFortPrefabDefinition fort;
            if (!RefugeFortPrefabCatalog.TryGet(_fortPrefabId, out fort))
            {
                fort = RefugeFortPrefabCatalog.GetDefault();
                Diagnostics.Info("Selected portable refuge fort was not registered; using the default blueprint.");
            }
            string layoutPath = RefugeFortPrefabCatalog.GetRuntimeLayoutPath(fort);
            if (!File.Exists(layoutPath))
            {
                throw new FileNotFoundException("The complete runtime refuge layout source is missing.", layoutPath);
            }

            XmlDocument document = new XmlDocument();
            document.Load(layoutPath);
            XmlNodeList nodes = document.SelectNodes("/prefabs/game_entity/children/game_entity");
            if (nodes == null || nodes.Count == 0)
            {
                throw new InvalidOperationException("The complete runtime refuge layout contains no top-level components.");
            }

            int placed = 0;
            int skipped = 0;
            int sourceIndex = 0;
            float minimumWorldX = float.MaxValue;
            float minimumWorldY = float.MaxValue;
            float minimumWorldZ = float.MaxValue;
            float maximumWorldX = float.MinValue;
            float maximumWorldY = float.MinValue;
            float maximumWorldZ = float.MinValue;
            float minimumTerrainZ = float.MaxValue;
            float maximumTerrainZ = float.MinValue;
            HashSet<string> authoredPlacementSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (XmlNode node in nodes)
            {
                int componentIndex = sourceIndex++;
                XmlElement entity = node as XmlElement;
                XmlElement transform = entity == null ? null : entity["transform"];
                if (entity == null || transform == null)
                {
                    skipped++;
                    Diagnostics.Info("Refuge placement component. Index=" + componentIndex
                        + "; Result=Skipped; Reason=MissingEntityOrTransform.");
                    continue;
                }

                string prefabId = entity.GetAttribute("old_prefab_name");
                if (string.IsNullOrWhiteSpace(prefabId))
                {
                    prefabId = entity.GetAttribute("prefab");
                }
                if (string.IsNullOrWhiteSpace(prefabId)
                    || string.Equals(prefabId, "empty_object", StringComparison.Ordinal)
                    || string.Equals(prefabId, "envmap_probe", StringComparison.Ordinal)
                    || string.Equals(prefabId, "game_entity", StringComparison.Ordinal))
                {
                    skipped++;
                    Diagnostics.Info("Refuge placement component. Index=" + componentIndex
                        + "; SourceName=" + entity.GetAttribute("name")
                        + "; Result=Skipped; Reason=NoRuntimePrefabReference.");
                    continue;
                }

                float localX;
                float localY;
                float localZ;
                Vec3 authoredEuler;
                if (!TryReadTransform(transform, out localX, out localY, out localZ, out authoredEuler))
                {
                    skipped++;
                    Diagnostics.Info("Refuge placement component. Index=" + componentIndex
                        + "; Prefab=" + prefabId
                        + "; Result=Skipped; Reason=InvalidAuthoredTransform.");
                    continue;
                }

                // The editor export contains six exact duplicate
                // battania_castle_corner records: a complete expanded entity
                // followed later by a thin prefab reference at the identical
                // frame. Native root loading would overlap both. The safe
                // component importer must preserve repeated assets at distinct
                // frames while rejecting only byte-equivalent placements.
                string placementSignature = prefabId
                    + "|" + transform.GetAttribute("position")
                    + "|" + transform.GetAttribute("rotation_euler")
                    + "|" + transform.GetAttribute("scale");
                if (!authoredPlacementSignatures.Add(placementSignature))
                {
                    skipped++;
                    Diagnostics.Info("Refuge placement component. Index=" + componentIndex
                        + "; Prefab=" + prefabId
                        + "; Result=Skipped; Reason=ExactDuplicateAuthoredPlacement.");
                    continue;
                }

                // Reconstruct the complete local frame with Bannerlord's own
                // prefab Euler convention, then compose it with the anchor.
                // Manual yaw-to-forward conversion introduces a 90-degree
                // axis error and discards authored pitch/roll, which is what
                // produced crossed walls, stairs, and towers in the earlier
                // component fallback.
                MatrixFrame localFrame = MatrixFrame.Identity;
                localFrame.origin = new Vec3(localX, localY, localZ);
                localFrame.rotation.ApplyEulerAngles(in authoredEuler);

                // The direct runtime recreation matches the editor-facing
                // wall orientation. Diagnostics verify the usable platform
                // direction against the compound center for every section.
                bool isPalisadeWall = string.Equals(
                    prefabId,
                    PalisadePrefabId,
                    StringComparison.Ordinal);

                Vec3 scale;
                bool hasScale = TryReadScale(transform, out scale);
                if (hasScale)
                {
                    localFrame.Scale(scale);
                }

                MatrixFrame frame = anchorFrame.TransformToParent(in localFrame);
                Vec3 position = frame.origin;
                float terrainHeight = Mission.Scene.GetTerrainHeight(position.AsVec2);
                float anchorTerrainHeight = Mission.Scene.GetTerrainHeight(anchorFrame.origin.AsVec2);
                bool followsTerrain = localZ <= TerrainFollowingLayoutMaximumLocalZ;
                if (followsTerrain)
                {
                    // Preserve the authored vertical offset from ground while
                    // allowing roots (walls, tents, houses, stairs) to follow
                    // the actual generated terrain beneath each footprint.
                    frame.origin.z += terrainHeight - anchorTerrainHeight;
                    position = frame.origin;
                }

                Vec2 platformVisualFacing = Vec2.Zero;
                float platformInwardDot = 0f;
                bool platformFacesInward = false;
                if (isPalisadeWall)
                {
                    // The native wall's usable platform side is opposite the
                    // corrected root-forward vector. This is the direction
                    // that must point toward the palisade center.
                    platformVisualFacing = new Vec2(
                        -frame.rotation.f.x,
                        -frame.rotation.f.y);
                    Vec2 towardCompoundCenter = new Vec2(
                        anchorFrame.origin.x - position.x,
                        anchorFrame.origin.y - position.y);
                    if (platformVisualFacing.LengthSquared > 0.001f
                        && towardCompoundCenter.LengthSquared > 0.001f)
                    {
                        platformVisualFacing.Normalize();
                        towardCompoundCenter.Normalize();
                        platformInwardDot = platformVisualFacing.x * towardCompoundCenter.x
                            + platformVisualFacing.y * towardCompoundCenter.y;
                        platformFacesInward = platformInwardDot > 0.25f;
                    }
                }

                bool placementSucceeded = TryPlaceAuthoredFortComponent(prefabId, frame);
                Diagnostics.Info(
                    "Refuge placement component. Index=" + componentIndex
                    + "; Prefab=" + prefabId
                    + "; Local=" + FormatDiagnosticVector(localX, localY, localZ)
                    + "; World=" + FormatDiagnosticVector(position.x, position.y, position.z)
                    + "; TerrainZ=" + FormatDiagnosticFloat(terrainHeight)
                    + "; GroundDelta=" + FormatDiagnosticFloat(position.z - terrainHeight)
                    + "; ElevationMode=" + (followsTerrain ? "FollowTerrain" : "LockedAuthoredHeight")
                    + "; Euler=" + FormatDiagnosticVector(
                        authoredEuler.x,
                        authoredEuler.y,
                        authoredEuler.z)
                    + "; PalisadePlatformFacingCorrection=None"
                    + "; PalisadePlatformVisualFacing=" + (isPalisadeWall
                        ? FormatDiagnosticVector(platformVisualFacing.x, platformVisualFacing.y, 0f)
                        : "N/A")
                    + "; PalisadePlatformInwardDot=" + (isPalisadeWall
                        ? FormatDiagnosticFloat(platformInwardDot)
                        : "N/A")
                    + "; PalisadePlatformFacesInward=" + (isPalisadeWall
                        ? platformFacesInward.ToString()
                        : "N/A")
                    + "; Creation=VisualOnlyNoPhysics"
                    + "; Scale=" + (hasScale
                        ? FormatDiagnosticVector(scale.x, scale.y, scale.z)
                        : "1.000,1.000,1.000")
                    + "; Result=" + (placementSucceeded ? "Placed" : "Failed") + ".");

                if (placementSucceeded)
                {
                    placed++;
                    minimumWorldX = Math.Min(minimumWorldX, position.x);
                    minimumWorldY = Math.Min(minimumWorldY, position.y);
                    minimumWorldZ = Math.Min(minimumWorldZ, position.z);
                    maximumWorldX = Math.Max(maximumWorldX, position.x);
                    maximumWorldY = Math.Max(maximumWorldY, position.y);
                    maximumWorldZ = Math.Max(maximumWorldZ, position.z);
                    minimumTerrainZ = Math.Min(minimumTerrainZ, terrainHeight);
                    maximumTerrainZ = Math.Max(maximumTerrainZ, terrainHeight);
                }
                else
                {
                    skipped++;
                }
            }

            Diagnostics.Info("Complete authored refuge component placement. Placed="
                + placed + "; Skipped=" + skipped
                + "; Anchor=" + FormatDiagnosticVector(
                    anchorFrame.origin.x,
                    anchorFrame.origin.y,
                    anchorFrame.origin.z)
                + (placed > 0
                    ? "; BoundsMin=" + FormatDiagnosticVector(minimumWorldX, minimumWorldY, minimumWorldZ)
                        + "; BoundsMax=" + FormatDiagnosticVector(maximumWorldX, maximumWorldY, maximumWorldZ)
                        + "; TerrainRange=" + FormatDiagnosticFloat(maximumTerrainZ - minimumTerrainZ)
                    : string.Empty)
                + ".");
            return placed;
        }

        private static string FormatDiagnosticFloat(float value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }

        private static string FormatDiagnosticVector(float x, float y, float z)
        {
            return FormatDiagnosticFloat(x) + ","
                + FormatDiagnosticFloat(y) + ","
                + FormatDiagnosticFloat(z);
        }

        private static bool TryReadTransform(
            XmlElement transform,
            out float x,
            out float y,
            out float z,
            out Vec3 euler)
        {
            x = y = z = 0f;
            euler = Vec3.Zero;
            if (!TryReadVector(transform.GetAttribute("position"), out x, out y, out z))
            {
                return false;
            }

            // Scene XML omits rotation_euler for its default identity
            // transform.  Those pieces are valid authored fort components;
            // do not silently drop them from the imported layout.
            string rotation = transform.GetAttribute("rotation_euler");
            if (string.IsNullOrWhiteSpace(rotation))
            {
                return true;
            }

            float pitch;
            float roll;
            float yaw;
            if (!TryReadVector(rotation, out pitch, out roll, out yaw))
            {
                return false;
            }

            euler = new Vec3(pitch, roll, yaw);
            return true;
        }

        private static bool TryReadScale(XmlElement transform, out Vec3 scale)
        {
            float x;
            float y;
            float z;
            if (TryReadVector(transform.GetAttribute("scale"), out x, out y, out z))
            {
                scale = new Vec3(x, y, z);
                return true;
            }

            scale = Vec3.One;
            return false;
        }

        private static bool TryReadVector(string value, out float x, out float y, out float z)
        {
            x = y = z = 0f;
            string[] parts = (value ?? string.Empty).Split(',');
            return parts.Length == 3
                && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z);
        }

        private bool TryPlaceAuthoredFortComponent(string prefabId, MatrixFrame frame)
        {
            try
            {
                // The frame overload always creates physics.  That produced
                // 73 independent native physics/script hierarchies from a
                // flattened editor export and the engine later faulted in
                // native code.  Instantiate each visual-only, then move it
                // into its already-composed authored frame.
                GameEntity entity = GameEntity.Instantiate(
                    Mission.Scene,
                    prefabId,
                    callScriptCallbacks: false,
                    createPhysics: false,
                    scriptInclusingTag: string.Empty);
                if (entity == null)
                {
                    Diagnostics.Info("Skipped unavailable rct_refuge_fort component: " + prefabId + ".");
                    return false;
                }

                entity.SetGlobalFrame(frame, true);
                TrackRuntimeLayoutPlacement(prefabId, frame);
                return true;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Failed to place rct_refuge_fort component: " + prefabId + ".", exception);
                return false;
            }
        }

        private MatrixFrame FindRefugeAnchorFrame()
        {
            // Campaign-patch camps cache their surveyed clearing in the same
            // frame used for player spawning. They are not registered native
            // profiles and intentionally contain no rct_refuge_anchor marker.
            if (_hasCachedNativeAnchorFrame)
            {
                return _cachedNativeAnchorFrame;
            }

            NativeSceneProfile nativeProfile;
            if (TryGetNativeSceneProfile(out nativeProfile))
            {
                if (!_hasCachedNativeAnchorFrame)
                {
                    FindPlayerSpawnFrame();
                }

                if (_hasCachedNativeAnchorFrame)
                {
                    return _cachedNativeAnchorFrame;
                }

                throw new InvalidOperationException("The native refuge anchor was not calibrated from a clear footprint.");
            }

            WeakGameEntity anchor = Mission.Scene.FindWeakEntityWithTag(RefugeAnchorTag);
            if (anchor.IsValid)
            {
                return anchor.GetGlobalFrame();
            }

            throw new InvalidOperationException("The refuge scene is missing its anchor marker.");
        }

        internal bool TryGetLayoutAnchorFrame(out MatrixFrame frame)
        {
            try
            {
                frame = FindRefugeAnchorFrame();
                return true;
            }
            catch (Exception exception)
            {
                frame = MatrixFrame.Identity;
                Diagnostics.Error("Refuge builder could not resolve the layout anchor.", exception);
                return false;
            }
        }

        internal List<RefugePrefabPlacement> GetRuntimeLayoutPlacements()
        {
            return new List<RefugePrefabPlacement>(_runtimeLayoutPlacements);
        }

        private bool TryGetNativeSceneProfile(out NativeSceneProfile profile)
        {
            for (int index = 0; index < NativeSceneProfiles.Length; index++)
            {
                if (string.Equals(NativeSceneProfiles[index].SceneId, _sceneId, StringComparison.Ordinal))
                {
                    profile = NativeSceneProfiles[index];
                    return true;
                }
            }

            profile = default(NativeSceneProfile);
            return false;
        }

        private MatrixFrame GetNativeProfileAnchorFrame(NativeSceneProfile profile)
        {
            int faceCount = Mission.Scene.GetNavMeshFaceCount();
            if (profile.AnchorNavMeshFace < 0 || profile.AnchorNavMeshFace >= faceCount)
            {
                throw new InvalidOperationException("The calibrated native refuge anchor is outside this scene's navmesh.");
            }

            Vec3 anchor = Vec3.Zero;
            Mission.Scene.GetNavMeshCenterPosition(profile.AnchorNavMeshFace, ref anchor);
            if (!anchor.IsValid)
            {
                throw new InvalidOperationException("The calibrated native refuge anchor is invalid.");
            }

            Vec3 forward = new Vec3(
                (float)Math.Sin(profile.HeadingRadians),
                (float)Math.Cos(profile.HeadingRadians),
                0f);
            MatrixFrame frame = MatrixFrame.Identity;
            frame.rotation = Mat3.CreateMat3WithForward(forward);
            frame.origin = anchor;
            Diagnostics.Info(
                "Native refuge profile calibration. Scene=" + profile.SceneId
                + "; NavMeshFace=" + profile.AnchorNavMeshFace
                + "; Anchor=" + anchor.x.ToString("F2") + ","
                + anchor.y.ToString("F2") + "," + anchor.z.ToString("F2")
                + "; Heading=" + profile.HeadingRadians.ToString("F3") + ".");
            return frame;
        }

        private void PlaceStarterPalisade(Vec3 center, Vec2 side, Vec2 forward)
        {
            // Native wall sections are placed tangent to an ellipse. Together
            // they read as a curved palisade, while every section remains
            // world-up vertical. The southern section is deliberately omitted
            // for one open entrance; no runtime door or gatehouse blocks it.
            int gateSegmentIndex = PalisadeSegmentCount / 2;
            for (int index = 0; index < PalisadeSegmentCount; index++)
            {
                if (index == gateSegmentIndex)
                {
                    continue;
                }

                if (IsEntranceTowerSegment(index, gateSegmentIndex))
                {
                    Vec3 entranceTowerPosition;
                    Vec2 entranceTowerTangent;
                    GetPalisadeSegmentTransform(
                        center,
                        side,
                        forward,
                        index,
                        out entranceTowerPosition,
                        out entranceTowerTangent);
                    PlaceEntranceTower(entranceTowerPosition, entranceTowerTangent);
                    continue;
                }

                if (((_upgrades & RefugeUpgrade.GuardTowers) == RefugeUpgrade.GuardTowers
                        || _activeUpgrade == RefugeUpgrade.GuardTowers)
                    && IsGuardTowerSegment(index))
                {
                    continue;
                }

                Vec3 position;
                Vec2 tangent;
                GetPalisadeSegmentTransform(center, side, forward, index, out position, out tangent);

                PlaceUprightPalisadeSegment(position, tangent);
            }

        }

        private static bool IsGuardTowerSegment(int index)
        {
            // Rear wall, from left to right: blue tower, stair, red tower,
            // red tower, stair, blue tower. The stairs occupy 22 and 2.
            return index == 21 || index == 23 || index == 1 || index == 3;
        }

        private static bool IsEntranceTowerSegment(int index, int gateSegmentIndex)
        {
            return index == gateSegmentIndex - 1 || index == gateSegmentIndex + 1;
        }

        private static void GetPalisadeSegmentTransform(
            Vec3 center,
            Vec2 side,
            Vec2 forward,
            int index,
            out Vec3 position,
            out Vec2 tangent)
        {
            double angle = Math.PI * 2d * index / PalisadeSegmentCount;
            float sin = (float)Math.Sin(angle);
            float cos = (float)Math.Cos(angle);
            position = Offset(
                center,
                side,
                forward,
                PalisadeSideRadius * sin,
                PalisadeForwardRadius * cos);

            // This is a circle, so the derivative gives the wall's long-axis
            // tangent. The mesh's long edge is local side, not local forward;
            // PlaceUprightPalisadeSegment converts this tangent to the correct
            // inward-facing direction when it creates the frame.
            tangent = new Vec2(
                side.x * PalisadeSideRadius * cos
                    - forward.x * PalisadeForwardRadius * sin,
                side.y * PalisadeSideRadius * cos
                    - forward.y * PalisadeForwardRadius * sin);
            if (tangent.LengthSquared < 0.001f)
            {
                tangent = side;
            }
            else
            {
                tangent.Normalize();
            }
        }

        private bool PlaceUprightPalisadeSegment(Vec3 center, Vec2 wallTangent)
        {
            float highestTerrain = float.MinValue;
            for (int sampleIndex = -2; sampleIndex <= 2; sampleIndex++)
            {
                float along = PalisadeHalfSegmentLength * sampleIndex / 2f;
                Vec2 sample = new Vec2(
                    center.x + wallTangent.x * along,
                    center.y + wallTangent.y * along);
                highestTerrain = Math.Max(
                    highestTerrain,
                    Mission.Scene.GetTerrainHeight(sample));
            }

            center.z = (highestTerrain == float.MinValue
                ? Mission.Scene.GetTerrainHeight(center.AsVec2)
                : highestTerrain) + PropGroundClearance - PalisadeBurialDepth;
            // castle_plank_wall_a is long along its local side axis. Point its
            // local forward toward the ring center so the fighting platform is
            // inside the refuge, while retaining the tangent as its long axis.
            // The opposite perpendicular points outward and puts the platform
            // on the exterior of the palisade.
            Vec2 wallFacing = new Vec2(wallTangent.y, -wallTangent.x);
            return TryPlaceLevelPrefab(PalisadePrefabId, center, wallFacing);
        }

        private void PlaceUpgradeSockets(Vec3 center, Vec2 side, Vec2 forward)
        {
            // All locations are inside the palisade. The protected stash is
            // specifically positioned just right of the gate, never outside.
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.Barracks,
                BarracksPrefabId,
                Offset(center, side, forward, -15f, 10f),
                forward);
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.Tavern,
                TentPrefabId,
                Offset(center, side, forward, 0f, 17f),
                new Vec2(-forward.x, -forward.y));
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.StaffTents,
                StaffTentPrefabId,
                Offset(center, side, forward, 15f, 10f),
                new Vec2(-forward.x, -forward.y));
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.SleepingQuarters,
                QuartersPrefabId,
                Offset(center, side, forward, -17f, -3f),
                forward);
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.Blacksmith,
                StoragePrefabId,
                Offset(center, side, forward, 17f, -3f),
                forward);
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.Stash,
                StoragePrefabId,
                Offset(center, side, forward, 15f, -12f),
                forward);
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.Infirmary,
                TentPrefabId,
                Offset(center, side, forward, -11f, -13f),
                forward);
            PlaceUpgradeOrConstruction(
                RefugeUpgrade.TrainingYard,
                StoragePrefabId,
                Offset(center, side, forward, 7f, -17f),
                forward);

            Vec3 blueLeft;
            Vec2 blueLeftTangent;
            GetPalisadeSegmentTransform(center, side, forward, 21, out blueLeft, out blueLeftTangent);
            Vec3 redLeft;
            Vec2 redLeftTangent;
            GetPalisadeSegmentTransform(center, side, forward, 23, out redLeft, out redLeftTangent);
            Vec3 redRight;
            Vec2 redRightTangent;
            GetPalisadeSegmentTransform(center, side, forward, 1, out redRight, out redRightTangent);
            Vec3 blueRight;
            Vec2 blueRightTangent;
            GetPalisadeSegmentTransform(center, side, forward, 3, out blueRight, out blueRightTangent);
            if ((_upgrades & RefugeUpgrade.GuardTowers) == RefugeUpgrade.GuardTowers)
            {
                PlaceGuardTower(blueLeft, blueLeftTangent);
                PlaceGuardTower(redLeft, redLeftTangent);
                PlaceGuardTower(redRight, redRightTangent);
                PlaceGuardTower(blueRight, blueRightTangent);
            }
            else if (_activeUpgrade == RefugeUpgrade.GuardTowers)
            {
                PlaceGuardTowerConstruction(blueLeft, blueLeftTangent);
                PlaceGuardTowerConstruction(redLeft, redLeftTangent);
                PlaceGuardTowerConstruction(redRight, redRightTangent);
                PlaceGuardTowerConstruction(blueRight, blueRightTangent);
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

            if (_activeUpgrade == upgrade)
            {
                PlaceConstructionStage(position, direction);
                return;
            }

            // An unbuilt socket is intentionally invisible. A fresh refuge
            // therefore contains only its main tent and palisade; materials
            // appear only after a real construction order is started.
        }

        private void PlaceConstructionStage(Vec3 position, Vec2 direction)
        {
            // Construction is deliberately made from lightweight props. It
            // stays inside a fixed socket and never introduces a new route
            // blocker outside the authored/native scene navmesh.
            if (_activeUpgradeProgress < 0.34f)
            {
                PlaceGroundPrefab(StoragePrefabId, position, direction);
                return;
            }

            if (!TryPlaceGroundPrefab(WatchTowerScaffoldPrefabId, position, direction))
            {
                PlaceGroundPrefab(StoragePrefabId, position, direction);
            }

            if (_activeUpgradeProgress >= 0.67f)
            {
                PlaceGroundPrefab(StoragePrefabId,
                    Offset(position, direction, new Vec2(direction.y, -direction.x), 2f, 0f), direction);
            }
        }

        private void PlaceGuardTower(Vec3 position, Vec2 direction)
        {
            // The four completed Guard Towers occupy the two rear red sockets
            // and the two side blue sockets from the refuge layout. Use the
            // same narrow/tall proportion as the player-authored fort scene.
            if (!TryPlaceScaledGroundPrefab(
                    WatchTowerPrefabId,
                    position,
                    direction,
                    new Vec3(0.347f, 0.355f, 1.435f)))
            {
                PlaceGroundPrefab("wooden_platform_2_a", position, direction);
            }
        }

        private void PlaceEntranceTower(Vec3 position, Vec2 wallTangent)
        {
            // The entrance is always flanked by these two compact towers. The
            // gate itself remains open; only the later perimeter towers are
            // tied to the Guard Towers upgrade.
            Vec2 inward = new Vec2(wallTangent.y, -wallTangent.x);
            if (!TryPlaceScaledGroundPrefab(
                    WatchTowerPrefabId,
                    position,
                    inward,
                    new Vec3(0.347f, 0.355f, 1.435f)))
            {
                PlaceGroundPrefab("wooden_platform_2_a", position, inward);
            }
        }

        private void PlaceGateStairAtSegment(
            Vec3 center,
            Vec2 side,
            Vec2 forward,
            int wallSegmentIndex)
        {
            Vec3 wallPosition;
            Vec2 wallTangent;
            GetPalisadeSegmentTransform(
                center,
                side,
                forward,
                wallSegmentIndex,
                out wallPosition,
                out wallTangent);

            Vec2 inward = new Vec2(wallTangent.y, -wallTangent.x);
            Vec3 stairFoot = wallPosition;
            stairFoot.x += inward.x * 4.5f;
            stairFoot.y += inward.y * 4.5f;
            Vec2 climbTowardWall = new Vec2(-inward.x, -inward.y);
            TryPlaceScaledGroundPrefab(
                GateStairsPrefabId,
                stairFoot,
                climbTowardWall,
                new Vec3(0.75f, 0.75f, 0.55f));
        }

        private void PlaceGuardTowerConstruction(Vec3 position, Vec2 direction)
        {
            if (_activeUpgradeProgress < 0.34f)
            {
                if (!TryPlaceGroundPrefab(WatchTowerFoundationPrefabId, position, direction))
                {
                    PlaceGroundPrefab(StoragePrefabId, position, direction);
                }
                return;
            }

            if (_activeUpgradeProgress < 0.67f)
            {
                if (!TryPlaceGroundPrefab(WatchTowerScaffoldPrefabId, position, direction))
                {
                    PlaceGroundPrefab("wooden_platform_2_a", position, direction);
                }
                return;
            }

            // The final third is visibly near completion but still avoids
            // using a finished tower until the campaign construction ends.
            if (!TryPlaceGroundPrefab(WatchTowerScaffoldPrefabId, position, direction))
            {
                PlaceGroundPrefab("wooden_platform_2_a", position, direction);
            }
            PlaceGroundPrefab(StoragePrefabId, Offset(position, direction, new Vec2(direction.y, -direction.x), 2f, 0f), direction);
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

        private void PlaceMainTent(Vec3 center, Vec2 side, Vec2 forward)
        {
            // Keep the tent level, but raise its base to the highest terrain
            // sample beneath its complete footprint. This avoids embedding a
            // corner of the native tent when the native scene is mildly sloped.
            float supportHeight = FindHighestTerrainUnderTent(center, side, forward);
            if (_isCampOnly && _hasPortableAnchor && Math.Abs(_portableAnchor.z) > 0.001f)
            {
                // Coast land can be a placed shoreline mesh above the terrain
                // bed. Never snap a saved solid-surface anchor back down to
                // the seabed when positioning the portable tent.
                supportHeight = Math.Max(supportHeight, _portableAnchor.z);
            }
            center.z = supportHeight + TentGroundClearance;
            TryPlaceLevelPrefab(TentPrefabId, center, forward);
        }

        private void PlaceGroundPrefab(string prefabId, Vec3 position, Vec2 forward)
        {
            position.z = Mission.Scene.GetTerrainHeight(position.AsVec2) + GetPrefabGroundOffset(prefabId);
            TryPlaceLevelPrefab(prefabId, position, forward);
        }

        private bool TryPlaceGroundPrefab(string prefabId, Vec3 position, Vec2 forward)
        {
            position.z = Mission.Scene.GetTerrainHeight(position.AsVec2) + GetPrefabGroundOffset(prefabId);
            return TryPlaceLevelPrefab(prefabId, position, forward);
        }

        private static float GetPrefabGroundOffset(string prefabId)
        {
            return IsTentPrefab(prefabId) ? TentGroundClearance : PropGroundClearance;
        }

        private static bool IsTentPrefab(string prefabId)
        {
            return !string.IsNullOrEmpty(prefabId)
                && prefabId.IndexOf("tent", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool TryPlaceScaledGroundPrefab(
            string prefabId,
            Vec3 position,
            Vec2 forward,
            Vec3 scale)
        {
            position.z = Mission.Scene.GetTerrainHeight(position.AsVec2) + PropGroundClearance;
            try
            {
                MatrixFrame frame = MatrixFrame.Identity;
                frame.rotation = Mat3.CreateMat3WithForward(new Vec3(forward.x, forward.y, 0f));
                frame.origin = position;
                frame.Scale(scale);
                GameEntity entity = InstantiateRefugePrefab(prefabId, frame);
                return entity != null;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Scaled refuge prefab could not be placed: " + prefabId + ".", exception);
                return false;
            }
        }

        private bool TryPlaceLevelPrefab(string prefabId, Vec3 position, Vec2 forward)
        {
            try
            {
                MatrixFrame frame = MatrixFrame.Identity;
                frame.rotation = Mat3.CreateMat3WithForward(new Vec3(forward.x, forward.y, 0f));
                frame.origin = position;
                GameEntity entity = InstantiateRefugePrefab(prefabId, frame);
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

        private GameEntity InstantiateRefugePrefab(string prefabId, MatrixFrame frame)
        {
            // Instantiate registered prefabs first, then move the resulting
            // hierarchy into position. The engine's initial-frame overload
            // can access invalid native memory for a large exported prefab.
            if (!_collisionInstantiationUnavailable)
            {
                try
                {
                    GameEntity visualEntity = GameEntity.Instantiate(
                        Mission.Scene,
                        prefabId,
                        callScriptCallbacks: false,
                        // Runtime collision is intentionally disabled for
                        // every generated refuge visual. The editor workshop
                        // owns the real collision and navmesh bake; native
                        // physics creation here has already produced an access
                        // violation while unloading the mission.
                        createPhysics: false,
                        scriptInclusingTag: string.Empty);
                    if (visualEntity != null)
                    {
                        visualEntity.SetGlobalFrame(frame, true);
                        TrackRuntimeLayoutPlacement(prefabId, frame);
                        Diagnostics.Info("Placed refuge runtime visual without physics. Prefab=" + prefabId + ".");
                        return visualEntity;
                    }
                }
                catch (Exception exception)
                {
                    // Some Bannerlord builds reject physics creation for
                    // runtime prefabs. Disable that path once, then preserve
                    // the proven visual layout instead of losing the refuge.
                    _collisionInstantiationUnavailable = true;
                    Diagnostics.Error(
                        "Runtime physics creation was rejected; using the safe visual fallback for this refuge visit.",
                        exception);
                }
            }

            RefugeFortPrefabDefinition fort;
            if (RefugeFortPrefabCatalog.TryGet(prefabId, out fort) && fort.RequiresSceneLink)
            {
                // Never retry the authored fort through the unsafe
                // initial-frame overload after physics creation fails.
                Diagnostics.Error(
                    "The authored refuge fort could not be instantiated through the supported physics-aware engine path.",
                    new InvalidOperationException("Authored refuge prefab instantiation failed safely."));
                return null;
            }

            GameEntity entity = GameEntity.Instantiate(Mission.Scene, prefabId, frame, false);
            if (entity != null)
            {
                TrackRuntimeLayoutPlacement(prefabId, frame);
            }
            return entity;
        }

        private void TrackRuntimeLayoutPlacement(string prefabId, MatrixFrame frame)
        {
            _runtimeLayoutPlacements.Add(new RefugePrefabPlacement(prefabId, frame));
        }

    }


    internal sealed class RefugePrefabPlacement
    {
        internal RefugePrefabPlacement(string prefabId, MatrixFrame frame)
        {
            PrefabId = prefabId;
            Frame = frame;
        }

        internal string PrefabId { get; }
        internal MatrixFrame Frame { get; }
    }
}
