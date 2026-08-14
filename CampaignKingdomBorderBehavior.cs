using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using SandBox.View.Map;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Draws campaign-map kingdom borders from an independently calculated
    /// settlement Voronoi diagram projected onto the live terrain and rendered
    /// as runtime vertex-coloured meshes.
    ///
    /// The entities are scene visuals only. Nothing is written to campaign
    /// saves, and the whole layer can be rebuilt after a map-scene reload.
    /// </summary>
    internal sealed class CampaignKingdomBorderBehavior : CampaignBehaviorBase
    {
        private const float MapPadding = 110f;

        private readonly List<GameEntity> _territoryFillEntities = new List<GameEntity>();
        private readonly List<GameEntity> _politicalFrontierEntities = new List<GameEntity>();
        private CampaignPoliticalTerritoryFill.Builder _pendingFillBuilder;
        private CampaignPoliticalOverlayView _politicalOverlayView;
        private Scene _mapScene;
        private bool _dirty = true;
        private bool _loggedFirstBuild;
        private bool _loggedSceneLookupFailure;
        private string _lastOwnershipSignature;
        private float _politicalLayerAlpha;
        private bool _politicalEntitiesReady;
        private List<PoliticalTerritoryCell> _pendingTerritoryCells;
        private CampaignPoliticalTerritoryFill.NearestSiteIndex _politicalSiteIndex;
        private float _pendingMinX;
        private float _pendingMinY;
        private float _pendingMaxX;
        private float _pendingMaxY;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Runtime scene entities are intentionally not save data.
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            MarkDirty();
        }

        private void OnGameLoadFinished()
        {
            MarkDirty();
        }

        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero claimant,
            Hero oldOwner,
            Hero newOwner,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            MarkDirty();
        }

        private void OnDailyTick()
        {
            // Catch ownership changes from mods that bypass the public event,
            // without rebuilding a large terrain mesh every in-game day.
            if (!string.Equals(
                _lastOwnershipSignature,
                BuildOwnershipSignature(),
                StringComparison.Ordinal))
            {
                MarkDirty();
            }
        }

        private void OnTick(float dt)
        {
            if (Campaign.Current == null) return;
            EnsurePoliticalOverlayView();
        }

        internal bool HasVisiblePoliticalFill { get { return _territoryFillEntities.Count > 0; } }
        internal bool HasCurrentPoliticalTopology { get { return !_dirty && _politicalSiteIndex != null; } }
        internal int PoliticalBoundaryVersion { get; private set; }

        internal void OnMapFrame(Camera camera)
        {
            if (Campaign.Current == null || camera == null) return;
            float altitude = camera.Frame.origin.z;
            SetPoliticalOverlayAlpha(Math.Max(0f, Math.Min(1f,
                (altitude - CampaignPoliticalOverlayView.FadeStartAltitude)
                / (CampaignPoliticalOverlayView.FadeEndAltitude - CampaignPoliticalOverlayView.FadeStartAltitude))));
            if (!_dirty || _politicalOverlayView == null || !_politicalOverlayView.IsMapReady) return;

            Scene currentScene = TryGetCampaignMapScene();
            if (currentScene == null) return;

            if (!ReferenceEquals(currentScene, _mapScene))
            {
                CampaignMapTerrainGridCache.ClearForScene(_mapScene);
                ClearBorderEntities();
                _mapScene = currentScene;
                _pendingTerritoryCells = null;
            }

            try
            {
                if (!RebuildBorders()) return;
                _dirty = false;
                if (!_loggedFirstBuild)
                {
                    _loggedFirstBuild = true;
                    Diagnostics.Info("Campaign political territory rendered from live settlement Voronoi ownership without frontier ribbons.");
                }
            }
            catch (Exception exception)
            {
                // A changed engine material or map scene must not stop the
                // campaign. Retry on a later frame after the scene settles.
                Diagnostics.Error("Campaign kingdom borders could not be rendered safely.", exception);
                ClearBorderEntities();
                MarkDirty();
            }
        }

        private bool RebuildBorders()
        {
            if (_pendingTerritoryCells == null) PrepareTerritoryCells();
            if (_pendingTerritoryCells == null || _pendingTerritoryCells.Count == 0) return true;

            IMapScene mapScene = Campaign.Current == null ? null : Campaign.Current.MapSceneWrapper;
            if (!CampaignMapTerrainGridCache.BeginOrAdvance(
                _mapScene,
                mapScene,
                _pendingMinX,
                _pendingMinY,
                _pendingMaxX,
                _pendingMaxY))
            {
                return false;
            }

            if (_pendingFillBuilder == null)
            {
                _pendingFillBuilder = CampaignPoliticalTerritoryFill.Begin(_pendingTerritoryCells);
                if (_pendingFillBuilder == null) throw new InvalidOperationException("The political territory build could not be started.");
            }
            _pendingFillBuilder.Advance(_mapScene);
            if (_pendingFillBuilder.IsFillComplete && !_pendingFillBuilder.FillEntitiesTaken)
            {
                List<GameEntity> fillReplacement = _pendingFillBuilder.TakeFillEntities();
                if (fillReplacement.Count == 0) throw new InvalidOperationException("The political territory mesh contained no renderable land.");
                ReplacePoliticalFillEntities(fillReplacement);
                _politicalOverlayView.RebuildLabels();
                Diagnostics.Info("Campaign political fill published before frontier completion: fillEntities="
                    + _territoryFillEntities.Count + ".");
            }
            if (!_pendingFillBuilder.IsComplete) return false;

            List<GameEntity> frontierReplacement = _pendingFillBuilder.TakeFrontierEntities();
            int landSamples = _pendingFillBuilder.LandSampleCount;
            int seaSamples = _pendingFillBuilder.SeaSampleCount;
            int triangleCount = _pendingFillBuilder.RenderedTriangleCount;
            int riverTriangleCount = _pendingFillBuilder.RiverRenderedTriangleCount;
            int enclosedWaterTriangleCount = _pendingFillBuilder.EnclosedWaterRenderedTriangleCount;
            int exteriorWaterTriangleRejectionCount = _pendingFillBuilder.ExteriorWaterTriangleRejectionCount;
            int riverEntityCount = _pendingFillBuilder.RiverEntityCount;
            int refinedCellCount = _pendingFillBuilder.RefinedCellCount;
            int terrainReliefRefinedCellCount = _pendingFillBuilder.TerrainReliefRefinedCellCount;
            int frontierSegmentCount = _pendingFillBuilder.FrontierRenderedSegmentCount;
            int frontierEntityCount = _pendingFillBuilder.FrontierEntityCount;
            int frontierCandidateSegmentCount = _pendingFillBuilder.FrontierCandidateSegmentCount;
            int frontierUnsupportedSegmentCount = _pendingFillBuilder.FrontierUnsupportedSegmentCount;
            int frontierProjectionRejectedSegmentCount = _pendingFillBuilder.FrontierProjectionRejectedSegmentCount;
            int frontierSaddleCellCount = _pendingFillBuilder.FrontierSaddleCellCount;
            int frontierAmbiguousCellCount = _pendingFillBuilder.FrontierAmbiguousCellCount;
            int frontierCoastRefinedCellCount = _pendingFillBuilder.FrontierCoastRefinedCellCount;
            int frontierExteriorWaterMidpointCount = _pendingFillBuilder.FrontierExteriorWaterMidpointCount;
            int frontierSameOwnerWaterChordRejectionCount = _pendingFillBuilder.FrontierSameOwnerWaterChordRejectionCount;
            string frontierBridgeDiagnostics = _pendingFillBuilder.FrontierBridgeDiagnostics;
            long meshMilliseconds = _pendingFillBuilder.MeshMilliseconds;
            long maximumBatchMilliseconds = _pendingFillBuilder.MaximumBatchMilliseconds;
            _pendingFillBuilder = null;
            ReplacePoliticalFrontierEntities(frontierReplacement);
            Diagnostics.Info("Campaign political terrain grid prepared: cells=" + _pendingTerritoryCells.Count
                + "; landSamples=" + landSamples
                + "; seaSamples=" + seaSamples
                + "; triangles=" + triangleCount
                + "; riverTriangles=" + riverTriangleCount
                + "; enclosedWaterTriangles=" + enclosedWaterTriangleCount
                + "; exteriorWaterTriangleRejects=" + exteriorWaterTriangleRejectionCount
                + "; riverEntities=" + riverEntityCount
                + "; refinedCells=" + refinedCellCount
                + "; terrainReliefRefinedCells=" + terrainReliefRefinedCellCount
                + "; frontierSegments=" + frontierSegmentCount
                + "; frontierCandidates=" + frontierCandidateSegmentCount
                + "; frontierUnsupported=" + frontierUnsupportedSegmentCount
                + "; frontierProjectionRejected=" + frontierProjectionRejectedSegmentCount
                + "; frontierSaddleCells=" + frontierSaddleCellCount
                + "; frontierAmbiguousCells=" + frontierAmbiguousCellCount
                + "; frontierCoastRefinedCells=" + frontierCoastRefinedCellCount
                + "; frontierExteriorWaterMidpoints=" + frontierExteriorWaterMidpointCount
                + "; frontierSameOwnerWaterChordRejects=" + frontierSameOwnerWaterChordRejectionCount
                + "; frontierBridgeDiagnostics=" + frontierBridgeDiagnostics
                + "; frontierEntities=" + frontierEntityCount
                + "; fillEntities=" + _territoryFillEntities.Count
                + "; frontierEntityCount=" + _politicalFrontierEntities.Count
                + "; heightSamples=" + CampaignMapTerrainGridCache.CompletedHeightSamples
                + "; terrainSamples=" + CampaignMapTerrainGridCache.CompletedTerrainSamples
                + "; nativeTopologyColumns=" + CampaignMapTerrainGridCache.NativeTopologyColumnsForDiagnostics
                + "; nativeTopologyRows=" + CampaignMapTerrainGridCache.NativeTopologyRowsForDiagnostics
                + "; nativeTopologySamples=" + CampaignMapTerrainGridCache.CompletedNativeTopologySamples
                + "; cacheWallMilliseconds=" + CampaignMapTerrainGridCache.ElapsedMilliseconds
                + "; heightNativeMilliseconds=" + CampaignMapTerrainGridCache.HeightNativeMilliseconds
                + "; terrainNativeMilliseconds=" + CampaignMapTerrainGridCache.TerrainNativeMilliseconds
                + "; exactHeightSamples=" + CampaignMapTerrainGridCache.ExactHeightSampleCount
                + "; exactHeightNativeMilliseconds=" + CampaignMapTerrainGridCache.ExactHeightNativeMilliseconds
                + "; openSeaHeightCeiling=" + CampaignMapTerrainGridCache.OpenSeaHeightCeiling.ToString("F3")
                + "; openSeaHeightSamples=" + CampaignMapTerrainGridCache.OpenSeaHeightSampleCount
                + "; baseLandCells=" + CampaignMapTerrainGridCache.BaseLandCellCount
                + "; elevatedRecoveryCells=" + CampaignMapTerrainGridCache.ElevatedRecoveryCellCount
                + "; retainedWaterCells=" + CampaignMapTerrainGridCache.RetainedWaterCellCount
                + "; nativeRiverCells=" + CampaignMapTerrainGridCache.NativeRiverCellCount
                + "; nativeWaterCells=" + CampaignMapTerrainGridCache.NativeWaterCellCount
                + "; nativeLakeCells=" + CampaignMapTerrainGridCache.NativeLakeCellCount
                + "; nativeCoastalSeaCells=" + CampaignMapTerrainGridCache.NativeCoastalSeaCellCount
                + "; nativeOpenSeaCells=" + CampaignMapTerrainGridCache.NativeOpenSeaCellCount
                + "; interiorNativeWaterCells=" + CampaignMapTerrainGridCache.InteriorNativeWaterCellCount
                + "; exteriorNativeWaterCells=" + CampaignMapTerrainGridCache.ExteriorNativeWaterCellCount
                + "; interiorNativeWaterComponents=" + CampaignMapTerrainGridCache.InteriorNativeWaterComponentCount
                + "; largestInteriorNativeWaterComponent=" + CampaignMapTerrainGridCache.LargestInteriorNativeWaterComponentCellCount
                + "; interiorNativeLakeCells=" + CampaignMapTerrainGridCache.InteriorNativeWaterLakeCellCount
                + "; interiorNativeCoastalSeaCells=" + CampaignMapTerrainGridCache.InteriorNativeWaterCoastalSeaCellCount
                + "; interiorNativeOpenSeaCells=" + CampaignMapTerrainGridCache.InteriorNativeWaterOpenSeaCellCount
                + "; interiorNativeWaterFillAccepts=" + CampaignMapTerrainGridCache.InteriorNativeWaterFillAcceptanceCount
                + "; interiorNativeWaterFrontierAccepts=" + CampaignMapTerrainGridCache.InteriorNativeWaterFrontierAcceptanceCount
                + "; exteriorNativeWaterFillRejects=" + CampaignMapTerrainGridCache.ExteriorNativeWaterFillRejectionCount
                + "; exteriorNativeWaterFrontierRejects=" + CampaignMapTerrainGridCache.ExteriorNativeWaterFrontierRejectionCount
                + "; interiorNativeWaterLargestComponents=" + CampaignMapTerrainGridCache.InteriorNativeWaterComponentDiagnostics
                + "; exactNativeTerrainProbes=" + CampaignMapTerrainGridCache.ExactNativeTerrainProbeCount
                + "; exactProtectedWaterRejections=" + CampaignMapTerrainGridCache.ExactProtectedWaterRejectionCount
                + "; meshMilliseconds=" + meshMilliseconds
                + "; maximumBatchMilliseconds=" + maximumBatchMilliseconds + ".");
            _pendingTerritoryCells = null;
            _lastOwnershipSignature = BuildOwnershipSignature();
            return true;
        }

        private void PrepareTerritoryCells()
        {
            List<BorderSite> sites = BuildSites();
            if (sites.Count < 2)
            {
                _pendingTerritoryCells = new List<PoliticalTerritoryCell>();
                return;
            }

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            foreach (BorderSite site in sites)
            {
                minX = Math.Min(minX, site.Position.x);
                minY = Math.Min(minY, site.Position.y);
                maxX = Math.Max(maxX, site.Position.x);
                maxY = Math.Max(maxY, site.Position.y);
            }

            minX -= MapPadding;
            minY -= MapPadding;
            maxX += MapPadding;
            maxY += MapPadding;

            List<PoliticalTerritoryCell> territoryCells = new List<PoliticalTerritoryCell>(sites.Count);
            for (int siteIndex = 0; siteIndex < sites.Count; siteIndex++)
            {
                territoryCells.Add(new PoliticalTerritoryCell(
                    sites[siteIndex].Position,
                    sites[siteIndex].FactionKey,
                    sites[siteIndex].Color));
            }

            _pendingTerritoryCells = territoryCells;
            _politicalSiteIndex = new CampaignPoliticalTerritoryFill.NearestSiteIndex(territoryCells);
            _pendingMinX = minX;
            _pendingMinY = minY;
            _pendingMaxX = maxX;
            _pendingMaxY = maxY;
        }

        private void MarkDirty()
        {
            CancelPendingFill();
            _pendingTerritoryCells = null;
            _politicalSiteIndex = null;
            PoliticalBoundaryVersion++;
            _dirty = true;
        }

        /// <summary>
        /// Returns true only when the supplied short segment follows an active
        /// political frontier. Requiring the same owner pair at both ends keeps
        /// province contours visible where a political frontier merely crosses
        /// them.
        /// </summary>
        internal bool IsAlignedWithPoliticalFrontier(Vec2 first, Vec2 second)
        {
            if (_politicalSiteIndex == null || !CampaignMapTerrainGridCache.IsReady) return false;
            Vec2 direction = second - first;
            if (direction.Normalize() < 0.001f) return false;
            Vec2 normal = new Vec2(-direction.y, direction.x);
            const float sideSampleDistance = 1.1f;
            string leftOwner;
            string rightOwner;
            if (!TryGetPoliticalOwnerPair(first, normal, sideSampleDistance, out leftOwner, out rightOwner)) return false;

            Vec2 midpoint = (first + second) * 0.5f;
            string midpointLeft;
            string midpointRight;
            if (!TryGetPoliticalOwnerPair(midpoint, normal, sideSampleDistance, out midpointLeft, out midpointRight)
                || !string.Equals(leftOwner, midpointLeft, StringComparison.Ordinal)
                || !string.Equals(rightOwner, midpointRight, StringComparison.Ordinal)) return false;

            string endLeft;
            string endRight;
            return TryGetPoliticalOwnerPair(second, normal, sideSampleDistance, out endLeft, out endRight)
                && string.Equals(leftOwner, endLeft, StringComparison.Ordinal)
                && string.Equals(rightOwner, endRight, StringComparison.Ordinal);
        }

        private bool TryGetPoliticalOwnerPair(
            Vec2 point,
            Vec2 normal,
            float sideSampleDistance,
            out string leftOwner,
            out string rightOwner)
        {
            leftOwner = null;
            rightOwner = null;
            Vec2 leftPoint = point + normal * sideSampleDistance;
            Vec2 rightPoint = point - normal * sideSampleDistance;
            if (!CampaignMapTerrainGridCache.IsFrontierLandExact(leftPoint)
                || !CampaignMapTerrainGridCache.IsFrontierLandExact(rightPoint)) return false;
            PoliticalTerritoryCell left = _politicalSiteIndex.FindNearest(leftPoint);
            PoliticalTerritoryCell right = _politicalSiteIndex.FindNearest(rightPoint);
            leftOwner = left == null ? null : left.OwnerKey;
            rightOwner = right == null ? null : right.OwnerKey;
            return !string.IsNullOrEmpty(leftOwner)
                && !string.IsNullOrEmpty(rightOwner)
                && !string.Equals(leftOwner, rightOwner, StringComparison.Ordinal);
        }

        private static string BuildOwnershipSignature()
        {
            List<string> ownership = new List<string>();
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement == null
                    || (!settlement.IsTown && !settlement.IsCastle && !settlement.IsVillage)
                    || settlement.OwnerClan == null)
                {
                    continue;
                }

                string factionId = settlement.OwnerClan.Kingdom != null
                    ? "kingdom:" + settlement.OwnerClan.Kingdom.StringId
                    : "clan:" + settlement.OwnerClan.StringId;
                ownership.Add(settlement.StringId + "=" + factionId);
            }
            ownership.Sort(StringComparer.Ordinal);
            return string.Join("|", ownership);
        }

        internal void SetPoliticalOverlayAlpha(float alpha)
        {
            float clamped = Math.Max(0f, Math.Min(1f, alpha));
            bool alphaChanged = Math.Abs(clamped - _politicalLayerAlpha) > 0.002f;
            _politicalLayerAlpha = clamped;
            ApplyPoliticalEntityVisibility(alphaChanged);
            ApplyFrontierZoomPresentation(alphaChanged);
        }

        private void ApplyPoliticalEntityVisibility(bool forceAlpha)
        {
            bool shouldRender = _politicalLayerAlpha > 0.001f;
            bool readinessChanged = shouldRender != _politicalEntitiesReady;
            if (!forceAlpha && !readinessChanged) return;
            foreach (GameEntity entity in _territoryFillEntities)
            {
                if (entity == null) continue;
                if (shouldRender)
                {
                    entity.SetAlpha(_politicalLayerAlpha * CampaignPoliticalTerritoryFill.PoliticalFillMaximumOpacity);
                    if (readinessChanged) entity.SetVisibilityExcludeParents(true);
                }
                else if (readinessChanged)
                {
                    entity.SetVisibilityExcludeParents(false);
                }
            }
            _politicalEntitiesReady = shouldRender;
        }

        private void ApplyFrontierZoomPresentation(bool updateHeight)
        {
            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin.z = -CampaignPoliticalTerritoryFill.CloseZoomFrontierDrop
                * (1f - _politicalLayerAlpha);
            foreach (GameEntity entity in _politicalFrontierEntities)
            {
                if (entity == null) continue;
                if (updateHeight) entity.SetGlobalFrame(frame, true);
                entity.SetAlpha(1f);
                entity.SetVisibilityExcludeParents(true);
            }
        }

        private void EnsurePoliticalOverlayView()
        {
            MapScreen mapScreen = MapScreen.Instance;
            if (mapScreen == null || (_politicalOverlayView != null && ReferenceEquals(_politicalOverlayView.MapScreen, mapScreen))) return;
            _politicalOverlayView = mapScreen.AddMapView<CampaignPoliticalOverlayView>() as CampaignPoliticalOverlayView;
            _politicalOverlayView?.AttachBehavior(this);
        }

        private List<BorderSite> BuildSites()
        {
            List<BorderSite> result = new List<BorderSite>();
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement == null
                    || (!settlement.IsTown && !settlement.IsCastle && !settlement.IsVillage)
                    || settlement.OwnerClan == null)
                {
                    continue;
                }

                Clan clan = settlement.OwnerClan;
                string factionKey = clan.Kingdom != null
                    ? "kingdom:" + clan.Kingdom.StringId
                    : "clan:" + clan.StringId;
                uint color = clan.Kingdom != null
                    ? clan.Kingdom.PrimaryBannerColor
                    : clan.Color;
                CampaignVec2 position = settlement.Position;
                result.Add(new BorderSite(
                    new Vec2(position.X, position.Y),
                    factionKey,
                    color));
            }
            return result;
        }

        private void ClearBorderEntities()
        {
            CancelPendingFill();
            ClearActiveBorderEntities();
        }

        private void ClearActiveBorderEntities()
        {
            ClearPoliticalFillEntities();
            ClearPoliticalFrontierEntities();
        }

        private void ReplacePoliticalFillEntities(List<GameEntity> replacement)
        {
            ClearPoliticalFillEntities();
            _territoryFillEntities.AddRange(replacement);
            _politicalEntitiesReady = false;
            ApplyPoliticalEntityVisibility(true);
        }

        private void ReplacePoliticalFrontierEntities(List<GameEntity> replacement)
        {
            ClearPoliticalFrontierEntities();
            _politicalFrontierEntities.AddRange(replacement);
            ApplyFrontierZoomPresentation(true);
        }

        private void ClearPoliticalFillEntities()
        {
            foreach (GameEntity entity in _territoryFillEntities)
            {
                if (entity == null) continue;
                try { entity.Remove(0); }
                catch (Exception exception) { Diagnostics.Error("A political territory fill entity could not be removed.", exception); }
            }
            _territoryFillEntities.Clear();
            _politicalEntitiesReady = false;
        }

        private void ClearPoliticalFrontierEntities()
        {
            foreach (GameEntity entity in _politicalFrontierEntities)
            {
                if (entity == null) continue;
                try { entity.Remove(0); }
                catch (Exception exception) { Diagnostics.Error("A political frontier entity could not be removed.", exception); }
            }
            _politicalFrontierEntities.Clear();
        }

        private void CancelPendingFill()
        {
            if (_pendingFillBuilder == null) return;
            _pendingFillBuilder.Cancel();
            _pendingFillBuilder = null;
        }

        private Scene TryGetCampaignMapScene()
        {
            try
            {
                object wrapper = Campaign.Current == null ? null : Campaign.Current.MapSceneWrapper;
                PropertyInfo sceneProperty = wrapper == null
                    ? null
                    : wrapper.GetType().GetProperty("Scene", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Scene scene = sceneProperty == null ? null : sceneProperty.GetValue(wrapper, null) as Scene;
                _loggedSceneLookupFailure = false;
                return scene;
            }
            catch (Exception exception)
            {
                if (!_loggedSceneLookupFailure)
                {
                    _loggedSceneLookupFailure = true;
                    Diagnostics.Error("Campaign map scene lookup failed; campaign borders will retry when the map scene is available.", exception);
                }
                return null;
            }
        }

        private sealed class BorderSite
        {
            internal BorderSite(Vec2 position, string factionKey, uint color)
            {
                Position = position;
                FactionKey = factionKey;
                Color = color;
            }

            internal Vec2 Position { get; private set; }
            internal string FactionKey { get; private set; }
            internal uint Color { get; private set; }
        }
    }
}
