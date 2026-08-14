using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Projects the authored Strategic Map province index onto the campaign
    /// terrain as a continuous black contour layer. This is intentionally separate
    /// from CampaignKingdomBorderBehavior: authored provinces stay fixed,
    /// while the solid Voronoi layer follows live settlement ownership.
    /// </summary>
    internal sealed class CampaignStrategicProvinceBorderBehavior : CampaignBehaviorBase
    {
        private const int ProvinceSampleGridPixels = 6;
        private const int ProvinceLabelRecoveryPixels = 18;
        private const int ProvinceSegmentsPerMesh = 48;
        private const double ProvinceBuildBudgetMilliseconds = 4d;
        private const float ProvinceBorderHeight = 5.5f;
        private const float ProvinceBorderWidth = 1.4f;
        private const float CloseZoomProvinceBorderDrop = 4.7f;
        private const float ProvinceCoastSampleSpacing = 2f;
        private const int CoastIntersectionIterations = 10;
        private const string ProvinceBorderMaterial = "vertex_color_mat";
        private const int ProvinceMeshRenderOrder = 110;
        private const uint ProvinceBorderColor = 0xFF000000u;

        private readonly List<GameEntity> _provinceEntities = new List<GameEntity>();
        private readonly List<GameEntity> _pendingProvinceEntities = new List<GameEntity>();
        private readonly CampaignKingdomBorderBehavior _politicalBorders;
        private List<ProvinceSegment> _cachedSegments;
        private double[] _pendingCampaignX;
        private double[] _pendingCampaignY;
        private int _nextPendingSegment;
        private Scene _mapScene;
        private bool _dirty = true;
        private bool _loggedFirstBuild;
        private bool _loggedSceneLookupFailure;
        private int _lastTriangleCount;
        private float _overlayAlpha;
        private float _lastAppliedOverlayAlpha = -1f;
        private int _lastPoliticalBoundaryVersion = -1;

        internal CampaignStrategicProvinceBorderBehavior(CampaignKingdomBorderBehavior politicalBorders)
        {
            _politicalBorders = politicalBorders;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Static visual geometry is rebuilt from the strategic-map asset.
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _dirty = true;
        }

        private void OnGameLoadFinished()
        {
            _dirty = true;
        }

        private void OnTick(float dt)
        {
            AdvanceBuild();
        }

        internal void OnMapFrame(float overlayAlpha)
        {
            _overlayAlpha = Math.Max(0f, Math.Min(1f, overlayAlpha));
            ApplyVisibility(false);
            AdvanceBuild();
        }

        private void AdvanceBuild()
        {
            if (_politicalBorders != null)
            {
                int politicalBoundaryVersion = _politicalBorders.PoliticalBoundaryVersion;
                if (politicalBoundaryVersion != _lastPoliticalBoundaryVersion)
                {
                    ClearProvinceEntities();
                    ClearPendingProvinceEntities();
                    ResetPendingBuildState();
                    _dirty = true;
                    _lastPoliticalBoundaryVersion = politicalBoundaryVersion;
                }
                if (!_politicalBorders.HasCurrentPoliticalTopology) return;
            }
            if (!_dirty || Campaign.Current == null || !CampaignMapTerrainGridCache.IsReady) return;

            Scene currentScene = TryGetCampaignMapScene();
            if (currentScene == null) return;
            if (!ReferenceEquals(currentScene, _mapScene))
            {
                ClearProvinceEntities();
                ClearPendingProvinceEntities();
                ResetPendingBuildState();
                _mapScene = currentScene;
            }

            try
            {
                if (_pendingCampaignX == null) BeginProvinceBorderBuild();
                if (_cachedSegments == null || _cachedSegments.Count == 0)
                {
                    _dirty = false;
                    return;
                }

                Stopwatch budget = Stopwatch.StartNew();
                do
                {
                    BuildNextProvinceMesh();
                }
                while (_nextPendingSegment < _cachedSegments.Count
                    && budget.Elapsed.TotalMilliseconds < ProvinceBuildBudgetMilliseconds);
                if (_nextPendingSegment < _cachedSegments.Count) return;

                ClearProvinceEntities();
                _provinceEntities.AddRange(_pendingProvinceEntities);
                _pendingProvinceEntities.Clear();
                ResetPendingBuildState();
                _dirty = false;
                ApplyVisibility(true);
                if (!_loggedFirstBuild)
                {
                    _loggedFirstBuild = true;
                    Diagnostics.Info("Campaign strategic province contours rendered: segments="
                        + (_cachedSegments == null ? 0 : _cachedSegments.Count)
                        + "; triangles=" + _lastTriangleCount
                        + "; entities=" + _provinceEntities.Count
                        + "; material=" + ProvinceBorderMaterial + ".");
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Campaign strategic province contours could not be rendered safely.", exception);
                ClearPendingProvinceEntities();
                ResetPendingBuildState();
                _dirty = true;
            }
        }

        private void BeginProvinceBorderBuild()
        {
            if (_cachedSegments == null) _cachedSegments = LoadStrategicProvinceSegments();
            if (_cachedSegments.Count == 0 || _mapScene == null) return;

            if (!TryFitStrategicToCampaignProjection(out _pendingCampaignX, out _pendingCampaignY))
            {
                _pendingCampaignX = new[] { 1d / 2.23d, 0d, 60d / 2.23d };
                _pendingCampaignY = new[] { 0d, -1d / 2.34d, 1895d / 2.34d };
                Diagnostics.Info("Campaign strategic province contours are using the conservative inverse-map projection.");
            }

            _nextPendingSegment = 0;
            _lastTriangleCount = 0;
        }

        private void BuildNextProvinceMesh()
        {
            if (_cachedSegments == null || _nextPendingSegment >= _cachedSegments.Count) return;
            int segmentCount = Math.Min(ProvinceSegmentsPerMesh, _cachedSegments.Count - _nextPendingSegment);
            int triangleCount;
            Mesh mesh = CreateProvinceBorderMesh(
                _cachedSegments,
                _nextPendingSegment,
                segmentCount,
                _pendingCampaignX,
                _pendingCampaignY,
                out triangleCount);
            _nextPendingSegment += segmentCount;
            if (mesh == null) return;

            GameEntity entity = GameEntity.CreateEmpty(_mapScene, false, true, true);
            if (entity == null) return;
            entity.SetGlobalFrame(MatrixFrame.Identity, true);
            entity.AddMesh(mesh, true);
            entity.SetVisibilityExcludeParents(false);
            entity.SetReadyToRender(true);
            entity.SetAlpha(0f);
            _pendingProvinceEntities.Add(entity);
            _lastTriangleCount += triangleCount;
        }

        private static List<ProvinceSegment> LoadStrategicProvinceSegments()
        {
            string assetPath = System.IO.Path.Combine(
                GetModuleRoot(),
                "GUI",
                "SpriteParts",
                "ui_world_calendar",
                "strategic_province_index.png");
            if (!File.Exists(assetPath))
            {
                throw new FileNotFoundException("The Strategic Map province index is missing.", assetPath);
            }

            using (Bitmap index = new Bitmap(assetPath))
            {
                if (index.Width != (int)CalendarStrategicMapLayout.SourceWidth
                    || index.Height != (int)CalendarStrategicMapLayout.SourceHeight)
                {
                    throw new InvalidDataException("The Strategic Map province index dimensions are invalid.");
                }

                byte[] nearestProvinceIds = BuildNearestProvinceIds(index);
                return BuildConnectedContourSegments(nearestProvinceIds, index.Width, index.Height);
            }
        }

        private static List<ProvinceSegment> BuildConnectedContourSegments(
            byte[] provinceIds,
            int width,
            int height)
        {
            List<ProvinceSegment> result = new List<ProvinceSegment>();
            int step = ProvinceSampleGridPixels;
            for (int y = 0; y + step < height; y += step)
            {
                for (int x = 0; x + step < width; x += step)
                {
                    int topLeft = GetProvinceId(provinceIds, width, x, y);
                    int topRight = GetProvinceId(provinceIds, width, x + step, y);
                    int bottomRight = GetProvinceId(provinceIds, width, x + step, y + step);
                    int bottomLeft = GetProvinceId(provinceIds, width, x, y + step);
                    Vec2[] crossings = new Vec2[4];
                    int crossingCount = 0;
                    if (IsInternalProvinceChange(topLeft, topRight))
                        crossings[crossingCount++] = new Vec2(x + step * 0.5f, y);
                    if (IsInternalProvinceChange(topRight, bottomRight))
                        crossings[crossingCount++] = new Vec2(x + step, y + step * 0.5f);
                    if (IsInternalProvinceChange(bottomLeft, bottomRight))
                        crossings[crossingCount++] = new Vec2(x + step * 0.5f, y + step);
                    if (IsInternalProvinceChange(topLeft, bottomLeft))
                        crossings[crossingCount++] = new Vec2(x, y + step * 0.5f);

                    if (crossingCount == 2)
                    {
                        result.Add(new ProvinceSegment(crossings[0], crossings[1]));
                    }
                    else if (crossingCount > 2)
                    {
                        Vec2 junction = new Vec2(x + step * 0.5f, y + step * 0.5f);
                        for (int crossing = 0; crossing < crossingCount; crossing++)
                        {
                            result.Add(new ProvinceSegment(crossings[crossing], junction));
                        }
                    }
                }
            }
            return result;
        }

        private static byte[] BuildNearestProvinceIds(Bitmap source)
        {
            int width = source.Width;
            int height = source.Height;
            byte[] nearest = new byte[width * height];
            int[] distance = new int[nearest.Length];
            Rectangle rectangle = new Rectangle(0, 0, width, height);
            using (Bitmap argb = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(argb)) graphics.DrawImageUnscaled(source, 0, 0);
                BitmapData data = argb.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] pixels = new byte[Math.Abs(data.Stride) * height];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                    for (int y = 0; y < height; y++)
                    {
                        int sourceRow = data.Stride >= 0 ? y * data.Stride : (height - 1 - y) * -data.Stride;
                        for (int x = 0; x < width; x++)
                        {
                            int pixel = sourceRow + x * 4;
                            byte red = pixels[pixel + 2];
                            int target = y * width + x;
                            if (pixels[pixel + 3] > 0 && red >= 1 && red <= 133)
                            {
                                nearest[target] = red;
                                distance[target] = 0;
                            }
                            else
                            {
                                distance[target] = int.MaxValue / 4;
                            }
                        }
                    }
                }
                finally
                {
                    argb.UnlockBits(data);
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int target = y * width + x;
                    if (x > 0) RecoverNearest(nearest, distance, target, target - 1, 3);
                    if (y > 0)
                    {
                        RecoverNearest(nearest, distance, target, target - width, 3);
                        if (x > 0) RecoverNearest(nearest, distance, target, target - width - 1, 4);
                        if (x + 1 < width) RecoverNearest(nearest, distance, target, target - width + 1, 4);
                    }
                }
            }
            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    int target = y * width + x;
                    if (x + 1 < width) RecoverNearest(nearest, distance, target, target + 1, 3);
                    if (y + 1 < height)
                    {
                        RecoverNearest(nearest, distance, target, target + width, 3);
                        if (x > 0) RecoverNearest(nearest, distance, target, target + width - 1, 4);
                        if (x + 1 < width) RecoverNearest(nearest, distance, target, target + width + 1, 4);
                    }
                }
            }

            int maximumDistance = ProvinceLabelRecoveryPixels * 3;
            for (int index = 0; index < nearest.Length; index++)
            {
                if (distance[index] > maximumDistance) nearest[index] = 0;
            }
            return nearest;
        }

        private static void RecoverNearest(
            byte[] nearest,
            int[] distance,
            int target,
            int source,
            int cost)
        {
            if (nearest[source] == 0) return;
            int candidate = distance[source] + cost;
            if (candidate >= distance[target]) return;
            distance[target] = candidate;
            nearest[target] = nearest[source];
        }

        private static int GetProvinceId(byte[] nearestProvinceIds, int width, int x, int y)
        {
            return nearestProvinceIds[y * width + x];
        }

        private static bool IsInternalProvinceChange(int first, int second)
        {
            return first != 0 && second != 0 && first != second;
        }

        private Mesh CreateProvinceBorderMesh(
            List<ProvinceSegment> segments,
            int firstSegment,
            int segmentCount,
            double[] campaignX,
            double[] campaignY,
            out int triangleCount)
        {
            triangleCount = 0;
            Mesh mesh = Mesh.CreateMesh(true);
            if (mesh == null) return null;
            mesh.SetMaterial(ProvinceBorderMaterial);
            mesh.SetMeshRenderOrder(ProvinceMeshRenderOrder);
            UIntPtr lockHandle = mesh.LockEditDataWrite();
            try
            {
                int endSegment = Math.Min(segments.Count, firstSegment + segmentCount);
                for (int segmentIndex = firstSegment; segmentIndex < endSegment; segmentIndex++)
                {
                    ProvinceSegment segment = segments[segmentIndex];
                    Vec2 first2D = ProjectToCampaign(segment.First, campaignX, campaignY);
                    Vec2 second2D = ProjectToCampaign(segment.Second, campaignX, campaignY);
                    triangleCount += AddCoastClippedProvinceSegment(
                        mesh, first2D, second2D, lockHandle);
                }
            }
            finally
            {
                mesh.UnlockEditDataWrite(lockHandle);
            }

            if (triangleCount == 0) return null;
            mesh.ComputeNormals();
            mesh.RecomputeBoundingBox();
            return mesh;
        }

        private int AddCoastClippedProvinceSegment(
            Mesh mesh,
            Vec2 first,
            Vec2 second,
            UIntPtr lockHandle)
        {
            float dx = second.x - first.x;
            float dy = second.y - first.y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            int divisions = Math.Max(1, (int)Math.Ceiling(length / ProvinceCoastSampleSpacing));
            float clearance = ProvinceBorderWidth * 0.65f;
            int triangles = 0;
            Vec2 previous = first;
            bool previousLand = CampaignMapTerrainGridCache.HasLandClearance(previous, clearance);
            for (int index = 1; index <= divisions; index++)
            {
                float t = (float)index / divisions;
                Vec2 current = new Vec2(first.x + dx * t, first.y + dy * t);
                bool currentLand = CampaignMapTerrainGridCache.HasLandClearance(current, clearance);
                if (previousLand || currentLand)
                {
                    Vec2 landFirst = previous;
                    Vec2 landSecond = current;
                    if (previousLand != currentLand)
                    {
                        Vec2 coast = FindCoastIntersection(
                            previous,
                            current,
                            previousLand,
                            clearance);
                        if (previousLand) landSecond = coast;
                        else landFirst = coast;
                    }

                    if ((previousLand && currentLand || DistanceSquared(landFirst, landSecond) > 0.0001f)
                        && (_politicalBorders == null
                            || !_politicalBorders.IsAlignedWithPoliticalFrontier(landFirst, landSecond)))
                    {
                        Vec3 firstBorder = ToProvinceBorderPoint(landFirst);
                        Vec3 secondBorder = ToProvinceBorderPoint(landSecond);
                        triangles += AddDoubleSidedRibbon(
                            mesh,
                            firstBorder,
                            secondBorder,
                            ProvinceBorderWidth,
                            ProvinceBorderColor,
                            lockHandle);
                    }
                }
                previous = current;
                previousLand = currentLand;
            }
            return triangles;
        }

        private static Vec2 FindCoastIntersection(
            Vec2 first,
            Vec2 second,
            bool firstLand,
            float clearance)
        {
            Vec2 land = firstLand ? first : second;
            Vec2 sea = firstLand ? second : first;
            for (int iteration = 0; iteration < CoastIntersectionIterations; iteration++)
            {
                Vec2 midpoint = (land + sea) * 0.5f;
                if (CampaignMapTerrainGridCache.HasLandClearance(midpoint, clearance)) land = midpoint;
                else sea = midpoint;
            }
            return land;
        }

        private static Vec3 ToProvinceBorderPoint(Vec2 point)
        {
            float height;
            if (!CampaignMapTerrainGridCache.TrySampleExactHeight(point, out height))
            {
                throw new InvalidOperationException("Province contour point lies outside the prepared terrain grid.");
            }
            return new Vec3(point.x, point.y, height + ProvinceBorderHeight);
        }

        private static float DistanceSquared(Vec2 first, Vec2 second)
        {
            float x = first.x - second.x;
            float y = first.y - second.y;
            return x * x + y * y;
        }

        private static int AddDoubleSidedRibbon(
            Mesh mesh,
            Vec3 first,
            Vec3 second,
            float width,
            uint color,
            UIntPtr lockHandle)
        {
            Vec2 direction = new Vec2(second.x - first.x, second.y - first.y);
            if (direction.Normalize() < 0.001f) return 0;
            float halfWidth = width * 0.5f;
            Vec2 normal = new Vec2(-direction.y * halfWidth, direction.x * halfWidth);
            Vec3 firstLeft = new Vec3(first.x + normal.x, first.y + normal.y, first.z);
            Vec3 firstRight = new Vec3(first.x - normal.x, first.y - normal.y, first.z);
            Vec3 secondLeft = new Vec3(second.x + normal.x, second.y + normal.y, second.z);
            Vec3 secondRight = new Vec3(second.x - normal.x, second.y - normal.y, second.z);
            Vec2 uv0 = new Vec2(0f, 0f);
            Vec2 uv1 = new Vec2(1f, 0f);
            Vec2 uv2 = new Vec2(1f, 1f);
            Vec2 uv3 = new Vec2(0f, 1f);
            mesh.AddTriangle(firstLeft, secondRight, secondLeft, uv0, uv2, uv1, color, lockHandle);
            mesh.AddTriangle(firstLeft, firstRight, secondRight, uv0, uv3, uv2, color, lockHandle);
            mesh.AddTriangle(firstLeft, secondLeft, secondRight, uv0, uv1, uv2, color, lockHandle);
            mesh.AddTriangle(firstLeft, secondRight, firstRight, uv0, uv2, uv3, color, lockHandle);
            return 4;
        }

        private static Vec2 ProjectToCampaign(Vec2 source, double[] campaignX, double[] campaignY)
        {
            return new Vec2(
                (float)((source.x * campaignX[0]) + (source.y * campaignX[1]) + campaignX[2]),
                (float)((source.x * campaignY[0]) + (source.y * campaignY[1]) + campaignY[2]));
        }

        private static bool TryFitStrategicToCampaignProjection(out double[] campaignX, out double[] campaignY)
        {
            campaignX = null;
            campaignY = null;
            double[,] normal = new double[3, 3];
            double[] targetX = new double[3];
            double[] targetY = new double[3];
            int samples = 0;
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement == null || settlement.Village != null) continue;
                Vec2 source = CalendarWorldLedgerVM.ProjectSettlementToReferenceMap(settlement);
                Vec2 campaign = settlement.GetPosition2D;
                double[] basis = { source.x, source.y, 1d };
                for (int row = 0; row < 3; row++)
                {
                    for (int column = 0; column < 3; column++)
                    {
                        normal[row, column] += basis[row] * basis[column];
                    }
                    targetX[row] += basis[row] * campaign.x;
                    targetY[row] += basis[row] * campaign.y;
                }
                samples++;
            }

            if (samples < 3 || !TrySolveThreeByThree(normal, targetX, out campaignX)) return false;
            return TrySolveThreeByThree(normal, targetY, out campaignY);
        }

        private static bool TrySolveThreeByThree(double[,] matrix, double[] values, out double[] result)
        {
            result = null;
            double[,] augmented = new double[3, 4];
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++) augmented[row, column] = matrix[row, column];
                augmented[row, 3] = values[row];
            }

            for (int pivot = 0; pivot < 3; pivot++)
            {
                int bestRow = pivot;
                for (int row = pivot + 1; row < 3; row++)
                {
                    if (Math.Abs(augmented[row, pivot]) > Math.Abs(augmented[bestRow, pivot])) bestRow = row;
                }
                if (Math.Abs(augmented[bestRow, pivot]) < 0.000001d) return false;
                if (bestRow != pivot)
                {
                    for (int column = pivot; column < 4; column++)
                    {
                        double swap = augmented[pivot, column];
                        augmented[pivot, column] = augmented[bestRow, column];
                        augmented[bestRow, column] = swap;
                    }
                }

                double divisor = augmented[pivot, pivot];
                for (int column = pivot; column < 4; column++) augmented[pivot, column] /= divisor;
                for (int row = 0; row < 3; row++)
                {
                    if (row == pivot) continue;
                    double factor = augmented[row, pivot];
                    for (int column = pivot; column < 4; column++) augmented[row, column] -= factor * augmented[pivot, column];
                }
            }

            result = new[] { augmented[0, 3], augmented[1, 3], augmented[2, 3] };
            return true;
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
                    Diagnostics.Error("Campaign map scene lookup failed for strategic province contours.", exception);
                }
                return null;
            }
        }

        private void ClearProvinceEntities()
        {
            foreach (GameEntity entity in _provinceEntities)
            {
                if (entity == null) continue;
                try
                {
                    entity.Remove(0);
                }
                catch (Exception exception)
                {
                    Diagnostics.Error("A campaign strategic province entity could not be removed.", exception);
                }
            }
            _provinceEntities.Clear();
            _lastAppliedOverlayAlpha = -1f;
        }

        private void ClearPendingProvinceEntities()
        {
            foreach (GameEntity entity in _pendingProvinceEntities)
            {
                if (entity == null) continue;
                try { entity.Remove(0); }
                catch (Exception exception)
                {
                    Diagnostics.Error("A pending province contour entity could not be removed.", exception);
                }
            }
            _pendingProvinceEntities.Clear();
        }

        private void ResetPendingBuildState()
        {
            _pendingCampaignX = null;
            _pendingCampaignY = null;
            _nextPendingSegment = 0;
        }

        private void ApplyVisibility(bool force)
        {
            if (!force && Math.Abs(_overlayAlpha - _lastAppliedOverlayAlpha) <= 0.002f) return;
            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin.z = -CloseZoomProvinceBorderDrop * (1f - _overlayAlpha);
            foreach (GameEntity entity in _provinceEntities)
            {
                if (entity == null) continue;
                entity.SetGlobalFrame(frame, true);
                entity.SetAlpha(1f);
                entity.SetVisibilityExcludeParents(true);
            }
            _lastAppliedOverlayAlpha = _overlayAlpha;
        }

        private static string GetModuleRoot()
        {
            string assemblyLocation = typeof(MySubModule).Assembly.Location;
            DirectoryInfo directory = string.IsNullOrEmpty(assemblyLocation)
                ? null
                : new FileInfo(assemblyLocation).Directory;
            for (int depth = 0; directory != null && depth < 5; depth++, directory = directory.Parent)
            {
                if (File.Exists(System.IO.Path.Combine(directory.FullName, "SubModule.xml"))) return directory.FullName;
            }
            throw new DirectoryNotFoundException("The Ages of Calradia module root could not be resolved.");
        }

        private sealed class ProvinceSegment
        {
            internal ProvinceSegment(Vec2 first, Vec2 second)
            {
                First = first;
                Second = second;
            }

            internal Vec2 First { get; private set; }
            internal Vec2 Second { get; private set; }
        }

    }
}
