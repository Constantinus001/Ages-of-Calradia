using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    internal sealed partial class CalendarWorldLedgerVM
    {
        private const float StrategicKingdomClusterLinkDistance = 340f;
        private readonly MBBindingList<CalendarStrategicKingdomLabelVM> _strategicKingdomLabels =
            new MBBindingList<CalendarStrategicKingdomLabelVM>();

        [DataSourceProperty]
        public MBBindingList<CalendarStrategicKingdomLabelVM> StrategicKingdomLabels
        {
            get { return _strategicKingdomLabels; }
        }

        private void BuildStrategicKingdomLabels(List<StrategicSettlementPoint> points)
        {
            _strategicKingdomLabels.Clear();
            Dictionary<Kingdom, List<StrategicSettlementPoint>> holdingsByKingdom =
                new Dictionary<Kingdom, List<StrategicSettlementPoint>>();
            foreach (StrategicSettlementPoint point in points)
            {
                Kingdom kingdom = point == null || point.Settlement == null || point.Settlement.OwnerClan == null
                    ? null
                    : point.Settlement.OwnerClan.Kingdom;
                if (kingdom == null || kingdom.IsEliminated) continue;
                List<StrategicSettlementPoint> holdings;
                if (!holdingsByKingdom.TryGetValue(kingdom, out holdings))
                {
                    holdings = new List<StrategicSettlementPoint>();
                    holdingsByKingdom.Add(kingdom, holdings);
                }
                holdings.Add(point);
            }

            List<Vec2> occupied = new List<Vec2>();
            List<KeyValuePair<Kingdom, List<StrategicSettlementPoint>>> orderedKingdoms =
                new List<KeyValuePair<Kingdom, List<StrategicSettlementPoint>>>(holdingsByKingdom);
            orderedKingdoms.Sort(delegate(
                KeyValuePair<Kingdom, List<StrategicSettlementPoint>> left,
                KeyValuePair<Kingdom, List<StrategicSettlementPoint>> right)
            {
                int bySize = right.Value.Count.CompareTo(left.Value.Count);
                return bySize != 0 ? bySize : string.CompareOrdinal(left.Key.StringId, right.Key.StringId);
            });
            foreach (KeyValuePair<Kingdom, List<StrategicSettlementPoint>> entry in orderedKingdoms)
            {
                List<StrategicSettlementPoint> cluster = FindDominantKingdomCluster(entry.Value);
                if (cluster.Count == 0) continue;
                float sourceX = 0f;
                float sourceY = 0f;
                foreach (StrategicSettlementPoint point in cluster)
                {
                    sourceX += point.SourceX;
                    sourceY += point.SourceY;
                }
                sourceX /= cluster.Count;
                sourceY /= cluster.Count;

                float x = sourceX * StrategicMapScale;
                float y = sourceY * StrategicMapScale;
                ResolveStrategicLabelOverlap(ref x, ref y, occupied);
                occupied.Add(new Vec2(x, y));
                _strategicKingdomLabels.Add(new CalendarStrategicKingdomLabelVM(
                    entry.Key.Name.ToString().ToUpperInvariant(),
                    (int)Math.Round(x - 150f),
                    (int)Math.Round(y - 20f),
                    cluster.Count));
            }
        }

        private static List<StrategicSettlementPoint> FindDominantKingdomCluster(
            List<StrategicSettlementPoint> holdings)
        {
            List<StrategicSettlementPoint> dominant = new List<StrategicSettlementPoint>();
            HashSet<StrategicSettlementPoint> remaining = new HashSet<StrategicSettlementPoint>(holdings);
            float linkSquared = StrategicKingdomClusterLinkDistance * StrategicKingdomClusterLinkDistance;
            while (remaining.Count > 0)
            {
                StrategicSettlementPoint seed = null;
                foreach (StrategicSettlementPoint point in remaining) { seed = point; break; }
                List<StrategicSettlementPoint> cluster = new List<StrategicSettlementPoint>();
                Queue<StrategicSettlementPoint> frontier = new Queue<StrategicSettlementPoint>();
                remaining.Remove(seed);
                frontier.Enqueue(seed);
                while (frontier.Count > 0)
                {
                    StrategicSettlementPoint current = frontier.Dequeue();
                    cluster.Add(current);
                    List<StrategicSettlementPoint> connected = new List<StrategicSettlementPoint>();
                    foreach (StrategicSettlementPoint candidate in remaining)
                    {
                        float dx = current.SourceX - candidate.SourceX;
                        float dy = current.SourceY - candidate.SourceY;
                        if ((dx * dx) + (dy * dy) <= linkSquared) connected.Add(candidate);
                    }
                    foreach (StrategicSettlementPoint candidate in connected)
                    {
                        remaining.Remove(candidate);
                        frontier.Enqueue(candidate);
                    }
                }
                if (cluster.Count > dominant.Count
                    || (cluster.Count == dominant.Count
                        && string.CompareOrdinal(GetClusterKey(cluster), GetClusterKey(dominant)) < 0))
                {
                    dominant = cluster;
                }
            }
            return dominant;
        }

        private static string GetClusterKey(List<StrategicSettlementPoint> cluster)
        {
            string key = null;
            foreach (StrategicSettlementPoint point in cluster)
            {
                string id = point == null || point.Settlement == null ? string.Empty : point.Settlement.StringId;
                if (key == null || string.CompareOrdinal(id, key) < 0) key = id;
            }
            return key ?? string.Empty;
        }

        private static void ResolveStrategicLabelOverlap(ref float x, ref float y, List<Vec2> occupied)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                bool overlaps = false;
                foreach (Vec2 position in occupied)
                {
                    if (Math.Abs(position.x - x) < 230f && Math.Abs(position.y - y) < 44f)
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (!overlaps) return;
                y += attempt % 2 == 0 ? 46f : -92f;
            }
        }
    }

    internal sealed class CalendarStrategicKingdomLabelVM : ViewModel
    {
        internal CalendarStrategicKingdomLabelVM(string name, int x, int y, int holdingCount)
        { Name = name ?? string.Empty; X = x; Y = y; HoldingCount = holdingCount; }
        [DataSourceProperty] public string Name { get; private set; }
        [DataSourceProperty] public int X { get; private set; }
        [DataSourceProperty] public int Y { get; private set; }
        [DataSourceProperty] public int HoldingCount { get; private set; }
        [DataSourceProperty] public string GoldColor { get { return "#FFD66FFF"; } }
    }
}
