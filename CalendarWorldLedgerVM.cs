using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    internal sealed partial class CalendarWorldLedgerVM : ViewModel
    {
        private const float StrategicMapViewportWidth = 810f;
        private const float StrategicMapViewportHeight = 610f;
        private const float StrategicMapMinimumZoom = 1f;
        private const float StrategicMapMaximumZoom = 2.5f;
        private const float StrategicMapZoomStep = 0.25f;
        // Markers are painted at a fixed size in the source texture. Keeping
        // their centres this far apart prevents town/castle silhouettes from
        // merging when the player zooms into dense settlement clusters.
        private const float StrategicMarkerMinimumSeparation = 52f;
        private const float StrategicMarkerEdgePadding = 20f;
        private string _title = "World Calendar";
        private string _monthTitle = string.Empty;
        private string _notesText = "No world events have been recorded yet.";
        private string _strategicText = string.Empty;
        private bool _isStrategicMap;
        private bool _isCalendarVisible = true;
        private readonly MBBindingList<CalendarWorldLedgerTabVM> _tabs = new MBBindingList<CalendarWorldLedgerTabVM>();
        private readonly MBBindingList<CalendarWorldCalendarDayVM> _days = new MBBindingList<CalendarWorldCalendarDayVM>();
        private readonly MBBindingList<CalendarWorldStrategicProvinceVM> _strategicProvinces = new MBBindingList<CalendarWorldStrategicProvinceVM>();
        private readonly MBBindingList<CalendarWorldStrategicProvinceVM> _strategicContestedProvinces = new MBBindingList<CalendarWorldStrategicProvinceVM>();
        private readonly MBBindingList<CalendarWorldStrategicMarkerVM> _strategicMarkers = new MBBindingList<CalendarWorldStrategicMarkerVM>();
        // The repeated ItemTemplate renderer intermittently loses an item's bound
        // sprite/color pair in Gauntlet. Fixed named layers are used by the map XML
        // instead; these arrays provide the matching data sources for those layers.
        private readonly CalendarWorldStrategicProvinceVM[] _fixedStrategicProvinces = new CalendarWorldStrategicProvinceVM[133];
        private readonly CalendarWorldStrategicProvinceVM[] _fixedStrategicContestedProvinces = new CalendarWorldStrategicProvinceVM[133];
        private readonly Action _close;
        private string _selectedFilter = "All";
        private string _selectedStrategicSettlementId = string.Empty;
        private float _strategicMapZoom = StrategicMapMinimumZoom;
        // Reference-map positions recovered from the supplied settlement-marker
        // artwork. These are the original 2000 x 2000 map coordinates, before
        // the displayed map's black-frame crop is applied. Keeping anchors for
        // every town (including the northern expansion) lets castle positions
        // be projected from the live campaign layout without relying on the
        // obsolete pre-expansion map coordinates.
        private static readonly Dictionary<string, Vec2> TownReferenceAnchors = new Dictionary<string, Vec2>(StringComparer.Ordinal)
        {
            { "town_N1", new Vec2(290f, 470.5f) }, // Hvalvik
            { "town_N2", new Vec2(938.5f, 394.5f) }, // Gretysfjord
            { "town_N3", new Vec2(1349f, 331.5f) }, // Thronderlag
            { "town_N4", new Vec2(1674f, 439.5f) }, // Hargard

            { "town_S1", new Vec2(930f, 712.5f) }, // Varcheg
            { "town_S2", new Vec2(1342f, 512f) }, // Balgard
            { "town_S3", new Vec2(1005.5f, 773f) }, // Omor
            { "town_S4", new Vec2(1192f, 694f) }, // Varnovapol
            { "town_S5", new Vec2(1411f, 662.5f) }, // Tyal
            { "town_S6", new Vec2(1125f, 625.5f) }, // Sibir
            { "town_S7", new Vec2(731f, 629.5f) }, // Revyl

            { "town_V1", new Vec2(518f, 1106.5f) }, // Sargot
            { "town_V2", new Vec2(477f, 907.5f) }, // Ocs Hall
            { "town_V3", new Vec2(371f, 913.5f) }, // Pravend
            { "town_V5", new Vec2(273f, 999.5f) }, // Galend
            { "town_V6", new Vec2(383f, 1078.5f) }, // Jaculan
            { "town_V7", new Vec2(499f, 1176.5f) }, // Charas
            { "town_V8", new Vec2(377f, 750.5f) }, // Ostican
            { "town_V9", new Vec2(450.5f, 804.5f) }, // Rovalt

            { "town_B1", new Vec2(663f, 1014.5f) }, // Marunath
            { "town_B2", new Vec2(618f, 909f) }, // Dunglanys
            { "town_B3", new Vec2(692f, 787.5f) }, // Car Banseth
            { "town_B4", new Vec2(752f, 955.5f) }, // Seonon
            { "town_B5", new Vec2(549f, 952.5f) }, // Pen Cannoc

            { "town_EN1", new Vec2(913f, 931f) }, // Epicrotea
            { "town_EN2", new Vec2(1075f, 915f) }, // Diathma
            { "town_EN3", new Vec2(1169.5f, 1026.5f) }, // Saneopa
            { "town_EN4", new Vec2(1203f, 861.5f) }, // Argoron
            { "town_EN5", new Vec2(1371f, 990.5f) }, // Myzea
            { "town_EN6", new Vec2(1396f, 891.5f) }, // Amprela

            { "town_EW1", new Vec2(677f, 1108.5f) }, // Lageta
            { "town_EW2", new Vec2(919f, 1274f) }, // Zeonica
            { "town_EW3", new Vec2(836.5f, 1224.5f) }, // Jalmarys
            { "town_EW4", new Vec2(680f, 1235.5f) }, // Ortysia
            { "town_EW5", new Vec2(975.5f, 1206.5f) }, // Amitatys
            { "town_EW6", new Vec2(853f, 1153f) }, // Rhotae

            { "town_ES1", new Vec2(1454f, 1373.5f) }, // Danustica
            { "town_ES2", new Vec2(1257.5f, 1341.5f) }, // Vostrum
            { "town_ES3", new Vec2(1096f, 1336f) }, // Poros
            { "town_ES4", new Vec2(1173f, 1211.5f) }, // Lycaron
            { "town_ES5", new Vec2(1498f, 1262.5f) }, // Onira
            { "town_ES6", new Vec2(1342f, 1129.5f) }, // Phycaon
            { "town_ES7", new Vec2(1469.5f, 1050.5f) }, // Syronea

            { "town_K1", new Vec2(1670f, 735.5f) }, // Baltakhand
            { "town_K2", new Vec2(1713f, 1051.5f) }, // Akkalat
            { "town_K3", new Vec2(1583.5f, 861.5f) }, // Makeb
            { "town_K4", new Vec2(1709.5f, 904.5f) }, // Ortongard
            { "town_K5", new Vec2(1592.5f, 1029.5f) }, // Chaikand
            { "town_K6", new Vec2(1690f, 1166.5f) }, // Odokh

            { "town_A1", new Vec2(636f, 1421.5f) }, // Quyaz
            { "town_A2", new Vec2(1571f, 1449f) }, // Husn Fulq
            { "town_A3", new Vec2(1049f, 1702.5f) }, // Iyakis
            { "town_A4", new Vec2(1375f, 1581f) }, // Razih
            { "town_A5", new Vec2(1230f, 1741.5f) }, // Hubyar
            { "town_A6", new Vec2(842f, 1537f) }, // Sanala
            { "town_A7", new Vec2(794.5f, 1708.5f) }, // Askar
            { "town_A8", new Vec2(1134f, 1636.5f) } // Qasira
        };

        // Castle anchors use the same source map and are kept separate only to
        // make audit updates easy. A small number of map icons are obscured by
        // the original artwork; those fall back to the live calibrated
        // projection from the surrounding town and castle anchors.
        private static readonly Dictionary<string, Vec2> CastleReferenceAnchors = new Dictionary<string, Vec2>(StringComparer.Ordinal)
        {
            { "castle_N1", new Vec2(1522.5f, 564f) },
            { "castle_N2", new Vec2(798.5f, 345f) },
            { "castle_N5", new Vec2(1141.5f, 290.5f) },
            { "castle_N7", new Vec2(278.5f, 380f) },
            { "castle_N8", new Vec2(1579.5f, 370.5f) },
            { "castle_N9", new Vec2(1698.5f, 537.5f) },

            { "castle_S1", new Vec2(662f, 607.5f) },
            { "castle_S2", new Vec2(944.5f, 819f) },
            { "castle_S3", new Vec2(827.5f, 830.5f) },
            { "castle_S4", new Vec2(870.5f, 748f) },
            { "castle_S5", new Vec2(1078.5f, 671f) },
            { "castle_S6", new Vec2(1279.5f, 616.5f) },
            { "castle_S7", new Vec2(1449.5f, 738f) },
            { "castle_S8", new Vec2(1420.5f, 607.5f) },

            { "castle_V1", new Vec2(432.5f, 1128.5f) },
            { "castle_V2", new Vec2(341.5f, 1021.5f) },
            { "castle_V3", new Vec2(293.5f, 862.5f) },
            { "castle_V4", new Vec2(501.5f, 826.5f) },
            { "castle_V5", new Vec2(423.5f, 726.5f) },
            { "castle_V6", new Vec2(404.5f, 873.5f) },
            { "castle_V7", new Vec2(469.5f, 1056.5f) },
            { "castle_V8", new Vec2(481.5f, 970f) },

            { "castle_B1", new Vec2(542.5f, 1044.5f) },
            { "castle_B4", new Vec2(736f, 868.5f) },
            { "castle_B5", new Vec2(745.5f, 1030.5f) },
            { "castle_B6", new Vec2(624.5f, 866.5f) },
            { "castle_B7", new Vec2(568.5f, 809.5f) },
            { "castle_B8", new Vec2(795.5f, 898.5f) },

            { "castle_EN1", new Vec2(1093.5f, 1076f) },
            { "castle_EN2", new Vec2(1439.5f, 793.5f) },
            { "castle_EN3", new Vec2(1032.5f, 995f) },
            { "castle_EN4", new Vec2(1325.5f, 999f) },
            { "castle_EN6", new Vec2(946.5f, 1074f) },
            { "castle_EN7", new Vec2(1498f, 794.5f) },
            { "castle_EN8", new Vec2(1157.5f, 1128.5f) },
            { "castle_EN9", new Vec2(874.5f, 973.5f) },

            { "castle_EW1", new Vec2(592.5f, 1297.5f) },
            { "castle_EW2", new Vec2(931.5f, 1218f) },
            { "castle_EW3", new Vec2(990.5f, 1326f) },
            { "castle_EW4", new Vec2(597.5f, 1166.5f) },
            { "castle_EW5", new Vec2(601.5f, 1096.5f) },
            { "castle_EW6", new Vec2(743.5f, 1114.5f) },
            { "castle_EW7", new Vec2(789.5f, 1329f) },
            { "castle_EW8", new Vec2(842.5f, 1009.5f) },

            { "castle_ES1", new Vec2(1514.5f, 1412.5f) },
            { "castle_ES2", new Vec2(1468.5f, 1150.5f) },
            { "castle_ES3", new Vec2(1362.5f, 1089.5f) },
            { "castle_ES4", new Vec2(1369.5f, 1377.5f) },
            { "castle_ES5", new Vec2(1322.5f, 1254f) },
            { "castle_ES6", new Vec2(1121.5f, 1218f) },
            { "castle_ES7", new Vec2(1470.5f, 1002f) },
            { "castle_ES8", new Vec2(1243.5f, 1108f) },

            { "castle_K1", new Vec2(1629.5f, 879.5f) },
            { "castle_K2", new Vec2(1589.5f, 1179f) },
            { "castle_K3", new Vec2(1655.5f, 963f) },
            { "castle_K5", new Vec2(1706.5f, 844.5f) },
            { "castle_K6", new Vec2(1653.5f, 651.5f) },
            { "castle_K7", new Vec2(1618.5f, 1052.5f) },
            { "castle_K8", new Vec2(1646.5f, 1224.5f) },
            { "castle_K9", new Vec2(1570.5f, 715.5f) },

            { "castle_A2", new Vec2(1414.5f, 1644.5f) },
            { "castle_A3", new Vec2(806.5f, 1610.5f) },
            { "castle_A4", new Vec2(994.5f, 1635.5f) },
            { "castle_A6", new Vec2(1563.5f, 1383.5f) },
            { "castle_A7", new Vec2(706f, 1501f) },
            { "castle_A8", new Vec2(1496.5f, 1533.5f) }
        };

        // These ten castle icons are obscured or merged into the source
        // artwork. Their coordinates are a locally calibrated projection from
        // the 123 visible town/castle markers, not the old global fallback.
        private static readonly Dictionary<string, Vec2> ProjectedCastleReferenceAnchors = new Dictionary<string, Vec2>(StringComparer.Ordinal)
        {
            { "castle_A1", new Vec2(572.6f, 1506.8f) },
            { "castle_A5", new Vec2(1147.0f, 1686.2f) },
            { "castle_A9", new Vec2(721.0f, 1628.6f) },
            { "castle_B2", new Vec2(594.9f, 1015.3f) },
            { "castle_B3", new Vec2(547.1f, 857.3f) },
            { "castle_EN5", new Vec2(1204.5f, 929.4f) },
            { "castle_K4", new Vec2(1613.1f, 815.8f) },
            { "castle_N3", new Vec2(931.1f, 167.1f) },
            { "castle_N4", new Vec2(1405.2f, 181.0f) },
            { "castle_N6", new Vec2(1612.5f, 274.1f) }
        };

        internal CalendarWorldLedgerVM(Action close)
        {
            _close = close;
            AddTab("All", "All Events"); AddTab("ByDay", "By Day"); AddTab("Diplomacy", "Diplomacy"); AddTab("Settlements", "Settlements"); AddTab("People", "People"); AddTab("Strategic", "Strategic Map");
            SelectTab(_tabs[0]);
        }

        [DataSourceProperty] public string Title { get { return _title; } }
        [DataSourceProperty] public string MonthTitle { get { return _monthTitle; } private set { if (_monthTitle == value) return; _monthTitle = value ?? string.Empty; OnPropertyChangedWithValue(_monthTitle, "MonthTitle"); } }
        [DataSourceProperty] public string NotesText { get { return _notesText; } private set { if (_notesText == value) return; _notesText = value ?? string.Empty; OnPropertyChangedWithValue(_notesText, "NotesText"); } }
        [DataSourceProperty] public string StrategicText { get { return _strategicText; } private set { if (_strategicText == value) return; _strategicText = value ?? string.Empty; OnPropertyChangedWithValue(_strategicText, "StrategicText"); } }
        [DataSourceProperty] public bool IsStrategicMap { get { return _isStrategicMap; } private set { if (_isStrategicMap == value) return; _isStrategicMap = value; OnPropertyChangedWithValue(value, "IsStrategicMap"); IsCalendarVisible = !value; } }
        [DataSourceProperty] public bool IsCalendarVisible { get { return _isCalendarVisible; } private set { if (_isCalendarVisible == value) return; _isCalendarVisible = value; OnPropertyChangedWithValue(value, "IsCalendarVisible"); } }
        [DataSourceProperty] public MBBindingList<CalendarWorldLedgerTabVM> Tabs { get { return _tabs; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldCalendarDayVM> Days { get { return _days; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicProvinceVM> StrategicProvinces { get { return _strategicProvinces; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicProvinceVM> StrategicContestedProvinces { get { return _strategicContestedProvinces; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicMarkerVM> StrategicMarkers { get { return _strategicMarkers; } }
        [DataSourceProperty] public float MapCanvasWidth { get { return StrategicMapViewportWidth * _strategicMapZoom; } }
        [DataSourceProperty] public float MapCanvasHeight { get { return StrategicMapViewportHeight * _strategicMapZoom; } }
        [DataSourceProperty] public string MapZoomText { get { return _strategicMapZoom.ToString("0.00x"); } }
        [DataSourceProperty] public bool CanZoomIn { get { return _strategicMapZoom < StrategicMapMaximumZoom; } }
        [DataSourceProperty] public bool CanZoomOut { get { return _strategicMapZoom > StrategicMapMinimumZoom; } }

        public void ExecuteClose() { if (_close != null) _close(); }
        public void ExecuteRefresh() { RefreshCalendar(); }
        internal void RefreshWorldState() { RefreshCalendar(); }
        public void ExecuteZoomIn() { SetStrategicMapZoom(_strategicMapZoom + StrategicMapZoomStep); }
        public void ExecuteZoomOut() { SetStrategicMapZoom(_strategicMapZoom - StrategicMapZoomStep); }
        public void ExecuteResetMapView() { SetStrategicMapZoom(StrategicMapMinimumZoom); }
        private void AddTab(string filter, string label) { _tabs.Add(new CalendarWorldLedgerTabVM(filter, label, SelectTab)); }
        private void SelectTab(CalendarWorldLedgerTabVM tab) { if (tab == null) return; _selectedFilter = tab.Filter; IsStrategicMap = string.Equals(_selectedFilter, "Strategic", StringComparison.Ordinal); foreach (CalendarWorldLedgerTabVM entry in _tabs) entry.IsSelected = ReferenceEquals(entry, tab); RefreshCalendar(); }

        private void RefreshCalendar()
        {
            NotesText = CalendarWorldLedgerBehavior.GetRecentEntriesText(_selectedFilter);
            BuildStrategicMapLayers();
            StrategicText = BuildStrategicPanelText();
            BuildMonthGrid();
        }

        private void BuildStrategicMapLayers()
        {
            Dictionary<string, IFaction> ownersBySettlementId = new Dictionary<string, IFaction>(StringComparer.Ordinal);
            Dictionary<string, IFaction> besiegersBySettlementId = new Dictionary<string, IFaction>(StringComparer.Ordinal);
            List<StrategicSettlementPoint> markerPoints = new List<StrategicSettlementPoint>();
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement == null || settlement.Town == null) continue;
                IFaction currentOwner = CalendarWorldLedgerBehavior.GetLiveSettlementFaction(settlement);
                if (currentOwner == null || string.IsNullOrEmpty(settlement.StringId)) continue;

                // The strategic map must always reflect the current campaign
                // owner. The saved daily tracker is retained for history and
                // diagnostics, but is never allowed to override live state.
                ownersBySettlementId[settlement.StringId] = currentOwner;
                IFaction besieger = GetBesiegerFaction(settlement);
                if (besieger != null)
                {
                    besiegersBySettlementId[settlement.StringId] = besieger;
                }
                Vec2 sourcePosition = ProjectSettlementToReferenceMap(settlement);
                markerPoints.Add(new StrategicSettlementPoint(
                    settlement,
                    sourcePosition.x,
                    sourcePosition.y,
                    currentOwner,
                    besieger != null));
            }

            ResolveStrategicMarkerSpacing(markerPoints);
            BuildStrategicProvinces(ownersBySettlementId, markerPoints);
            BuildStrategicContestedProvinces(besiegersBySettlementId);
            BuildStrategicMarkers(markerPoints);
        }

        // Keep each settlement's true anchor separately from its visible
        // marker. Towns retain their exact location first; nearby castles (or
        // a second town) are nudged just enough to stay distinct. Both the
        // painted symbol and its click target use this display position.
        private static void ResolveStrategicMarkerSpacing(List<StrategicSettlementPoint> points)
        {
            if (points == null || points.Count < 2) return;

            List<StrategicSettlementPoint> ordered = new List<StrategicSettlementPoint>(points);
            ordered.Sort(delegate(StrategicSettlementPoint left, StrategicSettlementPoint right)
            {
                bool leftIsTown = left != null && left.Settlement != null && left.Settlement.IsTown;
                bool rightIsTown = right != null && right.Settlement != null && right.Settlement.IsTown;
                if (leftIsTown != rightIsTown) return leftIsTown ? -1 : 1;

                string leftId = left == null || left.Settlement == null ? string.Empty : left.Settlement.StringId;
                string rightId = right == null || right.Settlement == null ? string.Empty : right.Settlement.StringId;
                return string.Compare(leftId, rightId, StringComparison.Ordinal);
            });

            List<StrategicSettlementPoint> placed = new List<StrategicSettlementPoint>();
            foreach (StrategicSettlementPoint point in ordered)
            {
                if (point == null) continue;
                point.ResetDisplayPosition();

                float candidateX = point.SourceX;
                float candidateY = point.SourceY;
                if (!IsStrategicMarkerPositionClear(candidateX, candidateY, placed))
                {
                    float pushX = 0f;
                    float pushY = 0f;
                    foreach (StrategicSettlementPoint occupied in placed)
                    {
                        float deltaX = candidateX - occupied.DisplayX;
                        float deltaY = candidateY - occupied.DisplayY;
                        float distance = (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                        if (distance >= StrategicMarkerMinimumSeparation) continue;

                        if (distance < 0.01f)
                        {
                            // A deterministic fallback covers rare third-party
                            // settlements that share the same campaign point.
                            int direction = StableMarkerDirection(point);
                            double radians = direction * (Math.PI * 2d / 12d);
                            deltaX = (float)Math.Cos(radians);
                            deltaY = (float)Math.Sin(radians);
                            distance = 1f;
                        }

                        float requiredPush = StrategicMarkerMinimumSeparation - distance + 4f;
                        pushX += (deltaX / distance) * requiredPush;
                        pushY += (deltaY / distance) * requiredPush;
                    }

                    candidateX = ClampStrategicMarkerX(point.SourceX + pushX);
                    candidateY = ClampStrategicMarkerY(point.SourceY + pushY);

                    if (!IsStrategicMarkerPositionClear(candidateX, candidateY, placed))
                    {
                        // A deterministic spiral is the final fallback for
                        // unusually dense modded settlement layouts.
                        bool placedInSpiral = false;
                        int startDirection = StableMarkerDirection(point);
                        for (int ring = 1; ring <= 5 && !placedInSpiral; ring++)
                        {
                            float radius = 24f + (ring * 20f);
                            for (int step = 0; step < 12; step++)
                            {
                                int direction = (startDirection + step) % 12;
                                double radians = direction * (Math.PI * 2d / 12d);
                                float spiralX = ClampStrategicMarkerX(point.SourceX + ((float)Math.Cos(radians) * radius));
                                float spiralY = ClampStrategicMarkerY(point.SourceY + ((float)Math.Sin(radians) * radius));
                                if (!IsStrategicMarkerPositionClear(spiralX, spiralY, placed)) continue;

                                candidateX = spiralX;
                                candidateY = spiralY;
                                placedInSpiral = true;
                                break;
                            }
                        }
                    }
                }

                point.SetDisplayPosition(candidateX, candidateY);
                placed.Add(point);
            }
        }

        private static bool IsStrategicMarkerPositionClear(float x, float y, List<StrategicSettlementPoint> placed)
        {
            foreach (StrategicSettlementPoint occupied in placed)
            {
                float deltaX = x - occupied.DisplayX;
                float deltaY = y - occupied.DisplayY;
                float distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
                if (distanceSquared < StrategicMarkerMinimumSeparation * StrategicMarkerMinimumSeparation) return false;
            }
            return true;
        }

        private static int StableMarkerDirection(StrategicSettlementPoint point)
        {
            string id = point == null || point.Settlement == null ? string.Empty : point.Settlement.StringId;
            int hash = 17;
            for (int index = 0; index < id.Length; index++) hash = (hash * 31) + id[index];
            return (hash & 0x7FFFFFFF) % 12;
        }

        private static float ClampStrategicMarkerX(float x)
        {
            return Math.Max(StrategicMarkerEdgePadding, Math.Min(CalendarStrategicMapLayout.SourceWidth - StrategicMarkerEdgePadding, x));
        }

        private static float ClampStrategicMarkerY(float y)
        {
            return Math.Max(StrategicMarkerEdgePadding, Math.Min(CalendarStrategicMapLayout.SourceHeight - StrategicMarkerEdgePadding, y));
        }

        private static IFaction GetBesiegerFaction(Settlement settlement)
        {
            if (settlement == null
                || !settlement.IsUnderSiege
                || settlement.SiegeEvent == null
                || settlement.SiegeEvent.BesiegerCamp == null)
            {
                return null;
            }

            return settlement.SiegeEvent.BesiegerCamp.MapFaction;
        }

        private static bool TryGetReferenceAnchor(string settlementId, out Vec2 referenceAnchor)
        {
            if (!string.IsNullOrEmpty(settlementId)
                && TownReferenceAnchors.TryGetValue(settlementId, out referenceAnchor))
            {
                return true;
            }
            if (!string.IsNullOrEmpty(settlementId)
                && CastleReferenceAnchors.TryGetValue(settlementId, out referenceAnchor))
            {
                return true;
            }
            if (!string.IsNullOrEmpty(settlementId)
                && ProjectedCastleReferenceAnchors.TryGetValue(settlementId, out referenceAnchor))
            {
                return true;
            }

            referenceAnchor = new Vec2(0f, 0f);
            return false;
        }

        private static Vec2 ProjectSettlementToReferenceMap(Settlement settlement)
        {
            Vec2 referenceAnchor;
            if (settlement != null
                && TryGetReferenceAnchor(settlement.StringId, out referenceAnchor))
            {
                return new Vec2(
                    referenceAnchor.x - CalendarStrategicMapLayout.CropLeft,
                    referenceAnchor.y - CalendarStrategicMapLayout.CropTop);
            }

            Vec2 campaignPosition = settlement == null ? new Vec2(0f, 0f) : settlement.GetPosition2D;
            // Only unknown, third-party settlements use this conservative
            // fallback. All vanilla/War Sails towns and castles use the
            // audited source-map coordinates above.
            const float sourceXScale = 2.23f;
            const float sourceXOffset = 20f;
            const float sourceYScale = -2.34f;
            const float sourceYOffset = 1985f;

            float sourceX = (campaignPosition.x * sourceXScale) + sourceXOffset - CalendarStrategicMapLayout.CropLeft;
            float sourceY = (campaignPosition.y * sourceYScale) + sourceYOffset - CalendarStrategicMapLayout.CropTop;
            sourceX = Math.Max(0f, Math.Min(CalendarStrategicMapLayout.SourceWidth, sourceX));
            sourceY = Math.Max(0f, Math.Min(CalendarStrategicMapLayout.SourceHeight, sourceY));
            return new Vec2(sourceX, sourceY);
        }

        private void BuildStrategicMarkers(List<StrategicSettlementPoint> points)
        {
            _strategicMarkers.Clear();
            float scaleX = MapCanvasWidth / CalendarStrategicMapLayout.SourceWidth;
            float scaleY = MapCanvasHeight / CalendarStrategicMapLayout.SourceHeight;
            foreach (StrategicSettlementPoint point in points)
            {
                bool isTown = point.Settlement != null && point.Settlement.IsTown;
                // The hit area stays generous, while the visible symbol is a
                // settlement-type icon rather than the old coloured square.
                int markerSize = 20;
                int iconSize = isTown ? 18 : 17;
                float markerHalfSize = markerSize / 2f;
                _strategicMarkers.Add(new CalendarWorldStrategicMarkerVM(
                    (int)Math.Round((point.DisplayX * scaleX) - markerHalfSize),
                    (int)Math.Round((point.DisplayY * scaleY) - markerHalfSize),
                    ToUiColor(point.Owner.Color),
                    point.Settlement,
                    isTown,
                    markerSize,
                    iconSize,
                    point.IsUnderSiege,
                    string.Equals(point.Settlement.StringId, _selectedStrategicSettlementId, StringComparison.Ordinal),
                    SelectStrategicSettlement));
            }
        }

        private void SelectStrategicSettlement(CalendarWorldStrategicMarkerVM marker)
        {
            if (marker == null || marker.Settlement == null) return;
            _selectedStrategicSettlementId = marker.Settlement.StringId ?? string.Empty;
            foreach (CalendarWorldStrategicMarkerVM entry in _strategicMarkers)
            {
                entry.IsSelected = ReferenceEquals(entry, marker);
            }
            StrategicText = BuildStrategicPanelText();
        }

        private void BuildStrategicProvinces(
            Dictionary<string, IFaction> ownersBySettlementId,
            List<StrategicSettlementPoint> markerPoints)
        {
            _strategicProvinces.Clear();
            Dictionary<string, uint> ownerColorsBySettlementId = new Dictionary<string, uint>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IFaction> entry in ownersBySettlementId)
            {
                if (!string.IsNullOrEmpty(entry.Key) && entry.Value != null)
                {
                    ownerColorsBySettlementId[entry.Key] = NormalizeFactionColor(entry.Value.Color);
                }
            }

            float scaleX = MapCanvasWidth / CalendarStrategicMapLayout.SourceWidth;
            float scaleY = MapCanvasHeight / CalendarStrategicMapLayout.SourceHeight;
            for (int index = 0; index < CalendarStrategicMapLayout.Provinces.Length; index++)
            {
                CalendarStrategicProvinceDefinition province = CalendarStrategicMapLayout.Provinces[index];
                string settlementId;
                IFaction owner = null;
                bool hasOwner = CalendarStrategicMapLayout.TryGetSettlementId(province.SpriteName, out settlementId)
                    && ownersBySettlementId.TryGetValue(settlementId, out owner);
                CalendarWorldStrategicProvinceVM layer = new CalendarWorldStrategicProvinceVM(
                    province.SpriteName,
                    (int)Math.Round((province.X - CalendarStrategicMapLayout.CropLeft) * scaleX),
                    (int)Math.Round((province.Y - CalendarStrategicMapLayout.CropTop) * scaleY),
                    Math.Max(1, (int)Math.Ceiling(province.Width * scaleX)),
                    Math.Max(1, (int)Math.Ceiling(province.Height * scaleY)),
                    hasOwner ? ToProvinceColor(owner.Color) : "#00000000");
                _strategicProvinces.Add(layer);
                _fixedStrategicProvinces[index] = layer;
                NotifyFixedStrategicProvince(index, false);
            }

            // One composed texture owns all faction-colour rendering. This
            // avoids Gauntlet dropping individual sprite-color bindings and
            // keeps every province interior filled on the live map.
            CalendarStrategicMapTextureProvider.UpdateMapState(ownerColorsBySettlementId, markerPoints);
        }

        private void BuildStrategicContestedProvinces(Dictionary<string, IFaction> besiegersBySettlementId)
        {
            _strategicContestedProvinces.Clear();

            float scaleX = MapCanvasWidth / CalendarStrategicMapLayout.SourceWidth;
            float scaleY = MapCanvasHeight / CalendarStrategicMapLayout.SourceHeight;
            for (int index = 0; index < CalendarStrategicMapLayout.Provinces.Length; index++)
            {
                CalendarStrategicProvinceDefinition province = CalendarStrategicMapLayout.Provinces[index];
                string settlementId;
                IFaction besieger = null;
                bool hasBesieger = CalendarStrategicMapLayout.TryGetSettlementId(province.SpriteName, out settlementId)
                    && besiegersBySettlementId.TryGetValue(settlementId, out besieger);

                // A semi-transparent attacker-color wash is shown only while
                // Bannerlord reports an active SiegeEvent. The base owner fill
                // and the transparent mask edge keep the black province lines
                // readable beneath it.
                CalendarWorldStrategicProvinceVM layer = new CalendarWorldStrategicProvinceVM(
                    province.SpriteName,
                    (int)Math.Round((province.X - CalendarStrategicMapLayout.CropLeft) * scaleX),
                    (int)Math.Round((province.Y - CalendarStrategicMapLayout.CropTop) * scaleY),
                    Math.Max(1, (int)Math.Ceiling(province.Width * scaleX)),
                    Math.Max(1, (int)Math.Ceiling(province.Height * scaleY)),
                    hasBesieger ? ToContestedColor(besieger.Color) : "#00000000");
                if (hasBesieger) _strategicContestedProvinces.Add(layer);
                _fixedStrategicContestedProvinces[index] = layer;
                NotifyFixedStrategicProvince(index, true);
            }
        }

        private void SetStrategicMapZoom(float value)
        {
            float zoom = Math.Max(StrategicMapMinimumZoom, Math.Min(StrategicMapMaximumZoom, value));
            if (Math.Abs(_strategicMapZoom - zoom) < 0.001f) return;
            _strategicMapZoom = zoom;
            OnPropertyChangedWithValue(MapCanvasWidth, "MapCanvasWidth");
            OnPropertyChangedWithValue(MapCanvasHeight, "MapCanvasHeight");
            OnPropertyChangedWithValue(MapZoomText, "MapZoomText");
            OnPropertyChangedWithValue(CanZoomIn, "CanZoomIn");
            OnPropertyChangedWithValue(CanZoomOut, "CanZoomOut");
            BuildStrategicMapLayers();
        }

        // IFaction.Color is stored by Bannerlord as AARRGGBB. Gauntlet expects
        // RRGGBBAA, so always use the engine conversion instead of reordering
        // the bytes by hand. Custom factions with an omitted alpha stay opaque.
        private static string ToUiColor(uint color)
        {
            uint argb = NormalizeFactionColor(color);
            return "#" + Color.UIntToColorString(argb);
        }

        private static string ToProvinceColor(uint color)
        {
            uint argb = NormalizeFactionColor(color);
            string rgba = Color.UIntToColorString(argb);
            // Province masks contain only their interior pixels; their edge pixels are
            // transparent. Use an opaque fill so the faction territory is unambiguous
            // while the black borders and the map-aligned city labels remain visible.
            return "#" + rgba.Substring(0, 6) + "FF";
        }

        private static string ToContestedColor(uint color)
        {
            uint argb = NormalizeFactionColor(color);
            string rgba = Color.UIntToColorString(argb);
            return "#" + rgba.Substring(0, 6) + "88";
        }

        private static uint NormalizeFactionColor(uint color)
        {
            return (color & 0xFF000000u) == 0 ? color | 0xFF000000u : color;
        }

        private string BuildStrategicPanelText()
        {
            if (string.IsNullOrEmpty(_selectedStrategicSettlementId))
            {
                return CalendarWorldLedgerBehavior.GetTrackedSettlementOwnersText();
            }

            Settlement selected = null;
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement != null && string.Equals(settlement.StringId, _selectedStrategicSettlementId, StringComparison.Ordinal))
                {
                    selected = settlement;
                    break;
                }
            }

            if (selected == null || selected.Town == null)
            {
                _selectedStrategicSettlementId = string.Empty;
                return CalendarWorldLedgerBehavior.GetTrackedSettlementOwnersText();
            }

            IFaction owner = CalendarWorldLedgerBehavior.GetLiveSettlementFaction(selected);
            IFaction besieger = GetBesiegerFaction(selected);
            StringBuilder text = new StringBuilder();
            text.Append(selected.Name).AppendLine();
            text.Append(selected.IsTown ? "Town" : "Castle").AppendLine();
            text.Append("Owner: ").Append(owner == null ? "Unknown" : owner.Name.ToString()).AppendLine();
            if (owner != null) text.Append("Map colour: ").Append(ToProvinceColor(owner.Color)).AppendLine();
            if (besieger != null)
            {
                text.Append("Status: UNDER SIEGE").AppendLine();
                text.Append("Besieging faction: ").Append(besieger.Name.ToString()).AppendLine();
            }

            if (!CanPlayerInspectSettlement(selected, owner))
            {
                text.AppendLine();
                text.Append("Detailed settlement information is available only for settlements you own or for the faction you serve.");
                return text.ToString();
            }

            Town town = selected.Town;
            text.AppendLine();
            text.Append("Prosperity: ").Append(Math.Round(town.Prosperity)).AppendLine();
            text.Append("Loyalty: ").Append(town.Loyalty.ToString("0.0")).AppendLine();
            text.Append("Security: ").Append(town.Security.ToString("0.0")).AppendLine();
            text.Append("Militia: ").Append(Math.Round(town.Militia)).AppendLine();
            text.Append("Food stocks: ").Append(town.FoodStocks.ToString("0.0"));
            return text.ToString();
        }

        private static bool CanPlayerInspectSettlement(Settlement settlement, IFaction owner)
        {
            if (settlement == null || owner == null || Clan.PlayerClan == null) return false;
            IFaction playerFaction = Clan.PlayerClan.MapFaction ?? Clan.PlayerClan;
            return ReferenceEquals(playerFaction, owner);
        }

        private static string BuildStrategicText()
        {
            StringBuilder text = new StringBuilder("LIVE SETTLEMENT OWNERS\n\n");
            int shown = 0;
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement == null || settlement.Town == null) continue;
                if (shown++ >= 26) break;
                IFaction owner = CalendarWorldLedgerBehavior.GetLiveSettlementFaction(settlement);
                text.Append(settlement.Name).Append(" — ").Append(owner == null ? "Unknown" : owner.Name.ToString()).AppendLine();
            }
            return text.ToString();
        }

        private void BuildMonthGrid()
        {
            _days.Clear();
            CampaignTime now = CampaignTime.Now;
            int year = CalendarTimeMath.GetYear(now); int month = CalendarTimeMath.GetMonth(now); bool leapYear = CalendarTimeMath.IsLeapYear(year);
            int monthLength = CalendarTimeMath.GetMonthLength(month, leapYear);
            long monthStart = CalendarTimeMath.DaysBeforeYear(year) + CalendarTimeMath.GetMonthStart(month, leapYear);
            int firstWeekday = (int)((monthStart % 7 + 7) % 7); long today = (long)Math.Floor(now.ToDays);
            MonthTitle = CalendarSettingsState.GetMonthName(month) + " " + year;
            for (int cell = 0; cell < 42; cell++)
            {
                int dayOfMonth = cell - firstWeekday + 1;
                CalendarWorldCalendarDayVM cellValue;
                if (dayOfMonth < 1 || dayOfMonth > monthLength)
                {
                    cellValue = CalendarWorldCalendarDayVM.Empty();
                }
                else
                {
                    long absoluteDay = monthStart + dayOfMonth - 1;
                    cellValue = new CalendarWorldCalendarDayVM(dayOfMonth.ToString(), CalendarWorldLedgerBehavior.GetDaySummary(absoluteDay, _selectedFilter), absoluteDay == today);
                }
                _days.Add(cellValue);
            }
        }
    }

    internal sealed class CalendarWorldCalendarDayVM : ViewModel
    {
        private readonly string _dayNumber; private readonly string _eventSummary; private string _backgroundColor;
        internal CalendarWorldCalendarDayVM(string dayNumber, string eventSummary, bool isToday) { _dayNumber = dayNumber; _eventSummary = eventSummary; _backgroundColor = isToday ? "#80652CFF" : "#22170FFF"; }
        internal static CalendarWorldCalendarDayVM Empty() { return new CalendarWorldCalendarDayVM(string.Empty, string.Empty, false) { _backgroundColor = "#00000000" }; }
        [DataSourceProperty] public string DayNumber { get { return _dayNumber; } }
        [DataSourceProperty] public string EventSummary { get { return _eventSummary; } }
        [DataSourceProperty] public string BackgroundColor { get { return _backgroundColor; } }
    }

    internal sealed class CalendarWorldStrategicMarkerVM : ViewModel
    {
        private readonly int _x;
        private readonly int _y;
        private readonly string _color;
        private readonly bool _isTown;
        private readonly int _size;
        private readonly int _iconSize;
        private readonly Settlement _settlement;
        private readonly Action<CalendarWorldStrategicMarkerVM> _select;
        private readonly bool _isUnderSiege;
        private bool _isSelected;

        internal CalendarWorldStrategicMarkerVM(
            int x,
            int y,
            string color,
            Settlement settlement,
            bool isTown,
            int size,
            int iconSize,
            bool isUnderSiege,
            bool isSelected,
            Action<CalendarWorldStrategicMarkerVM> select)
        {
            _x = x;
            _y = y;
            _color = color;
            _settlement = settlement;
            _isTown = isTown;
            _size = size;
            _iconSize = iconSize;
            _isUnderSiege = isUnderSiege;
            _isSelected = isSelected;
            _select = select;
        }
        internal Settlement Settlement { get { return _settlement; } }
        [DataSourceProperty] public int X { get { return _x; } }
        [DataSourceProperty] public int Y { get { return _y; } }
        [DataSourceProperty] public string Color { get { return _color; } }
        [DataSourceProperty] public string Label { get { return _settlement == null ? string.Empty : _settlement.Name.ToString(); } }
        [DataSourceProperty] public bool IsTown { get { return _isTown; } }
        [DataSourceProperty] public bool IsCastle { get { return !_isTown; } }
        [DataSourceProperty] public bool IsUnderSiege { get { return _isUnderSiege; } }
        [DataSourceProperty] public int Size { get { return _size; } }
        [DataSourceProperty] public int IconSize { get { return _iconSize; } }
        [DataSourceProperty] public string BorderColor { get { return _isSelected ? "#FFE3A3FF" : "#100D0BFF"; } }
        [DataSourceProperty] public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChangedWithValue(value, "IsSelected");
                OnPropertyChangedWithValue(BorderColor, "BorderColor");
            }
        }
        public void ExecuteSelect() { if (_select != null) _select(this); }
    }

    internal sealed class CalendarWorldStrategicProvinceVM : ViewModel
    {
        private readonly string _spriteName;
        private readonly int _x;
        private readonly int _y;
        private readonly int _width;
        private readonly int _height;
        private readonly string _color;

        internal CalendarWorldStrategicProvinceVM(string spriteName, int x, int y, int width, int height, string color)
        {
            _spriteName = spriteName ?? string.Empty;
            _x = x;
            _y = y;
            _width = width;
            _height = height;
            _color = color ?? "#FFFFFFFF";
        }

        [DataSourceProperty] public string SpriteName { get { return _spriteName; } }
        [DataSourceProperty] public int X { get { return _x; } }
        [DataSourceProperty] public int Y { get { return _y; } }
        [DataSourceProperty] public int Width { get { return _width; } }
        [DataSourceProperty] public int Height { get { return _height; } }
        [DataSourceProperty] public string Color { get { return _color; } }
    }

    internal sealed class StrategicSettlementPoint
    {
        internal StrategicSettlementPoint(Settlement settlement, float sourceX, float sourceY, IFaction owner, bool isUnderSiege)
        {
            Settlement = settlement;
            SourceX = sourceX;
            SourceY = sourceY;
            DisplayX = sourceX;
            DisplayY = sourceY;
            Owner = owner;
            IsUnderSiege = isUnderSiege;
        }

        internal Settlement Settlement { get; private set; }
        internal float SourceX { get; private set; }
        internal float SourceY { get; private set; }
        internal float DisplayX { get; private set; }
        internal float DisplayY { get; private set; }
        internal IFaction Owner { get; private set; }
        internal bool IsUnderSiege { get; private set; }

        internal void ResetDisplayPosition()
        {
            DisplayX = SourceX;
            DisplayY = SourceY;
        }

        internal void SetDisplayPosition(float x, float y)
        {
            DisplayX = x;
            DisplayY = y;
        }
    }

    internal sealed class CalendarWorldLedgerTabVM : ViewModel
    {
        private readonly Action<CalendarWorldLedgerTabVM> _select; private readonly string _baseLabel; private bool _isSelected; private string _label;
        internal CalendarWorldLedgerTabVM(string filter, string label, Action<CalendarWorldLedgerTabVM> select) { Filter = filter; _baseLabel = label; _label = label; _select = select; }
        internal string Filter { get; private set; }
        [DataSourceProperty] public string Label { get { return _label; } private set { if (_label == value) return; _label = value; OnPropertyChangedWithValue(value, "Label"); } }
        [DataSourceProperty] public bool IsSelected { get { return _isSelected; } set { if (_isSelected == value) return; _isSelected = value; Label = value ? "• " + _baseLabel : _baseLabel; OnPropertyChangedWithValue(value, "IsSelected"); } }
        public void ExecuteSelect() { if (_select != null) _select(this); }
    }
}
