using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    internal sealed class PoliticalTerritoryCell
    {
        internal PoliticalTerritoryCell(Vec2 site, string ownerKey, uint color)
        {
            Site = site;
            OwnerKey = ownerKey;
            Color = color;
        }

        internal Vec2 Site { get; private set; }
        internal string OwnerKey { get; private set; }
        internal uint Color { get; private set; }
    }

    /// <summary>
    /// Builds opaque, vertex-coloured terrain meshes from the shared campaign
    /// grid. Each advance submits one bounded row entity so Bannerlord never
    /// has to finalize one very large editable mesh on the map thread.
    /// </summary>
    internal static class CampaignPoliticalTerritoryFill
    {
        private const float FillHeight = 4f;
        private const float EnclosedWaterCapLift = 4f;
        private const float TerrainReliefRefinementThreshold = 0.75f;
        private const uint FillBrightnessPercent = 50u;
        private const uint FrontierBrightnessPercent = 85u;
        internal const float PoliticalFillMaximumOpacity = 1f;
        private const uint FillAlpha = 0xFF000000u;
        private const string FillMaterial = "vertex_color_mat";
        private const int PoliticalMeshRenderOrder = 100;
        private const int RiverMeshRenderOrder = 100;
        private const int FrontierMeshRenderOrder = 108;
        private const int FrontierSubdivisions = 4;
        private const int FrontierCoastRefinementDepth = 1;
        private const float FrontierHeight = 5f;
        private const float FrontierWidth = 1.6f;
        private const float FrontierSideSampleDistance = 2.75f;
        private const float FrontierLandSupportSampleDistance = 1f;
        private const int FrontierCapStepsPerHalf = 4;
        internal const float CloseZoomFrontierDrop = 4.65f;
        private const int MaximumRefinementDepth = 2;
        private const int MaximumRowsPerFrame = 8;
        private const double FrameBudgetMilliseconds = 4d;

        internal static Builder Begin(List<PoliticalTerritoryCell> cells)
        {
            return cells == null || cells.Count == 0 || !CampaignMapTerrainGridCache.IsReady
                ? null
                : new Builder(cells);
        }

        private static bool AddTriangle(
            Mesh mesh,
            NearestSiteIndex siteIndex,
            Vec3 first,
            Vec3 second,
            Vec3 third,
            UIntPtr handle)
        {
            Vec2 center = new Vec2(
                (first.x + second.x + third.x) / 3f,
                (first.y + second.y + third.y) / 3f);
            PoliticalTerritoryCell owner = siteIndex.FindNearest(center);
            if (owner == null) return false;
            uint color = ScaleOpaqueColor(owner.Color, FillBrightnessPercent);
            Vec2 uv = Vec2.Zero;
            mesh.AddTriangle(first, second, third, uv, uv, uv, color, handle);
            mesh.AddTriangle(first, third, second, uv, uv, uv, color, handle);
            return true;
        }

        internal sealed class Builder
        {
            private readonly NearestSiteIndex _siteIndex;
            private readonly HashSet<FrontierPointKey> _frontierCapPoints = new HashSet<FrontierPointKey>();
            private readonly List<string> _frontierBridgeDiagnostics = new List<string>();
            private List<GameEntity> _entities = new List<GameEntity>();
            private List<GameEntity> _frontierEntities = new List<GameEntity>();
            private int _nextRow;
            private int _nextFrontierRow;

            internal Builder(List<PoliticalTerritoryCell> cells)
            {
                _siteIndex = new NearestSiteIndex(cells);
            }

            internal int LandSampleCount { get; private set; }
            internal int SeaSampleCount { get; private set; }
            internal int RenderedTriangleCount { get; private set; }
            internal int RiverRenderedTriangleCount { get; private set; }
            internal int EnclosedWaterRenderedTriangleCount { get; private set; }
            internal int ExteriorWaterTriangleRejectionCount { get; private set; }
            internal int RiverEntityCount { get; private set; }
            internal int RefinedCellCount { get; private set; }
            internal int TerrainReliefRefinedCellCount { get; private set; }
            internal int FrontierRenderedSegmentCount { get; private set; }
            internal int FrontierEntityCount { get; private set; }
            internal int FrontierCandidateSegmentCount { get; private set; }
            internal int FrontierUnsupportedSegmentCount { get; private set; }
            internal int FrontierProjectionRejectedSegmentCount { get; private set; }
            internal int FrontierSaddleCellCount { get; private set; }
            internal int FrontierAmbiguousCellCount { get; private set; }
            internal int FrontierCoastRefinedCellCount { get; private set; }
            internal int FrontierExteriorWaterMidpointCount { get; private set; }
            internal int FrontierSameOwnerWaterChordRejectionCount { get; private set; }
            internal string FrontierBridgeDiagnostics { get { return string.Join(" | ", _frontierBridgeDiagnostics.ToArray()); } }
            internal long MeshMilliseconds { get; private set; }
            internal long MaximumBatchMilliseconds { get; private set; }
            internal bool IsFillComplete { get { return _nextRow >= CampaignMapTerrainGridCache.Rows; } }
            internal bool FillEntitiesTaken { get { return _entities == null; } }
            internal bool IsComplete
            {
                get
                {
                    return _nextRow >= CampaignMapTerrainGridCache.Rows
                        && _nextFrontierRow >= CampaignMapTerrainGridCache.Rows * FrontierSubdivisions;
                }
            }

            internal void Advance(Scene scene)
            {
                if (scene == null || IsComplete) return;
                System.Diagnostics.Stopwatch frameTimer = System.Diagnostics.Stopwatch.StartNew();
                for (int count = 0;
                    count < MaximumRowsPerFrame
                        && !IsComplete
                        && (count == 0 || frameTimer.Elapsed.TotalMilliseconds < FrameBudgetMilliseconds);
                    count++)
                {
                    if (_nextRow < CampaignMapTerrainGridCache.Rows) BuildNextRow(scene);
                    else BuildNextFrontierRow(scene);
                }
            }

            private void BuildNextRow(Scene scene)
            {
                System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
                int row = _nextRow++;
                Mesh landMesh = CreateRowMesh(PoliticalMeshRenderOrder);
                Mesh riverMesh = CreateRowMesh(RiverMeshRenderOrder);
                Mesh enclosedWaterMesh = CreateRowMesh(RiverMeshRenderOrder);
                int rowLandTriangles = 0;
                int rowRiverTriangles = 0;
                int rowEnclosedWaterTriangles = 0;
                UIntPtr landHandle = landMesh.LockEditDataWrite();
                UIntPtr riverHandle = riverMesh.LockEditDataWrite();
                UIntPtr enclosedWaterHandle = enclosedWaterMesh.LockEditDataWrite();
                try
                {
                    for (int column = 0; column < CampaignMapTerrainGridCache.GridColumns; column++)
                    {
                        Vec3 lowerLeft = CampaignMapTerrainGridCache.GetGridPoint(row, column, FillHeight);
                        Vec3 lowerRight = CampaignMapTerrainGridCache.GetGridPoint(row, column + 1, FillHeight);
                        Vec3 upperRight = CampaignMapTerrainGridCache.GetGridPoint(row + 1, column + 1, FillHeight);
                        Vec3 upperLeft = CampaignMapTerrainGridCache.GetGridPoint(row + 1, column, FillHeight);
                        AddAdaptiveCell(
                            landMesh,
                            riverMesh,
                            enclosedWaterMesh,
                            lowerLeft,
                            lowerRight,
                            upperRight,
                            upperLeft,
                            0,
                            landHandle,
                            riverHandle,
                            enclosedWaterHandle,
                            ref rowLandTriangles,
                            ref rowRiverTriangles,
                            ref rowEnclosedWaterTriangles);
                    }
                }
                finally
                {
                    riverMesh.UnlockEditDataWrite(riverHandle);
                    enclosedWaterMesh.UnlockEditDataWrite(enclosedWaterHandle);
                    landMesh.UnlockEditDataWrite(landHandle);
                }

                if (rowLandTriangles > 0) AddRowEntity(scene, landMesh, forceDecals: false, riverCap: false);
                if (rowRiverTriangles > 0) AddRowEntity(scene, riverMesh, forceDecals: false, riverCap: true);
                if (rowEnclosedWaterTriangles > 0) AddRowEntity(scene, enclosedWaterMesh, forceDecals: false, riverCap: true);

                timer.Stop();
                MeshMilliseconds += timer.ElapsedMilliseconds;
                MaximumBatchMilliseconds = Math.Max(MaximumBatchMilliseconds, timer.ElapsedMilliseconds);
            }

            private static Mesh CreateRowMesh(int renderOrder)
            {
                Mesh mesh = Mesh.CreateMesh(true);
                if (mesh == null) throw new InvalidOperationException("Bannerlord could not allocate a political fill row mesh.");
                mesh.SetMaterial(FillMaterial);
                mesh.SetMeshRenderOrder(renderOrder);
                return mesh;
            }

            private void AddRowEntity(Scene scene, Mesh mesh, bool forceDecals, bool riverCap)
            {
                mesh.ComputeNormals();
                mesh.RecomputeBoundingBox();
                GameEntity entity = GameEntity.CreateEmpty(scene, false, true, true);
                if (entity == null) throw new InvalidOperationException("Bannerlord could not allocate a political fill row entity.");
                entity.SetGlobalFrame(MatrixFrame.Identity, true);
                entity.AddMesh(mesh, true);
                entity.SetForceDecalsToRender(forceDecals);
                entity.SetVisibilityExcludeParents(false);
                entity.SetReadyToRender(true);
                entity.SetAlpha(0f);
                _entities.Add(entity);
                if (riverCap) RiverEntityCount++;
            }

            private void BuildNextFrontierRow(Scene scene)
            {
                System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
                int fineRow = _nextFrontierRow++;
                int fineColumns = CampaignMapTerrainGridCache.GridColumns * FrontierSubdivisions;
                int fineRows = CampaignMapTerrainGridCache.Rows * FrontierSubdivisions;
                Vec3 minimum = CampaignMapTerrainGridCache.GetGridPoint(0, 0, 0f);
                Vec3 maximum = CampaignMapTerrainGridCache.GetGridPoint(
                    CampaignMapTerrainGridCache.Rows,
                    CampaignMapTerrainGridCache.GridColumns,
                    0f);
                float stepX = (maximum.x - minimum.x) / fineColumns;
                float stepY = (maximum.y - minimum.y) / fineRows;
                float lowerY = minimum.y + fineRow * stepY;
                float upperY = lowerY + stepY;
                FrontierRegion[] lowerRegions = new FrontierRegion[fineColumns + 1];
                FrontierRegion[] upperRegions = new FrontierRegion[fineColumns + 1];
                for (int column = 0; column <= fineColumns; column++)
                {
                    float x = minimum.x + column * stepX;
                    lowerRegions[column] = GetFrontierRegion(new Vec2(x, lowerY));
                    upperRegions[column] = GetFrontierRegion(new Vec2(x, upperY));
                }

                Mesh mesh = CreateRowMesh(FrontierMeshRenderOrder);
                int segmentCount = 0;
                UIntPtr lockHandle = mesh.LockEditDataWrite();
                try
                {
                    for (int column = 0; column < fineColumns; column++)
                    {
                        float leftX = minimum.x + column * stepX;
                        float rightX = leftX + stepX;
                        Vec2 lowerLeft = new Vec2(leftX, lowerY);
                        Vec2 lowerRight = new Vec2(rightX, lowerY);
                        Vec2 upperRight = new Vec2(rightX, upperY);
                        Vec2 upperLeft = new Vec2(leftX, upperY);
                        BuildFrontierCell(
                            mesh,
                            lowerLeft,
                            lowerRight,
                            upperRight,
                            upperLeft,
                            lowerRegions[column],
                            lowerRegions[column + 1],
                            upperRegions[column + 1],
                            upperRegions[column],
                            0,
                            lockHandle,
                            ref segmentCount);
                    }
                }
                finally
                {
                    mesh.UnlockEditDataWrite(lockHandle);
                }

                if (segmentCount > 0)
                {
                    AddFrontierEntity(scene, mesh);
                    FrontierRenderedSegmentCount += segmentCount;
                }

                timer.Stop();
                MeshMilliseconds += timer.ElapsedMilliseconds;
                MaximumBatchMilliseconds = Math.Max(MaximumBatchMilliseconds, timer.ElapsedMilliseconds);
            }

            private void BuildFrontierCell(
                Mesh mesh,
                Vec2 lowerLeft,
                Vec2 lowerRight,
                Vec2 upperRight,
                Vec2 upperLeft,
                FrontierRegion lowerLeftRegion,
                FrontierRegion lowerRightRegion,
                FrontierRegion upperRightRegion,
                FrontierRegion upperLeftRegion,
                int depth,
                UIntPtr lockHandle,
                ref int segmentCount)
            {
                bool hasLand = lowerLeftRegion.Land || lowerRightRegion.Land || upperRightRegion.Land || upperLeftRegion.Land;
                bool hasWater = !lowerLeftRegion.Land || !lowerRightRegion.Land || !upperRightRegion.Land || !upperLeftRegion.Land;
                if (depth < FrontierCoastRefinementDepth && hasLand && hasWater)
                {
                    FrontierCoastRefinedCellCount++;
                    Vec2 lowerMiddle = (lowerLeft + lowerRight) * 0.5f;
                    Vec2 rightMiddle = (lowerRight + upperRight) * 0.5f;
                    Vec2 upperMiddle = (upperRight + upperLeft) * 0.5f;
                    Vec2 leftMiddle = (upperLeft + lowerLeft) * 0.5f;
                    Vec2 center = (lowerLeft + upperRight) * 0.5f;
                    FrontierRegion lowerMiddleRegion = GetFrontierRegion(lowerMiddle);
                    FrontierRegion rightMiddleRegion = GetFrontierRegion(rightMiddle);
                    FrontierRegion upperMiddleRegion = GetFrontierRegion(upperMiddle);
                    FrontierRegion leftMiddleRegion = GetFrontierRegion(leftMiddle);
                    FrontierRegion centerRegion = GetFrontierRegion(center);
                    BuildFrontierCell(mesh, lowerLeft, lowerMiddle, center, leftMiddle, lowerLeftRegion, lowerMiddleRegion, centerRegion, leftMiddleRegion, depth + 1, lockHandle, ref segmentCount);
                    BuildFrontierCell(mesh, lowerMiddle, lowerRight, rightMiddle, center, lowerMiddleRegion, lowerRightRegion, rightMiddleRegion, centerRegion, depth + 1, lockHandle, ref segmentCount);
                    BuildFrontierCell(mesh, center, rightMiddle, upperRight, upperMiddle, centerRegion, rightMiddleRegion, upperRightRegion, upperMiddleRegion, depth + 1, lockHandle, ref segmentCount);
                    BuildFrontierCell(mesh, leftMiddle, center, upperMiddle, upperLeft, leftMiddleRegion, centerRegion, upperMiddleRegion, upperLeftRegion, depth + 1, lockHandle, ref segmentCount);
                    return;
                }

                List<FrontierCrossing> crossings = new List<FrontierCrossing>(4);
                AddFrontierCrossing(crossings, lowerLeft, lowerRight, lowerLeftRegion, lowerRightRegion);
                AddFrontierCrossing(crossings, lowerRight, upperRight, lowerRightRegion, upperRightRegion);
                AddFrontierCrossing(crossings, upperRight, upperLeft, upperRightRegion, upperLeftRegion);
                AddFrontierCrossing(crossings, upperLeft, lowerLeft, upperLeftRegion, lowerLeftRegion);
                if (crossings.Count == 2)
                {
                    if (AddFrontierSegment(mesh, crossings[0].Point, crossings[1].Point, lockHandle)) segmentCount++;
                    return;
                }
                if (crossings.Count == 4
                    && lowerLeftRegion.Equals(upperRightRegion)
                    && lowerRightRegion.Equals(upperLeftRegion))
                {
                    FrontierSaddleCellCount++;
                    RecordFrontierCellDiagnostic("saddle", lowerLeft.x, lowerRight.x, lowerLeft.y, upperLeft.y, crossings.Count);
                    FrontierRegion centerRegion = GetFrontierRegion((lowerLeft + upperRight) * 0.5f);
                    bool centerMatchesLowerLeft = centerRegion.Equals(lowerLeftRegion);
                    int firstA = centerMatchesLowerLeft ? 0 : 3;
                    int firstB = centerMatchesLowerLeft ? 1 : 0;
                    int secondA = centerMatchesLowerLeft ? 2 : 1;
                    int secondB = centerMatchesLowerLeft ? 3 : 2;
                    if (AddFrontierSegment(mesh, crossings[firstA].Point, crossings[firstB].Point, lockHandle)) segmentCount++;
                    if (AddFrontierSegment(mesh, crossings[secondA].Point, crossings[secondB].Point, lockHandle)) segmentCount++;
                    return;
                }
                if (crossings.Count > 2)
                {
                    FrontierAmbiguousCellCount++;
                    RecordFrontierCellDiagnostic("ambiguous", lowerLeft.x, lowerRight.x, lowerLeft.y, upperLeft.y, crossings.Count);
                    Vec2 center = (lowerLeft + upperRight) * 0.5f;
                    foreach (FrontierCrossing crossing in crossings)
                    {
                        if (AddFrontierSegment(mesh, crossing.Point, center, lockHandle)) segmentCount++;
                    }
                }
            }

            private void AddFrontierEntity(Scene scene, Mesh mesh)
            {
                mesh.ComputeNormals();
                mesh.RecomputeBoundingBox();
                GameEntity entity = GameEntity.CreateEmpty(scene, false, true, true);
                if (entity == null) throw new InvalidOperationException("Bannerlord could not allocate a political frontier entity.");
                entity.SetGlobalFrame(MatrixFrame.Identity, true);
                entity.AddMesh(mesh, true);
                entity.SetForceDecalsToRender(false);
                entity.SetVisibilityExcludeParents(false);
                entity.SetReadyToRender(true);
                entity.SetAlpha(0f);
                _frontierEntities.Add(entity);
                FrontierEntityCount++;
            }

            private FrontierRegion GetFrontierRegion(Vec2 point)
            {
                if (!CampaignMapTerrainGridCache.IsFrontierLandExact(point)) return new FrontierRegion(null, false);
                return new FrontierRegion(_siteIndex.FindNearest(point), true);
            }

            private static void AddFrontierCrossing(
                List<FrontierCrossing> crossings,
                Vec2 firstPoint,
                Vec2 secondPoint,
                FrontierRegion first,
                FrontierRegion second)
            {
                if (first.Equals(second)) return;
                crossings.Add(new FrontierCrossing((firstPoint + secondPoint) * 0.5f));
            }

            private static uint GetFrontierSideColor(FrontierRegion side, FrontierRegion opposite)
            {
                PoliticalTerritoryCell owner = side.Land ? side.Owner : opposite.Owner;
                return ScaleOpaqueColor(
                    owner == null ? 0xFF606060u : owner.Color,
                    FrontierBrightnessPercent);
            }

            private bool AddFrontierSegment(
                Mesh mesh,
                Vec2 first,
                Vec2 second,
                UIntPtr lockHandle)
            {
                Vec2 direction = second - first;
                float segmentLength = direction.Normalize();
                if (segmentLength < 0.001f) return false;
                FrontierCandidateSegmentCount++;
                Vec2 midpoint = (first + second) * 0.5f;
                bool exteriorWaterMidpoint = CampaignMapTerrainGridCache.GetNativeWaterTopologyDiagnostic(midpoint) == "exterior";
                if (exteriorWaterMidpoint)
                {
                    FrontierExteriorWaterMidpointCount++;
                    RecordFrontierSegmentDiagnostic("exterior-midpoint", first, second, segmentLength);
                }
                if (!HasFrontierLandSupport(first, second, direction))
                {
                    FrontierUnsupportedSegmentCount++;
                    return false;
                }
                float halfWidth = FrontierWidth * 0.5f;
                Vec2 normalUnit = new Vec2(-direction.y, direction.x);
                Vec2 widthOffset = normalUnit * halfWidth;
                FrontierRegion leftRegion = GetFrontierRegion(midpoint + normalUnit * FrontierSideSampleDistance);
                FrontierRegion rightRegion = GetFrontierRegion(midpoint - normalUnit * FrontierSideSampleDistance);
                if (exteriorWaterMidpoint
                    && leftRegion.Land
                    && rightRegion.Land
                    && leftRegion.Equals(rightRegion))
                {
                    FrontierSameOwnerWaterChordRejectionCount++;
                    RecordFrontierSegmentDiagnostic("same-owner-water-chord", first, second, segmentLength);
                    return false;
                }
                uint leftColor = GetFrontierSideColor(leftRegion, rightRegion);
                uint rightColor = GetFrontierSideColor(rightRegion, leftRegion);
                Vec3 firstCenter;
                Vec3 secondCenter;
                Vec3 firstLeft;
                Vec3 firstRight;
                Vec3 secondLeft;
                Vec3 secondRight;
                if (!TryGetFrontierPoint(first, out firstCenter)
                    || !TryGetFrontierPoint(second, out secondCenter)
                    || !TryGetFrontierPoint(first + widthOffset, out firstLeft)
                    || !TryGetFrontierPoint(first - widthOffset, out firstRight)
                    || !TryGetFrontierPoint(second + widthOffset, out secondLeft)
                    || !TryGetFrontierPoint(second - widthOffset, out secondRight))
                {
                    FrontierProjectionRejectedSegmentCount++;
                    return false;
                }

                AddDoubleSidedQuad(mesh, firstLeft, firstCenter, secondCenter, secondLeft, leftColor, lockHandle);
                AddDoubleSidedQuad(mesh, firstCenter, firstRight, secondRight, secondCenter, rightColor, lockHandle);
                AddSplitRoundCapIfNew(mesh, first, firstCenter, direction, normalUnit, leftColor, rightColor, lockHandle);
                AddSplitRoundCapIfNew(mesh, second, secondCenter, direction, normalUnit, leftColor, rightColor, lockHandle);
                return true;
            }

            private void RecordFrontierCellDiagnostic(
                string kind,
                float leftX,
                float rightX,
                float lowerY,
                float upperY,
                int crossings)
            {
                if (_frontierBridgeDiagnostics.Count >= 24) return;
                _frontierBridgeDiagnostics.Add(kind
                    + "@(" + ((leftX + rightX) * 0.5f).ToString("F1")
                    + "," + ((lowerY + upperY) * 0.5f).ToString("F1")
                    + ") crossings=" + crossings);
            }

            private void RecordFrontierSegmentDiagnostic(
                string kind,
                Vec2 first,
                Vec2 second,
                float length)
            {
                if (_frontierBridgeDiagnostics.Count >= 24) return;
                _frontierBridgeDiagnostics.Add(kind
                    + " first=(" + first.x.ToString("F1") + "," + first.y.ToString("F1") + ")"
                    + " second=(" + second.x.ToString("F1") + "," + second.y.ToString("F1") + ")"
                    + " length=" + length.ToString("F2"));
            }

            private bool HasFrontierLandSupport(Vec2 first, Vec2 second, Vec2 direction)
            {
                Vec2 normal = new Vec2(-direction.y, direction.x);
                int coastSide = 0;
                for (int sample = 0; sample <= 4; sample++)
                {
                    float blend = sample * 0.25f;
                    Vec2 point = first + (second - first) * blend;
                    FrontierRegion left = GetFrontierRegion(point + normal * FrontierLandSupportSampleDistance);
                    FrontierRegion right = GetFrontierRegion(point - normal * FrontierLandSupportSampleDistance);
                    if (!left.Land && !right.Land) return false;
                    int currentCoastSide = left.Land == right.Land ? 0 : (left.Land ? 1 : -1);
                    if (currentCoastSide != 0)
                    {
                        if (coastSide != 0 && coastSide != currentCoastSide) return false;
                        coastSide = currentCoastSide;
                    }
                }
                return true;
            }

            private void AddSplitRoundCapIfNew(
                Mesh mesh,
                Vec2 center2D,
                Vec3 center,
                Vec2 direction,
                Vec2 normal,
                uint leftColor,
                uint rightColor,
                UIntPtr lockHandle)
            {
                if (!_frontierCapPoints.Add(new FrontierPointKey(center2D))) return;
                AddSplitRoundCap(mesh, center2D, center, direction, normal, leftColor, rightColor, lockHandle);
            }

            private static void AddSplitRoundCap(
                Mesh mesh,
                Vec2 center2D,
                Vec3 center,
                Vec2 direction,
                Vec2 normal,
                uint leftColor,
                uint rightColor,
                UIntPtr lockHandle)
            {
                AddHalfRoundCap(mesh, center2D, center, direction, normal, 1f, leftColor, lockHandle);
                AddHalfRoundCap(mesh, center2D, center, direction, normal, -1f, rightColor, lockHandle);
            }

            private static void AddHalfRoundCap(
                Mesh mesh,
                Vec2 center2D,
                Vec3 center,
                Vec2 direction,
                Vec2 normal,
                float normalSign,
                uint color,
                UIntPtr lockHandle)
            {
                float radius = FrontierWidth * 0.5f;
                Vec3 previous;
                if (!TryGetFrontierPoint(center2D + direction * radius, out previous)) return;
                for (int step = 1; step <= FrontierCapStepsPerHalf; step++)
                {
                    float angle = (float)Math.PI * step / FrontierCapStepsPerHalf;
                    Vec2 offset = direction * ((float)Math.Cos(angle) * radius)
                        + normal * ((float)Math.Sin(angle) * radius * normalSign);
                    Vec3 current;
                    if (!TryGetFrontierPoint(center2D + offset, out current)) return;
                    AddDoubleSidedFanTriangle(mesh, center, previous, current, color, lockHandle);
                    previous = current;
                }
            }

            private static void AddDoubleSidedFanTriangle(
                Mesh mesh,
                Vec3 center,
                Vec3 first,
                Vec3 second,
                uint color,
                UIntPtr lockHandle)
            {
                Vec2 uv = Vec2.Zero;
                mesh.AddTriangle(center, first, second, uv, uv, uv, color, lockHandle);
                mesh.AddTriangle(center, second, first, uv, uv, uv, color, lockHandle);
            }

            private static bool TryGetFrontierPoint(Vec2 point, out Vec3 terrainPoint)
            {
                float height;
                if (!CampaignMapTerrainGridCache.TrySampleExactHeight(point, out height))
                {
                    terrainPoint = Vec3.Zero;
                    return false;
                }
                terrainPoint = new Vec3(point.x, point.y, height + FrontierHeight);
                return true;
            }

            private static void AddDoubleSidedQuad(
                Mesh mesh,
                Vec3 firstOuter,
                Vec3 firstInner,
                Vec3 secondInner,
                Vec3 secondOuter,
                uint color,
                UIntPtr lockHandle)
            {
                Vec2 uv0 = new Vec2(0f, 0f);
                Vec2 uv1 = new Vec2(1f, 0f);
                Vec2 uv2 = new Vec2(1f, 1f);
                Vec2 uv3 = new Vec2(0f, 1f);
                mesh.AddTriangle(firstOuter, secondInner, secondOuter, uv0, uv2, uv1, color, lockHandle);
                mesh.AddTriangle(firstOuter, firstInner, secondInner, uv0, uv3, uv2, color, lockHandle);
                mesh.AddTriangle(firstOuter, secondOuter, secondInner, uv0, uv1, uv2, color, lockHandle);
                mesh.AddTriangle(firstOuter, secondInner, firstInner, uv0, uv2, uv3, color, lockHandle);
            }

            internal List<GameEntity> TakeFillEntities()
            {
                if (!IsFillComplete) throw new InvalidOperationException("Political fill entities were requested before fill construction completed.");
                if (_entities == null) throw new InvalidOperationException("Political fill entities were already taken.");
                List<GameEntity> result = _entities;
                _entities = null;
                return result;
            }

            internal List<GameEntity> TakeFrontierEntities()
            {
                if (!IsComplete) throw new InvalidOperationException("Political frontier entities were requested before construction completed.");
                if (_frontierEntities == null) throw new InvalidOperationException("Political frontier entities were already taken.");
                List<GameEntity> result = _frontierEntities;
                _frontierEntities = null;
                return result;
            }

            internal void Cancel()
            {
                if (_entities != null)
                {
                    foreach (GameEntity entity in _entities)
                    {
                        if (entity == null) continue;
                        try { entity.Remove(0); }
                        catch (Exception exception) { Diagnostics.Error("A pending political fill entity could not be removed.", exception); }
                    }
                    _entities.Clear();
                }
                if (_frontierEntities != null)
                {
                    foreach (GameEntity entity in _frontierEntities)
                    {
                        if (entity == null) continue;
                        try { entity.Remove(0); }
                        catch (Exception exception) { Diagnostics.Error("A pending political frontier entity could not be removed.", exception); }
                    }
                    _frontierEntities.Clear();
                }
            }

            private void CountTriangle(
                Mesh landMesh,
                Mesh riverMesh,
                Mesh enclosedWaterMesh,
                Vec3 first,
                Vec3 second,
                Vec3 third,
                UIntPtr landHandle,
                UIntPtr riverHandle,
                UIntPtr enclosedWaterHandle,
                ref int rowLandTriangles,
                ref int rowRiverTriangles,
                ref int rowEnclosedWaterTriangles)
            {
                Vec2 center = new Vec2(
                    (first.x + second.x + third.x) / 3f,
                    (first.y + second.y + third.y) / 3f);
                bool nativeRiver;
                if (!CampaignMapTerrainGridCache.IsPoliticalLandExact(center, out nativeRiver))
                {
                    SeaSampleCount++;
                    return;
                }
                if (TouchesExteriorWater(first, second, third))
                {
                    ExteriorWaterTriangleRejectionCount++;
                    return;
                }

                LandSampleCount++;
                bool enclosedWater = CampaignMapTerrainGridCache.IsInteriorWaterAt(center);
                Mesh targetMesh = nativeRiver ? riverMesh : (enclosedWater ? enclosedWaterMesh : landMesh);
                UIntPtr targetHandle = nativeRiver ? riverHandle : (enclosedWater ? enclosedWaterHandle : landHandle);
                if (enclosedWater)
                {
                    first.z += EnclosedWaterCapLift;
                    second.z += EnclosedWaterCapLift;
                    third.z += EnclosedWaterCapLift;
                }
                if (!AddTriangle(targetMesh, _siteIndex, first, second, third, targetHandle)) return;
                RenderedTriangleCount++;
                if (nativeRiver)
                {
                    RiverRenderedTriangleCount++;
                    rowRiverTriangles++;
                }
                else if (enclosedWater)
                {
                    EnclosedWaterRenderedTriangleCount++;
                    rowEnclosedWaterTriangles++;
                }
                else rowLandTriangles++;
            }

            private static bool TouchesExteriorWater(Vec3 first, Vec3 second, Vec3 third)
            {
                Vec2 first2D = new Vec2(first.x, first.y);
                Vec2 second2D = new Vec2(second.x, second.y);
                Vec2 third2D = new Vec2(third.x, third.y);
                return CampaignMapTerrainGridCache.IsExteriorNativeWaterAt(first2D)
                    || CampaignMapTerrainGridCache.IsExteriorNativeWaterAt(second2D)
                    || CampaignMapTerrainGridCache.IsExteriorNativeWaterAt(third2D)
                    || CampaignMapTerrainGridCache.IsExteriorNativeWaterAt((first2D + second2D) * 0.5f)
                    || CampaignMapTerrainGridCache.IsExteriorNativeWaterAt((second2D + third2D) * 0.5f)
                    || CampaignMapTerrainGridCache.IsExteriorNativeWaterAt((third2D + first2D) * 0.5f);
            }

            private void AddAdaptiveCell(
                Mesh landMesh,
                Mesh riverMesh,
                Mesh enclosedWaterMesh,
                Vec3 lowerLeft,
                Vec3 lowerRight,
                Vec3 upperRight,
                Vec3 upperLeft,
                int depth,
                UIntPtr landHandle,
                UIntPtr riverHandle,
                UIntPtr enclosedWaterHandle,
                ref int rowLandTriangles,
                ref int rowRiverTriangles,
                ref int rowEnclosedWaterTriangles)
            {
                Vec2 center = new Vec2(
                    (lowerLeft.x + upperRight.x) * 0.5f,
                    (lowerLeft.y + upperRight.y) * 0.5f);
                if (depth < MaximumRefinementDepth
                    && CrossesVisualBoundary(lowerLeft, lowerRight, upperRight, upperLeft, center))
                {
                    RefinedCellCount++;
                    Vec3 lowerMiddle = GetTerrainPoint(new Vec2(center.x, lowerLeft.y));
                    Vec3 rightMiddle = GetTerrainPoint(new Vec2(lowerRight.x, center.y));
                    Vec3 upperMiddle = GetTerrainPoint(new Vec2(center.x, upperLeft.y));
                    Vec3 leftMiddle = GetTerrainPoint(new Vec2(lowerLeft.x, center.y));
                    Vec3 middle = GetTerrainPoint(center);
                    AddAdaptiveCell(landMesh, riverMesh, enclosedWaterMesh, lowerLeft, lowerMiddle, middle, leftMiddle, depth + 1, landHandle, riverHandle, enclosedWaterHandle, ref rowLandTriangles, ref rowRiverTriangles, ref rowEnclosedWaterTriangles);
                    AddAdaptiveCell(landMesh, riverMesh, enclosedWaterMesh, lowerMiddle, lowerRight, rightMiddle, middle, depth + 1, landHandle, riverHandle, enclosedWaterHandle, ref rowLandTriangles, ref rowRiverTriangles, ref rowEnclosedWaterTriangles);
                    AddAdaptiveCell(landMesh, riverMesh, enclosedWaterMesh, middle, rightMiddle, upperRight, upperMiddle, depth + 1, landHandle, riverHandle, enclosedWaterHandle, ref rowLandTriangles, ref rowRiverTriangles, ref rowEnclosedWaterTriangles);
                    AddAdaptiveCell(landMesh, riverMesh, enclosedWaterMesh, leftMiddle, middle, upperMiddle, upperLeft, depth + 1, landHandle, riverHandle, enclosedWaterHandle, ref rowLandTriangles, ref rowRiverTriangles, ref rowEnclosedWaterTriangles);
                    return;
                }

                CountTriangle(landMesh, riverMesh, enclosedWaterMesh, lowerLeft, lowerRight, upperRight, landHandle, riverHandle, enclosedWaterHandle, ref rowLandTriangles, ref rowRiverTriangles, ref rowEnclosedWaterTriangles);
                CountTriangle(landMesh, riverMesh, enclosedWaterMesh, lowerLeft, upperRight, upperLeft, landHandle, riverHandle, enclosedWaterHandle, ref rowLandTriangles, ref rowRiverTriangles, ref rowEnclosedWaterTriangles);
            }

            private bool CrossesVisualBoundary(
                Vec3 lowerLeft,
                Vec3 lowerRight,
                Vec3 upperRight,
                Vec3 upperLeft,
                Vec2 center)
            {
                VisualRegion first = GetVisualRegion(new Vec2(lowerLeft.x, lowerLeft.y));
                bool crossesRegion = !first.Equals(GetVisualRegion(new Vec2(lowerRight.x, lowerRight.y)))
                    || !first.Equals(GetVisualRegion(new Vec2(upperRight.x, upperRight.y)))
                    || !first.Equals(GetVisualRegion(new Vec2(upperLeft.x, upperLeft.y)))
                    || !first.Equals(GetVisualRegion(center));
                if (crossesRegion) return true;

                float exactCenterHeight;
                if (!CampaignMapTerrainGridCache.TrySampleExactHeight(center, out exactCenterHeight)) return false;
                float interpolatedCenterHeight = (lowerLeft.z + lowerRight.z + upperRight.z + upperLeft.z) * 0.25f;
                if (Math.Abs(exactCenterHeight + FillHeight - interpolatedCenterHeight)
                    < TerrainReliefRefinementThreshold)
                {
                    return false;
                }

                TerrainReliefRefinedCellCount++;
                return true;
            }

            private VisualRegion GetVisualRegion(Vec2 point)
            {
                bool nativeRiver;
                bool politicalLand = CampaignMapTerrainGridCache.IsPoliticalLandExact(point, out nativeRiver);
                return politicalLand
                    ? new VisualRegion(_siteIndex.FindNearest(point), true, nativeRiver)
                    : new VisualRegion(null, false, false);
            }

            private static Vec3 GetTerrainPoint(Vec2 point)
            {
                float height;
                if (!CampaignMapTerrainGridCache.TrySampleExactHeight(point, out height))
                {
                    throw new InvalidOperationException("Adaptive political vertex lies outside the prepared terrain grid.");
                }
                return new Vec3(point.x, point.y, height + FillHeight);
            }
        }

        private static uint ScaleOpaqueColor(uint color, uint brightnessPercent)
        {
            uint red = ((color >> 16) & 0xFFu) * brightnessPercent / 100u;
            uint green = ((color >> 8) & 0xFFu) * brightnessPercent / 100u;
            uint blue = (color & 0xFFu) * brightnessPercent / 100u;
            return FillAlpha | (red << 16) | (green << 8) | blue;
        }

        private struct FrontierPointKey : IEquatable<FrontierPointKey>
        {
            private readonly int _x;
            private readonly int _y;

            internal FrontierPointKey(Vec2 point)
            {
                _x = (int)Math.Round(point.x * 1000f);
                _y = (int)Math.Round(point.y * 1000f);
            }

            public bool Equals(FrontierPointKey other)
            {
                return _x == other._x && _y == other._y;
            }

            public override bool Equals(object obj)
            {
                return obj is FrontierPointKey && Equals((FrontierPointKey)obj);
            }

            public override int GetHashCode()
            {
                return (_x * 397) ^ _y;
            }
        }

        private struct FrontierCrossing
        {
            internal FrontierCrossing(Vec2 point)
            {
                Point = point;
            }

            internal Vec2 Point { get; private set; }
        }

        private struct FrontierRegion : IEquatable<FrontierRegion>
        {
            internal FrontierRegion(PoliticalTerritoryCell owner, bool land)
            {
                Owner = owner;
                Land = land;
            }

            internal PoliticalTerritoryCell Owner { get; private set; }
            internal bool Land { get; private set; }

            public bool Equals(FrontierRegion other)
            {
                if (Land != other.Land) return false;
                if (!Land) return true;
                string ownerKey = Owner == null ? null : Owner.OwnerKey;
                string otherOwnerKey = other.Owner == null ? null : other.Owner.OwnerKey;
                return string.Equals(ownerKey, otherOwnerKey, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is FrontierRegion && Equals((FrontierRegion)obj);
            }

            public override int GetHashCode()
            {
                return (Land ? 397 : 0) ^ (Owner == null || Owner.OwnerKey == null ? 0 : Owner.OwnerKey.GetHashCode());
            }
        }

        private struct VisualRegion : IEquatable<VisualRegion>
        {
            private readonly PoliticalTerritoryCell _owner;
            private readonly bool _land;
            private readonly bool _river;

            internal VisualRegion(PoliticalTerritoryCell owner, bool land, bool river)
            {
                _owner = owner;
                _land = land;
                _river = river;
            }

            public bool Equals(VisualRegion other)
            {
                return _land == other._land
                    && _river == other._river
                    && ReferenceEquals(_owner, other._owner);
            }

            public override bool Equals(object obj)
            {
                return obj is VisualRegion && Equals((VisualRegion)obj);
            }

            public override int GetHashCode()
            {
                int hash = ((_owner == null ? 0 : _owner.GetHashCode()) * 397) ^ _land.GetHashCode();
                return hash * 397 ^ _river.GetHashCode();
            }
        }

        internal sealed class NearestSiteIndex
        {
            private readonly Node _root;

            internal NearestSiteIndex(List<PoliticalTerritoryCell> cells)
            {
                _root = Build(new List<PoliticalTerritoryCell>(cells), 0);
            }

            internal PoliticalTerritoryCell FindNearest(Vec2 point)
            {
                PoliticalTerritoryCell nearest = null;
                float nearestDistance = float.MaxValue;
                Search(_root, point, ref nearest, ref nearestDistance);
                return nearest;
            }

            private static Node Build(List<PoliticalTerritoryCell> cells, int depth)
            {
                if (cells.Count == 0) return null;
                bool splitX = (depth & 1) == 0;
                cells.Sort((first, second) => splitX
                    ? first.Site.x.CompareTo(second.Site.x)
                    : first.Site.y.CompareTo(second.Site.y));
                int middle = cells.Count / 2;
                return new Node(
                    cells[middle],
                    splitX,
                    Build(cells.GetRange(0, middle), depth + 1),
                    Build(cells.GetRange(middle + 1, cells.Count - middle - 1), depth + 1));
            }

            private static void Search(
                Node node,
                Vec2 point,
                ref PoliticalTerritoryCell nearest,
                ref float nearestDistance)
            {
                if (node == null) return;
                float x = point.x - node.Cell.Site.x;
                float y = point.y - node.Cell.Site.y;
                float distance = x * x + y * y;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = node.Cell;
                }

                float axisDistance = node.SplitX ? x : y;
                Node near = axisDistance < 0f ? node.Lower : node.Upper;
                Node far = axisDistance < 0f ? node.Upper : node.Lower;
                Search(near, point, ref nearest, ref nearestDistance);
                if (axisDistance * axisDistance < nearestDistance)
                {
                    Search(far, point, ref nearest, ref nearestDistance);
                }
            }

            private sealed class Node
            {
                internal Node(PoliticalTerritoryCell cell, bool splitX, Node lower, Node upper)
                {
                    Cell = cell;
                    SplitX = splitX;
                    Lower = lower;
                    Upper = upper;
                }

                internal PoliticalTerritoryCell Cell { get; private set; }
                internal bool SplitX { get; private set; }
                internal Node Lower { get; private set; }
                internal Node Upper { get; private set; }
            }
        }
    }
}
