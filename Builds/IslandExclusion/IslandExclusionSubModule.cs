using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AgesOfCalradia.IslandExclusion
{
    /// <summary>
    /// Keeps the August 10 13:52 renderer intact, removes selected disconnected
    /// islands, and consistently includes both requested enclosed lakes.
    /// </summary>
    public sealed class IslandExclusionSubModule : MBSubModuleBase
    {
        private const string HarmonyId = "AgesOfCalradia.ExactRenderedIslandExclusion.20260810";
        private static readonly object MaskLock = new object();
        private static MethodInfo _exactLandMethod;
        private static MethodInfo _countTriangleMethod;
        private static MethodInfo _takeFillEntitiesMethod;
        private static MethodInfo _takeFrontierEntitiesMethod;
        private static MethodInfo _addFrontierSegmentMethod;
        private static MethodInfo _addFrontierEntityMethod;
        private static MethodInfo _provinceRibbonMethod;
        private static MethodInfo _scaleOpaqueColorMethod;
        private static MethodInfo _createRowMeshMethod;
        private static MethodInfo _nearestSiteFindMethod;
        private static MethodInfo _touchesExteriorWaterMethod;
        private static MethodInfo _addRowEntityMethod;
        private static MethodInfo _applyPoliticalEntityVisibilityMethod;
        private static FieldInfo _territoryFillEntitiesField;
        private static FieldInfo _politicalLayerAlphaField;
        private static MethodInfo _trySampleExactHeightMethod;
        private static FieldInfo _terrainSceneField;
        private static MethodInfo _getWaterLevelAtPositionMethod;
        private static FieldInfo _landMaskField;
        private static MethodInfo _isAuthoredLandMethod;
        private static FieldInfo _projectionXField;
        private static FieldInfo _projectionYField;
        private static LegacyPoliticalLandMask _legacyPoliticalLandMask;
        private static MethodInfo _tryGetReferenceAnchorMethod;
        private static MethodInfo _diagnosticsInfo;
        private static MethodInfo _tryGetNativeTerrainMethod;
        private static MethodInfo _isProtectedWaterMethod;
        private static ExactIslandMask _mask;
        private static ExactLakeMask _lakeMask;
        private static object _sourceMapScene;
        private static bool _maskBuildFailed;
        private static long _exactClassifierCalls;
        private static long _targetLakeRegionCalls;
        private static long _targetWaterMatches;
        private static long _forcedLakeFillCount;
        private static long _battaniaWaterMatches;
        private static long _easternWaterMatches;
        private static bool _loggedFirstBattaniaMatch;
        private static bool _loggedFirstEasternMatch;
        private static long _liftedLakeTriangleCount;
        private static bool _loggedFirstLiftedTriangle;
        private static long _suppressedLakeTriangleCount;
        private static bool _loggedFirstSuppressedEasternTriangle;
        private static object _lakeCapSourceMapScene;
        private static readonly HashSet<object> LakeFrontierBuilders = new HashSet<object>();
        private static bool _addingCustomLakeFrontier;
        private static long _suppressedDisplacedEasternFrontierSegments;
        private static long _suppressedDisplacedProvinceRibbons;
        private static long _referenceControlColorCalls;
        private static long _referenceFillMaterialCalls;
        private static long _shiftedEasternOwnershipQueries;
        private static long _filledNativeRiverSamples;
        private static long _heightRecoveredPoliticalSamples;
        private static long _heightRecoveredExteriorTriangles;
        private static readonly HashSet<GameEntity> PoliticalWaterEntities = new HashSet<GameEntity>();
        private static readonly object PoliticalSettingsLock = new object();
        private static long _nextPoliticalSettingsRefreshTicks;
        private static int _cachedPoliticalOpacityPercent = 100;
        private static int _cachedPoliticalBrightnessPercent = 100;
        private static bool _cachedPoliticalSolidWater = true;

        [ThreadStatic]
        private static bool _buildingMask;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            Type terrainCacheType = AccessTools.TypeByName("TwelveMonthCalendar.CampaignMapTerrainGridCache");
            _exactLandMethod = AccessTools.Method(
                terrainCacheType,
                "IsPoliticalLandExact",
                new[] { typeof(Vec2), typeof(bool).MakeByRefType() });
            _trySampleExactHeightMethod = AccessTools.Method(
                terrainCacheType,
                "TrySampleExactHeight",
                new[] { typeof(Vec2), typeof(float).MakeByRefType() });
            _tryGetNativeTerrainMethod = AccessTools.Method(
                terrainCacheType,
                "TryGetNativeTerrain",
                new[] { typeof(Vec2), typeof(TerrainType).MakeByRefType() });
            _isProtectedWaterMethod = AccessTools.Method(
                terrainCacheType,
                "IsProtectedWater",
                new[] { typeof(TerrainType) });
            _terrainSceneField = AccessTools.Field(terrainCacheType, "_scene");
            _getWaterLevelAtPositionMethod = _terrainSceneField == null
                ? null
                : AccessTools.Method(
                    _terrainSceneField.FieldType,
                    "GetWaterLevelAtPosition",
                    new[] { typeof(Vec2), typeof(bool), typeof(bool) });
            _landMaskField = AccessTools.Field(terrainCacheType, "_landMask");
            _isAuthoredLandMethod = _landMaskField == null
                ? null
                : AccessTools.Method(_landMaskField.FieldType, "IsAuthoredLand", new[] { typeof(Vec2) });
            _projectionXField = _landMaskField == null
                ? null
                : AccessTools.Field(_landMaskField.FieldType, "_projectionX");
            _projectionYField = _landMaskField == null
                ? null
                : AccessTools.Field(_landMaskField.FieldType, "_projectionY");
            Type ledgerType = AccessTools.TypeByName("TwelveMonthCalendar.CalendarWorldLedgerVM");
            _tryGetReferenceAnchorMethod = ledgerType == null
                ? null
                : AccessTools.Method(
                    ledgerType,
                    "TryGetReferenceAnchor",
                    new[] { typeof(string), typeof(Vec2).MakeByRefType() });
            _legacyPoliticalLandMask = LegacyPoliticalLandMask.TryLoad();
            MethodInfo postfix = AccessTools.Method(
                typeof(IslandExclusionSubModule),
                nameof(ApplyExactIslandExclusion));
            Type fillBuilderType = AccessTools.TypeByName("TwelveMonthCalendar.CampaignPoliticalTerritoryFill+Builder");
            _countTriangleMethod = AccessTools.Method(fillBuilderType, "CountTriangle");
            _takeFillEntitiesMethod = AccessTools.Method(fillBuilderType, "TakeFillEntities");
            _takeFrontierEntitiesMethod = AccessTools.Method(fillBuilderType, "TakeFrontierEntities");
            _addFrontierSegmentMethod = AccessTools.Method(fillBuilderType, "AddFrontierSegment");
            _addFrontierEntityMethod = AccessTools.Method(fillBuilderType, "AddFrontierEntity");
            Type provinceBorderType = AccessTools.TypeByName(
                "TwelveMonthCalendar.CampaignStrategicProvinceBorderBehavior");
            _provinceRibbonMethod = AccessTools.Method(
                provinceBorderType,
                "AddDoubleSidedRibbon");
            Type politicalFillType = AccessTools.TypeByName(
                "TwelveMonthCalendar.CampaignPoliticalTerritoryFill");
            _scaleOpaqueColorMethod = AccessTools.Method(
                politicalFillType,
                "ScaleOpaqueColor",
                new[] { typeof(uint), typeof(uint) });
            _createRowMeshMethod = AccessTools.Method(
                fillBuilderType,
                "CreateRowMesh",
                new[] { typeof(int) });
            _touchesExteriorWaterMethod = AccessTools.Method(
                fillBuilderType,
                "TouchesExteriorWater",
                new[] { typeof(Vec3), typeof(Vec3), typeof(Vec3) });
            _addRowEntityMethod = AccessTools.Method(fillBuilderType, "AddRowEntity");
            Type politicalBehaviorType = AccessTools.TypeByName(
                "TwelveMonthCalendar.CampaignKingdomBorderBehavior");
            _applyPoliticalEntityVisibilityMethod = AccessTools.Method(
                politicalBehaviorType,
                "ApplyPoliticalEntityVisibility",
                new[] { typeof(bool) });
            _territoryFillEntitiesField = AccessTools.Field(
                politicalBehaviorType,
                "_territoryFillEntities");
            _politicalLayerAlphaField = AccessTools.Field(
                politicalBehaviorType,
                "_politicalLayerAlpha");
            Type nearestSiteType = AccessTools.TypeByName(
                "TwelveMonthCalendar.CampaignPoliticalTerritoryFill+NearestSiteIndex");
            _nearestSiteFindMethod = AccessTools.Method(
                nearestSiteType,
                "FindNearest",
                new[] { typeof(Vec2) });
            MethodInfo liftPrefix = AccessTools.Method(
                typeof(IslandExclusionSubModule),
                nameof(LiftTargetLakeTriangle));
            MethodInfo capPrefix = AccessTools.Method(
                typeof(IslandExclusionSubModule),
                nameof(AddTargetLakeCapEntities));

            Type diagnosticsType = AccessTools.TypeByName("TwelveMonthCalendar.Diagnostics");
            _diagnosticsInfo = diagnosticsType == null
                ? null
                : AccessTools.Method(diagnosticsType, "Info", new[] { typeof(string) });

            Harmony harmony = new Harmony(HarmonyId);
            if (_exactLandMethod != null && postfix != null)
            {
                harmony.Patch(_exactLandMethod, postfix: new HarmonyMethod(postfix));
            }
            MethodInfo controlColorPostfix = AccessTools.Method(
                typeof(IslandExclusionSubModule),
                nameof(ApplyReferenceControlColor));
            if (_scaleOpaqueColorMethod != null && controlColorPostfix != null)
            {
                harmony.Patch(
                    _scaleOpaqueColorMethod,
                    postfix: new HarmonyMethod(controlColorPostfix));
            }
            MethodInfo fillMaterialPostfix = AccessTools.Method(
                typeof(IslandExclusionSubModule),
                nameof(ApplyReferenceFillMaterial));
            if (_createRowMeshMethod != null && fillMaterialPostfix != null)
            {
                harmony.Patch(
                    _createRowMeshMethod,
                    postfix: new HarmonyMethod(fillMaterialPostfix));
            }
            MethodInfo exteriorTrianglePostfix = AccessTools.Method(
                typeof(IslandExclusionSubModule),
                nameof(AllowHeightGatedInteriorTriangle));
            if (_touchesExteriorWaterMethod != null && exteriorTrianglePostfix != null)
            {
                harmony.Patch(
                    _touchesExteriorWaterMethod,
                    postfix: new HarmonyMethod(exteriorTrianglePostfix));
            }
            if (_countTriangleMethod != null && liftPrefix != null)
            {
                harmony.Patch(
                    _countTriangleMethod,
                    prefix: new HarmonyMethod(liftPrefix));
            }
            MethodInfo waterEntityPostfix = AccessTools.Method(
                typeof(IslandExclusionSubModule),
                nameof(TrackPoliticalWaterEntity));
            if (_addRowEntityMethod != null && waterEntityPostfix != null)
            {
                harmony.Patch(
                    _addRowEntityMethod,
                    postfix: new HarmonyMethod(waterEntityPostfix));
            }
            MethodInfo configurableAlphaPostfix = AccessTools.Method(
                typeof(IslandExclusionSubModule),
                nameof(ApplyConfigurablePoliticalAlpha));
            if (_applyPoliticalEntityVisibilityMethod != null && configurableAlphaPostfix != null)
            {
                harmony.Patch(
                    _applyPoliticalEntityVisibilityMethod,
                    postfix: new HarmonyMethod(configurableAlphaPostfix));
            }
            WriteLog(
                "The approved southwest and northern island exclusions are enabled; "
                + "inland rivers and enclosed water use the Kingdom Frontiers height-gated fill rule; "
                + "lake exclusions and custom lake-border displacement are disabled. "
                + "Reference control-color styling remains enabled.");
        }

        private static void ShiftEasternOwnershipQuery(ref Vec2 point)
        {
            // Move only the ownership sampling strip east of the lake onto the
            // Chanopsis/Popsia side. The excluded lake itself never reaches
            // FindNearest, so the visible split follows its eastern shoreline.
            if (point.x < 662f || point.x > 696f
                || point.y < 378f || point.y > 446f)
            {
                return;
            }

            Vec2 original = point;
            point.x = 704f;
            _shiftedEasternOwnershipQueries++;
            if (_shiftedEasternOwnershipQueries == 1)
            {
                WriteLog(
                    "First east-lake ownership query shifted to Chanopsis/Popsia: original=("
                    + original.x.ToString("F2") + "," + original.y.ToString("F2")
                    + "); query=(" + point.x.ToString("F2") + ","
                    + point.y.ToString("F2") + ").");
            }
        }

        private static void ApplyReferenceControlColor(
            uint color,
            uint brightnessPercent,
            ref uint __result)
        {
            // Kingdom Frontiers uses the faction's full RGB with FF vertex
            // alpha. Opaque land vertices prevent the dark native river texture
            // from showing through triangles already routed into the land mesh.
            if (brightnessPercent == 50u)
            {
                int configuredBrightness = GetPoliticalBrightnessPercent();
                uint red = Math.Min(255u, ((color >> 16) & 0xFFu) * (uint)configuredBrightness / 100u);
                uint green = Math.Min(255u, ((color >> 8) & 0xFFu) * (uint)configuredBrightness / 100u);
                uint blue = Math.Min(255u, (color & 0xFFu) * (uint)configuredBrightness / 100u);
                __result = 0xFF000000u | (red << 16) | (green << 8) | blue;
                _referenceControlColorCalls++;
                if (_referenceControlColorCalls == 1)
                {
                    WriteLog(
                        "Reference control-color hook executed: input=0x"
                        + color.ToString("X8") + "; output=0x"
                        + __result.ToString("X8") + ".");
                }
            }
        }

        private static void ApplyReferenceFillMaterial(int renderOrder, ref Mesh __result)
        {
            if (renderOrder != 100 || __result == null) return;
            string material = GetPoliticalOpacityPercent() < 100
                ? "vertex_color_blend_mat"
                : "vertex_color_mat";
            __result.SetMaterial(material);
            _referenceFillMaterialCalls++;
            if (_referenceFillMaterialCalls == 1)
            {
                WriteLog("Configurable political-fill material applied: " + material
                    + "; opacity=" + GetPoliticalOpacityPercent()
                    + "; brightness=" + GetPoliticalBrightnessPercent()
                    + "; solidWater=" + GetPoliticalSolidWater() + ".");
            }
        }

        private static void TrackPoliticalWaterEntity(object __instance, bool riverCap)
        {
            if (!riverCap || __instance == null) return;
            try
            {
                FieldInfo entitiesField = AccessTools.Field(__instance.GetType(), "_entities");
                List<GameEntity> entities = entitiesField == null
                    ? null
                    : entitiesField.GetValue(__instance) as List<GameEntity>;
                if (entities == null || entities.Count == 0) return;
                GameEntity entity = entities[entities.Count - 1];
                if (entity == null) return;
                lock (PoliticalWaterEntities) PoliticalWaterEntities.Add(entity);
            }
            catch
            {
            }
        }

        private static void ApplyConfigurablePoliticalAlpha(object __instance)
        {
            if (__instance == null
                || _territoryFillEntitiesField == null
                || _politicalLayerAlphaField == null)
            {
                return;
            }

            try
            {
                List<GameEntity> entities = _territoryFillEntitiesField.GetValue(__instance)
                    as List<GameEntity>;
                if (entities == null) return;
                float layerAlpha = (float)_politicalLayerAlphaField.GetValue(__instance);
                float mainlandOpacity = GetPoliticalOpacityPercent() / 100f;
                bool solidWater = GetPoliticalSolidWater();
                foreach (GameEntity entity in entities)
                {
                    if (entity == null) continue;
                    bool waterEntity;
                    lock (PoliticalWaterEntities) waterEntity = PoliticalWaterEntities.Contains(entity);
                    entity.SetAlpha(layerAlpha * (solidWater && waterEntity ? 1f : mainlandOpacity));
                }
            }
            catch
            {
            }
        }

        private static int GetPoliticalOpacityPercent()
        {
            RefreshPoliticalSettingsSnapshot();
            return _cachedPoliticalOpacityPercent;
        }

        private static int GetPoliticalBrightnessPercent()
        {
            RefreshPoliticalSettingsSnapshot();
            return _cachedPoliticalBrightnessPercent;
        }

        private static bool GetPoliticalSolidWater()
        {
            RefreshPoliticalSettingsSnapshot();
            return _cachedPoliticalSolidWater;
        }

        private static void RefreshPoliticalSettingsSnapshot()
        {
            long now = DateTime.UtcNow.Ticks;
            if (now < _nextPoliticalSettingsRefreshTicks) return;

            lock (PoliticalSettingsLock)
            {
                now = DateTime.UtcNow.Ticks;
                if (now < _nextPoliticalSettingsRefreshTicks) return;

                object opacity = GetMcmSetting("PoliticalControlOpacityPercent");
                object brightness = GetMcmSetting("PoliticalControlBrightnessPercent");
                object solidWater = GetMcmSetting("PoliticalSolidWater");
                if (opacity is int)
                {
                    _cachedPoliticalOpacityPercent = Math.Max(10, Math.Min(100, (int)opacity));
                }
                if (brightness is int)
                {
                    _cachedPoliticalBrightnessPercent = Math.Max(25, Math.Min(125, (int)brightness));
                }
                if (solidWater is bool)
                {
                    _cachedPoliticalSolidWater = (bool)solidWater;
                }

                // Political mesh construction can query this setting millions
                // of times. Reflect at most once per second, not once per map
                // sample; UI changes still become visible promptly.
                _nextPoliticalSettingsRefreshTicks = now + TimeSpan.TicksPerSecond;
            }
        }

        private static object GetMcmSetting(string propertyName)
        {
            try
            {
                Type settingsType = AccessTools.TypeByName(
                    "AgesOfCalradia.MCM.CalendarMcmSettings");
                if (settingsType != null)
                {
                    PropertyInfo instanceProperty = AccessTools.Property(settingsType, "Instance");
                    object instance = instanceProperty == null
                        ? null
                        : instanceProperty.GetValue(null, null);
                    PropertyInfo settingProperty = AccessTools.Property(settingsType, propertyName);
                    if (instance != null && settingProperty != null)
                    {
                        return settingProperty.GetValue(instance, null);
                    }
                }

                // The native Calendar Options tab writes directly to the core
                // settings state when MCM is unavailable. Read the same value
                // here so either front end controls the renderer immediately.
                Type stateType = AccessTools.TypeByName(
                    "TwelveMonthCalendar.CalendarSettingsState");
                PropertyInfo stateProperty = stateType == null
                    ? null
                    : AccessTools.Property(stateType, propertyName);
                return stateProperty == null
                    ? null
                    : stateProperty.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        private static bool SuppressDisplacedProvinceRibbon(
            Vec3 first,
            Vec3 second,
            ref int __result)
        {
            Vec2 midpoint = new Vec2(
                (first.x + second.x) * 0.5f,
                (first.y + second.y) * 0.5f);
            bool displaced = midpoint.x >= 662f && midpoint.x <= 696f
                && midpoint.y >= 378f && midpoint.y <= 446f;
            if (!displaced) return true;

            __result = 0;
            _suppressedDisplacedProvinceRibbons++;
            if (_suppressedDisplacedProvinceRibbons == 1)
            {
                WriteLog(
                    "First displaced Chanopsis/Popsia province ribbon suppressed: midpoint=("
                    + midpoint.x.ToString("F2") + "," + midpoint.y.ToString("F2") + ").");
            }
            return false;
        }

        private static bool SuppressDisplacedEasternFrontier(Vec2 first, Vec2 second)
        {
            if (_addingCustomLakeFrontier) return true;

            Vec2 midpoint = (first + second) * 0.5f;
            // Removes only the obsolete north/south frontier through the
            // Chanopsis/Popsia mountain corridor. The replacement shoreline
            // lies west of this window and is added separately from the exact
            // 6:59 lake component.
            bool displaced = midpoint.x >= 662f && midpoint.x <= 696f
                && midpoint.y >= 378f && midpoint.y <= 446f;
            if (!displaced) return true;

            _suppressedDisplacedEasternFrontierSegments++;
            if (_suppressedDisplacedEasternFrontierSegments == 1)
            {
                WriteLog(
                    "First displaced Chanopsis/Popsia frontier segment suppressed: midpoint=("
                    + midpoint.x.ToString("F2") + "," + midpoint.y.ToString("F2") + ").");
            }
            return false;
        }

        private static void AddCompleteEasternLakeFrontier(object __instance)
        {
            if (__instance == null
                || _addFrontierSegmentMethod == null
                || _addFrontierEntityMethod == null
                || _terrainSceneField == null)
            {
                return;
            }

            lock (LakeFrontierBuilders)
            {
                if (LakeFrontierBuilders.Contains(__instance)) return;
                LakeFrontierBuilders.Add(__instance);
            }

            try
            {
                GetMask();
                if (_lakeMask == null) return;
                List<Tuple<Vec2, Vec2>> shoreline =
                    _lakeMask.GetBoundarySegments("eastern");
                if (shoreline.Count == 0) return;

                Scene scene = _terrainSceneField.GetValue(null) as Scene;
                if ((TaleWorlds.DotNet.NativeObject)(object)scene
                    == (TaleWorlds.DotNet.NativeObject)null)
                {
                    return;
                }

                Mesh mesh = Mesh.CreateMesh(true);
                if ((TaleWorlds.DotNet.NativeObject)(object)mesh
                    == (TaleWorlds.DotNet.NativeObject)null)
                {
                    return;
                }
                mesh.SetMaterial("vertex_color_mat");
                mesh.SetMeshRenderOrder(108);
                UIntPtr handle = mesh.LockEditDataWrite();
                int rendered = 0;
                try
                {
                    _addingCustomLakeFrontier = true;
                    foreach (Tuple<Vec2, Vec2> segment in shoreline)
                    {
                        object result = _addFrontierSegmentMethod.Invoke(
                            __instance,
                            new object[] { mesh, segment.Item1, segment.Item2, handle });
                        if (result is bool && (bool)result) rendered++;
                    }
                }
                finally
                {
                    _addingCustomLakeFrontier = false;
                    mesh.UnlockEditDataWrite(handle);
                }

                if (rendered <= 0) return;
                _addFrontierEntityMethod.Invoke(
                    __instance,
                    new object[] { scene, mesh });
                WriteLog(
                    "Complete eastern lake shoreline frontier added: sourceSegments="
                    + shoreline.Count + "; renderedSegments=" + rendered
                    + "; displacedSegmentsSuppressed="
                    + _suppressedDisplacedEasternFrontierSegments + ".");
            }
            catch (Exception exception)
            {
                WriteLog("Complete eastern lake shoreline frontier failed: " + exception + ".");
            }
        }

        private static bool SuppressLakeOverlappingTriangle(Vec3 first, Vec3 second, Vec3 third)
        {
            GetMask();
            if (_lakeMask == null) return true;

            Vec2 firstPoint = new Vec2(first.x, first.y);
            Vec2 secondPoint = new Vec2(second.x, second.y);
            Vec2 thirdPoint = new Vec2(third.x, third.y);
            Vec2 center = new Vec2(
                (first.x + second.x + third.x) / 3f,
                (first.y + second.y + third.y) / 3f);

            string region;
            if (!_lakeMask.TryGetOverlappingRegion(
                firstPoint, secondPoint, thirdPoint, out region))
            {
                return true;
            }

            _suppressedLakeTriangleCount++;
            if (region == "eastern" && !_loggedFirstSuppressedEasternTriangle)
            {
                _loggedFirstSuppressedEasternTriangle = true;
                WriteLog(
                    "First eastern lake-overlapping final triangle suppressed: center=("
                    + center.x.ToString("F2") + "," + center.y.ToString("F2")
                    + "); total=" + _suppressedLakeTriangleCount + ".");
            }
            return false;
        }

        private static void ApplyExactIslandExclusion(Vec2 point, ref bool nativeRiver, ref bool __result)
        {
            if (_buildingMask) return;

            _exactClassifierCalls++;
            ExactIslandMask mask = GetMask();

            // Exclusions win before any inland-water recovery. This preserves
            // the two approved archipelagos even where their terrain is high.
            if (mask != null && mask.Contains(point))
            {
                nativeRiver = false;
                __result = false;
                return;
            }

            string lakeRegion;
            if (_lakeMask != null
                && _lakeMask.TryGetRegion(point, out lakeRegion)
                && lakeRegion == "Battania")
            {
                nativeRiver = GetPoliticalSolidWater();
                __result = true;
                _forcedLakeFillCount++;
                if (!_loggedFirstBattaniaMatch)
                {
                    _loggedFirstBattaniaMatch = true;
                    WriteLog(
                        "First exact Battania lake component forced into political fill: x="
                        + point.x.ToString("F2") + "; y=" + point.y.ToString("F2") + ".");
                }
                return;
            }

            // Kingdom Frontiers renders territory from map height instead of
            // carving the fill by River/NonNavigableRiver terrain tags. Keep the
            // same 2.6 threshold here for rejected inland samples. Low open sea
            // remains empty, while rivers and enclosed water join the land mesh.
            if (!__result)
            {
                float mapHeight;
                if (TryGetKingdomFrontiersMapHeight(point, out mapHeight)
                    && mapHeight >= 2.6f)
                {
                    nativeRiver = GetPoliticalSolidWater();
                    __result = true;
                    _heightRecoveredPoliticalSamples++;
                    if (_heightRecoveredPoliticalSamples == 1)
                    {
                        WriteLog(
                            "First height-gated inland-water sample recovered into political fill: x="
                            + point.x.ToString("F2") + "; y=" + point.y.ToString("F2")
                            + "; height=" + mapHeight.ToString("F2") + ".");
                    }
                }
            }

            if (nativeRiver)
            {
                __result = true;
                if (!GetPoliticalSolidWater()) nativeRiver = false;
                _filledNativeRiverSamples++;
                if (_filledNativeRiverSamples == 1)
                {
                    WriteLog(
                        "First native-river sample redirected into political land fill: x="
                        + point.x.ToString("F2") + "; y=" + point.y.ToString("F2") + ".");
                }
            }

            if (!__result) return;
        }

        private static bool TryGetKingdomFrontiersMapHeight(Vec2 point, out float height)
        {
            height = 0f;
            try
            {
                if (Campaign.Current == null || Campaign.Current.MapSceneWrapper == null)
                {
                    return false;
                }

                CampaignVec2 campaignPoint = new CampaignVec2(point, isOnLand: false);
                return Campaign.Current.MapSceneWrapper.GetHeightAtPoint(campaignPoint, ref height)
                    && !float.IsNaN(height)
                    && !float.IsInfinity(height);
            }
            catch
            {
                return false;
            }
        }

        private static void AllowHeightGatedInteriorTriangle(
            Vec3 first,
            Vec3 second,
            Vec3 third,
            ref bool __result)
        {
            if (!__result) return;

            Vec2 center = new Vec2(
                (first.x + second.x + third.x) / 3f,
                (first.y + second.y + third.y) / 3f);
            ExactIslandMask mask = GetMask();
            if (mask != null && mask.Contains(center)) return;

            string lakeRegion;
            if (_lakeMask != null
                && _lakeMask.TryGetRegion(center, out lakeRegion)
                && lakeRegion == "Battania")
            {
                __result = false;
                _heightRecoveredExteriorTriangles++;
                return;
            }

            float mapHeight;
            if (!TryGetKingdomFrontiersMapHeight(center, out mapHeight)
                || mapHeight < 2.6f)
            {
                return;
            }

            __result = false;
            _heightRecoveredExteriorTriangles++;
            if (_heightRecoveredExteriorTriangles == 1)
            {
                WriteLog(
                    "First post-classifier exterior-water triangle retained by height gate: x="
                    + center.x.ToString("F2") + "; y=" + center.y.ToString("F2")
                    + "; height=" + mapHeight.ToString("F2") + ".");
            }
        }

        private static bool IsTargetLakeExclusion(Vec2 point)
        {
            // The 13:52 native map reports the visible lake surfaces as
            // Mountain / NonNavigableRiver, not water. These tight spans come
            // from the connected-component diagnostics and cover only the two
            // requested enclosed features. Surrounding mainland is already
            // political land, so forcing the span cannot cut mainland.
            bool battaniaLake;
            bool easternLake;
            GetTargetLakeSpan(point, out battaniaLake, out easternLake);
            if (!battaniaLake && !easternLake) return false;

            if (_landMaskField == null) return false;
            try
            {
                object landMask = _landMaskField.GetValue(null);
                if (landMask == null) return false;

                bool authoredLand;
                double[] projectionX = _projectionXField == null
                    ? null
                    : _projectionXField.GetValue(landMask) as double[];
                double[] projectionY = _projectionYField == null
                    ? null
                    : _projectionYField.GetValue(landMask) as double[];
                if (_legacyPoliticalLandMask != null
                    && projectionX != null && projectionX.Length >= 3
                    && projectionY != null && projectionY.Length >= 3)
                {
                    authoredLand = _legacyPoliticalLandMask.IsLand(point, projectionX, projectionY);
                }
                else
                {
                    if (_isAuthoredLandMethod == null) return false;
                    authoredLand = (bool)_isAuthoredLandMethod.Invoke(landMask, new object[] { point });
                }

                if (authoredLand) return false;
            }
            catch
            {
                return false;
            }

            _targetLakeRegionCalls++;
            _targetWaterMatches++;
            if (battaniaLake)
            {
                _battaniaWaterMatches++;
                if (!_loggedFirstBattaniaMatch)
                {
                    _loggedFirstBattaniaMatch = true;
                    WriteLog("First Battania lake exclusion match: x=" + point.x.ToString("F2")
                        + "; y=" + point.y.ToString("F2") + ".");
                }
            }
            else
            {
                _easternWaterMatches++;
                if (!_loggedFirstEasternMatch)
                {
                    _loggedFirstEasternMatch = true;
                    WriteLog("First eastern lake exclusion match: x=" + point.x.ToString("F2")
                        + "; y=" + point.y.ToString("F2") + ".");
                }
            }
            return true;
        }

        private sealed class LegacyPoliticalLandMask
        {
            private readonly int _width;
            private readonly int _height;
            private readonly byte[] _land;
            private readonly object _anchorLock = new object();
            private List<ProjectionAnchor> _anchors;

            private sealed class ProjectionAnchor
            {
                internal readonly Vec2 CampaignPosition;
                internal readonly double ResidualX;
                internal readonly double ResidualY;

                internal ProjectionAnchor(Vec2 campaignPosition, double residualX, double residualY)
                {
                    CampaignPosition = campaignPosition;
                    ResidualX = residualX;
                    ResidualY = residualY;
                }
            }

            private LegacyPoliticalLandMask(int width, int height, byte[] land)
            {
                _width = width;
                _height = height;
                _land = land;
            }

            internal static LegacyPoliticalLandMask TryLoad()
            {
                try
                {
                    string moduleRoot = ResolveModuleRoot();
                    string path = System.IO.Path.Combine(
                        moduleRoot,
                        "GUI",
                        "SpriteParts",
                        "ui_world_calendar",
                        "campaign_political_land_mask.png");
                    if (!File.Exists(path))
                    {
                        WriteLog("The preserved 6:59 political land mask is missing: " + path + ".");
                        return null;
                    }

                    using (Bitmap source = new Bitmap(path))
                    using (Bitmap bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.DrawImageUnscaled(source, 0, 0);
                        }

                        Rectangle rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                        BitmapData data = bitmap.LockBits(
                            rectangle,
                            ImageLockMode.ReadOnly,
                            PixelFormat.Format32bppArgb);
                        try
                        {
                            int stride = Math.Abs(data.Stride);
                            byte[] pixels = new byte[stride * data.Height];
                            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                            byte[] land = new byte[data.Width * data.Height];
                            for (int y = 0; y < data.Height; y++)
                            {
                                int sourceY = data.Stride >= 0 ? y : data.Height - 1 - y;
                                for (int x = 0; x < data.Width; x++)
                                {
                                    if (pixels[sourceY * stride + x * 4 + 3] != 0)
                                    {
                                        land[y * data.Width + x] = 1;
                                    }
                                }
                            }
                            return new LegacyPoliticalLandMask(data.Width, data.Height, land);
                        }
                        finally
                        {
                            bitmap.UnlockBits(data);
                        }
                    }
                }
                catch (Exception exception)
                {
                    WriteLog("The preserved 6:59 political land mask could not be loaded: " + exception.Message);
                    return null;
                }
            }

            internal bool IsLand(Vec2 point, double[] projectionX, double[] projectionY)
            {
                double sourceX = point.x * projectionX[0]
                    + point.y * projectionX[1] + projectionX[2] - 80.0;
                double sourceY = point.x * projectionY[0]
                    + point.y * projectionY[1] + projectionY[2] - 90.0;
                EnsureProjectionAnchors(projectionX, projectionY);
                ApplyLocalAnchorCorrection(point, ref sourceX, ref sourceY);
                int x = (int)Math.Round(sourceX);
                int y = (int)Math.Round(sourceY);
                return x >= 0 && y >= 0 && x < _width && y < _height
                    && _land[y * _width + x] != 0;
            }

            private void EnsureProjectionAnchors(double[] projectionX, double[] projectionY)
            {
                if (_anchors != null) return;
                lock (_anchorLock)
                {
                    if (_anchors != null) return;
                    List<ProjectionAnchor> anchors = new List<ProjectionAnchor>();
                    if (_tryGetReferenceAnchorMethod != null && Campaign.Current != null)
                    {
                        foreach (Settlement settlement in Settlement.All)
                        {
                            if (settlement == null || (!settlement.IsTown && !settlement.IsCastle)) continue;
                            try
                            {
                                object[] arguments = { settlement.StringId, default(Vec2) };
                                if (!(bool)_tryGetReferenceAnchorMethod.Invoke(null, arguments)) continue;
                                Vec2 reference = (Vec2)arguments[1];
                                Vec2 campaign = settlement.GetPosition2D;
                                double projectedX = campaign.x * projectionX[0]
                                    + campaign.y * projectionX[1] + projectionX[2] - 80.0;
                                double projectedY = campaign.x * projectionY[0]
                                    + campaign.y * projectionY[1] + projectionY[2] - 90.0;
                                anchors.Add(new ProjectionAnchor(
                                    campaign,
                                    reference.x - 80.0 - projectedX,
                                    reference.y - 90.0 - projectedY));
                            }
                            catch
                            {
                                // A missing settlement anchor must not disable the remaining anchors.
                            }
                        }
                    }
                    _anchors = anchors;
                    WriteLog("Preserved 6:59 lake-mask projection anchors prepared: " + anchors.Count + ".");
                }
            }

            private void ApplyLocalAnchorCorrection(
                Vec2 point,
                ref double sourceX,
                ref double sourceY)
            {
                if (_anchors == null || _anchors.Count == 0) return;

                ProjectionAnchor[] nearest = new ProjectionAnchor[4];
                double[] distances = { double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue };
                foreach (ProjectionAnchor anchor in _anchors)
                {
                    double dx = point.x - anchor.CampaignPosition.x;
                    double dy = point.y - anchor.CampaignPosition.y;
                    double distance = dx * dx + dy * dy;
                    for (int index = 0; index < nearest.Length; index++)
                    {
                        if (distance >= distances[index]) continue;
                        for (int shift = nearest.Length - 1; shift > index; shift--)
                        {
                            nearest[shift] = nearest[shift - 1];
                            distances[shift] = distances[shift - 1];
                        }
                        nearest[index] = anchor;
                        distances[index] = distance;
                        break;
                    }
                }

                if (nearest[0] == null) return;
                if (distances[0] < 0.0001)
                {
                    sourceX += nearest[0].ResidualX;
                    sourceY += nearest[0].ResidualY;
                    return;
                }

                double totalWeight = 0.0;
                double correctionX = 0.0;
                double correctionY = 0.0;
                for (int index = 0; index < nearest.Length; index++)
                {
                    if (nearest[index] == null) continue;
                    double weight = 1.0 / (distances[index] + 4.0);
                    totalWeight += weight;
                    correctionX += nearest[index].ResidualX * weight;
                    correctionY += nearest[index].ResidualY * weight;
                }
                if (totalWeight <= 0.0) return;
                sourceX += correctionX / totalWeight;
                sourceY += correctionY / totalWeight;
            }

            private static string ResolveModuleRoot()
            {
                DirectoryInfo directory = new FileInfo(typeof(IslandExclusionSubModule).Assembly.Location).Directory;
                for (int depth = 0; directory != null && depth < 6; depth++, directory = directory.Parent)
                {
                    if (File.Exists(System.IO.Path.Combine(directory.FullName, "SubModule.xml")))
                    {
                        return directory.FullName;
                    }
                }
                throw new DirectoryNotFoundException("The Ages of Calradia module root could not be resolved.");
            }
        }

        private static void GetTargetLakeSpan(Vec2 point, out bool battaniaLake, out bool easternLake)
        {
            // Converted from the atlas component bounds using the installed
            // strategic_settlements_native.csv affine projection.
            battaniaLake = point.x >= 418f && point.x <= 468f
                && point.y >= 439f && point.y <= 480f;
            easternLake = point.x >= 674f && point.x <= 706f
                && point.y >= 439f && point.y <= 484f;

            // IsTargetLakeExclusion applies the authored shoreline only after
            // this tight projection-derived geographic gate.
        }

        private static void LiftTargetLakeTriangle(ref Vec3 first, ref Vec3 second, ref Vec3 third)
        {
            Vec2 center = new Vec2(
                (first.x + second.x + third.x) / 3f,
                (first.y + second.y + third.y) / 3f);
            GetMask();
            string lakeRegion;
            if (_lakeMask == null
                || !_lakeMask.TryGetOverlappingRegion(
                    new Vec2(first.x, first.y),
                    new Vec2(second.x, second.y),
                    new Vec2(third.x, third.y),
                    out lakeRegion)
                || lakeRegion != "Battania")
            {
                return;
            }

            const float lakeCapLift = 4f;
            float originalAverageZ = (first.z + second.z + third.z) / 3f;
            float waterLevel = float.NaN;
            object scene = _terrainSceneField == null ? null : _terrainSceneField.GetValue(null);
            if (scene != null && _getWaterLevelAtPositionMethod != null)
            {
                try
                {
                    waterLevel = (float)_getWaterLevelAtPositionMethod.Invoke(
                        scene,
                        new object[] { center, true, true });
                }
                catch
                {
                    waterLevel = float.NaN;
                }
            }

            if (!float.IsNaN(waterLevel) && !float.IsInfinity(waterLevel) && waterLevel > -50f)
            {
                float capHeight = waterLevel + lakeCapLift;
                first.z = Math.Max(first.z, capHeight);
                second.z = Math.Max(second.z, capHeight);
                third.z = Math.Max(third.z, capHeight);
            }
            else
            {
                first.z += lakeCapLift;
                second.z += lakeCapLift;
                third.z += lakeCapLift;
            }
            _liftedLakeTriangleCount++;
            if (!_loggedFirstLiftedTriangle)
            {
                _loggedFirstLiftedTriangle = true;
                WriteLog("First target-lake triangle lifted: x=" + center.x.ToString("F2")
                    + "; y=" + center.y.ToString("F2")
                    + "; originalZ=" + originalAverageZ.ToString("F2")
                    + "; waterLevel=" + (float.IsNaN(waterLevel) ? "unavailable" : waterLevel.ToString("F2"))
                    + "; finalZ=" + ((first.z + second.z + third.z) / 3f).ToString("F2") + ".");
            }
        }

        private static void AddTargetLakeCapEntities(object __instance)
        {
            object mapScene = Campaign.Current == null ? null : Campaign.Current.MapSceneWrapper;
            if (__instance == null || mapScene == null || ReferenceEquals(_lakeCapSourceMapScene, mapScene)) return;

            try
            {
                FieldInfo entitiesField = AccessTools.Field(__instance.GetType(), "_entities");
                FieldInfo siteIndexField = AccessTools.Field(__instance.GetType(), "_siteIndex");
                List<GameEntity> entities = entitiesField == null
                    ? null
                    : entitiesField.GetValue(__instance) as List<GameEntity>;
                object siteIndex = siteIndexField == null ? null : siteIndexField.GetValue(__instance);
                object sceneObject = _terrainSceneField == null ? null : _terrainSceneField.GetValue(null);
                Scene scene = sceneObject as Scene;
                if (entities == null || siteIndex == null || scene == null || _trySampleExactHeightMethod == null)
                {
                    WriteLog("Dedicated target-lake cap skipped: builder state was unavailable.");
                    return;
                }

                MethodInfo findNearest = AccessTools.Method(siteIndex.GetType(), "FindNearest", new[] { typeof(Vec2) });
                if (findNearest == null)
                {
                    WriteLog("Dedicated target-lake cap skipped: ownership lookup was unavailable.");
                    return;
                }

                Mesh mesh = Mesh.CreateMesh(true);
                if (mesh == null) throw new InvalidOperationException("Bannerlord could not allocate the target-lake cap mesh.");
                mesh.SetMaterial("vertex_color_mat");
                mesh.SetMeshRenderOrder(106);
                UIntPtr handle = mesh.LockEditDataWrite();
                int triangles = 0;
                try
                {
                    AddTargetLakeCapRegion(mesh, handle, siteIndex, findNearest, 418f, 468f, 439f, 480f, ref triangles);
                    AddTargetLakeCapRegion(mesh, handle, siteIndex, findNearest, 674f, 706f, 439f, 484f, ref triangles);
                }
                finally
                {
                    mesh.UnlockEditDataWrite(handle);
                }

                if (triangles == 0)
                {
                    WriteLog("Dedicated target-lake cap produced zero triangles.");
                    return;
                }

                mesh.ComputeNormals();
                mesh.RecomputeBoundingBox();
                GameEntity entity = GameEntity.CreateEmpty(scene, false, true, true);
                if (entity == null) throw new InvalidOperationException("Bannerlord could not allocate the target-lake cap entity.");
                MatrixFrame frame = MatrixFrame.Identity;
                entity.SetGlobalFrame(frame);
                entity.AddMesh(mesh, true);
                entity.SetForceDecalsToRender(false);
                entity.SetEnforcedMaximumLodLevel(0);
                entity.SetVisibilityExcludeParents(true);
                entity.SetReadyToRender(true);
                entity.SetAlpha(1f);
                entity.UpdateVisibilityMask();
                entities.Add(entity);
                _lakeCapSourceMapScene = mapScene;
                WriteLog("Dedicated target-lake cap added: triangles=" + triangles
                    + "; renderOrder=106; hasScene=" + entity.HasScene()
                    + "; visible=" + entity.IsVisibleIncludeParents() + ".");
            }
            catch (Exception exception)
            {
                WriteLog("Dedicated target-lake cap failed: " + exception + ".");
            }
        }

        private static void AddTargetLakeCapRegion(
            Mesh mesh,
            UIntPtr handle,
            object siteIndex,
            MethodInfo findNearest,
            float minimumX,
            float maximumX,
            float minimumY,
            float maximumY,
            ref int triangles)
        {
            const float step = 0.5f;
            const float capLift = 8f;
            Vec2 uv = Vec2.Zero;
            for (float y = minimumY; y + step <= maximumY; y += step)
            {
                for (float x = minimumX; x + step <= maximumX; x += step)
                {
                    Vec2 center = new Vec2(x + step * 0.5f, y + step * 0.5f);
                    bool battania;
                    bool eastern;
                    GetTargetLakeSpan(center, out battania, out eastern);
                    if (!battania && !eastern) continue;

                    object owner = findNearest.Invoke(siteIndex, new object[] { center });
                    if (owner == null) continue;
                    PropertyInfo colorProperty = AccessTools.Property(owner.GetType(), "Color");
                    FieldInfo colorField = colorProperty == null ? AccessTools.Field(owner.GetType(), "Color") : null;
                    uint color = colorProperty != null
                        ? (uint)colorProperty.GetValue(owner, null)
                        : colorField != null ? (uint)colorField.GetValue(owner) : 0xFFFFFFFFu;
                    color = ScalePoliticalColor(color);

                    float lowerLeftHeight;
                    float lowerRightHeight;
                    float upperRightHeight;
                    float upperLeftHeight;
                    if (!TryGetCapHeight(new Vec2(x, y), out lowerLeftHeight)
                        || !TryGetCapHeight(new Vec2(x + step, y), out lowerRightHeight)
                        || !TryGetCapHeight(new Vec2(x + step, y + step), out upperRightHeight)
                        || !TryGetCapHeight(new Vec2(x, y + step), out upperLeftHeight))
                    {
                        continue;
                    }

                    Vec3 lowerLeft = new Vec3(x, y, lowerLeftHeight + capLift, -1f);
                    Vec3 lowerRight = new Vec3(x + step, y, lowerRightHeight + capLift, -1f);
                    Vec3 upperRight = new Vec3(x + step, y + step, upperRightHeight + capLift, -1f);
                    Vec3 upperLeft = new Vec3(x, y + step, upperLeftHeight + capLift, -1f);
                    mesh.AddTriangle(lowerLeft, lowerRight, upperRight, uv, uv, uv, color, handle);
                    mesh.AddTriangle(lowerLeft, upperRight, lowerRight, uv, uv, uv, color, handle);
                    mesh.AddTriangle(lowerLeft, upperRight, upperLeft, uv, uv, uv, color, handle);
                    mesh.AddTriangle(lowerLeft, upperLeft, upperRight, uv, uv, uv, color, handle);
                    triangles += 2;
                }
            }
        }

        private static bool TryGetCapHeight(Vec2 point, out float height)
        {
            height = 0f;
            object[] arguments = { point, 0f };
            if (!(bool)_trySampleExactHeightMethod.Invoke(null, arguments)) return false;
            height = (float)arguments[1];
            return !float.IsNaN(height) && !float.IsInfinity(height);
        }

        private static uint ScalePoliticalColor(uint color)
        {
            uint red = ((color >> 16) & 0xFF) * 50u / 100u;
            uint green = ((color >> 8) & 0xFF) * 50u / 100u;
            uint blue = (color & 0xFF) * 50u / 100u;
            return 0xFF000000u | (red << 16) | (green << 8) | blue;
        }

        private static void WritePeriodicLakeDiagnostics()
        {
            long regionCalls = _targetLakeRegionCalls;
            if (regionCalls == 0 || regionCalls % 250 != 0) return;

            WriteLog(
                "Target-lake diagnostics: exactCalls=" + _exactClassifierCalls
                + "; regionCalls=" + regionCalls
                + "; waterMatches=" + _targetWaterMatches
                + "; battaniaMatches=" + _battaniaWaterMatches
                + "; easternMatches=" + _easternWaterMatches
                + "; forcedFilled=" + _forcedLakeFillCount
                + "; liftedTriangles=" + _liftedLakeTriangleCount
                + ".");
        }

        private static void WriteTargetLakeProbeScan(
            string name,
            float minimumX,
            float maximumX,
            float minimumY,
            float maximumY)
        {
            if (_tryGetNativeTerrainMethod == null || _isProtectedWaterMethod == null)
            {
                WriteLog("Target-lake scan " + name + " skipped: native terrain methods unavailable.");
                return;
            }

            const float step = 0.75f;
            int samples = 0;
            int valid = 0;
            int water = 0;
            float waterMinimumX = float.MaxValue;
            float waterMaximumX = float.MinValue;
            float waterMinimumY = float.MaxValue;
            float waterMaximumY = float.MinValue;
            Dictionary<TerrainType, int> terrainCounts = new Dictionary<TerrainType, int>();

            try
            {
                for (float y = minimumY + step * 0.5f; y < maximumY; y += step)
                {
                    for (float x = minimumX + step * 0.5f; x < maximumX; x += step)
                    {
                        samples++;
                        object[] arguments = { new Vec2(x, y), default(TerrainType) };
                        if (!(bool)_tryGetNativeTerrainMethod.Invoke(null, arguments)) continue;

                        valid++;
                        TerrainType terrain = (TerrainType)arguments[1];
                        int count;
                        terrainCounts.TryGetValue(terrain, out count);
                        terrainCounts[terrain] = count + 1;
                        bool isWater = terrain == TerrainType.Water
                            || (bool)_isProtectedWaterMethod.Invoke(null, new object[] { terrain });
                        if (!isWater) continue;

                        water++;
                        waterMinimumX = Math.Min(waterMinimumX, x);
                        waterMaximumX = Math.Max(waterMaximumX, x);
                        waterMinimumY = Math.Min(waterMinimumY, y);
                        waterMaximumY = Math.Max(waterMaximumY, y);
                    }
                }

                List<string> counts = new List<string>();
                foreach (KeyValuePair<TerrainType, int> pair in terrainCounts)
                {
                    counts.Add(pair.Key + ":" + pair.Value);
                }
                WriteLog(
                    "Target-lake scan " + name
                    + ": samples=" + samples
                    + "; valid=" + valid
                    + "; invalid=" + (samples - valid)
                    + "; water=" + water
                    + "; waterBounds=" + (water > 0
                        ? "(" + waterMinimumX.ToString("F2") + ".." + waterMaximumX.ToString("F2")
                            + "," + waterMinimumY.ToString("F2") + ".." + waterMaximumY.ToString("F2") + ")"
                        : "none")
                    + "; terrainCounts=" + string.Join(",", counts.ToArray()) + ".");
            }
            catch (Exception exception)
            {
                WriteLog("Target-lake scan " + name + " failed: " + exception + ".");
            }
        }

        private static ExactIslandMask GetMask()
        {
            object mapScene = Campaign.Current == null ? null : Campaign.Current.MapSceneWrapper;
            if (mapScene == null || _exactLandMethod == null) return null;

            if (_mask != null && ReferenceEquals(_sourceMapScene, mapScene)) return _mask;
            if (_maskBuildFailed && ReferenceEquals(_sourceMapScene, mapScene)) return null;

            lock (MaskLock)
            {
                if (_mask != null && ReferenceEquals(_sourceMapScene, mapScene)) return _mask;

                _sourceMapScene = mapScene;
                _mask = null;
                _lakeMask = null;
                _maskBuildFailed = false;
                try
                {
                    _buildingMask = true;
                    _mask = ExactIslandMask.Build(_exactLandMethod);
                    object landMask = _landMaskField == null ? null : _landMaskField.GetValue(null);
                    double[] projectionX = landMask == null || _projectionXField == null
                        ? null
                        : _projectionXField.GetValue(landMask) as double[];
                    double[] projectionY = landMask == null || _projectionYField == null
                        ? null
                        : _projectionYField.GetValue(landMask) as double[];
                    if (_legacyPoliticalLandMask != null
                        && projectionX != null && projectionX.Length >= 3
                        && projectionY != null && projectionY.Length >= 3)
                    {
                        Func<Vec2, bool> legacyLand =
                            point => _legacyPoliticalLandMask.IsLand(point, projectionX, projectionY);
                        Func<Vec2, bool> currentAuthoredLand = _isAuthoredLandMethod == null
                            ? legacyLand
                            : new Func<Vec2, bool>(point =>
                                (bool)_isAuthoredLandMethod.Invoke(landMask, new object[] { point }));
                        _lakeMask = ExactLakeMask.Build(legacyLand, currentAuthoredLand);
                        WriteLog(
                            "Exact enclosed-lake mask prepared using island-style cached components: "
                            + _lakeMask.Report + ".");
                    }
                    WriteLog(
                        "Exact rendered-island mask prepared: selectedComponents=" + _mask.SelectedComponentCount
                        + "; selectedCells=" + _mask.SelectedCellCount
                        + "; regions=" + _mask.RegionReport + ".");
                }
                catch (Exception exception)
                {
                    _maskBuildFailed = true;
                    WriteLog("Exact rendered-island mask failed open: " + exception);
                }
                finally
                {
                    _buildingMask = false;
                }
                return _mask;
            }
        }

        private static void WriteLog(string message)
        {
            string fullMessage = "Ages of Calradia island exclusion: " + message;
            try
            {
                if (_diagnosticsInfo != null)
                {
                    _diagnosticsInfo.Invoke(null, new object[] { fullMessage });
                    return;
                }
            }
            catch
            {
            }
            Debug.Print(fullMessage);
        }
    }

    internal sealed class ExactLakeMask
    {
        private readonly RegionMask[] _regions;

        internal string Report { get; private set; }

        private ExactLakeMask(RegionMask[] regions)
        {
            _regions = regions;
            List<string> reports = new List<string>();
            foreach (RegionMask region in regions) reports.Add(region.Report);
            Report = string.Join(" | ", reports.ToArray());
        }

        internal static ExactLakeMask Build(
            Func<Vec2, bool> legacyAuthoredLand,
            Func<Vec2, bool> currentAuthoredLand)
        {
            return new ExactLakeMask(new[]
            {
                // These regions surround the runtime-proven lake coordinates
                // from the working 6:59 implementation. Only transparent
                // components fully enclosed by each region are selected.
                RegionMask.Build(
                    "Battania", 370f, 465f, 455f, 545f,
                    legacyAuthoredLand, includeBoundaryConnectedWater: false),
                // Preserved 6:59 mask oval between Saneopa, Phycaon and
                // Lycaron. The wider gate keeps the entire oval away from the
                // boundary so river/road components are never selected.
                RegionMask.Build(
                    "eastern", 620f, 685f, 375f, 455f,
                    legacyAuthoredLand, includeBoundaryConnectedWater: false)
            });
        }

        internal bool Contains(Vec2 point)
        {
            string region;
            return TryGetRegion(point, out region);
        }

        internal bool TryGetRegion(Vec2 point, out string regionName)
        {
            foreach (RegionMask region in _regions)
            {
                if (!region.Contains(point)) continue;
                regionName = region.Name;
                return true;
            }
            regionName = null;
            return false;
        }

        internal bool TryGetOverlappingRegion(
            Vec2 first,
            Vec2 second,
            Vec2 third,
            out string regionName)
        {
            foreach (RegionMask region in _regions)
            {
                if (!region.OverlapsTriangle(first, second, third)) continue;
                regionName = region.Name;
                return true;
            }
            regionName = null;
            return false;
        }

        internal List<Tuple<Vec2, Vec2>> GetBoundarySegments(string regionName)
        {
            foreach (RegionMask region in _regions)
            {
                if (region.Name == regionName) return region.GetBoundarySegments();
            }
            return new List<Tuple<Vec2, Vec2>>();
        }

        private sealed class RegionMask
        {
            private const float Step = 0.75f;
            private readonly string _name;
            private readonly float _minimumX;
            private readonly float _maximumX;
            private readonly float _minimumY;
            private readonly float _maximumY;
            private readonly int _columns;
            private readonly int _rows;
            private readonly bool[] _excluded;
            private readonly int _components;
            private readonly int _cells;

            internal string Report
            {
                get
                {
                    return _name + "=" + _columns + "x" + _rows
                        + ",components:" + _components + ",cells:" + _cells;
                }
            }

            internal string Name { get { return _name; } }

            private RegionMask(
                string name,
                float minimumX,
                float maximumX,
                float minimumY,
                float maximumY,
                int columns,
                int rows,
                bool[] excluded,
                int components,
                int cells)
            {
                _name = name;
                _minimumX = minimumX;
                _maximumX = maximumX;
                _minimumY = minimumY;
                _maximumY = maximumY;
                _columns = columns;
                _rows = rows;
                _excluded = excluded;
                _components = components;
                _cells = cells;
            }

            internal static RegionMask Build(
                string name,
                float minimumX,
                float maximumX,
                float minimumY,
                float maximumY,
                Func<Vec2, bool> isAuthoredLand,
                bool includeBoundaryConnectedWater)
            {
                int columns = (int)Math.Floor((maximumX - minimumX) / Step) + 1;
                int rows = (int)Math.Floor((maximumY - minimumY) / Step) + 1;
                bool[] water = new bool[columns * rows];
                for (int row = 0; row < rows; row++)
                {
                    float y = minimumY + row * Step;
                    for (int column = 0; column < columns; column++)
                    {
                        float x = minimumX + column * Step;
                        water[row * columns + column] = !isAuthoredLand(new Vec2(x, y));
                    }
                }

                bool[] selected = new bool[water.Length];
                bool[] visited = new bool[water.Length];
                int selectedComponents = 0;
                int selectedCells = 0;
                for (int seed = 0; seed < water.Length; seed++)
                {
                    if (!water[seed] || visited[seed]) continue;
                    Queue<int> pending = new Queue<int>();
                    List<int> component = new List<int>();
                    bool touchesBoundary = false;
                    pending.Enqueue(seed);
                    visited[seed] = true;
                    while (pending.Count > 0)
                    {
                        int index = pending.Dequeue();
                        component.Add(index);
                        int row = index / columns;
                        int column = index - row * columns;
                        if (row <= 1 || row >= rows - 2 || column <= 1 || column >= columns - 2)
                        {
                            touchesBoundary = true;
                        }
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nextRow = row + dy;
                            if (nextRow < 0 || nextRow >= rows) continue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nextColumn = column + dx;
                                if (nextColumn < 0 || nextColumn >= columns) continue;
                                int next = nextRow * columns + nextColumn;
                                if (water[next] && !visited[next])
                                {
                                    visited[next] = true;
                                    pending.Enqueue(next);
                                }
                            }
                        }
                    }
                    if ((!includeBoundaryConnectedWater && touchesBoundary) || component.Count < 4) continue;
                    foreach (int index in component) selected[index] = true;
                    selectedComponents++;
                    selectedCells += component.Count;
                }

                bool[] halo = AddOneCellHalo(selected, columns, rows);
                selectedCells = 0;
                foreach (bool value in halo)
                {
                    if (value) selectedCells++;
                }
                return new RegionMask(
                    name, minimumX, maximumX, minimumY, maximumY,
                    columns, rows, halo, selectedComponents, selectedCells);
            }

            private static bool[] AddOneCellHalo(bool[] source, int columns, int rows)
            {
                bool[] result = (bool[])source.Clone();
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        if (!source[row * columns + column]) continue;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nextRow = row + dy;
                            if (nextRow < 0 || nextRow >= rows) continue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nextColumn = column + dx;
                                if (nextColumn >= 0 && nextColumn < columns)
                                {
                                    result[nextRow * columns + nextColumn] = true;
                                }
                            }
                        }
                    }
                }
                return result;
            }

            internal bool Contains(Vec2 point)
            {
                if (point.x < _minimumX || point.x > _maximumX
                    || point.y < _minimumY || point.y > _maximumY)
                {
                    return false;
                }
                int column = (int)Math.Round((point.x - _minimumX) / Step);
                int row = (int)Math.Round((point.y - _minimumY) / Step);
                column = Math.Max(0, Math.Min(_columns - 1, column));
                row = Math.Max(0, Math.Min(_rows - 1, row));
                return _excluded[row * _columns + column];
            }

            internal bool OverlapsTriangle(Vec2 first, Vec2 second, Vec2 third)
            {
                float triangleMinimumX = Math.Min(first.x, Math.Min(second.x, third.x));
                float triangleMaximumX = Math.Max(first.x, Math.Max(second.x, third.x));
                float triangleMinimumY = Math.Min(first.y, Math.Min(second.y, third.y));
                float triangleMaximumY = Math.Max(first.y, Math.Max(second.y, third.y));
                if (triangleMaximumX < _minimumX || triangleMinimumX > _maximumX
                    || triangleMaximumY < _minimumY || triangleMinimumY > _maximumY)
                {
                    return false;
                }

                int firstColumn = Math.Max(
                    0, (int)Math.Floor((triangleMinimumX - _minimumX) / Step) - 1);
                int lastColumn = Math.Min(
                    _columns - 1, (int)Math.Ceiling((triangleMaximumX - _minimumX) / Step) + 1);
                int firstRow = Math.Max(
                    0, (int)Math.Floor((triangleMinimumY - _minimumY) / Step) - 1);
                int lastRow = Math.Min(
                    _rows - 1, (int)Math.Ceiling((triangleMaximumY - _minimumY) / Step) + 1);
                for (int row = firstRow; row <= lastRow; row++)
                {
                    for (int column = firstColumn; column <= lastColumn; column++)
                    {
                        if (!_excluded[row * _columns + column]) continue;
                        Vec2 sample = new Vec2(
                            _minimumX + column * Step,
                            _minimumY + row * Step);
                        if (PointInsideTriangle(sample, first, second, third)) return true;
                    }
                }

                // Covers the inverse case where a small triangle lies wholly
                // inside one selected mask cell without enclosing its center.
                return Contains(first) || Contains(second) || Contains(third);
            }

            internal List<Tuple<Vec2, Vec2>> GetBoundarySegments()
            {
                List<Tuple<Vec2, Vec2>> segments = new List<Tuple<Vec2, Vec2>>();
                float halfStep = Step * 0.5f;
                for (int row = 0; row < _rows; row++)
                {
                    float centerY = _minimumY + row * Step;
                    for (int column = 0; column < _columns; column++)
                    {
                        if (!_excluded[row * _columns + column]) continue;
                        float centerX = _minimumX + column * Step;
                        float left = centerX - halfStep;
                        float right = centerX + halfStep;
                        float bottom = centerY - halfStep;
                        float top = centerY + halfStep;
                        if (!IsSelected(row, column - 1))
                        {
                            segments.Add(Tuple.Create(
                                new Vec2(left, bottom), new Vec2(left, top)));
                        }
                        if (!IsSelected(row, column + 1))
                        {
                            segments.Add(Tuple.Create(
                                new Vec2(right, top), new Vec2(right, bottom)));
                        }
                        if (!IsSelected(row - 1, column))
                        {
                            segments.Add(Tuple.Create(
                                new Vec2(right, bottom), new Vec2(left, bottom)));
                        }
                        if (!IsSelected(row + 1, column))
                        {
                            segments.Add(Tuple.Create(
                                new Vec2(left, top), new Vec2(right, top)));
                        }
                    }
                }
                return segments;
            }

            private bool IsSelected(int row, int column)
            {
                return row >= 0 && row < _rows && column >= 0 && column < _columns
                    && _excluded[row * _columns + column];
            }

            private static bool PointInsideTriangle(
                Vec2 point,
                Vec2 first,
                Vec2 second,
                Vec2 third)
            {
                float firstCross = Cross(second - first, point - first);
                float secondCross = Cross(third - second, point - second);
                float thirdCross = Cross(first - third, point - third);
                bool hasNegative = firstCross < 0f || secondCross < 0f || thirdCross < 0f;
                bool hasPositive = firstCross > 0f || secondCross > 0f || thirdCross > 0f;
                return !(hasNegative && hasPositive);
            }

            private static float Cross(Vec2 first, Vec2 second)
            {
                return first.x * second.y - first.y * second.x;
            }
        }
    }

    internal sealed class ExactIslandMask
    {
        private readonly RegionMask[] _regions;

        internal int SelectedComponentCount { get; private set; }
        internal int SelectedCellCount { get; private set; }
        internal string RegionReport { get; private set; }

        private ExactIslandMask(RegionMask[] regions)
        {
            _regions = regions;
            List<string> report = new List<string>();
            foreach (RegionMask region in regions)
            {
                SelectedComponentCount += region.SelectedComponentCount;
                SelectedCellCount += region.SelectedCellCount;
                report.Add(region.Report);
            }
            RegionReport = string.Join(" | ", report.ToArray());
        }

        internal static ExactIslandMask Build(MethodInfo exactLandMethod)
        {
            return new ExactIslandMask(new[]
            {
                RegionMask.Build("southwest", 40f, 340f, 220f, 465f, exactLandMethod),
                RegionMask.Build("north-chain", 260f, 410f, 590f, 730f, exactLandMethod)
            });
        }

        internal bool Contains(Vec2 point)
        {
            foreach (RegionMask region in _regions)
            {
                if (region.Contains(point)) return true;
            }
            return false;
        }

        private sealed class RegionMask
        {
            private const float Step = 0.75f;
            private const int MaximumIslandCells = 5000;

            private readonly string _name;
            private readonly float _minimumX;
            private readonly float _maximumX;
            private readonly float _minimumY;
            private readonly float _maximumY;
            private readonly int _columns;
            private readonly int _rows;
            private readonly bool[] _excluded;

            internal int SelectedComponentCount { get; private set; }
            internal int SelectedCellCount { get; private set; }
            internal string Report
            {
                get
                {
                    return _name + "=" + _columns + "x" + _rows
                        + ",components:" + SelectedComponentCount
                        + ",cells:" + SelectedCellCount;
                }
            }

            private RegionMask(
                string name,
                float minimumX,
                float maximumX,
                float minimumY,
                float maximumY,
                int columns,
                int rows,
                bool[] excluded)
            {
                _name = name;
                _minimumX = minimumX;
                _maximumX = maximumX;
                _minimumY = minimumY;
                _maximumY = maximumY;
                _columns = columns;
                _rows = rows;
                _excluded = excluded;
            }

            internal static RegionMask Build(
                string name,
                float minimumX,
                float maximumX,
                float minimumY,
                float maximumY,
                MethodInfo exactLandMethod)
            {
                int columns = (int)Math.Floor((maximumX - minimumX) / Step) + 1;
                int rows = (int)Math.Floor((maximumY - minimumY) / Step) + 1;
                bool[] land = new bool[columns * rows];
                for (int row = 0; row < rows; row++)
                {
                    float y = minimumY + row * Step;
                    for (int column = 0; column < columns; column++)
                    {
                        float x = minimumX + column * Step;
                        object[] arguments = { new Vec2(x, y), false };
                        land[row * columns + column] = (bool)exactLandMethod.Invoke(null, arguments);
                    }
                }

                bool[] selected = new bool[land.Length];
                bool[] visited = new bool[land.Length];
                int selectedComponents = 0;
                for (int index = 0; index < land.Length; index++)
                {
                    if (!land[index] || visited[index]) continue;
                    bool touchesBoundary;
                    List<int> component = CollectComponent(
                        index, land, visited, columns, rows, out touchesBoundary);
                    if (touchesBoundary || component.Count > MaximumIslandCells) continue;
                    foreach (int componentIndex in component) selected[componentIndex] = true;
                    selectedComponents++;
                }

                bool[] halo = AddOneCellHalo(selected, columns, rows);
                RegionMask result = new RegionMask(
                    name, minimumX, maximumX, minimumY, maximumY, columns, rows, halo);
                result.SelectedComponentCount = selectedComponents;
                foreach (bool value in halo)
                {
                    if (value) result.SelectedCellCount++;
                }
                return result;
            }

            internal bool Contains(Vec2 point)
            {
                if (point.x < _minimumX || point.x > _maximumX
                    || point.y < _minimumY || point.y > _maximumY)
                {
                    return false;
                }
                int column = (int)Math.Round((point.x - _minimumX) / Step);
                int row = (int)Math.Round((point.y - _minimumY) / Step);
                column = Math.Max(0, Math.Min(_columns - 1, column));
                row = Math.Max(0, Math.Min(_rows - 1, row));
                return _excluded[row * _columns + column];
            }

            private static List<int> CollectComponent(
                int seed,
                bool[] land,
                bool[] visited,
                int columns,
                int rows,
                out bool touchesBoundary)
            {
                Queue<int> pending = new Queue<int>();
                List<int> component = new List<int>();
                pending.Enqueue(seed);
                visited[seed] = true;
                touchesBoundary = false;
                while (pending.Count > 0)
                {
                    int index = pending.Dequeue();
                    component.Add(index);
                    int row = index / columns;
                    int column = index - row * columns;
                    if (row <= 1 || row >= rows - 2 || column <= 1 || column >= columns - 2)
                    {
                        touchesBoundary = true;
                    }
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nextRow = row + dy;
                        if (nextRow < 0 || nextRow >= rows) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nextColumn = column + dx;
                            if (nextColumn < 0 || nextColumn >= columns) continue;
                            int next = nextRow * columns + nextColumn;
                            if (!visited[next] && land[next])
                            {
                                visited[next] = true;
                                pending.Enqueue(next);
                            }
                        }
                    }
                }
                return component;
            }

            private static bool[] AddOneCellHalo(bool[] source, int columns, int rows)
            {
                bool[] result = (bool[])source.Clone();
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        if (!source[row * columns + column]) continue;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nextRow = row + dy;
                            if (nextRow < 0 || nextRow >= rows) continue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nextColumn = column + dx;
                                if (nextColumn >= 0 && nextColumn < columns)
                                {
                                    result[nextRow * columns + nextColumn] = true;
                                }
                            }
                        }
                    }
                }
                return result;
            }
        }
    }
}
