using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Draws campaign-map kingdom borders using the same useful technique as
    /// Artem's Better UI Visuals: a settlement Voronoi diagram projected onto
    /// the live terrain and rendered as runtime vertex-coloured meshes.
    ///
    /// The entities are scene visuals only. Nothing is written to campaign
    /// saves, and the whole layer can be rebuilt after a map-scene reload.
    /// </summary>
    internal sealed class CampaignKingdomBorderBehavior : CampaignBehaviorBase
    {
        private const float MapPadding = 110f;
        private const float BorderHeight = 1.5f;
        private const float BorderWidth = 2.25f;
        private const float BorderOffset = 1.35f;
        private const float MinimumEdgeLength = 2f;
        private const string BorderMaterial = "vertex_color_mat";

        private readonly List<GameEntity> _borderEntities = new List<GameEntity>();
        private Scene _mapScene;
        private bool _dirty = true;
        private bool _loggedFirstBuild;
        private bool _loggedSceneLookupFailure;

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
            _dirty = true;
        }

        private void OnGameLoadFinished()
        {
            _dirty = true;
        }

        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero claimant,
            Hero oldOwner,
            Hero newOwner,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            _dirty = true;
        }

        private void OnDailyTick()
        {
            // This also catches ownership transfers raised by another mod or
            // by a game path that does not fire the public owner-change event.
            _dirty = true;
        }

        private void OnTick(float dt)
        {
            if (!_dirty || Campaign.Current == null) return;

            Scene currentScene = TryGetCampaignMapScene();
            if (currentScene == null) return;

            if (!ReferenceEquals(currentScene, _mapScene))
            {
                ClearBorderEntities();
                _mapScene = currentScene;
                _dirty = true;
            }

            try
            {
                RebuildBorders();
                _dirty = false;
                if (!_loggedFirstBuild)
                {
                    _loggedFirstBuild = true;
                    Diagnostics.Info("Campaign kingdom borders rendered from live settlement Voronoi cells.");
                }
            }
            catch (Exception exception)
            {
                // A changed engine material or map scene must not stop the
                // campaign. Retry on a later frame after the scene settles.
                Diagnostics.Error("Campaign kingdom borders could not be rendered safely.", exception);
                ClearBorderEntities();
                _dirty = true;
            }
        }

        private void RebuildBorders()
        {
            ClearBorderEntities();

            List<BorderSite> sites = BuildSites();
            if (sites.Count < 2) return;

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

            Dictionary<string, BorderEdge> uniqueEdges = new Dictionary<string, BorderEdge>(StringComparer.Ordinal);
            for (int siteIndex = 0; siteIndex < sites.Count; siteIndex++)
            {
                List<Vec2> cell = ClipCell(sites, siteIndex, minX, minY, maxX, maxY);
                if (cell.Count < 2) continue;

                for (int pointIndex = 0; pointIndex < cell.Count; pointIndex++)
                {
                    Vec2 first = cell[pointIndex];
                    Vec2 second = cell[(pointIndex + 1) % cell.Count];
                    if (DistanceSquared(first, second) < MinimumEdgeLength * MinimumEdgeLength) continue;

                    int neighborIndex = FindEdgeNeighbor(sites, siteIndex, first, second);
                    if (neighborIndex < 0 || neighborIndex == siteIndex) continue;
                    BorderSite firstSite = sites[siteIndex];
                    BorderSite secondSite = sites[neighborIndex];
                    if (string.Equals(firstSite.FactionKey, secondSite.FactionKey, StringComparison.Ordinal)) continue;

                    string edgeKey = MakeEdgeKey(firstSite.Settlement.StringId, secondSite.Settlement.StringId, first, second);
                    if (!uniqueEdges.ContainsKey(edgeKey))
                    {
                        uniqueEdges.Add(edgeKey, new BorderEdge(first, second, firstSite, secondSite));
                    }
                }
            }

            foreach (BorderEdge edge in uniqueEdges.Values)
            {
                RenderEdge(edge);
            }
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
                string ownerKey = clan.Kingdom != null
                    ? "kingdom:" + clan.Kingdom.StringId
                    : "clan:" + clan.StringId;
                uint color = clan.Kingdom != null
                    ? clan.Kingdom.PrimaryBannerColor
                    : clan.Color;
                CampaignVec2 position = settlement.Position;
                result.Add(new BorderSite(
                    settlement,
                    new Vec2(position.X, position.Y),
                    ownerKey,
                    color));
            }
            return result;
        }

        private static List<Vec2> ClipCell(
            List<BorderSite> sites,
            int siteIndex,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            BorderSite source = sites[siteIndex];
            List<Vec2> polygon = new List<Vec2>
            {
                new Vec2(minX, minY),
                new Vec2(maxX, minY),
                new Vec2(maxX, maxY),
                new Vec2(minX, maxY)
            };

            for (int otherIndex = 0; otherIndex < sites.Count && polygon.Count > 0; otherIndex++)
            {
                if (otherIndex == siteIndex) continue;

                BorderSite other = sites[otherIndex];
                float a = 2f * (other.Position.x - source.Position.x);
                float b = 2f * (other.Position.y - source.Position.y);
                float c = (other.Position.x * other.Position.x + other.Position.y * other.Position.y)
                    - (source.Position.x * source.Position.x + source.Position.y * source.Position.y);
                polygon = ClipPolygon(polygon, a, b, c);
            }

            return polygon;
        }

        private static List<Vec2> ClipPolygon(List<Vec2> polygon, float a, float b, float c)
        {
            const float epsilon = 0.001f;
            List<Vec2> clipped = new List<Vec2>();
            for (int index = 0; index < polygon.Count; index++)
            {
                Vec2 current = polygon[index];
                Vec2 next = polygon[(index + 1) % polygon.Count];
                float currentValue = a * current.x + b * current.y - c;
                float nextValue = a * next.x + b * next.y - c;
                bool currentInside = currentValue <= epsilon;
                bool nextInside = nextValue <= epsilon;

                if (currentInside) clipped.Add(current);
                if (currentInside != nextInside)
                {
                    float denominator = currentValue - nextValue;
                    if (Math.Abs(denominator) > 0.000001f)
                    {
                        float t = currentValue / denominator;
                        clipped.Add(new Vec2(
                            current.x + (next.x - current.x) * t,
                            current.y + (next.y - current.y) * t));
                    }
                }
            }
            return clipped;
        }

        private static int FindEdgeNeighbor(List<BorderSite> sites, int sourceIndex, Vec2 first, Vec2 second)
        {
            Vec2 midpoint = new Vec2((first.x + second.x) * 0.5f, (first.y + second.y) * 0.5f);
            float sourceDistance = DistanceSquared(midpoint, sites[sourceIndex].Position);
            int neighborIndex = -1;
            float bestDifference = float.MaxValue;
            for (int index = 0; index < sites.Count; index++)
            {
                if (index == sourceIndex) continue;
                float difference = Math.Abs(DistanceSquared(midpoint, sites[index].Position) - sourceDistance);
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    neighborIndex = index;
                }
            }

            // Edges on the padded map rectangle have no settlement on their
            // other side. A true Voronoi edge is nearly equidistant.
            return bestDifference < 0.5f ? neighborIndex : -1;
        }

        private void RenderEdge(BorderEdge edge)
        {
            int subdivisions = Math.Max(2, (int)Math.Ceiling((float)Math.Sqrt(DistanceSquared(edge.First, edge.Second)) / 20f));
            List<Vec2> centerLine = GenerateLinePoints(edge.First, edge.Second, subdivisions);
            List<Vec2> firstSide = OffsetPolyline(centerLine, BorderOffset);
            List<Vec2> secondSide = OffsetPolyline(centerLine, -BorderOffset);
            AddLineEntity(SampleTerrainHeights(centerLine, BorderHeight), 5.5f, 0xB0000000u);
            AddLineEntity(SampleTerrainHeights(firstSide, BorderHeight), BorderWidth, edge.FirstSite.Color);
            AddLineEntity(SampleTerrainHeights(secondSide, BorderHeight), BorderWidth, edge.SecondSite.Color);
        }

        private void AddLineEntity(List<Vec3> points, float width, uint color)
        {
            Mesh mesh = CreateLineMesh(points, width, color);
            if (mesh == null || _mapScene == null) return;

            GameEntity entity = GameEntity.CreateEmpty(_mapScene, false, true, true);
            if (entity == null) return;

            MatrixFrame frame = MatrixFrame.Identity;
            entity.SetGlobalFrame(frame, true);
            entity.AddMesh(mesh, true);
            entity.SetVisibilityExcludeParents(true);
            entity.SetReadyToRender(true);
            _borderEntities.Add(entity);
        }

        private static Mesh CreateLineMesh(List<Vec3> points, float width, uint color)
        {
            if (points == null || points.Count < 2) return null;

            Mesh mesh = Mesh.CreateMesh(true);
            if (mesh == null) return null;
            mesh.SetMaterial(BorderMaterial);
            UIntPtr lockHandle = mesh.LockEditDataWrite();
            try
            {
                float halfWidth = width * 0.5f;
                for (int index = 0; index < points.Count - 1; index++)
                {
                    Vec3 start = points[index];
                    Vec3 end = points[index + 1];
                    Vec2 direction = new Vec2(end.x - start.x, end.y - start.y);
                    if (direction.Normalize() < 0.001f) continue;
                    Vec2 normal = new Vec2(-direction.y * halfWidth, direction.x * halfWidth);

                    Vec3 startLeft = new Vec3(start.x + normal.x, start.y + normal.y, start.z);
                    Vec3 startRight = new Vec3(start.x - normal.x, start.y - normal.y, start.z);
                    Vec3 endLeft = new Vec3(end.x + normal.x, end.y + normal.y, end.z);
                    Vec3 endRight = new Vec3(end.x - normal.x, end.y - normal.y, end.z);
                    Vec2 uv0 = new Vec2(0f, 0f);
                    Vec2 uv1 = new Vec2(1f, 0f);
                    Vec2 uv2 = new Vec2(1f, 1f);
                    Vec2 uv3 = new Vec2(0f, 1f);

                    mesh.AddTriangle(startLeft, endLeft, endRight, uv0, uv1, uv2, color, lockHandle);
                    mesh.AddTriangle(startLeft, endRight, startRight, uv0, uv2, uv3, color, lockHandle);
                }
            }
            finally
            {
                mesh.UnlockEditDataWrite(lockHandle);
            }

            mesh.ComputeNormals();
            mesh.RecomputeBoundingBox();
            return mesh;
        }

        private static List<Vec3> SampleTerrainHeights(List<Vec2> points, float heightOffset)
        {
            List<Vec3> result = new List<Vec3>();
            foreach (Vec2 point in points)
            {
                float terrainHeight = 0f;
                CampaignVec2 campaignPoint = new CampaignVec2(point, true);
                if (Campaign.Current != null && Campaign.Current.MapSceneWrapper != null)
                {
                    Campaign.Current.MapSceneWrapper.GetHeightAtPoint(campaignPoint, ref terrainHeight);
                }
                result.Add(new Vec3(point.x, point.y, terrainHeight + heightOffset));
            }
            return result;
        }

        private static List<Vec2> GenerateLinePoints(Vec2 first, Vec2 second, int segments)
        {
            List<Vec2> points = new List<Vec2>();
            int count = Math.Max(1, segments);
            for (int index = 0; index <= count; index++)
            {
                float t = (float)index / count;
                points.Add(new Vec2(
                    first.x + (second.x - first.x) * t,
                    first.y + (second.y - first.y) * t));
            }
            return points;
        }

        private static List<Vec2> OffsetPolyline(List<Vec2> points, float offset)
        {
            List<Vec2> result = new List<Vec2>();
            for (int index = 0; index < points.Count; index++)
            {
                Vec2 previous = points[Math.Max(0, index - 1)];
                Vec2 next = points[Math.Min(points.Count - 1, index + 1)];
                Vec2 direction = new Vec2(next.x - previous.x, next.y - previous.y);
                if (direction.Normalize() < 0.001f) continue;
                Vec2 normal = new Vec2(-direction.y * offset, direction.x * offset);
                result.Add(new Vec2(points[index].x + normal.x, points[index].y + normal.y));
            }
            return result;
        }

        private void ClearBorderEntities()
        {
            foreach (GameEntity entity in _borderEntities)
            {
                if (entity == null) continue;
                try
                {
                    entity.Remove(0);
                }
                catch (Exception exception)
                {
                    // The engine may already have discarded this scene. The
                    // managed list is still cleared below, so stale handles
                    // cannot block the next scene rebuild.
                    Diagnostics.Error("A campaign border entity could not be removed after the map scene changed.", exception);
                }
            }
            _borderEntities.Clear();
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

        private static float DistanceSquared(Vec2 first, Vec2 second)
        {
            float x = first.x - second.x;
            float y = first.y - second.y;
            return x * x + y * y;
        }

        private static string MakeEdgeKey(string firstId, string secondId, Vec2 first, Vec2 second)
        {
            string left = string.CompareOrdinal(firstId, secondId) < 0 ? firstId : secondId;
            string right = string.CompareOrdinal(firstId, secondId) < 0 ? secondId : firstId;
            Vec2 midpoint = new Vec2((first.x + second.x) * 0.5f, (first.y + second.y) * 0.5f);
            return left + "|" + right + "|"
                + Math.Round(midpoint.x, 2).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + "|" + Math.Round(midpoint.y, 2).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        private sealed class BorderSite
        {
            internal BorderSite(Settlement settlement, Vec2 position, string factionKey, uint color)
            {
                Settlement = settlement;
                Position = position;
                FactionKey = factionKey;
                Color = color;
            }

            internal Settlement Settlement { get; private set; }
            internal Vec2 Position { get; private set; }
            internal string FactionKey { get; private set; }
            internal uint Color { get; private set; }
        }

        private sealed class BorderEdge
        {
            internal BorderEdge(Vec2 first, Vec2 second, BorderSite firstSite, BorderSite secondSite)
            {
                First = first;
                Second = second;
                FirstSite = firstSite;
                SecondSite = secondSite;
            }

            internal Vec2 First { get; private set; }
            internal Vec2 Second { get; private set; }
            internal BorderSite FirstSite { get; private set; }
            internal BorderSite SecondSite { get; private set; }
        }
    }
}
