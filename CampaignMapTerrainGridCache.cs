using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Owns one scene-bound, time-sliced terrain grid shared by campaign-map
    /// political fills and province contours. Native terrain calls stay on the
    /// main thread but are capped by a small per-frame wall-clock budget.
    /// </summary>
    internal static class CampaignMapTerrainGridCache
    {
        internal const int GridColumns = 96;
        private const int NativeTopologyColumns = GridColumns * 4;
        private const int IslandMaximumTopologyCells = 2048;
        private const float TargetArchipelagoExclusionRadius = 28f;
        private const float CoastalFrontierSuppressionRadius = 22f;
        private const float TargetArchipelagoMinimumX = 70f;
        private const float TargetArchipelagoMaximumX = 325f;
        private const float TargetArchipelagoMinimumY = 230f;
        private const float TargetArchipelagoMaximumY = 445f;
        private static readonly Vec2[] TargetArchipelagoSeeds =
        {
            new Vec2(216.40f, 371.73f), new Vec2(292.54f, 313.88f),
            new Vec2(265.14f, 247.88f), new Vec2(146.39f, 320.14f),
            new Vec2(144.64f, 354.53f), new Vec2(162.48f, 368.84f),
            new Vec2(307.80f, 334.89f), new Vec2(271.58f, 335.33f),
            new Vec2(83.55f, 433.02f), new Vec2(194.24f, 350.46f)
        };
        private static readonly Vec2[] CoastalFrontierSuppressionCenters =
        {
            new Vec2(323.66f, 351.95f)
        };
        private const double WorkBudgetMilliseconds = 5d;
        private const float BoundsTolerance = 0.01f;

        private static Scene _scene;
        private static IMapScene _mapScene;
        private static float _minX;
        private static float _minY;
        private static float _maxX;
        private static float _maxY;
        private static float _stepX;
        private static float _stepY;
        private static int _rows;
        private static float[,] _heights;
        private static TerrainType[] _triangleTerrain;
        private static TerrainType[] _nativeTerrain;
        private static TerrainType[] _nativeTopologyTerrain;
        private static bool[] _nativeTopologyTerrainValid;
        private static bool[] _protectedWaterCells;
        private static bool[] _riverCells;
        private static bool[] _interiorWaterCells;
        private static bool[] _excludedIslandCells;
        private static CampaignStrategicLandMask _landMask;
        private static int _nextHeightSample;
        private static int _nextTerrainSample;
        private static int _nextNativeTopologySample;
        private static int _nativeTopologyRows;
        private static float _nativeTopologyStepX;
        private static float _nativeTopologyStepY;
        private static long _startedTimestamp;
        private static long _completedTimestamp;
        private static long _heightNativeTicks;
        private static long _terrainNativeTicks;
        private static long _exactHeightNativeTicks;
        private static int _exactHeightSampleCount;
        private static float _openSeaHeightCeiling;
        private static bool _openSeaHeightReady;
        private static int _openSeaHeightSampleCount;
        private static int _baseLandCellCount;
        private static int _elevatedRecoveryCellCount;
        private static int _retainedWaterCellCount;
        private static int _nativeRiverCellCount;
        private static int _nativeWaterCellCount;
        private static int _nativeLakeCellCount;
        private static int _nativeCoastalSeaCellCount;
        private static int _nativeOpenSeaCellCount;
        private static int _exactNativeTerrainProbeCount;
        private static int _exactProtectedWaterRejectionCount;
        private static int _interiorNativeWaterCellCount;
        private static int _exteriorNativeWaterCellCount;
        private static int _interiorNativeWaterComponentCount;
        private static int _largestInteriorNativeWaterComponentCellCount;
        private static int _interiorNativeWaterLakeCellCount;
        private static int _interiorNativeWaterCoastalSeaCellCount;
        private static int _interiorNativeWaterOpenSeaCellCount;
        private static int _interiorNativeWaterFillAcceptanceCount;
        private static int _interiorNativeWaterFrontierAcceptanceCount;
        private static int _exteriorNativeWaterFillRejectionCount;
        private static int _exteriorNativeWaterFrontierRejectionCount;
        private static string _interiorNativeWaterComponentDiagnostics = string.Empty;
        private static bool _interiorWaterTopologyReady;

        internal static bool IsReady
        {
            get
            {
                return _heights != null
                    && _triangleTerrain != null
                    && _nextHeightSample >= HeightSampleCount
                    && _nextTerrainSample >= TerrainSampleCount
                    && _interiorWaterTopologyReady;
            }
        }

        internal static int Rows { get { return _rows; } }
        internal static int HeightSampleCount { get { return (_rows + 1) * (GridColumns + 1); } }
        internal static int TerrainSampleCount { get { return _rows * GridColumns; } }
        private static int NativeTopologySampleCount { get { return _nativeTopologyRows * NativeTopologyColumns; } }
        internal static int CompletedHeightSamples { get { return _nextHeightSample; } }
        internal static int CompletedTerrainSamples { get { return _nextTerrainSample; } }
        internal static int CompletedNativeTopologySamples { get { return _nextNativeTopologySample; } }
        internal static int NativeTopologyColumnsForDiagnostics { get { return NativeTopologyColumns; } }
        internal static int NativeTopologyRowsForDiagnostics { get { return _nativeTopologyRows; } }
        internal static long ElapsedMilliseconds
        {
            get
            {
                long ended = _completedTimestamp > 0 ? _completedTimestamp : Stopwatch.GetTimestamp();
                return ToMilliseconds(ended - _startedTimestamp);
            }
        }
        internal static long HeightNativeMilliseconds { get { return ToMilliseconds(_heightNativeTicks); } }
        internal static long TerrainNativeMilliseconds { get { return ToMilliseconds(_terrainNativeTicks); } }
        internal static long ExactHeightNativeMilliseconds { get { return ToMilliseconds(_exactHeightNativeTicks); } }
        internal static int ExactHeightSampleCount { get { return _exactHeightSampleCount; } }
        internal static float OpenSeaHeightCeiling { get { return _openSeaHeightCeiling; } }
        internal static int OpenSeaHeightSampleCount { get { return _openSeaHeightSampleCount; } }
        internal static int BaseLandCellCount { get { return _baseLandCellCount; } }
        internal static int ElevatedRecoveryCellCount { get { return _elevatedRecoveryCellCount; } }
        internal static int RetainedWaterCellCount { get { return _retainedWaterCellCount; } }
        internal static int NativeRiverCellCount { get { return _nativeRiverCellCount; } }
        internal static int NativeWaterCellCount { get { return _nativeWaterCellCount; } }
        internal static int NativeLakeCellCount { get { return _nativeLakeCellCount; } }
        internal static int NativeCoastalSeaCellCount { get { return _nativeCoastalSeaCellCount; } }
        internal static int NativeOpenSeaCellCount { get { return _nativeOpenSeaCellCount; } }
        internal static int ExactNativeTerrainProbeCount { get { return _exactNativeTerrainProbeCount; } }
        internal static int ExactProtectedWaterRejectionCount { get { return _exactProtectedWaterRejectionCount; } }
        internal static int InteriorNativeWaterCellCount { get { return _interiorNativeWaterCellCount; } }
        internal static int ExteriorNativeWaterCellCount { get { return _exteriorNativeWaterCellCount; } }
        internal static int InteriorNativeWaterComponentCount { get { return _interiorNativeWaterComponentCount; } }
        internal static int LargestInteriorNativeWaterComponentCellCount { get { return _largestInteriorNativeWaterComponentCellCount; } }
        internal static int InteriorNativeWaterLakeCellCount { get { return _interiorNativeWaterLakeCellCount; } }
        internal static int InteriorNativeWaterCoastalSeaCellCount { get { return _interiorNativeWaterCoastalSeaCellCount; } }
        internal static int InteriorNativeWaterOpenSeaCellCount { get { return _interiorNativeWaterOpenSeaCellCount; } }
        internal static int InteriorNativeWaterFillAcceptanceCount { get { return _interiorNativeWaterFillAcceptanceCount; } }
        internal static int InteriorNativeWaterFrontierAcceptanceCount { get { return _interiorNativeWaterFrontierAcceptanceCount; } }
        internal static int ExteriorNativeWaterFillRejectionCount { get { return _exteriorNativeWaterFillRejectionCount; } }
        internal static int ExteriorNativeWaterFrontierRejectionCount { get { return _exteriorNativeWaterFrontierRejectionCount; } }
        internal static string InteriorNativeWaterComponentDiagnostics { get { return _interiorNativeWaterComponentDiagnostics; } }

        internal static bool BeginOrAdvance(
            Scene scene,
            IMapScene mapScene,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            if (scene == null || mapScene == null || maxX <= minX || maxY <= minY) return false;
            if (!Matches(scene, mapScene, minX, minY, maxX, maxY))
            {
                Reset(scene, mapScene, minX, minY, maxX, maxY);
            }
            if (IsReady) return true;

            Stopwatch budget = Stopwatch.StartNew();
            do
            {
                if (_nextHeightSample < HeightSampleCount) SampleNextHeight();
                else if (_nextTerrainSample < TerrainSampleCount)
                {
                    if (!_openSeaHeightReady) PrepareOpenSeaHeightCeiling();
                    SampleNextTerrain();
                }
                else if (_nextNativeTopologySample < NativeTopologySampleCount) SampleNextNativeTopology();
                else if (!_interiorWaterTopologyReady) FinalizeInteriorWaterTopology();
                else break;
            }
            while (budget.Elapsed.TotalMilliseconds < WorkBudgetMilliseconds);
            if (IsReady && _completedTimestamp == 0) _completedTimestamp = Stopwatch.GetTimestamp();
            return IsReady;
        }

        internal static void ClearForScene(Scene scene)
        {
            if (scene == null || ReferenceEquals(scene, _scene)) Clear();
        }

        internal static Vec3 GetGridPoint(int row, int column, float heightOffset)
        {
            if (!IsReady
                || row < 0 || row > _rows
                || column < 0 || column > GridColumns)
            {
                throw new InvalidOperationException("Campaign terrain grid is not ready for the requested vertex.");
            }
            return new Vec3(
                _minX + column * _stepX,
                _minY + row * _stepY,
                _heights[row, column] + heightOffset);
        }

        internal static TerrainType GetCellTerrain(int row, int column)
        {
            if (!IsReady || row < 0 || row >= _rows || column < 0 || column >= GridColumns)
            {
                throw new InvalidOperationException("Campaign terrain grid is not ready for the requested triangle.");
            }
            return _triangleTerrain[row * GridColumns + column];
        }

        internal static bool IsPoliticalLand(TerrainType terrain)
        {
            return terrain != TerrainType.Water
                && terrain != TerrainType.CoastalSea
                && terrain != TerrainType.OpenSea
                && terrain != TerrainType.SeaRestriction;
        }

        private static bool IsProtectedWater(TerrainType terrain)
        {
            return terrain == TerrainType.CoastalSea
                || terrain == TerrainType.OpenSea
                || terrain == TerrainType.SeaRestriction;
        }

        internal static bool IsPoliticalLandAt(Vec2 point)
        {
            if (!IsReady || point.x < _minX || point.y < _minY || point.x > _maxX || point.y > _maxY) return false;
            float gridX = (point.x - _minX) / _stepX;
            float gridY = (point.y - _minY) / _stepY;
            int column = Math.Min(GridColumns - 1, Math.Max(0, (int)Math.Floor(gridX)));
            int row = Math.Min(_rows - 1, Math.Max(0, (int)Math.Floor(gridY)));
            return IsPoliticalLand(GetCellTerrain(row, column));
        }

        internal static bool IsInteriorWaterAt(Vec2 point)
        {
            if (_interiorWaterCells == null
                || point.x < _minX || point.y < _minY
                || point.x > _maxX || point.y > _maxY)
            {
                return false;
            }
            int column = Math.Min(NativeTopologyColumns - 1, Math.Max(0, (int)Math.Floor((point.x - _minX) / _nativeTopologyStepX)));
            int row = Math.Min(_nativeTopologyRows - 1, Math.Max(0, (int)Math.Floor((point.y - _minY) / _nativeTopologyStepY)));
            return _interiorWaterCells[row * NativeTopologyColumns + column];
        }

        private static bool IsExcludedIslandAt(Vec2 point)
        {
            foreach (Vec2 seed in TargetArchipelagoSeeds)
            {
                float offsetX = point.x - seed.x;
                float offsetY = point.y - seed.y;
                if (offsetX * offsetX + offsetY * offsetY
                    <= TargetArchipelagoExclusionRadius * TargetArchipelagoExclusionRadius)
                {
                    return true;
                }
            }

            if (_excludedIslandCells == null) return false;
            int column = Math.Min(NativeTopologyColumns - 1, Math.Max(0, (int)Math.Floor((point.x - _minX) / _nativeTopologyStepX)));
            int row = Math.Min(_nativeTopologyRows - 1, Math.Max(0, (int)Math.Floor((point.y - _minY) / _nativeTopologyStepY)));
            return _excludedIslandCells[row * NativeTopologyColumns + column];
        }

        private static bool IsCoastalFrontierSuppressedAt(Vec2 point)
        {
            foreach (Vec2 center in CoastalFrontierSuppressionCenters)
            {
                float offsetX = point.x - center.x;
                float offsetY = point.y - center.y;
                if (offsetX * offsetX + offsetY * offsetY
                    <= CoastalFrontierSuppressionRadius * CoastalFrontierSuppressionRadius)
                {
                    return true;
                }
            }
            return false;
        }



        internal static bool IsExteriorNativeWaterAt(Vec2 point)
        {
            if (_nativeTopologyTerrain == null
                || _nativeTopologyTerrainValid == null
                || point.x < _minX || point.y < _minY
                || point.x > _maxX || point.y > _maxY)
            {
                return false;
            }
            int column = Math.Min(NativeTopologyColumns - 1, Math.Max(0, (int)Math.Floor((point.x - _minX) / _nativeTopologyStepX)));
            int row = Math.Min(_nativeTopologyRows - 1, Math.Max(0, (int)Math.Floor((point.y - _minY) / _nativeTopologyStepY)));
            int index = row * NativeTopologyColumns + column;
            return _nativeTopologyTerrainValid[index]
                && !_interiorWaterCells[index]
                && IsNativeWaterTerrain(_nativeTopologyTerrain[index]);
        }

        internal static string GetNativeWaterTopologyDiagnostic(Vec2 point)
        {
            if (IsInteriorWaterAt(point)) return "interior";
            if (IsExteriorNativeWaterAt(point)) return "exterior";
            return "non-water";
        }

        internal static bool IsPoliticalLandExact(Vec2 point)
        {
            bool nativeRiver;
            return IsPoliticalLandExact(point, out nativeRiver);
        }

        internal static bool IsFrontierLandExact(Vec2 point)
        {
            if (!IsReady || _landMask == null || _mapScene == null) return false;
            if (IsExcludedIslandAt(point)) return false;
            if (IsCoastalFrontierSuppressedAt(point)) return false;
            bool strictAuthoredLand = _landMask.IsAuthoredLand(point);
            bool enclosedAuthoredWater = _landMask.IsEnclosedWater(point);
            bool interiorNativeWater = IsInteriorWaterAt(point);
            bool exteriorNativeWater = IsExteriorNativeWaterAt(point);
            bool coarseProtectedWater = IsCoarseProtectedWater(point);
            if (exteriorNativeWater)
            {
                _exteriorNativeWaterFrontierRejectionCount++;
                return false;
            }
            if (interiorNativeWater
                || ((strictAuthoredLand || enclosedAuthoredWater) && !coarseProtectedWater))
            {
                if (interiorNativeWater) _interiorNativeWaterFrontierAcceptanceCount++;
                return true;
            }

            TerrainType nativeTerrain;
            _exactNativeTerrainProbeCount++;
            if (TryGetNativeTerrain(point, out nativeTerrain))
            {
                if ((!interiorNativeWater && IsProtectedWater(nativeTerrain))
                    || (nativeTerrain == TerrainType.Water && !enclosedAuthoredWater && !interiorNativeWater))
                {
                    _exactProtectedWaterRejectionCount++;
                    return false;
                }
                if (IsPoliticalLand(nativeTerrain)) return true;
            }

            if (strictAuthoredLand || enclosedAuthoredWater || interiorNativeWater)
            {
                if (interiorNativeWater) _interiorNativeWaterFrontierAcceptanceCount++;
                return true;
            }
            float height;
            return TrySampleHeight(point, out height)
                && height > _openSeaHeightCeiling
                && _landMask.IsPoliticalLand(point, height, _openSeaHeightCeiling);
        }

        internal static bool IsPoliticalLandExact(Vec2 point, out bool nativeRiver)
        {
            nativeRiver = false;
            if (!IsReady || _landMask == null || _mapScene == null) return false;
            if (IsExcludedIslandAt(point)) return false;
            nativeRiver = IsCoarseRiverAt(point);
            bool strictAuthoredLand = _landMask.IsAuthoredLand(point);
            bool enclosedAuthoredWater = _landMask.IsEnclosedWater(point);
            bool interiorNativeWater = IsInteriorWaterAt(point);
            bool exteriorNativeWater = IsExteriorNativeWaterAt(point);
            if (exteriorNativeWater)
            {
                _exteriorNativeWaterFillRejectionCount++;
                return false;
            }
            TerrainType nativeTerrain = TerrainType.Water;
            bool hasNativeTerrain = false;

            // Most authored-land samples need no native face lookup. Only
            // recheck authored land inside a coarse cell known to contain
            // protected water, which preserves precise coastlines without
            // restoring the rejected all-point navigation scan.
            if (!strictAuthoredLand || IsCoarseProtectedWater(point))
            {
                _exactNativeTerrainProbeCount++;
                hasNativeTerrain = TryGetNativeTerrain(point, out nativeTerrain);
                if (hasNativeTerrain && IsRiverTerrain(nativeTerrain)) nativeRiver = true;
                if (hasNativeTerrain && !interiorNativeWater && IsProtectedWater(nativeTerrain))
                {
                    _exactProtectedWaterRejectionCount++;
                    return false;
                }
            }
            if (strictAuthoredLand || enclosedAuthoredWater || interiorNativeWater)
            {
                if (interiorNativeWater) _interiorNativeWaterFillAcceptanceCount++;
                return true;
            }

            float height;
            if (!TrySampleHeight(point, out height)) return false;
            if (hasNativeTerrain && IsPoliticalLand(nativeTerrain)) return true;

            return _landMask.IsPoliticalLand(point, height, _openSeaHeightCeiling);
        }

        internal static bool IsCoarseRiverAt(Vec2 point)
        {
            if (_riverCells == null
                || point.x < _minX || point.y < _minY
                || point.x > _maxX || point.y > _maxY)
            {
                return false;
            }

            float gridX = (point.x - _minX) / _stepX;
            float gridY = (point.y - _minY) / _stepY;
            int column = Math.Min(GridColumns - 1, Math.Max(0, (int)Math.Floor(gridX)));
            int row = Math.Min(_rows - 1, Math.Max(0, (int)Math.Floor(gridY)));
            if (IsRiverCell(row, column)) return true;

            const float edgeTolerance = 0.0001f;
            bool onLeftEdge = column > 0 && Math.Abs(gridX - Math.Round(gridX)) <= edgeTolerance;
            bool onBottomEdge = row > 0 && Math.Abs(gridY - Math.Round(gridY)) <= edgeTolerance;
            return onLeftEdge && IsRiverCell(row, column - 1)
                || onBottomEdge && IsRiverCell(row - 1, column)
                || onLeftEdge && onBottomEdge && IsRiverCell(row - 1, column - 1);
        }

        private static bool IsCoarseProtectedWater(Vec2 point)
        {
            if (_protectedWaterCells == null
                || point.x < _minX || point.y < _minY
                || point.x > _maxX || point.y > _maxY)
            {
                return false;
            }

            float gridX = (point.x - _minX) / _stepX;
            float gridY = (point.y - _minY) / _stepY;
            int column = Math.Min(GridColumns - 1, Math.Max(0, (int)Math.Floor(gridX)));
            int row = Math.Min(_rows - 1, Math.Max(0, (int)Math.Floor(gridY)));
            return _protectedWaterCells[row * GridColumns + column];
        }

        internal static bool HasLandClearance(Vec2 point, float radius)
        {
            if (!IsPoliticalLandAt(point)) return false;
            const float diagonal = 0.70710678f;
            return IsPoliticalLandAt(new Vec2(point.x + radius, point.y))
                && IsPoliticalLandAt(new Vec2(point.x - radius, point.y))
                && IsPoliticalLandAt(new Vec2(point.x, point.y + radius))
                && IsPoliticalLandAt(new Vec2(point.x, point.y - radius))
                && IsPoliticalLandAt(new Vec2(point.x + radius * diagonal, point.y + radius * diagonal))
                && IsPoliticalLandAt(new Vec2(point.x - radius * diagonal, point.y + radius * diagonal))
                && IsPoliticalLandAt(new Vec2(point.x + radius * diagonal, point.y - radius * diagonal))
                && IsPoliticalLandAt(new Vec2(point.x - radius * diagonal, point.y - radius * diagonal));
        }

        internal static bool TrySampleHeight(Vec2 point, out float height)
        {
            height = 0f;
            if (!IsReady || point.x < _minX || point.y < _minY || point.x > _maxX || point.y > _maxY) return false;
            float gridX = (point.x - _minX) / _stepX;
            float gridY = (point.y - _minY) / _stepY;
            int left = Math.Min(GridColumns - 1, Math.Max(0, (int)Math.Floor(gridX)));
            int bottom = Math.Min(_rows - 1, Math.Max(0, (int)Math.Floor(gridY)));
            int right = left + 1;
            int top = bottom + 1;
            float xBlend = Math.Max(0f, Math.Min(1f, gridX - left));
            float yBlend = Math.Max(0f, Math.Min(1f, gridY - bottom));
            float lower = Lerp(_heights[bottom, left], _heights[bottom, right], xBlend);
            float upper = Lerp(_heights[top, left], _heights[top, right], xBlend);
            height = Lerp(lower, upper, yBlend);
            return true;
        }

        internal static bool TrySampleExactHeight(Vec2 point, out float height)
        {
            height = 0f;
            if (!IsReady
                || _scene == null
                || point.x < _minX || point.y < _minY
                || point.x > _maxX || point.y > _maxY)
            {
                return false;
            }

            Vec3 normal;
            long started = Stopwatch.GetTimestamp();
            _scene.GetTerrainHeightAndNormal(point, out height, out normal);
            _exactHeightNativeTicks += Stopwatch.GetTimestamp() - started;
            _exactHeightSampleCount++;
            return !float.IsNaN(height) && !float.IsInfinity(height);
        }

        private static void Reset(Scene scene, IMapScene mapScene, float minX, float minY, float maxX, float maxY)
        {
            _scene = scene;
            _mapScene = mapScene;
            _minX = minX;
            _minY = minY;
            _maxX = maxX;
            _maxY = maxY;
            float width = maxX - minX;
            float mapHeight = maxY - minY;
            _rows = Math.Max(1, (int)Math.Round(GridColumns * mapHeight / width));
            _stepX = width / GridColumns;
            _stepY = mapHeight / _rows;
            _heights = new float[_rows + 1, GridColumns + 1];
            _triangleTerrain = new TerrainType[_rows * GridColumns];
            _nativeTerrain = new TerrainType[_rows * GridColumns];
            _nativeTopologyRows = Math.Max(1, (int)Math.Round(NativeTopologyColumns * mapHeight / width));
            _nativeTopologyStepX = width / NativeTopologyColumns;
            _nativeTopologyStepY = mapHeight / _nativeTopologyRows;
            _nativeTopologyTerrain = new TerrainType[_nativeTopologyRows * NativeTopologyColumns];
            _nativeTopologyTerrainValid = new bool[_nativeTopologyRows * NativeTopologyColumns];
            _protectedWaterCells = new bool[_rows * GridColumns];
            _riverCells = new bool[_rows * GridColumns];
            _interiorWaterCells = new bool[_nativeTopologyRows * NativeTopologyColumns];
            _landMask = CampaignStrategicLandMask.Load();
            _nextHeightSample = 0;
            _nextTerrainSample = 0;
            _nextNativeTopologySample = 0;
            _interiorWaterTopologyReady = false;
            _startedTimestamp = Stopwatch.GetTimestamp();
            _completedTimestamp = 0;
            _heightNativeTicks = 0;
            _terrainNativeTicks = 0;
            _exactHeightNativeTicks = 0;
            _exactHeightSampleCount = 0;
            _openSeaHeightCeiling = 0f;
            _openSeaHeightReady = false;
            _openSeaHeightSampleCount = 0;
            _baseLandCellCount = 0;
            _elevatedRecoveryCellCount = 0;
            _retainedWaterCellCount = 0;
            ResetNativeTerrainCounts();
        }

        private static void Clear()
        {
            _scene = null;
            _mapScene = null;
            _heights = null;
            _triangleTerrain = null;
            _nativeTerrain = null;
            _nativeTopologyTerrain = null;
            _nativeTopologyTerrainValid = null;
            _protectedWaterCells = null;
            _riverCells = null;
            _interiorWaterCells = null;
            _landMask = null;
            _rows = 0;
            _nextHeightSample = 0;
            _nextTerrainSample = 0;
            _nextNativeTopologySample = 0;
            _nativeTopologyRows = 0;
            _nativeTopologyStepX = 0f;
            _nativeTopologyStepY = 0f;
            _interiorWaterTopologyReady = false;
            _startedTimestamp = 0;
            _completedTimestamp = 0;
            _heightNativeTicks = 0;
            _terrainNativeTicks = 0;
            _exactHeightNativeTicks = 0;
            _exactHeightSampleCount = 0;
            _openSeaHeightCeiling = 0f;
            _openSeaHeightReady = false;
            _openSeaHeightSampleCount = 0;
            _baseLandCellCount = 0;
            _elevatedRecoveryCellCount = 0;
            _retainedWaterCellCount = 0;
            ResetNativeTerrainCounts();
        }

        private static void SampleNextHeight()
        {
            int row = _nextHeightSample / (GridColumns + 1);
            int column = _nextHeightSample % (GridColumns + 1);
            Vec2 point = new Vec2(_minX + column * _stepX, _minY + row * _stepY);
            float height;
            Vec3 normal;
            long started = Stopwatch.GetTimestamp();
            _scene.GetTerrainHeightAndNormal(point, out height, out normal);
            _heightNativeTicks += Stopwatch.GetTimestamp() - started;
            _heights[row, column] = height;
            _nextHeightSample++;
        }

        private static void SampleNextTerrain()
        {
            int row = _nextTerrainSample / GridColumns;
            int column = _nextTerrainSample % GridColumns;
            Vec2 center = new Vec2(
                _minX + (column + 0.5f) * _stepX,
                _minY + (row + 0.5f) * _stepY);
            float height = GetCellHeight(row, column);
            TerrainType nativeTerrain;
            bool hasNativeTerrain = TryGetNativeTerrain(center, out nativeTerrain);
            _nativeTerrain[_nextTerrainSample] = hasNativeTerrain ? nativeTerrain : TerrainType.Water;
            if (hasNativeTerrain) CountNativeTerrain(nativeTerrain);
            bool baseLand = _landMask.IsPoliticalLand(center);
            bool protectedWater = hasNativeTerrain && IsProtectedWater(nativeTerrain);
            bool river = hasNativeTerrain && IsRiverTerrain(nativeTerrain);
            bool politicalLand = protectedWater
                ? false
                : (hasNativeTerrain && IsPoliticalLand(nativeTerrain))
                    || baseLand
                    || _landMask.IsPoliticalLand(center, height, _openSeaHeightCeiling);
            if (baseLand) _baseLandCellCount++;
            else if (politicalLand) _elevatedRecoveryCellCount++;
            else _retainedWaterCellCount++;
            _protectedWaterCells[_nextTerrainSample] = protectedWater;
            _riverCells[_nextTerrainSample] = river;
            _triangleTerrain[_nextTerrainSample] = politicalLand ? TerrainType.Plain : TerrainType.Water;
            _nextTerrainSample++;
        }

        private static void SampleNextNativeTopology()
        {
            int row = _nextNativeTopologySample / NativeTopologyColumns;
            int column = _nextNativeTopologySample % NativeTopologyColumns;
            Vec2 center = new Vec2(
                _minX + (column + 0.5f) * _nativeTopologyStepX,
                _minY + (row + 0.5f) * _nativeTopologyStepY);
            TerrainType terrain;
            bool validTerrain = TryGetNativeTerrain(center, out terrain);
            _nativeTopologyTerrainValid[_nextNativeTopologySample] = validTerrain;
            _nativeTopologyTerrain[_nextNativeTopologySample] = validTerrain ? terrain : TerrainType.Water;
            _nextNativeTopologySample++;
        }

        private static void FinalizeInteriorWaterTopology()
        {
            bool[] exteriorWater = new bool[NativeTopologySampleCount];
            Queue<int> pending = new Queue<int>();
            for (int column = 0; column < NativeTopologyColumns; column++)
            {
                AddExteriorNativeWater(0, column, exteriorWater, pending);
                if (_nativeTopologyRows > 1) AddExteriorNativeWater(_nativeTopologyRows - 1, column, exteriorWater, pending);
            }
            for (int row = 1; row < _nativeTopologyRows - 1; row++)
            {
                AddExteriorNativeWater(row, 0, exteriorWater, pending);
                if (NativeTopologyColumns > 1) AddExteriorNativeWater(row, NativeTopologyColumns - 1, exteriorWater, pending);
            }
            while (pending.Count > 0)
            {
                int index = pending.Dequeue();
                int row = index / NativeTopologyColumns;
                int column = index % NativeTopologyColumns;
                AddExteriorNativeWater(row - 1, column, exteriorWater, pending);
                AddExteriorNativeWater(row + 1, column, exteriorWater, pending);
                AddExteriorNativeWater(row, column - 1, exteriorWater, pending);
                AddExteriorNativeWater(row, column + 1, exteriorWater, pending);
            }

            for (int index = 0; index < NativeTopologySampleCount; index++)
            {
                if (exteriorWater[index]) _exteriorNativeWaterCellCount++;
            }

            bool[] visitedInteriorWater = new bool[NativeTopologySampleCount];
            List<NativeWaterTopologyComponent> components = new List<NativeWaterTopologyComponent>();
            for (int index = 0; index < NativeTopologySampleCount; index++)
            {
                if (exteriorWater[index]
                    || visitedInteriorWater[index]
                    || !IsNativeWaterTerrain(_nativeTopologyTerrain[index]))
                {
                    continue;
                }

                NativeWaterTopologyComponent component = MarkInteriorNativeWaterComponent(index, exteriorWater, visitedInteriorWater);
                _interiorNativeWaterComponentCount++;
                _largestInteriorNativeWaterComponentCellCount = Math.Max(
                    _largestInteriorNativeWaterComponentCellCount,
                    component.CellCount);
                components.Add(component);
            }
            components.Sort((left, right) => right.CellCount.CompareTo(left.CellCount));
            int diagnosticCount = Math.Min(12, components.Count);
            string[] diagnosticParts = new string[diagnosticCount];
            for (int index = 0; index < diagnosticCount; index++) diagnosticParts[index] = components[index].Describe();
            _interiorNativeWaterComponentDiagnostics = string.Join(" | ", diagnosticParts);
            BuildIslandExclusion(exteriorWater);
            _interiorWaterTopologyReady = true;
        }

        private static void BuildIslandExclusion(bool[] exteriorWater)
        {
            _excludedIslandCells = new bool[NativeTopologySampleCount];
            foreach (Vec2 seedPoint in TargetArchipelagoSeeds)
            {
                int column = Math.Min(NativeTopologyColumns - 1, Math.Max(0, (int)Math.Floor((seedPoint.x - _minX) / _nativeTopologyStepX)));
                int row = Math.Min(_nativeTopologyRows - 1, Math.Max(0, (int)Math.Floor((seedPoint.y - _minY) / _nativeTopologyStepY)));
                int seed = FindNearestIslandTerrainSeed(row, column, exteriorWater);
                if (seed < 0 || _excludedIslandCells[seed])
                {
                    Diagnostics.Info("Campaign target archipelago seed ignored: x=" + seedPoint.x.ToString("F2")
                        + "; y=" + seedPoint.y.ToString("F2") + ".");
                    continue;
                }
                if (seed != row * NativeTopologyColumns + column)
                {
                    Diagnostics.Info("Campaign target archipelago seed snapped from water: x=" + seedPoint.x.ToString("F2")
                        + "; y=" + seedPoint.y.ToString("F2") + ".");
                }

                bool[] visited = new bool[NativeTopologySampleCount];
                List<int> component = new List<int>();
                Queue<int> pending = new Queue<int>();
                visited[seed] = true;
                pending.Enqueue(seed);
                while (pending.Count > 0 && component.Count <= IslandMaximumTopologyCells)
                {
                    int index = pending.Dequeue();
                    component.Add(index);
                    int currentRow = index / NativeTopologyColumns; int currentColumn = index % NativeTopologyColumns;
                    AddIslandNeighbor(currentRow - 1, currentColumn, exteriorWater, visited, pending);
                    AddIslandNeighbor(currentRow + 1, currentColumn, exteriorWater, visited, pending);
                    AddIslandNeighbor(currentRow, currentColumn - 1, exteriorWater, visited, pending);
                    AddIslandNeighbor(currentRow, currentColumn + 1, exteriorWater, visited, pending);
                }

                bool exclude = component.Count <= IslandMaximumTopologyCells;
                if (exclude)
                {
                    foreach (int index in component) _excludedIslandCells[index] = true;
                }
                Diagnostics.Info("Campaign target archipelago component: x=" + seedPoint.x.ToString("F2")
                    + "; y=" + seedPoint.y.ToString("F2") + "; cells=" + component.Count
                    + "; excluded=" + exclude + ".");
            }
        }

        private static void AddIslandNeighbor(int row, int column, bool[] exterior, bool[] visited, Queue<int> pending)
        {
            if (row < 0 || column < 0 || row >= _nativeTopologyRows || column >= NativeTopologyColumns) return;
            int index = row * NativeTopologyColumns + column;
            if (visited[index] || !_nativeTopologyTerrainValid[index] || exterior[index]) return;
            visited[index] = true; pending.Enqueue(index);
        }

        private static void ExcludeUnseededTargetArchipelagoComponents(bool[] exteriorWater)
        {
            bool[] visited = new bool[NativeTopologySampleCount];
            int excludedComponents = 0;
            for (int seed = 0; seed < NativeTopologySampleCount; seed++)
            {
                if (visited[seed] || !_nativeTopologyTerrainValid[seed] || exteriorWater[seed]) continue;

                List<int> component = new List<int>();
                Queue<int> pending = new Queue<int>();
                int componentCount = 0;
                bool touchesTargetArea = false;
                visited[seed] = true;
                pending.Enqueue(seed);
                while (pending.Count > 0)
                {
                    int index = pending.Dequeue();
                    componentCount++;
                    if (componentCount <= IslandMaximumTopologyCells) component.Add(index);
                    int row = index / NativeTopologyColumns;
                    int column = index % NativeTopologyColumns;
                    float x = _minX + (column + 0.5f) * _nativeTopologyStepX;
                    float y = _minY + (row + 0.5f) * _nativeTopologyStepY;
                    if (x >= TargetArchipelagoMinimumX && x <= TargetArchipelagoMaximumX
                        && y >= TargetArchipelagoMinimumY && y <= TargetArchipelagoMaximumY)
                    {
                        touchesTargetArea = true;
                    }
                    AddIslandNeighbor(row - 1, column, exteriorWater, visited, pending);
                    AddIslandNeighbor(row + 1, column, exteriorWater, visited, pending);
                    AddIslandNeighbor(row, column - 1, exteriorWater, visited, pending);
                    AddIslandNeighbor(row, column + 1, exteriorWater, visited, pending);
                }

                if (!touchesTargetArea || componentCount > IslandMaximumTopologyCells) continue;
                bool changed = false;
                foreach (int index in component)
                {
                    if (_excludedIslandCells[index]) continue;
                    _excludedIslandCells[index] = true;
                    changed = true;
                }
                if (changed) excludedComponents++;
            }
            Diagnostics.Info("Campaign target archipelago automatic components excluded=" + excludedComponents + ".");
        }

        private static int FindNearestIslandTerrainSeed(int centerRow, int centerColumn, bool[] exteriorWater)
        {
            const int SearchRadius = 8;
            for (int radius = 0; radius <= SearchRadius; radius++)
            {
                for (int rowOffset = -radius; rowOffset <= radius; rowOffset++)
                {
                    for (int columnOffset = -radius; columnOffset <= radius; columnOffset++)
                    {
                        if (Math.Max(Math.Abs(rowOffset), Math.Abs(columnOffset)) != radius) continue;
                        int row = centerRow + rowOffset;
                        int column = centerColumn + columnOffset;
                        if (row < 0 || column < 0 || row >= _nativeTopologyRows || column >= NativeTopologyColumns) continue;
                        int index = row * NativeTopologyColumns + column;
                        if (_nativeTopologyTerrainValid[index] && !exteriorWater[index]) return index;
                    }
                }
            }
            return -1;
        }

        private static NativeWaterTopologyComponent MarkInteriorNativeWaterComponent(
            int seed,
            bool[] exteriorWater,
            bool[] visitedInteriorWater)
        {
            NativeWaterTopologyComponent component = new NativeWaterTopologyComponent();
            Queue<int> pending = new Queue<int>();
            visitedInteriorWater[seed] = true;
            pending.Enqueue(seed);
            while (pending.Count > 0)
            {
                int index = pending.Dequeue();
                TerrainType terrain = _nativeTopologyTerrain[index];
                int row = index / NativeTopologyColumns;
                int column = index % NativeTopologyColumns;
                _interiorWaterCells[index] = true;
                _interiorNativeWaterCellCount++;
                component.Add(row, column, terrain);
                CountInteriorNativeWaterTerrain(terrain);
                AddInteriorNativeWaterNeighbor(row - 1, column, exteriorWater, visitedInteriorWater, pending);
                AddInteriorNativeWaterNeighbor(row + 1, column, exteriorWater, visitedInteriorWater, pending);
                AddInteriorNativeWaterNeighbor(row, column - 1, exteriorWater, visitedInteriorWater, pending);
                AddInteriorNativeWaterNeighbor(row, column + 1, exteriorWater, visitedInteriorWater, pending);
            }
            return component;
        }

        private sealed class NativeWaterTopologyComponent
        {
            private int _minimumRow = int.MaxValue;
            private int _maximumRow = int.MinValue;
            private int _minimumColumn = int.MaxValue;
            private int _maximumColumn = int.MinValue;
            private int _coastalSeaCells;
            private int _openSeaCells;
            private int _lakeCells;

            internal int CellCount { get; private set; }

            internal void Add(int row, int column, TerrainType terrain)
            {
                CellCount++;
                _minimumRow = Math.Min(_minimumRow, row);
                _maximumRow = Math.Max(_maximumRow, row);
                _minimumColumn = Math.Min(_minimumColumn, column);
                _maximumColumn = Math.Max(_maximumColumn, column);
                if (terrain == TerrainType.CoastalSea) _coastalSeaCells++;
                else if (terrain == TerrainType.OpenSea) _openSeaCells++;
                else if (terrain == TerrainType.Lake) _lakeCells++;
            }

            internal string Describe()
            {
                float centerX = _minX + ((_minimumColumn + _maximumColumn + 1) * 0.5f) * _nativeTopologyStepX;
                float centerY = _minY + ((_minimumRow + _maximumRow + 1) * 0.5f) * _nativeTopologyStepY;
                return "cells=" + CellCount
                    + "@(" + centerX.ToString("F1") + "," + centerY.ToString("F1") + ")"
                    + " span=" + (_maximumColumn - _minimumColumn + 1) + "x" + (_maximumRow - _minimumRow + 1)
                    + " lake=" + _lakeCells
                    + " coastal=" + _coastalSeaCells
                    + " open=" + _openSeaCells;
            }
        }

        private static void AddInteriorNativeWaterNeighbor(
            int row,
            int column,
            bool[] exteriorWater,
            bool[] visitedInteriorWater,
            Queue<int> pending)
        {
            if (row < 0 || column < 0 || row >= _nativeTopologyRows || column >= NativeTopologyColumns) return;
            int index = row * NativeTopologyColumns + column;
            if (exteriorWater[index]
                || visitedInteriorWater[index]
                || !IsNativeWaterTerrain(_nativeTopologyTerrain[index])) return;
            visitedInteriorWater[index] = true;
            pending.Enqueue(index);
        }

        private static void CountInteriorNativeWaterTerrain(TerrainType terrain)
        {
            if (terrain == TerrainType.Lake) _interiorNativeWaterLakeCellCount++;
            else if (terrain == TerrainType.CoastalSea) _interiorNativeWaterCoastalSeaCellCount++;
            else if (terrain == TerrainType.OpenSea) _interiorNativeWaterOpenSeaCellCount++;
        }

        private static void AddExteriorNativeWater(
            int row,
            int column,
            bool[] exteriorWater,
            Queue<int> pending)
        {
            if (row < 0 || column < 0 || row >= _nativeTopologyRows || column >= NativeTopologyColumns) return;
            int index = row * NativeTopologyColumns + column;
            TerrainType terrain = _nativeTopologyTerrain[index];
            if (exteriorWater[index] || !IsNativeWaterTerrain(terrain))
            {
                return;
            }
            exteriorWater[index] = true;
            pending.Enqueue(index);
        }

        private static bool IsNativeWaterTerrain(TerrainType terrain)
        {
            return terrain == TerrainType.Water
                || terrain == TerrainType.Lake
                || IsProtectedWater(terrain);
        }

        private static void PrepareOpenSeaHeightCeiling()
        {
            List<float> perimeterWaterHeights = new List<float>();
            for (int column = 0; column < GridColumns; column++)
            {
                AddPerimeterWaterHeight(0, column, perimeterWaterHeights);
                if (_rows > 1) AddPerimeterWaterHeight(_rows - 1, column, perimeterWaterHeights);
            }
            for (int row = 1; row < _rows - 1; row++)
            {
                AddPerimeterWaterHeight(row, 0, perimeterWaterHeights);
                if (GridColumns > 1) AddPerimeterWaterHeight(row, GridColumns - 1, perimeterWaterHeights);
            }
            if (perimeterWaterHeights.Count == 0)
            {
                for (int row = 0; row < _rows; row++)
                {
                    for (int column = 0; column < GridColumns; column++)
                    {
                        AddPerimeterWaterHeight(row, column, perimeterWaterHeights);
                    }
                }
            }
            if (perimeterWaterHeights.Count == 0)
            {
                _openSeaHeightCeiling = float.MaxValue;
                _openSeaHeightReady = true;
                return;
            }

            float median = Median(perimeterWaterHeights);
            List<float> deviations = new List<float>(perimeterWaterHeights.Count);
            foreach (float height in perimeterWaterHeights) deviations.Add(Math.Abs(height - median));
            float medianDeviation = Median(deviations);
            _openSeaHeightCeiling = median + Math.Max(0.5f, medianDeviation * 3f);
            _openSeaHeightSampleCount = perimeterWaterHeights.Count;
            _openSeaHeightReady = true;
        }

        private static void AddPerimeterWaterHeight(int row, int column, List<float> heights)
        {
            Vec2 center = GetCellCenter(row, column);
            if (!_landMask.IsPoliticalLand(center)) heights.Add(GetCellHeight(row, column));
        }

        private static Vec2 GetCellCenter(int row, int column)
        {
            return new Vec2(
                _minX + (column + 0.5f) * _stepX,
                _minY + (row + 0.5f) * _stepY);
        }

        private static float GetCellHeight(int row, int column)
        {
            return (_heights[row, column]
                + _heights[row, column + 1]
                + _heights[row + 1, column]
                + _heights[row + 1, column + 1]) * 0.25f;
        }

        private static bool TryGetNativeTerrain(Vec2 point, out TerrainType terrain)
        {
            terrain = TerrainType.Water;
            if (_mapScene == null) return false;
            CampaignVec2 campaignPoint = new CampaignVec2(point, isOnLand: false);
            // CampaignVec2.IsValid() also applies party-navigation rules and
            // can reject a real water face before its terrain is classified.
            // Face validity is the correct contract for this visual probe.
            if (!campaignPoint.Face.IsValid()) return false;
            long started = Stopwatch.GetTimestamp();
            terrain = _mapScene.GetTerrainTypeAtPosition(campaignPoint);
            _terrainNativeTicks += Stopwatch.GetTimestamp() - started;
            return true;
        }

        private static bool IsRiverTerrain(TerrainType terrain)
        {
            return terrain == TerrainType.River
                || terrain == TerrainType.NonNavigableRiver
                || terrain == TerrainType.Fording;
        }

        private static bool IsRiverCell(int row, int column)
        {
            return row >= 0 && row < _rows
                && column >= 0 && column < GridColumns
                && _riverCells[row * GridColumns + column];
        }

        private static void ResetNativeTerrainCounts()
        {
            _nativeRiverCellCount = 0;
            _nativeWaterCellCount = 0;
            _nativeLakeCellCount = 0;
            _nativeCoastalSeaCellCount = 0;
            _nativeOpenSeaCellCount = 0;
            _exactNativeTerrainProbeCount = 0;
            _exactProtectedWaterRejectionCount = 0;
            _interiorNativeWaterCellCount = 0;
            _exteriorNativeWaterCellCount = 0;
            _interiorNativeWaterComponentCount = 0;
            _largestInteriorNativeWaterComponentCellCount = 0;
            _interiorNativeWaterLakeCellCount = 0;
            _interiorNativeWaterCoastalSeaCellCount = 0;
            _interiorNativeWaterOpenSeaCellCount = 0;
            _interiorNativeWaterFillAcceptanceCount = 0;
            _interiorNativeWaterFrontierAcceptanceCount = 0;
            _exteriorNativeWaterFillRejectionCount = 0;
            _exteriorNativeWaterFrontierRejectionCount = 0;
            _interiorNativeWaterComponentDiagnostics = string.Empty;
        }

        private static void CountNativeTerrain(TerrainType terrain)
        {
            if (terrain == TerrainType.River) _nativeRiverCellCount++;
            else if (terrain == TerrainType.Water) _nativeWaterCellCount++;
            else if (terrain == TerrainType.Lake) _nativeLakeCellCount++;
            else if (terrain == TerrainType.CoastalSea) _nativeCoastalSeaCellCount++;
            else if (terrain == TerrainType.OpenSea) _nativeOpenSeaCellCount++;
        }

        private static float Median(List<float> values)
        {
            values.Sort();
            int middle = values.Count / 2;
            return (values.Count & 1) == 0
                ? (values[middle - 1] + values[middle]) * 0.5f
                : values[middle];
        }

        private static bool Matches(Scene scene, IMapScene mapScene, float minX, float minY, float maxX, float maxY)
        {
            return ReferenceEquals(scene, _scene)
                && ReferenceEquals(mapScene, _mapScene)
                && Math.Abs(minX - _minX) <= BoundsTolerance
                && Math.Abs(minY - _minY) <= BoundsTolerance
                && Math.Abs(maxX - _maxX) <= BoundsTolerance
                && Math.Abs(maxY - _maxY) <= BoundsTolerance;
        }

        private static float Lerp(float first, float second, float amount)
        {
            return first + (second - first) * amount;
        }

        private static long ToMilliseconds(long ticks)
        {
            if (ticks <= 0) return 0;
            return (long)Math.Round(ticks * 1000d / Stopwatch.Frequency);
        }
    }
}
