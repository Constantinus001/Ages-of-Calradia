using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    internal sealed partial class CalendarWorldLedgerVM : ViewModel
    {
        private const float StrategicMapViewportWidth = 810f;
        private const float StrategicMapViewportHeight = 610f;
        private const float StrategicMapMinimumZoom = 0.5f;
        private const float StrategicMapDefaultZoom = 1f;
        private const float StrategicMapMaximumZoom = 5f;
        private const float StrategicMapZoomStep = 0.25f;
        private const int FirstCalendarMonth = 0;
        private const int LastCalendarMonth = 11;
        private const int CalendarFutureMonths = 12;
        // Markers are painted at a fixed size in the source texture. Keeping
        // their centres this far apart prevents town/castle silhouettes from
        // merging when the player zooms into dense settlement clusters.
        private const float StrategicMarkerEdgePadding = 20f;
        private string _title = "World Events";
        private string _monthTitle = string.Empty;
        private string _monthSummaryTitle = string.Empty;
        private string _monthSummaryText = string.Empty;
        private string _yearSummaryTitle = string.Empty;
        private string _yearSummaryText = string.Empty;
        private string _notesText = "No world events have been recorded yet.";
        private string _notesTitle = "NOTES";
        private string _strategicText = string.Empty;
        private bool _isStrategicMap;
        private bool _isCalendarVisible = true;
        private bool _isSummariesVisible;
        private bool _isPointerOverStrategicMap;
        private readonly MBBindingList<CalendarWorldLedgerTabVM> _tabs = new MBBindingList<CalendarWorldLedgerTabVM>();
        private readonly MBBindingList<CalendarWorldCalendarDayVM> _days = new MBBindingList<CalendarWorldCalendarDayVM>();
        private readonly MBBindingList<CalendarWorldCalendarMonthVM> _calendarMonths = new MBBindingList<CalendarWorldCalendarMonthVM>();
        private readonly MBBindingList<CalendarWorldSavedSummaryVM> _savedSummaries = new MBBindingList<CalendarWorldSavedSummaryVM>();
        private readonly MBBindingList<CalendarWorldStrategicProvinceVM> _strategicProvinces = new MBBindingList<CalendarWorldStrategicProvinceVM>();
        private readonly MBBindingList<CalendarWorldStrategicProvinceVM> _strategicContestedProvinces = new MBBindingList<CalendarWorldStrategicProvinceVM>();
        private readonly MBBindingList<CalendarWorldStrategicMarkerVM> _strategicMarkers = new MBBindingList<CalendarWorldStrategicMarkerVM>();
        private readonly MBBindingList<CalendarWorldStrategicVillageVM> _selectedStrategicVillages = new MBBindingList<CalendarWorldStrategicVillageVM>();
        private readonly MBBindingList<CalendarWorldStrategicVillageVM> _trackedStrategicSettlements = new MBBindingList<CalendarWorldStrategicVillageVM>();
        // The repeated ItemTemplate renderer intermittently loses an item's bound
        // sprite/color pair in Gauntlet. Fixed named layers are used by the map XML
        // instead; these arrays provide the matching data sources for those layers.
        private readonly CalendarWorldStrategicProvinceVM[] _fixedStrategicProvinces = new CalendarWorldStrategicProvinceVM[133];
        private readonly CalendarWorldStrategicProvinceVM[] _fixedStrategicContestedProvinces = new CalendarWorldStrategicProvinceVM[133];
        private readonly Action _close;
        private string _selectedFilter = "All";
        private string _selectedStrategicSettlementId = string.Empty;
        private long _selectedCalendarDay = long.MinValue;
        private int _displayCalendarYear = int.MinValue;
        private int _displayCalendarMonth = int.MinValue;
        private float _strategicMapZoom = StrategicMapDefaultZoom;
        // Reference-map positions recovered from our authored settlement-marker
        // artwork. These are the original 2000 x 2000 map coordinates, before
        // the displayed map's black-frame crop is applied. They are calibration
        // anchors only; all other settlement positions come from Bannerlord's
        // live Settlement.GetPosition2D values.
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
            AddTab("Calendar", "Calendar");
            AddTab("Summaries", "Saved Summaries");
            AddTab("Strategic", "Strategic Map");
            SelectTab(_tabs[0]);
        }

        [DataSourceProperty] public string Title { get { return _title; } }
        [DataSourceProperty] public string MonthTitle { get { return _monthTitle; } private set { if (_monthTitle == value) return; _monthTitle = value ?? string.Empty; OnPropertyChangedWithValue(_monthTitle, "MonthTitle"); } }
        [DataSourceProperty] public string MonthSummaryTitle { get { return _monthSummaryTitle; } private set { if (_monthSummaryTitle == value) return; _monthSummaryTitle = value ?? string.Empty; OnPropertyChangedWithValue(_monthSummaryTitle, "MonthSummaryTitle"); } }
        [DataSourceProperty] public string MonthSummaryText { get { return _monthSummaryText; } private set { if (_monthSummaryText == value) return; _monthSummaryText = value ?? string.Empty; OnPropertyChangedWithValue(_monthSummaryText, "MonthSummaryText"); } }
        [DataSourceProperty] public string YearSummaryTitle { get { return _yearSummaryTitle; } private set { if (_yearSummaryTitle == value) return; _yearSummaryTitle = value ?? string.Empty; OnPropertyChangedWithValue(_yearSummaryTitle, "YearSummaryTitle"); } }
        [DataSourceProperty] public string YearSummaryText { get { return _yearSummaryText; } private set { if (_yearSummaryText == value) return; _yearSummaryText = value ?? string.Empty; OnPropertyChangedWithValue(_yearSummaryText, "YearSummaryText"); } }
        [DataSourceProperty] public string NotesText { get { return _notesText; } private set { if (_notesText == value) return; _notesText = value ?? string.Empty; OnPropertyChangedWithValue(_notesText, "NotesText"); } }
        [DataSourceProperty] public string NotesTitle { get { return _notesTitle; } private set { if (_notesTitle == value) return; _notesTitle = value ?? string.Empty; OnPropertyChangedWithValue(_notesTitle, "NotesTitle"); } }
        [DataSourceProperty] public string StrategicText { get { return _strategicText; } private set { if (_strategicText == value) return; _strategicText = value ?? string.Empty; OnPropertyChangedWithValue(_strategicText, "StrategicText"); } }
        [DataSourceProperty] public bool IsStrategicMap { get { return _isStrategicMap; } private set { if (_isStrategicMap == value) return; _isStrategicMap = value; OnPropertyChangedWithValue(value, "IsStrategicMap"); } }
        [DataSourceProperty] public bool IsCalendarVisible { get { return _isCalendarVisible; } private set { if (_isCalendarVisible == value) return; _isCalendarVisible = value; OnPropertyChangedWithValue(value, "IsCalendarVisible"); } }
        [DataSourceProperty] public bool IsSummariesVisible { get { return _isSummariesVisible; } private set { if (_isSummariesVisible == value) return; _isSummariesVisible = value; OnPropertyChangedWithValue(value, "IsSummariesVisible"); } }
        [DataSourceProperty] public MBBindingList<CalendarWorldLedgerTabVM> Tabs { get { return _tabs; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldCalendarDayVM> Days { get { return _days; } }
        [DataSourceProperty] public bool CanPreviousCalendarMonth { get { return CanMoveDisplayedCalendarMonth(-1); } }
        [DataSourceProperty] public bool CanNextCalendarMonth { get { return CanMoveDisplayedCalendarMonth(1); } }
        [DataSourceProperty] public MBBindingList<CalendarWorldCalendarMonthVM> CalendarMonths { get { return _calendarMonths; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldSavedSummaryVM> SavedSummaries { get { return _savedSummaries; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicProvinceVM> StrategicProvinces { get { return _strategicProvinces; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicProvinceVM> StrategicContestedProvinces { get { return _strategicContestedProvinces; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicMarkerVM> StrategicMarkers { get { return _strategicMarkers; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicVillageVM> SelectedStrategicVillages { get { return _selectedStrategicVillages; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicVillageVM> TrackedStrategicSettlements { get { return _trackedStrategicSettlements; } }
        [DataSourceProperty] public bool HasTrackedStrategicSettlements { get { return _trackedStrategicSettlements.Count > 0; } }
        [DataSourceProperty] public bool ShowTrackedStrategicSettlements
        {
            get { return HasTrackedStrategicSettlements && !HasSelectedStrategicSettlement; }
        }
        [DataSourceProperty] public bool ShowStrategicMapLegend { get { return CalendarSettingsState.StrategicMapShowLegend; } }
        [DataSourceProperty] public int StrategicMapLegendWidth { get { return CalendarSettingsState.StrategicMapLegendWidth; } }
        [DataSourceProperty] public int StrategicMapLegendHeight { get { return CalendarSettingsState.StrategicMapLegendHeight; } }
        [DataSourceProperty] public int StrategicMapLegendMarginTop { get { return CalendarSettingsState.StrategicMapLegendMarginTop; } }
        [DataSourceProperty] public int StrategicMapLegendIconSize { get { return CalendarSettingsState.StrategicMapLegendIconSize; } }
        [DataSourceProperty] public int StrategicMapLegendFontSize { get { return CalendarSettingsState.StrategicMapLegendFontSize; } }
        [DataSourceProperty] public int StrategicMapLegendContentWidth { get { return Math.Max(160, CalendarSettingsState.StrategicMapLegendWidth - 12); } }
        private static float StrategicMapFitScale
        {
            get
            {
                // Fill the entire viewport instead of letterboxing the nearly
                // square map inside the wider panel. The small vertical excess
                // remains available through drag panning.
                return Math.Max(
                    StrategicMapViewportWidth / CalendarStrategicMapLayout.SourceWidth,
                    StrategicMapViewportHeight / CalendarStrategicMapLayout.SourceHeight);
            }
        }

        private float StrategicMapScale { get { return StrategicMapFitScale * _strategicMapZoom; } }
        [DataSourceProperty] public float MapCanvasWidth { get { return CalendarStrategicMapLayout.SourceWidth * StrategicMapScale; } }
        [DataSourceProperty] public float MapCanvasHeight { get { return CalendarStrategicMapLayout.SourceHeight * StrategicMapScale; } }
        [DataSourceProperty] public string MapZoomText { get { return _strategicMapZoom.ToString("0.00x"); } }
        [DataSourceProperty] public bool CanZoomIn { get { return _strategicMapZoom < StrategicMapMaximumZoom; } }
        [DataSourceProperty] public bool CanZoomOut { get { return _strategicMapZoom > StrategicMapMinimumZoom; } }
        [DataSourceProperty] public bool HasSelectedStrategicSettlement { get { return GetSelectedStrategicSettlement() != null; } }
        [DataSourceProperty] public bool IsSelectedStrategicSettlementTracked
        {
            get
            {
                Settlement selected = GetSelectedStrategicSettlement();
                return selected != null && Campaign.Current != null
                    && Campaign.Current.VisualTrackerManager != null
                    && Campaign.Current.VisualTrackerManager.CheckTracked(selected);
            }
        }
        [DataSourceProperty] public string TrackSelectedSettlementText
        {
            get { return IsSelectedStrategicSettlementTracked ? "Untrack Settlement" : "Track Settlement"; }
        }
        [DataSourceProperty] public int StrategicSummaryScrollerHeight
        {
            get { return HasSelectedStrategicSettlement ? 383 : 425; }
        }
        [DataSourceProperty] public int StrategicSummaryScrollerMarginBottom
        {
            get { return HasSelectedStrategicSettlement ? 102 : 60; }
        }

        public void ExecuteClose() { if (_close != null) _close(); }
        public void ExecuteRefresh() { RefreshCalendar(); }
        internal void RefreshWorldState() { RefreshCalendar(); }
        public void ExecuteZoomIn() { SetStrategicMapZoom(_strategicMapZoom + StrategicMapZoomStep); }
        public void ExecuteZoomOut() { SetStrategicMapZoom(_strategicMapZoom - StrategicMapZoomStep); }
        public void ExecuteResetMapView() { SetStrategicMapZoom(StrategicMapDefaultZoom); }
        public void ExecuteTrackSelectedSettlement()
        {
            Settlement selected = GetSelectedStrategicSettlement();
            if (selected == null || Campaign.Current == null || Campaign.Current.VisualTrackerManager == null) return;

            ToggleSettlementTracking(selected, "settlement");

            RefreshTrackedStrategicSettlements();
            NotifyStrategicSettlementSelectionChanged();
            StrategicText = BuildStrategicPanelText();
        }
        public void ExecuteShowKingdomSummary()
        {
            _selectedStrategicSettlementId = string.Empty;
            foreach (CalendarWorldStrategicMarkerVM marker in _strategicMarkers)
            {
                marker.IsSelected = false;
            }

            RefreshSelectedStrategicVillages();
            StrategicText = BuildStrategicPanelText();
            NotifyStrategicSettlementSelectionChanged();
            Diagnostics.Info("Strategic settlement details closed; Kingdom Summary restored.");
        }
        public void ExecuteStrategicMapHoverBegin() { _isPointerOverStrategicMap = true; }
        public void ExecuteStrategicMapHoverEnd() { _isPointerOverStrategicMap = false; }
        internal void AdjustStrategicMapZoomFromMouseWheel(float delta)
        {
            // The Kingdom Summary owns its own vertical scrolling. Only let
            // the wheel affect map zoom while the pointer is physically over
            // the map viewport, never merely because the Strategic tab is
            // selected.
            if (!IsStrategicMap || !_isPointerOverStrategicMap || Math.Abs(delta) < 0.001f) return;
            SetStrategicMapZoom(_strategicMapZoom + (delta > 0f ? StrategicMapZoomStep : -StrategicMapZoomStep));
        }
        private void AddTab(string filter, string label) { _tabs.Add(new CalendarWorldLedgerTabVM(filter, label, SelectTab)); }
        private void SelectTab(CalendarWorldLedgerTabVM tab)
        {
            if (tab == null) return;
            _selectedFilter = "All";
            IsStrategicMap = string.Equals(tab.Filter, "Strategic", StringComparison.Ordinal);
            IsSummariesVisible = string.Equals(tab.Filter, "Summaries", StringComparison.Ordinal);
            IsCalendarVisible = !IsStrategicMap && !IsSummariesVisible;
            if (!IsStrategicMap) _isPointerOverStrategicMap = false;
            foreach (CalendarWorldLedgerTabVM entry in _tabs) entry.IsSelected = ReferenceEquals(entry, tab);
            RefreshCalendar();
        }

        private void RefreshCalendar()
        {
            RefreshCalendarNotes();
            BuildStrategicMapLayers();
            RefreshSelectedStrategicVillages();
            RefreshTrackedStrategicSettlements();
            StrategicText = BuildStrategicPanelText();
            NotifyStrategicSettlementSelectionChanged();
            BuildCalendarHistory();
        }

        private void BuildStrategicMapLayers()
        {
            CalendarStrategicSettlementReference.CaptureNativeSnapshot();
            Dictionary<string, IFaction> ownersBySettlementId = new Dictionary<string, IFaction>(StringComparer.Ordinal);
            Dictionary<string, IFaction> besiegersBySettlementId = new Dictionary<string, IFaction>(StringComparer.Ordinal);
            List<StrategicSettlementPoint> markerPoints = new List<StrategicSettlementPoint>();
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement != null && settlement.Village != null)
                {
                    continue;
                }

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
                    besieger));
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
            float minimumSeparation = CalendarSettingsState.StrategicMapMarkerSpacing;
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
                        if (distance >= minimumSeparation) continue;

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

                        float requiredPush = minimumSeparation - distance + 4f;
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
                float minimumSeparation = CalendarSettingsState.StrategicMapMarkerSpacing;
                if (distanceSquared < minimumSeparation * minimumSeparation) return false;
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

        internal static Vec2 ProjectSettlementToReferenceMap(Settlement settlement)
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
            Vec2 projectedPosition = ProjectNativeSettlementPosition(campaignPosition);
            float sourceX = projectedPosition.x - CalendarStrategicMapLayout.CropLeft;
            float sourceY = projectedPosition.y - CalendarStrategicMapLayout.CropTop;
            sourceX = Math.Max(0f, Math.Min(CalendarStrategicMapLayout.SourceWidth, sourceX));
            sourceY = Math.Max(0f, Math.Min(CalendarStrategicMapLayout.SourceHeight, sourceY));
            return new Vec2(sourceX, sourceY);
        }

        private static Vec2 ProjectNativeSettlementPosition(Vec2 campaignPosition)
        {
            // Fit both output axes from the native campaign positions of our
            // authored town/castle anchors. This keeps village and third-party
            // settlement placement independent of any external map dataset.
            double[,] normal = new double[3, 3];
            double[] nativeX = new double[3];
            double[] nativeY = new double[3];
            int samples = 0;
            foreach (Settlement candidate in Settlement.All)
            {
                if (candidate == null || candidate.Village != null) continue;
                Vec2 anchor;
                if (!TryGetReferenceAnchor(candidate.StringId, out anchor)) continue;

                Vec2 position = candidate.GetPosition2D;
                double[] basis = { position.x, position.y, 1d };
                for (int row = 0; row < 3; row++)
                {
                    for (int column = 0; column < 3; column++)
                    {
                        normal[row, column] += basis[row] * basis[column];
                    }
                    nativeX[row] += basis[row] * anchor.x;
                    nativeY[row] += basis[row] * anchor.y;
                }
                samples++;
            }

            double[] xCoefficients;
            double[] yCoefficients;
            if (samples < 3
                || !TrySolveThreeByThree(normal, nativeX, out xCoefficients)
                || !TrySolveThreeByThree(normal, nativeY, out yCoefficients))
            {
                // Conservative fallback for very early campaign initialization
                // or a total-conversion map with too few matching anchors.
                return new Vec2(
                    (campaignPosition.x * 2.23f) + 20f,
                    (campaignPosition.y * -2.34f) + 1985f);
            }

            return new Vec2(
                (float)((campaignPosition.x * xCoefficients[0])
                    + (campaignPosition.y * xCoefficients[1])
                    + xCoefficients[2]),
                (float)((campaignPosition.x * yCoefficients[0])
                    + (campaignPosition.y * yCoefficients[1])
                    + yCoefficients[2]));
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
                    for (int column = pivot; column < 4; column++)
                    {
                        augmented[row, column] -= factor * augmented[pivot, column];
                    }
                }
            }

            result = new double[] { augmented[0, 3], augmented[1, 3], augmented[2, 3] };
            return true;
        }

        private void BuildStrategicMarkers(List<StrategicSettlementPoint> points)
        {
            _strategicMarkers.Clear();
            float scaleX = StrategicMapScale;
            float scaleY = StrategicMapScale;
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
            string previousSelection = _selectedStrategicSettlementId;
            Diagnostics.Info("Strategic settlement selection started: "
                + (marker.Settlement.StringId ?? "<no-id>") + " (" + marker.Settlement.Name + ").");
            try
            {
                _selectedStrategicSettlementId = marker.Settlement.StringId ?? string.Empty;
                foreach (CalendarWorldStrategicMarkerVM entry in _strategicMarkers)
                {
                    entry.IsSelected = ReferenceEquals(entry, marker);
                }
                RefreshSelectedStrategicVillages();
                Diagnostics.Info("Strategic settlement villages prepared: settlement="
                    + _selectedStrategicSettlementId + "; count=" + _selectedStrategicVillages.Count + ".");
                StrategicText = BuildStrategicPanelText();
                NotifyStrategicSettlementSelectionChanged();
                Diagnostics.Info("Strategic settlement selection completed: " + _selectedStrategicSettlementId + ".");
            }
            catch (Exception exception)
            {
                _selectedStrategicSettlementId = previousSelection;
                foreach (CalendarWorldStrategicMarkerVM entry in _strategicMarkers)
                {
                    entry.IsSelected = entry.Settlement != null
                        && string.Equals(entry.Settlement.StringId, previousSelection, StringComparison.Ordinal);
                }
                RefreshSelectedStrategicVillages();
                StrategicText = BuildStrategicPanelText();
                NotifyStrategicSettlementSelectionChanged();
                Diagnostics.Error("Strategic settlement selection failed and was rolled back.", exception);
            }
        }

        private void RefreshSelectedStrategicVillages()
        {
            _selectedStrategicVillages.Clear();
            Settlement selected = GetSelectedStrategicSettlement();
            if (selected == null || selected.BoundVillages == null) return;

            List<Village> villages = new List<Village>();
            foreach (Village village in selected.BoundVillages)
            {
                if (village != null && village.Settlement != null) villages.Add(village);
            }
            villages.Sort(delegate(Village left, Village right)
            {
                return string.Compare(
                    left.Settlement.Name.ToString(),
                    right.Settlement.Name.ToString(),
                    StringComparison.CurrentCultureIgnoreCase);
            });
            foreach (Village village in villages)
            {
                _selectedStrategicVillages.Add(new CalendarWorldStrategicVillageVM(
                    village.Settlement,
                    ToggleStrategicVillageTracking));
            }
        }

        private void ToggleStrategicVillageTracking(CalendarWorldStrategicVillageVM village)
        {
            if (village == null || village.Settlement == null) return;
            ToggleSettlementTracking(village.Settlement, "village");
            village.RefreshTracking();
            RefreshTrackedStrategicSettlements();
        }

        private void ToggleTrackedStrategicSettlement(CalendarWorldStrategicVillageVM item)
        {
            if (item == null || item.Settlement == null) return;
            ToggleSettlementTracking(item.Settlement, "settlement-list item");
            RefreshSelectedStrategicVillages();
            RefreshTrackedStrategicSettlements();
            NotifyStrategicSettlementSelectionChanged();
            StrategicText = BuildStrategicPanelText();
        }

        private void RefreshTrackedStrategicSettlements()
        {
            _trackedStrategicSettlements.Clear();
            if (Campaign.Current == null || Campaign.Current.VisualTrackerManager == null)
            {
                OnPropertyChangedWithValue(false, "HasTrackedStrategicSettlements");
                OnPropertyChangedWithValue(false, "ShowTrackedStrategicSettlements");
                return;
            }

            List<Settlement> tracked = new List<Settlement>();
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement == null || (settlement.Town == null && settlement.Village == null)) continue;
                if (Campaign.Current.VisualTrackerManager.CheckTracked(settlement)) tracked.Add(settlement);
            }
            tracked.Sort(delegate(Settlement left, Settlement right)
            {
                return string.Compare(
                    left.Name.ToString(),
                    right.Name.ToString(),
                    StringComparison.CurrentCultureIgnoreCase);
            });
            foreach (Settlement settlement in tracked)
            {
                _trackedStrategicSettlements.Add(new CalendarWorldStrategicVillageVM(
                    settlement,
                    ToggleTrackedStrategicSettlement));
            }
            OnPropertyChangedWithValue(HasTrackedStrategicSettlements, "HasTrackedStrategicSettlements");
            OnPropertyChangedWithValue(ShowTrackedStrategicSettlements, "ShowTrackedStrategicSettlements");
            Diagnostics.Info("Strategic tracked-settlement list refreshed: count="
                + _trackedStrategicSettlements.Count + ".");
        }

        private static void ToggleSettlementTracking(Settlement settlement, string kind)
        {
            if (settlement == null || Campaign.Current == null || Campaign.Current.VisualTrackerManager == null) return;
            if (Campaign.Current.VisualTrackerManager.CheckTracked(settlement))
            {
                Campaign.Current.VisualTrackerManager.RemoveTrackedObject(settlement, false);
                Diagnostics.Info("Strategic " + kind + " tracking: untracked " + settlement.StringId + " (" + settlement.Name + ").");
            }
            else
            {
                Campaign.Current.VisualTrackerManager.RegisterObject(settlement);
                Diagnostics.Info("Strategic " + kind + " tracking: tracked " + settlement.StringId + " (" + settlement.Name + ").");
            }
        }

        private Settlement GetSelectedStrategicSettlement()
        {
            if (string.IsNullOrEmpty(_selectedStrategicSettlementId)) return null;
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement != null
                    && string.Equals(settlement.StringId, _selectedStrategicSettlementId, StringComparison.Ordinal))
                {
                    return settlement;
                }
            }
            return null;
        }

        private void NotifyStrategicSettlementSelectionChanged()
        {
            OnPropertyChangedWithValue(HasSelectedStrategicSettlement, "HasSelectedStrategicSettlement");
            OnPropertyChangedWithValue(IsSelectedStrategicSettlementTracked, "IsSelectedStrategicSettlementTracked");
            OnPropertyChangedWithValue(TrackSelectedSettlementText, "TrackSelectedSettlementText");
            OnPropertyChangedWithValue(StrategicSummaryScrollerHeight, "StrategicSummaryScrollerHeight");
            OnPropertyChangedWithValue(StrategicSummaryScrollerMarginBottom, "StrategicSummaryScrollerMarginBottom");
            OnPropertyChangedWithValue(ShowTrackedStrategicSettlements, "ShowTrackedStrategicSettlements");
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

            float scaleX = StrategicMapScale;
            float scaleY = StrategicMapScale;
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
            RealisticCalendarStrategicMapAtlasTextureProvider.UpdateMapState(ownerColorsBySettlementId, markerPoints);
        }

        private void BuildStrategicContestedProvinces(Dictionary<string, IFaction> besiegersBySettlementId)
        {
            // Siege hatching is composed directly into the one reliable map
            // texture. Keep these legacy Gauntlet bindings transparent so a
            // second semi-transparent wash cannot muddy the striped result.
            _strategicContestedProvinces.Clear();

            float scaleX = StrategicMapScale;
            float scaleY = StrategicMapScale;
            for (int index = 0; index < CalendarStrategicMapLayout.Provinces.Length; index++)
            {
                CalendarStrategicProvinceDefinition province = CalendarStrategicMapLayout.Provinces[index];
                CalendarWorldStrategicProvinceVM layer = new CalendarWorldStrategicProvinceVM(
                    province.SpriteName,
                    (int)Math.Round((province.X - CalendarStrategicMapLayout.CropLeft) * scaleX),
                    (int)Math.Round((province.Y - CalendarStrategicMapLayout.CropTop) * scaleY),
                    Math.Max(1, (int)Math.Ceiling(province.Width * scaleX)),
                    Math.Max(1, (int)Math.Ceiling(province.Height * scaleY)),
                    "#00000000");
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

        private static uint NormalizeFactionColor(uint color)
        {
            return (color & 0xFF000000u) == 0 ? color | 0xFF000000u : color;
        }

        private string BuildStrategicPanelText()
        {
            if (string.IsNullOrEmpty(_selectedStrategicSettlementId))
            {
                return BuildKingdomStrengthText();
            }

            Settlement selected = GetSelectedStrategicSettlement();

            if (selected == null || selected.Town == null)
            {
                _selectedStrategicSettlementId = string.Empty;
                return BuildKingdomStrengthText();
            }

            IFaction owner = CalendarWorldLedgerBehavior.GetLiveSettlementFaction(selected);
            IFaction besieger = GetBesiegerFaction(selected);
            StringBuilder text = new StringBuilder();
            text.Append(selected.Name).AppendLine();
            text.Append(selected.IsTown ? "Town" : "Castle").AppendLine();
            text.Append("Kingdom: ").Append(owner == null ? "Unknown" : owner.Name.ToString()).AppendLine();
            Clan owningClan = selected.OwnerClan;
            text.Append("Owning clan: ").Append(owningClan == null ? "None" : owningClan.Name.ToString()).AppendLine();
            if (owningClan != null && owningClan.Leader != null)
            {
                text.Append("Clan leader: ").Append(owningClan.Leader.Name.ToString()).AppendLine();
            }
            if (owner != null) text.Append("Map colour: ").Append(ToProvinceColor(owner.Color)).AppendLine();
            if (besieger != null)
            {
                text.Append("Status: UNDER SIEGE").AppendLine();
                text.Append("Besieging faction: ").Append(besieger.Name.ToString()).AppendLine();
            }

            text.Append("Tracking: ").Append(IsSelectedStrategicSettlementTracked ? "Yes" : "No").AppendLine();
            text.Append("Bound villages: ").Append(selected.BoundVillages == null ? 0 : selected.BoundVillages.Count).AppendLine();

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

        /// <summary>
        /// A live strategic overview rather than the old saved settlement-owner
        /// list. Wealth is the current total gold held by the kingdom's clans;
        /// fielded troops are all mobile-party members currently on campaign
        /// for that kingdom, so neither value is a stale snapshot.
        /// </summary>
        private static string BuildKingdomStrengthText()
        {
            StringBuilder text = new StringBuilder("KINGDOM SUMMARY\n\n");
            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom == null) continue;

                long wealth = 0L;
                foreach (Clan clan in Clan.All)
                {
                    if (clan != null && ReferenceEquals(clan.Kingdom, kingdom)) wealth += clan.Gold;
                }

                int fieldedTroops = 0;
                foreach (MobileParty party in MobileParty.All)
                {
                    if (party == null || !ReferenceEquals(party.MapFaction, kingdom) || party.MemberRoster == null) continue;
                    fieldedTroops += party.MemberRoster.TotalManCount;
                }

                int clanCount = 0;
                foreach (Clan clan in Clan.All)
                {
                    if (clan != null && ReferenceEquals(clan.Kingdom, kingdom)) clanCount++;
                }

                int townCount = 0;
                int castleCount = 0;
                foreach (Settlement settlement in Settlement.All)
                {
                    if (settlement == null || settlement.Town == null) continue;
                    IFaction settlementOwner = CalendarWorldLedgerBehavior.GetLiveSettlementFaction(settlement);
                    if (!ReferenceEquals(settlementOwner, kingdom)) continue;
                    if (settlement.IsTown) townCount++;
                    else if (settlement.IsCastle) castleCount++;
                }

                text.Append(kingdom.Name).AppendLine();
                text.Append("  Leader: ").Append(kingdom.Leader == null ? "Unknown" : kingdom.Leader.Name.ToString())
                    .Append(" | Ruling clan: ").Append(kingdom.RulingClan == null ? "Unknown" : kingdom.RulingClan.Name.ToString()).AppendLine();
                text.Append("  Wealth: ").Append(wealth.ToString("N0"))
                    .Append(" | Fielded: ").Append(fieldedTroops.ToString("N0")).AppendLine();
                text.Append("  Clans: ").Append(clanCount)
                    .Append(" | Towns: ").Append(townCount)
                    .Append(" | Castles: ").Append(castleCount).AppendLine();
            }

            text.AppendLine();
            text.Append("Select a town or castle to see its kingdom and owning clan.");
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

        private void BuildCalendarHistory()
        {
            _days.Clear();
            _calendarMonths.Clear();
            CampaignTime now = CampaignTime.Now;
            int currentYear = CalendarTimeMath.GetYear(now);
            int currentMonth = CalendarTimeMath.GetMonth(now);
            long today = (long)Math.Floor(CalendarTimeMath.ToCalendarAbsoluteDays(now));
            Dictionary<long, List<QuestBase>> questDueByDay = GetActiveQuestDeadlines(today);
            long futureCalendarEnd = today + CalendarFutureMonths * 31L;
            foreach (long dueDay in questDueByDay.Keys)
            {
                futureCalendarEnd = Math.Max(futureCalendarEnd, dueDay);
            }
            int lastVisibleYear = CalendarTimeMath.GetYear(CalendarTimeMath.FromCalendarAbsoluteDays(futureCalendarEnd));
            int lastVisibleMonth = CalendarTimeMath.GetMonth(CalendarTimeMath.FromCalendarAbsoluteDays(futureCalendarEnd));
            long firstRecordedDay = CalendarWorldLedgerBehavior.GetFirstRecordedDay(today);
            int firstYear = CalendarTimeMath.GetYear(CalendarTimeMath.FromCalendarAbsoluteDays(firstRecordedDay));
            int firstMonth = CalendarTimeMath.GetMonth(CalendarTimeMath.FromCalendarAbsoluteDays(firstRecordedDay));
            if (_displayCalendarYear == int.MinValue || _displayCalendarMonth == int.MinValue)
            {
                _displayCalendarYear = currentYear;
                _displayCalendarMonth = currentMonth;
            }
            ClampDisplayedCalendarMonth(firstRecordedDay, futureCalendarEnd);
            MonthTitle = "Campaign Calendar";

            for (int year = firstYear; year <= lastVisibleYear; year++)
            {
                int startMonth = year == firstYear ? firstMonth : FirstCalendarMonth;
                int endMonth = year == lastVisibleYear ? lastVisibleMonth : LastCalendarMonth;
                for (int month = startMonth; month <= endMonth; month++)
                {
                    bool leapYear = CalendarTimeMath.IsLeapYear(year);
                    int monthLength = CalendarTimeMath.GetMonthLength(month, leapYear);
                    long monthStart = CalendarTimeMath.DaysBeforeYear(year) + CalendarTimeMath.GetMonthStart(month, leapYear);
                    MBBindingList<CalendarWorldCalendarDayVM> monthDays = new MBBindingList<CalendarWorldCalendarDayVM>();
                    int firstWeekday = (int)((monthStart % 7 + 7) % 7);
                    for (int cell = 0; cell < 42; cell++)
                    {
                        int dayOfMonth = cell - firstWeekday + 1;
                        if (dayOfMonth < 1 || dayOfMonth > monthLength)
                        {
                            monthDays.Add(CalendarWorldCalendarDayVM.Empty());
                            continue;
                        }

                        long absoluteDay = monthStart + dayOfMonth - 1;
                        // History starts at the first recorded month; inside
                        // each visible month, show the complete native date
                        // grid so day numbers never disappear.
                        List<QuestBase> dueQuests;
                        questDueByDay.TryGetValue(absoluteDay, out dueQuests);
                        monthDays.Add(new CalendarWorldCalendarDayVM(
                            dayOfMonth.ToString(),
                            BuildCalendarDaySummary(absoluteDay, dueQuests),
                            absoluteDay,
                            absoluteDay == today,
                            true,
                            absoluteDay == _selectedCalendarDay,
                            dueQuests != null && dueQuests.Count > 0,
                            SelectCalendarDay));
                    }

                    int eventCount = CalendarWorldLedgerBehavior.CountRecordedEntries(monthStart, monthStart + monthLength);
                    int questCount = CountQuestDeadlines(questDueByDay, monthStart, monthStart + monthLength);
                    bool isCurrentMonth = year == currentYear && month == currentMonth;
                    CalendarWorldCalendarMonthVM monthViewModel = new CalendarWorldCalendarMonthVM(
                        CalendarSettingsState.GetMonthName(month) + " " + year,
                        BuildMonthEventCountText(eventCount, questCount),
                        monthDays,
                        monthStart,
                        monthStart + monthLength,
                        isCurrentMonth,
                        SelectCalendarMonth);
                    _calendarMonths.Add(monthViewModel);
                    if (isCurrentMonth) SetMonthSummary(monthViewModel);
                }
            }

            BuildSavedSummaries(firstYear, firstMonth, currentYear, currentMonth);

            BuildMonthGrid();
        }

        public void ExecutePreviousCalendarMonth() { MoveDisplayedCalendarMonth(-1); }
        public void ExecuteNextCalendarMonth() { MoveDisplayedCalendarMonth(1); }

        private void MoveDisplayedCalendarMonth(int direction)
        {
            if (!CanMoveDisplayedCalendarMonth(direction)) return;
            _displayCalendarMonth += direction;
            if (_displayCalendarMonth < FirstCalendarMonth)
            {
                _displayCalendarMonth = LastCalendarMonth;
                _displayCalendarYear--;
            }
            else if (_displayCalendarMonth > LastCalendarMonth)
            {
                _displayCalendarMonth = FirstCalendarMonth;
                _displayCalendarYear++;
            }
            BuildMonthGrid();
            OnPropertyChangedWithValue(CanPreviousCalendarMonth, "CanPreviousCalendarMonth");
            OnPropertyChangedWithValue(CanNextCalendarMonth, "CanNextCalendarMonth");
        }

        private bool CanMoveDisplayedCalendarMonth(int direction)
        {
            if (direction == 0 || Campaign.Current == null) return false;
            int year = _displayCalendarYear;
            int month = _displayCalendarMonth + direction;
            if (month < FirstCalendarMonth) { month = LastCalendarMonth; year--; }
            if (month > LastCalendarMonth) { month = FirstCalendarMonth; year++; }
            bool leapYear = CalendarTimeMath.IsLeapYear(year);
            long candidateStart = CalendarTimeMath.DaysBeforeYear(year) + CalendarTimeMath.GetMonthStart(month, leapYear);
            long today = (long)Math.Floor(CalendarTimeMath.ToCalendarAbsoluteDays(CampaignTime.Now));
            long firstRecordedDay = CalendarWorldLedgerBehavior.GetFirstRecordedDay(today);
            int firstCalendarYear = CalendarTimeMath.GetYear(CalendarTimeMath.FromCalendarAbsoluteDays(firstRecordedDay));
            int firstCalendarMonth = CalendarTimeMath.GetMonth(CalendarTimeMath.FromCalendarAbsoluteDays(firstRecordedDay));
            bool firstCalendarLeapYear = CalendarTimeMath.IsLeapYear(firstCalendarYear);
            long firstVisibleMonthStart = CalendarTimeMath.DaysBeforeYear(firstCalendarYear)
                + CalendarTimeMath.GetMonthStart(firstCalendarMonth, firstCalendarLeapYear);
            long lastVisibleDay = GetCalendarFutureEndDay(today);
            return candidateStart >= firstVisibleMonthStart && candidateStart <= lastVisibleDay;
        }

        private void ClampDisplayedCalendarMonth(long firstRecordedDay, long lastVisibleDay)
        {
            int firstCalendarYear = CalendarTimeMath.GetYear(CalendarTimeMath.FromCalendarAbsoluteDays(firstRecordedDay));
            int firstCalendarMonth = CalendarTimeMath.GetMonth(CalendarTimeMath.FromCalendarAbsoluteDays(firstRecordedDay));
            bool firstCalendarLeapYear = CalendarTimeMath.IsLeapYear(firstCalendarYear);
            long firstVisibleMonthStart = CalendarTimeMath.DaysBeforeYear(firstCalendarYear)
                + CalendarTimeMath.GetMonthStart(firstCalendarMonth, firstCalendarLeapYear);
            bool leapYear = CalendarTimeMath.IsLeapYear(_displayCalendarYear);
            long displayedStart = CalendarTimeMath.DaysBeforeYear(_displayCalendarYear)
                + CalendarTimeMath.GetMonthStart(_displayCalendarMonth, leapYear);
            if (displayedStart < firstVisibleMonthStart)
            {
                _displayCalendarYear = firstCalendarYear;
                _displayCalendarMonth = firstCalendarMonth;
            }
            else if (displayedStart > lastVisibleDay)
            {
                _displayCalendarYear = CalendarTimeMath.GetYear(CalendarTimeMath.FromCalendarAbsoluteDays(lastVisibleDay));
                _displayCalendarMonth = CalendarTimeMath.GetMonth(CalendarTimeMath.FromCalendarAbsoluteDays(lastVisibleDay));
            }
        }

        private static long GetCalendarFutureEndDay(long today)
        {
            long result = today + CalendarFutureMonths * 31L;
            Dictionary<long, List<QuestBase>> deadlines = GetActiveQuestDeadlines(today);
            foreach (long dueDay in deadlines.Keys) result = Math.Max(result, dueDay);
            return result;
        }

        private void SelectCalendarMonth(CalendarWorldCalendarMonthVM month)
        {
            if (month == null) return;
            SetMonthSummary(month);
        }

        private void SelectCalendarDay(CalendarWorldCalendarDayVM day)
        {
            if (day == null || !day.IsSelectable) return;
            _selectedCalendarDay = day.AbsoluteDay;
            foreach (CalendarWorldCalendarMonthVM month in _calendarMonths)
            {
                foreach (CalendarWorldCalendarDayVM entry in month.Days)
                {
                    entry.IsSelected = entry.IsSelectable && entry.AbsoluteDay == _selectedCalendarDay;
                }
            }
            RefreshCalendarNotes();
            Diagnostics.Info("Calendar history day selected: absoluteDay=" + _selectedCalendarDay + ".");
        }

        private void RefreshCalendarNotes()
        {
            long today = Campaign.Current == null
                ? long.MinValue
                : (long)Math.Floor(CalendarTimeMath.ToCalendarAbsoluteDays(CampaignTime.Now));
            if (_selectedCalendarDay != long.MinValue)
            {
                NotesTitle = CalendarFormatter.Format(
                    CalendarTimeMath.FromCalendarAbsoluteDays(_selectedCalendarDay)).ToUpperInvariant();
                string eventText = _selectedCalendarDay <= today
                    ? CalendarWorldLedgerBehavior.GetImportantEventsText(
                    _selectedCalendarDay,
                    _selectedCalendarDay + 1,
                    int.MaxValue,
                    true)
                    : "No saved world events yet.";
                string questText = GetQuestDeadlineText(_selectedCalendarDay);
                NotesText = string.IsNullOrEmpty(questText)
                    ? eventText
                    : "QUEST DEADLINES\n" + questText + "\n\n" + eventText;
                return;
            }

            _selectedCalendarDay = long.MinValue;
            NotesTitle = "NOTES";
            NotesText = CalendarWorldLedgerBehavior.GetRecentEntriesText(_selectedFilter);
        }

        private void SetMonthSummary(CalendarWorldCalendarMonthVM month)
        {
            SetMonthSummary(month.Title, month.StartDay, month.EndDay);
        }

        private void SetMonthSummary(string monthTitle, long monthStart, long monthEnd)
        {
            int monthEvents = CalendarWorldLedgerBehavior.CountRecordedEntries(monthStart, monthEnd);
            int questCount = CountQuestDeadlines(GetActiveQuestDeadlines((long)Math.Floor(
                CalendarTimeMath.ToCalendarAbsoluteDays(CampaignTime.Now))), monthStart, monthEnd);
            int monthCapacity = Math.Max(1, (int)(monthEnd - monthStart));
            int selectedMonthEvents = Math.Min(monthCapacity, monthEvents);
            MonthSummaryTitle = monthTitle + " MONTHLY SUMMARY (" + selectedMonthEvents + "/" + monthCapacity + "; " + questCount + " quest deadlines)";
            string eventText = CalendarWorldLedgerBehavior.GetImportantEventsText(monthStart, monthEnd, monthCapacity, true);
            string questText = GetQuestDeadlineText(monthStart, monthEnd);
            MonthSummaryText = string.IsNullOrEmpty(questText)
                ? eventText
                : "QUEST DEADLINES\n" + questText + "\n\n" + eventText;

            int year = CalendarTimeMath.GetYear(CalendarTimeMath.FromCalendarAbsoluteDays(monthStart));
            int importantEventCount;
            int monthsWithEvents;
            YearSummaryText = BuildYearImportantSummary(
                year,
                FirstCalendarMonth,
                LastCalendarMonth,
                out importantEventCount,
                out monthsWithEvents);
            YearSummaryTitle = year + " YEARLY SUMMARY (" + importantEventCount + "/120)";
        }

        private void BuildSavedSummaries(int firstYear, int firstMonth, int currentYear, int currentMonth)
        {
            _savedSummaries.Clear();
            for (int year = currentYear; year >= firstYear; year--)
            {
                int startMonth = year == firstYear ? firstMonth : FirstCalendarMonth;
                int endMonth = year == currentYear ? currentMonth : LastCalendarMonth;
                int yearImportantCount;
                int monthsWithEvents;
                string yearText = BuildYearImportantSummary(year, startMonth, endMonth, out yearImportantCount, out monthsWithEvents);
                int yearBodyHeight = Math.Max(180, 70 + (yearImportantCount * 20) + (monthsWithEvents * 28));
                _savedSummaries.Add(new CalendarWorldSavedSummaryVM(
                    year + " YEARLY SUMMARY — " + yearImportantCount + "/120 important events",
                    yearText,
                    yearBodyHeight,
                    false));

                for (int month = endMonth; month >= startMonth; month--)
                {
                    bool leapYear = CalendarTimeMath.IsLeapYear(year);
                    int monthLength = CalendarTimeMath.GetMonthLength(month, leapYear);
                    long monthStart = CalendarTimeMath.DaysBeforeYear(year) + CalendarTimeMath.GetMonthStart(month, leapYear);
                    int eventCount = CalendarWorldLedgerBehavior.CountRecordedEntries(monthStart, monthStart + monthLength);
                    string monthText = CalendarWorldLedgerBehavior.GetImportantEventsText(monthStart, monthStart + monthLength, monthLength, true);
                    int monthBodyHeight = Math.Max(150, 70 + (Math.Min(monthLength, eventCount) * 22));
                    _savedSummaries.Add(new CalendarWorldSavedSummaryVM(
                        CalendarSettingsState.GetMonthName(month) + " " + year + " MONTHLY SUMMARY — " + Math.Min(monthLength, eventCount) + "/" + monthLength + " important events",
                        monthText,
                        monthBodyHeight,
                        false));
                }
            }
        }

        private static string BuildYearImportantSummary(int year, int startMonth, int endMonth, out int importantEventCount, out int monthsWithEvents)
        {
            StringBuilder text = new StringBuilder();
            importantEventCount = 0;
            monthsWithEvents = 0;

            // CalendarTimeMath and CalendarSettingsState use zero-based month
            // indices. Clamp summary requests at this boundary so corrupt or
            // legacy UI state can never prevent the World Events screen from
            // opening by asking for month 12.
            startMonth = Math.Max(FirstCalendarMonth, Math.Min(LastCalendarMonth, startMonth));
            endMonth = Math.Max(FirstCalendarMonth, Math.Min(LastCalendarMonth, endMonth));
            if (startMonth > endMonth)
            {
                return "No events were recorded for this year.";
            }

            for (int month = startMonth; month <= endMonth; month++)
            {
                bool leapYear = CalendarTimeMath.IsLeapYear(year);
                int monthLength = CalendarTimeMath.GetMonthLength(month, leapYear);
                long monthStart = CalendarTimeMath.DaysBeforeYear(year) + CalendarTimeMath.GetMonthStart(month, leapYear);
                int monthEventCount = CalendarWorldLedgerBehavior.CountRecordedEntries(monthStart, monthStart + monthLength);
                if (monthEventCount <= 0) continue;

                monthsWithEvents++;
                int selectedCount = Math.Min(10, monthEventCount);
                importantEventCount += selectedCount;
                if (text.Length > 0) text.AppendLine().AppendLine();
                text.Append(CalendarSettingsState.GetMonthName(month).ToUpperInvariant()).Append(" — ").Append(selectedCount).Append(" important events").AppendLine();
                text.Append(CalendarWorldLedgerBehavior.GetImportantEventsText(monthStart, monthStart + monthLength, 10, false));
            }
            return text.Length == 0 ? "No events were recorded for this year." : text.ToString();
        }

        private void BuildMonthGrid()
        {
            _days.Clear();
            CampaignTime now = CampaignTime.Now;
            int year = _displayCalendarYear;
            int month = _displayCalendarMonth;
            bool leapYear = CalendarTimeMath.IsLeapYear(year);
            int monthLength = CalendarTimeMath.GetMonthLength(month, leapYear);
            long monthStart = CalendarTimeMath.DaysBeforeYear(year) + CalendarTimeMath.GetMonthStart(month, leapYear);
            int firstWeekday = (int)((monthStart % 7 + 7) % 7);
            long today = (long)Math.Floor(CalendarTimeMath.ToCalendarAbsoluteDays(now));
            Dictionary<long, List<QuestBase>> questDueByDay = GetActiveQuestDeadlines(today);
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
                    List<QuestBase> dueQuests;
                    questDueByDay.TryGetValue(absoluteDay, out dueQuests);
                    cellValue = new CalendarWorldCalendarDayVM(
                        dayOfMonth.ToString(),
                        BuildCalendarDaySummary(absoluteDay, dueQuests),
                        absoluteDay,
                        absoluteDay == today,
                        true,
                        absoluteDay == _selectedCalendarDay,
                        dueQuests != null && dueQuests.Count > 0,
                        SelectCalendarDay);
                }
                _days.Add(cellValue);
            }
            SetMonthSummary(MonthTitle, monthStart, monthStart + monthLength);
        }

        private static Dictionary<long, List<QuestBase>> GetActiveQuestDeadlines(long today)
        {
            Dictionary<long, List<QuestBase>> result = new Dictionary<long, List<QuestBase>>();
            if (Campaign.Current == null || Campaign.Current.QuestManager == null) return result;

            foreach (QuestBase quest in Campaign.Current.QuestManager.Quests)
            {
                if (quest == null || !quest.IsOngoing || quest.IsRemainingTimeHidden) continue;
                double dueDays = CalendarTimeMath.ToCalendarAbsoluteDays(quest.QuestDueTime);
                if (double.IsNaN(dueDays) || double.IsInfinity(dueDays)) continue;
                long dueDay = (long)Math.Floor(dueDays);
                // A non-expiring quest is represented by an out-of-range date.
                if (dueDay < today - 1L || dueDay > today + 3650L) continue;

                List<QuestBase> dueQuests;
                if (!result.TryGetValue(dueDay, out dueQuests))
                {
                    dueQuests = new List<QuestBase>();
                    result.Add(dueDay, dueQuests);
                }
                dueQuests.Add(quest);
            }
            return result;
        }

        private static int CountQuestDeadlines(Dictionary<long, List<QuestBase>> dueByDay, long startDay, long endDay)
        {
            int count = 0;
            foreach (KeyValuePair<long, List<QuestBase>> entry in dueByDay)
            {
                if (entry.Key >= startDay && entry.Key < endDay) count += entry.Value.Count;
            }
            return count;
        }

        private static string BuildMonthEventCountText(int eventCount, int questCount)
        {
            string events = eventCount == 1 ? "1 saved event" : eventCount + " saved events";
            return questCount == 0
                ? events
                : events + "; " + (questCount == 1 ? "1 quest due" : questCount + " quests due");
        }

        private static string BuildCalendarDaySummary(long absoluteDay, List<QuestBase> dueQuests)
        {
            if (dueQuests != null && dueQuests.Count > 0)
            {
                return dueQuests.Count == 1 ? "QUEST DUE" : dueQuests.Count + " QUESTS DUE";
            }
            return CalendarWorldLedgerBehavior.GetDaySummary(absoluteDay, "All");
        }

        private static string GetQuestDeadlineText(long day)
        {
            return GetQuestDeadlineText(day, day + 1L);
        }

        private static string GetQuestDeadlineText(long startDay, long endDay)
        {
            Dictionary<long, List<QuestBase>> dueByDay = GetActiveQuestDeadlines((long)Math.Floor(
                CalendarTimeMath.ToCalendarAbsoluteDays(CampaignTime.Now)));
            StringBuilder text = new StringBuilder();
            foreach (KeyValuePair<long, List<QuestBase>> entry in dueByDay)
            {
                if (entry.Key < startDay || entry.Key >= endDay) continue;
                foreach (QuestBase quest in entry.Value)
                {
                    if (text.Length > 0) text.AppendLine();
                    text.Append(CalendarFormatter.Format(
                        CalendarTimeMath.FromCalendarAbsoluteDays(entry.Key))).Append(" — ");
                    text.Append(quest.Title == null ? "Unnamed quest" : quest.Title.ToString());
                }
            }
            return text.ToString();
        }
    }

    internal sealed class CalendarWorldCalendarDayVM : ViewModel
    {
        private readonly string _dayNumber;
        private readonly string _eventSummary;
        private readonly long _absoluteDay;
        private readonly bool _isToday;
        private readonly bool _isSelectable;
        private readonly bool _hasQuestDue;
        private readonly Action<CalendarWorldCalendarDayVM> _select;
        private bool _isSelected;

        internal CalendarWorldCalendarDayVM(
            string dayNumber,
            string eventSummary,
            long absoluteDay,
            bool isToday,
            bool isSelectable,
            bool isSelected,
            bool hasQuestDue,
            Action<CalendarWorldCalendarDayVM> select)
        {
            _dayNumber = dayNumber;
            _eventSummary = eventSummary;
            _absoluteDay = absoluteDay;
            _isToday = isToday;
            _isSelectable = isSelectable;
            _isSelected = isSelected;
            _hasQuestDue = hasQuestDue;
            _select = select;
        }
        internal static CalendarWorldCalendarDayVM Empty()
        {
            return new CalendarWorldCalendarDayVM(
                string.Empty,
                string.Empty,
                long.MinValue,
                false,
                false,
                false,
                false,
                null);
        }
        internal long AbsoluteDay { get { return _absoluteDay; } }
        [DataSourceProperty] public string DayNumber { get { return _dayNumber; } }
        [DataSourceProperty] public string EventSummary { get { return _eventSummary; } }
        [DataSourceProperty] public bool IsSelectable { get { return _isSelectable; } }
        [DataSourceProperty] public bool HasQuestDue { get { return _hasQuestDue; } }
        [DataSourceProperty] public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChangedWithValue(value, "IsSelected");
                OnPropertyChangedWithValue(BackgroundColor, "BackgroundColor");
            }
        }
        [DataSourceProperty] public string BackgroundColor
        {
            get
            {
                if (!_isSelectable) return "#00000000";
                if (_isSelected) return "#B58A3FFF";
                if (_hasQuestDue) return "#6A342CFF";
                return _isToday ? "#80652CFF" : "#22170FFF";
            }
        }
        public void ExecuteSelect() { if (_isSelectable && _select != null) _select(this); }
    }

    internal sealed class CalendarWorldCalendarMonthVM : ViewModel
    {
        private readonly string _title;
        private readonly string _eventCountText;
        private readonly MBBindingList<CalendarWorldCalendarDayVM> _days;
        private readonly long _startDay;
        private readonly long _endDay;
        private readonly Action<CalendarWorldCalendarMonthVM> _select;
        private bool _isExpanded;

        internal CalendarWorldCalendarMonthVM(string title, string eventCountText, MBBindingList<CalendarWorldCalendarDayVM> days, long startDay, long endDay, bool isExpanded, Action<CalendarWorldCalendarMonthVM> select)
        {
            _title = title;
            _eventCountText = eventCountText;
            _days = days;
            _startDay = startDay;
            _endDay = endDay;
            _isExpanded = isExpanded;
            _select = select;
        }

        [DataSourceProperty] public string Title { get { return _title; } }
        [DataSourceProperty] public string EventCountText { get { return _eventCountText; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldCalendarDayVM> Days { get { return _days; } }
        [DataSourceProperty] public bool IsExpanded { get { return _isExpanded; } private set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChangedWithValue(value, "IsExpanded"); OnPropertyChangedWithValue(_isExpanded ? "-" : "+", "ExpandGlyph"); } }
        [DataSourceProperty] public string ExpandGlyph { get { return _isExpanded ? "-" : "+"; } }
        internal long StartDay { get { return _startDay; } }
        internal long EndDay { get { return _endDay; } }
        public void ExecuteToggle() { IsExpanded = !IsExpanded; if (_select != null) _select(this); }
    }

    internal sealed class CalendarWorldSavedSummaryVM : ViewModel
    {
        private readonly string _title;
        private readonly string _summaryText;
        private readonly int _bodyHeight;
        private bool _isExpanded;

        internal CalendarWorldSavedSummaryVM(string title, string summaryText, int bodyHeight, bool isExpanded)
        {
            _title = title ?? string.Empty;
            _summaryText = summaryText ?? string.Empty;
            _bodyHeight = bodyHeight;
            _isExpanded = isExpanded;
        }

        [DataSourceProperty] public string Title { get { return _title; } }
        [DataSourceProperty] public string SummaryText { get { return _summaryText; } }
        [DataSourceProperty] public int BodyHeight { get { return _bodyHeight; } }
        [DataSourceProperty] public bool IsExpanded { get { return _isExpanded; } private set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChangedWithValue(value, "IsExpanded"); OnPropertyChangedWithValue(_isExpanded ? "-" : "+", "ExpandGlyph"); } }
        [DataSourceProperty] public string ExpandGlyph { get { return _isExpanded ? "-" : "+"; } }
        public void ExecuteToggle() { IsExpanded = !IsExpanded; }
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

    internal sealed class CalendarWorldStrategicVillageVM : ViewModel
    {
        private readonly Settlement _settlement;
        private readonly Action<CalendarWorldStrategicVillageVM> _toggleTracking;

        internal CalendarWorldStrategicVillageVM(
            Settlement settlement,
            Action<CalendarWorldStrategicVillageVM> toggleTracking)
        {
            _settlement = settlement;
            _toggleTracking = toggleTracking;
        }

        internal Settlement Settlement { get { return _settlement; } }
        [DataSourceProperty] public string Name
        {
            get { return _settlement == null ? string.Empty : _settlement.Name.ToString(); }
        }
        [DataSourceProperty] public bool IsTracked
        {
            get
            {
                return _settlement != null
                    && Campaign.Current != null
                    && Campaign.Current.VisualTrackerManager != null
                    && Campaign.Current.VisualTrackerManager.CheckTracked(_settlement);
            }
        }
        [DataSourceProperty] public string TrackText
        {
            get { return IsTracked ? "Untrack" : "Track"; }
        }

        public void ExecuteToggleTrack()
        {
            if (_toggleTracking != null) _toggleTracking(this);
        }

        internal void RefreshTracking()
        {
            OnPropertyChangedWithValue(IsTracked, "IsTracked");
            OnPropertyChangedWithValue(TrackText, "TrackText");
        }
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
        internal StrategicSettlementPoint(Settlement settlement, float sourceX, float sourceY, IFaction owner, IFaction besieger)
        {
            Settlement = settlement;
            SourceX = sourceX;
            SourceY = sourceY;
            DisplayX = sourceX;
            DisplayY = sourceY;
            Owner = owner;
            Besieger = besieger;
        }

        internal Settlement Settlement { get; private set; }
        internal float SourceX { get; private set; }
        internal float SourceY { get; private set; }
        internal float DisplayX { get; private set; }
        internal float DisplayY { get; private set; }
        internal IFaction Owner { get; private set; }
        internal IFaction Besieger { get; private set; }
        internal bool IsUnderSiege { get { return Besieger != null; } }

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

    internal sealed class StrategicVillagePoint
    {
        internal StrategicVillagePoint(string settlementId, float sourceX, float sourceY)
        {
            SettlementId = settlementId ?? string.Empty;
            SourceX = sourceX;
            SourceY = sourceY;
        }

        internal string SettlementId { get; private set; }
        internal float SourceX { get; private set; }
        internal float SourceY { get; private set; }
    }

    internal sealed class CalendarWorldLedgerTabVM : ViewModel
    {
        private readonly Action<CalendarWorldLedgerTabVM> _select; private readonly string _baseLabel; private bool _isSelected; private string _label;
        internal CalendarWorldLedgerTabVM(string filter, string label, Action<CalendarWorldLedgerTabVM> select) { Filter = filter; _baseLabel = label; _label = label; _select = select; }
        internal string Filter { get; private set; }
        [DataSourceProperty] public string Label { get { return _label; } private set { if (_label == value) return; _label = value; OnPropertyChangedWithValue(value, "Label"); } }
        [DataSourceProperty] public bool IsSelected { get { return _isSelected; } set { if (_isSelected == value) return; _isSelected = value; Label = _baseLabel; OnPropertyChangedWithValue(value, "IsSelected"); } }
        public void ExecuteSelect() { if (_select != null) _select(this); }
    }
}
