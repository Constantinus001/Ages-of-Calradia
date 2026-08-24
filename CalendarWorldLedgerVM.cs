using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    internal sealed partial class CalendarWorldLedgerVM : ViewModel
    {
        // Match the authored strategic cabinet's inner map aperture.  Using the
        // full page dimensions here makes the live atlas paint over its frame.
        // Keep the runtime canvas on the same proportional coordinate system as
        // the staged 1220-by-871 World Events cabinet.  The XML viewport is
        // 741-by-427, so matching these values prevents base-zoom cropping.
        private const float StrategicMapViewportWidth = 741f;
        private const float StrategicMapViewportHeight = 427f;
        // 1.00x is the fitted 900px pane width. Allowing values below this
        // exposes an empty strip between the atlas and the Legend border.
        private const float StrategicMapMinimumZoom = 1f;
        private const float StrategicMapDefaultZoom = 1f;
        private const float StrategicMapMaximumZoom = 5f;
        private const float StrategicMapZoomStep = 0.25f;
        private const int StrategicPartyLegendMinimumHeight = 198;
        private const int StrategicPartyLegendBaseHeight = 108;
        private const int FirstCalendarMonth = 0;
        private const int LastCalendarMonth = 11;
        private const int CalendarFutureMonths = 12;
        // Markers are painted at a fixed size in the source texture. Keeping
        // their centres this far apart prevents town/castle silhouettes from
        // merging when the player zooms into dense settlement clusters.
        private const float StrategicMarkerMinimumSeparation = 52f;
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
        private string _characterStoryTitle = string.Empty;
        private string _characterStorySubtitle = string.Empty;
        private string _characterStoryText = string.Empty;
        private string _characterMilestoneText = string.Empty;
        private string _characterKillsText = "0";
        private string _characterKnockoutsText = "0";
        private string _characterRecordNote = string.Empty;
        private CharacterImageIdentifierVM _characterPortrait = new CharacterImageIdentifierVM(null);
        private BannerImageIdentifierVM _characterClanBanner = new BannerImageIdentifierVM(null, true);
        private string _companionsText = string.Empty;
        private bool _hasCompanionRows;
        private string _diplomacyText = string.Empty;
        private string _foreignOfficePeaceText = "0";
        private string _foreignOfficeWarText = "0";
        private string _foreignOfficeIncomeText = "0";
        private string _foreignOfficeRealmText = string.Empty;
        private bool _isDiplomacyRelationsPage = true;
        private bool _isKingdomFinancesPage;
        private string _kingdomFinanceRealmText = string.Empty;
        private string _kingdomTreasuryText = "0";
        private string _kingdomIncomeText = "0 / day";
        private string _kingdomExpensesText = "0 / day";
        private string _kingdomNetText = "0 / day";
        private string _kingdomFinanceStatusText = string.Empty;
        private string _warStatisticsText = string.Empty;
        private string _warActiveWarsText = "0";
        private string _warTroopsFieldedText = "0";
        private string _warLossesText = "0";
        private string _warDiplomacyRecordText = string.Empty;
        private string _warPlayerStatusText = string.Empty;
        private string _warStatisticsFootnote = string.Empty;
        private bool _hasWarStatisticsRows;
        private bool _isStrategicMap;
        private bool _isCalendarVisible = true;
        private bool _isCalendarSectionVisible = true;
        private bool _isSummariesVisible;
        private bool _isSavedSummariesPage = true;
        private bool _isMarriagesPage;
        private string _marriagePlayerName = string.Empty;
        private string _marriagePlayerDetails = string.Empty;
        private string _marriagePlayerStatus = string.Empty;
        private string _marriageStatusText = string.Empty;
        private string _marriageSortMode = "All";
        private string _marriageNameSearchText = string.Empty;
        private CharacterImageIdentifierVM _marriagePlayerPortrait = new CharacterImageIdentifierVM(null);
        private bool _isStorySectionVisible;
        private bool _isCharacterStoryVisible;
        private bool _isCompanionsVisible;
        private bool _isDiplomacyVisible;
        private bool _isStrategicSectionVisible;
        private bool _isWarStatisticsVisible;
        private bool _isPointerOverStrategicMap;
        private readonly MBBindingList<CalendarWorldLedgerTabVM> _tabs = new MBBindingList<CalendarWorldLedgerTabVM>();
        private readonly MBBindingList<CalendarWorldCalendarDayVM> _days = new MBBindingList<CalendarWorldCalendarDayVM>();
        private readonly MBBindingList<CalendarWorldCalendarMonthVM> _calendarMonths = new MBBindingList<CalendarWorldCalendarMonthVM>();
        private readonly MBBindingList<CalendarDiplomacyRelationVM> _diplomacyRelations = new MBBindingList<CalendarDiplomacyRelationVM>();
        private readonly MBBindingList<CalendarKingdomFinanceRowVM> _kingdomFinanceRows = new MBBindingList<CalendarKingdomFinanceRowVM>();
        private readonly MBBindingList<CalendarCompanionRecordVM> _companionRows = new MBBindingList<CalendarCompanionRecordVM>();
        private readonly MBBindingList<CalendarWarStatisticsRowVM> _warStatisticsRows = new MBBindingList<CalendarWarStatisticsRowVM>();
        private readonly MBBindingList<CalendarWorldSavedSummaryVM> _savedSummaries = new MBBindingList<CalendarWorldSavedSummaryVM>();
        private readonly MBBindingList<CalendarMarriageCandidateVM> _marriageCandidates = new MBBindingList<CalendarMarriageCandidateVM>();
        private readonly MBBindingList<CalendarWorldStrategicProvinceVM> _strategicProvinces = new MBBindingList<CalendarWorldStrategicProvinceVM>();
        private readonly MBBindingList<CalendarWorldStrategicProvinceVM> _strategicContestedProvinces = new MBBindingList<CalendarWorldStrategicProvinceVM>();
        private readonly MBBindingList<CalendarWorldStrategicMarkerVM> _strategicMarkers = new MBBindingList<CalendarWorldStrategicMarkerVM>();
        private readonly MBBindingList<CalendarStrategicKingdomSummaryVM> _strategicKingdomRows = new MBBindingList<CalendarStrategicKingdomSummaryVM>();
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
            AddTab("Calendar", "Realm Chronicle");
            AddTab("Character", "My Story");
            AddTab("Diplomacy", "Realm Affairs");
            AddTab("Strategic", "Military Affairs");
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
        [DataSourceProperty] public string CharacterStoryTitle { get { return _characterStoryTitle; } private set { if (_characterStoryTitle == value) return; _characterStoryTitle = value ?? string.Empty; OnPropertyChangedWithValue(_characterStoryTitle, "CharacterStoryTitle"); } }
        [DataSourceProperty] public string CharacterStorySubtitle { get { return _characterStorySubtitle; } private set { if (_characterStorySubtitle == value) return; _characterStorySubtitle = value ?? string.Empty; OnPropertyChangedWithValue(_characterStorySubtitle, "CharacterStorySubtitle"); } }
        [DataSourceProperty] public string CharacterStoryText { get { return _characterStoryText; } private set { if (_characterStoryText == value) return; _characterStoryText = value ?? string.Empty; OnPropertyChangedWithValue(_characterStoryText, "CharacterStoryText"); } }
        [DataSourceProperty] public string CharacterMilestoneText { get { return _characterMilestoneText; } private set { if (_characterMilestoneText == value) return; _characterMilestoneText = value ?? string.Empty; OnPropertyChangedWithValue(_characterMilestoneText, "CharacterMilestoneText"); } }
        [DataSourceProperty] public string CharacterKillsText { get { return _characterKillsText; } private set { if (_characterKillsText == value) return; _characterKillsText = value ?? "0"; OnPropertyChangedWithValue(_characterKillsText, "CharacterKillsText"); } }
        [DataSourceProperty] public string CharacterKnockoutsText { get { return _characterKnockoutsText; } private set { if (_characterKnockoutsText == value) return; _characterKnockoutsText = value ?? "0"; OnPropertyChangedWithValue(_characterKnockoutsText, "CharacterKnockoutsText"); } }
        [DataSourceProperty] public string CharacterRecordNote { get { return _characterRecordNote; } private set { if (_characterRecordNote == value) return; _characterRecordNote = value ?? string.Empty; OnPropertyChangedWithValue(_characterRecordNote, "CharacterRecordNote"); } }
        [DataSourceProperty] public CharacterImageIdentifierVM CharacterPortrait { get { return _characterPortrait; } private set { if (_characterPortrait == value) return; _characterPortrait = value ?? new CharacterImageIdentifierVM(null); OnPropertyChangedWithValue(_characterPortrait, "CharacterPortrait"); } }
        [DataSourceProperty] public BannerImageIdentifierVM CharacterClanBanner { get { return _characterClanBanner; } private set { if (_characterClanBanner == value) return; _characterClanBanner = value ?? new BannerImageIdentifierVM(null, true); OnPropertyChangedWithValue(_characterClanBanner, "CharacterClanBanner"); } }
        [DataSourceProperty] public string CompanionsText { get { return _companionsText; } private set { if (_companionsText == value) return; _companionsText = value ?? string.Empty; OnPropertyChangedWithValue(_companionsText, "CompanionsText"); } }
        [DataSourceProperty] public bool HasCompanionRows { get { return _hasCompanionRows; } private set { if (_hasCompanionRows == value) return; _hasCompanionRows = value; OnPropertyChangedWithValue(value, "HasCompanionRows"); OnPropertyChangedWithValue(!value, "ShowCompanionEmptyState"); } }
        [DataSourceProperty] public bool ShowCompanionEmptyState { get { return !_hasCompanionRows; } }
        [DataSourceProperty] public MBBindingList<CalendarCompanionRecordVM> CompanionRows { get { return _companionRows; } }
        [DataSourceProperty] public string DiplomacyText { get { return _diplomacyText; } private set { if (_diplomacyText == value) return; _diplomacyText = value ?? string.Empty; OnPropertyChangedWithValue(_diplomacyText, "DiplomacyText"); } }
        [DataSourceProperty] public string ForeignOfficePeaceText { get { return _foreignOfficePeaceText; } private set { if (_foreignOfficePeaceText == value) return; _foreignOfficePeaceText = value ?? "0"; OnPropertyChangedWithValue(_foreignOfficePeaceText, "ForeignOfficePeaceText"); } }
        [DataSourceProperty] public string ForeignOfficeWarText { get { return _foreignOfficeWarText; } private set { if (_foreignOfficeWarText == value) return; _foreignOfficeWarText = value ?? "0"; OnPropertyChangedWithValue(_foreignOfficeWarText, "ForeignOfficeWarText"); } }
        [DataSourceProperty] public string ForeignOfficeIncomeText { get { return _foreignOfficeIncomeText; } private set { if (_foreignOfficeIncomeText == value) return; _foreignOfficeIncomeText = value ?? "0"; OnPropertyChangedWithValue(_foreignOfficeIncomeText, "ForeignOfficeIncomeText"); } }
        [DataSourceProperty] public string ForeignOfficeRealmText { get { return _foreignOfficeRealmText; } private set { if (_foreignOfficeRealmText == value) return; _foreignOfficeRealmText = value ?? string.Empty; OnPropertyChangedWithValue(_foreignOfficeRealmText, "ForeignOfficeRealmText"); } }
        [DataSourceProperty] public bool IsDiplomacyRelationsPage { get { return _isDiplomacyRelationsPage; } private set { if (_isDiplomacyRelationsPage == value) return; _isDiplomacyRelationsPage = value; OnPropertyChangedWithValue(value, "IsDiplomacyRelationsPage"); } }
        [DataSourceProperty] public bool IsKingdomFinancesPage { get { return _isKingdomFinancesPage; } private set { if (_isKingdomFinancesPage == value) return; _isKingdomFinancesPage = value; OnPropertyChangedWithValue(value, "IsKingdomFinancesPage"); NotifyKingdomFinanceRowStateChanged(); } }
        [DataSourceProperty] public string KingdomFinanceRealmText { get { return _kingdomFinanceRealmText; } private set { if (_kingdomFinanceRealmText == value) return; _kingdomFinanceRealmText = value ?? string.Empty; OnPropertyChangedWithValue(_kingdomFinanceRealmText, "KingdomFinanceRealmText"); } }
        [DataSourceProperty] public string KingdomTreasuryText { get { return _kingdomTreasuryText; } private set { if (_kingdomTreasuryText == value) return; _kingdomTreasuryText = value ?? "0"; OnPropertyChangedWithValue(_kingdomTreasuryText, "KingdomTreasuryText"); } }
        [DataSourceProperty] public string KingdomIncomeText { get { return _kingdomIncomeText; } private set { if (_kingdomIncomeText == value) return; _kingdomIncomeText = value ?? "0 / day"; OnPropertyChangedWithValue(_kingdomIncomeText, "KingdomIncomeText"); } }
        [DataSourceProperty] public string KingdomExpensesText { get { return _kingdomExpensesText; } private set { if (_kingdomExpensesText == value) return; _kingdomExpensesText = value ?? "0 / day"; OnPropertyChangedWithValue(_kingdomExpensesText, "KingdomExpensesText"); } }
        [DataSourceProperty] public string KingdomNetText { get { return _kingdomNetText; } private set { if (_kingdomNetText == value) return; _kingdomNetText = value ?? "0 / day"; OnPropertyChangedWithValue(_kingdomNetText, "KingdomNetText"); } }
        [DataSourceProperty] public string KingdomFinanceStatusText { get { return _kingdomFinanceStatusText; } private set { if (_kingdomFinanceStatusText == value) return; _kingdomFinanceStatusText = value ?? string.Empty; OnPropertyChangedWithValue(_kingdomFinanceStatusText, "KingdomFinanceStatusText"); } }
        [DataSourceProperty] public string WarStatisticsText { get { return _warStatisticsText; } private set { if (_warStatisticsText == value) return; _warStatisticsText = value ?? string.Empty; OnPropertyChangedWithValue(_warStatisticsText, "WarStatisticsText"); } }
        [DataSourceProperty] public string WarActiveWarsText { get { return _warActiveWarsText; } private set { if (_warActiveWarsText == value) return; _warActiveWarsText = value ?? string.Empty; OnPropertyChangedWithValue(_warActiveWarsText, "WarActiveWarsText"); } }
        [DataSourceProperty] public string WarTroopsFieldedText { get { return _warTroopsFieldedText; } private set { if (_warTroopsFieldedText == value) return; _warTroopsFieldedText = value ?? string.Empty; OnPropertyChangedWithValue(_warTroopsFieldedText, "WarTroopsFieldedText"); } }
        [DataSourceProperty] public string WarLossesText { get { return _warLossesText; } private set { if (_warLossesText == value) return; _warLossesText = value ?? string.Empty; OnPropertyChangedWithValue(_warLossesText, "WarLossesText"); } }
        [DataSourceProperty] public string WarDiplomacyRecordText { get { return _warDiplomacyRecordText; } private set { if (_warDiplomacyRecordText == value) return; _warDiplomacyRecordText = value ?? string.Empty; OnPropertyChangedWithValue(_warDiplomacyRecordText, "WarDiplomacyRecordText"); } }
        [DataSourceProperty] public string WarPlayerStatusText { get { return _warPlayerStatusText; } private set { if (_warPlayerStatusText == value) return; _warPlayerStatusText = value ?? string.Empty; OnPropertyChangedWithValue(_warPlayerStatusText, "WarPlayerStatusText"); } }
        [DataSourceProperty] public string WarStatisticsFootnote { get { return _warStatisticsFootnote; } private set { if (_warStatisticsFootnote == value) return; _warStatisticsFootnote = value ?? string.Empty; OnPropertyChangedWithValue(_warStatisticsFootnote, "WarStatisticsFootnote"); } }
        [DataSourceProperty] public bool HasWarStatisticsRows { get { return _hasWarStatisticsRows; } private set { if (_hasWarStatisticsRows == value) return; _hasWarStatisticsRows = value; OnPropertyChangedWithValue(value, "HasWarStatisticsRows"); } }
        [DataSourceProperty] public MBBindingList<CalendarWarStatisticsRowVM> WarStatisticsRows { get { return _warStatisticsRows; } }
        [DataSourceProperty] public bool IsStrategicMap { get { return _isStrategicMap; } private set { if (_isStrategicMap == value) return; _isStrategicMap = value; OnPropertyChangedWithValue(value, "IsStrategicMap"); } }
        [DataSourceProperty] public bool IsCalendarVisible { get { return _isCalendarVisible; } private set { if (_isCalendarVisible == value) return; _isCalendarVisible = value; OnPropertyChangedWithValue(value, "IsCalendarVisible"); } }
        [DataSourceProperty] public bool IsCalendarSectionVisible { get { return _isCalendarSectionVisible; } private set { if (_isCalendarSectionVisible == value) return; _isCalendarSectionVisible = value; OnPropertyChangedWithValue(value, "IsCalendarSectionVisible"); } }
        [DataSourceProperty] public bool IsSummariesVisible { get { return _isSummariesVisible; } private set { if (_isSummariesVisible == value) return; _isSummariesVisible = value; OnPropertyChangedWithValue(value, "IsSummariesVisible"); } }
        [DataSourceProperty] public bool IsSavedSummariesPage { get { return _isSavedSummariesPage; } private set { if (_isSavedSummariesPage == value) return; _isSavedSummariesPage = value; OnPropertyChangedWithValue(value, "IsSavedSummariesPage"); } }
        [DataSourceProperty] public bool IsMarriagesPage { get { return _isMarriagesPage; } private set { if (_isMarriagesPage == value) return; _isMarriagesPage = value; OnPropertyChangedWithValue(value, "IsMarriagesPage"); OnPropertyChangedWithValue(IsRealmLedgerVisible, "IsRealmLedgerVisible"); } }
        [DataSourceProperty] public string MarriagePlayerName { get { return _marriagePlayerName; } private set { if (_marriagePlayerName == value) return; _marriagePlayerName = value ?? string.Empty; OnPropertyChangedWithValue(_marriagePlayerName, "MarriagePlayerName"); } }
        [DataSourceProperty] public string MarriagePlayerDetails { get { return _marriagePlayerDetails; } private set { if (_marriagePlayerDetails == value) return; _marriagePlayerDetails = value ?? string.Empty; OnPropertyChangedWithValue(_marriagePlayerDetails, "MarriagePlayerDetails"); } }
        [DataSourceProperty] public string MarriagePlayerStatus { get { return _marriagePlayerStatus; } private set { if (_marriagePlayerStatus == value) return; _marriagePlayerStatus = value ?? string.Empty; OnPropertyChangedWithValue(_marriagePlayerStatus, "MarriagePlayerStatus"); } }
        [DataSourceProperty] public string MarriageStatusText { get { return _marriageStatusText; } private set { if (_marriageStatusText == value) return; _marriageStatusText = value ?? string.Empty; OnPropertyChangedWithValue(_marriageStatusText, "MarriageStatusText"); } }
        [DataSourceProperty] public string MarriageNameSearchText
        {
            get { return _marriageNameSearchText; }
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(_marriageNameSearchText, normalized, StringComparison.Ordinal)) return;
                _marriageNameSearchText = normalized;
                OnPropertyChangedWithValue(_marriageNameSearchText, "MarriageNameSearchText");
                RefreshMarriages();
            }
        }
        [DataSourceProperty] public bool IsMarriageSortName { get { return string.Equals(_marriageSortMode, "Name", StringComparison.Ordinal); } }
        [DataSourceProperty] public bool IsMarriageSortAge { get { return string.Equals(_marriageSortMode, "Age", StringComparison.Ordinal); } }
        [DataSourceProperty] public bool IsMarriageSortKingdom { get { return string.Equals(_marriageSortMode, "Kingdom", StringComparison.Ordinal); } }
        [DataSourceProperty] public bool IsMarriageSortAll { get { return string.Equals(_marriageSortMode, "All", StringComparison.Ordinal); } }
        [DataSourceProperty] public CharacterImageIdentifierVM MarriagePlayerPortrait { get { return _marriagePlayerPortrait; } private set { if (_marriagePlayerPortrait == value) return; _marriagePlayerPortrait = value ?? new CharacterImageIdentifierVM(null); OnPropertyChangedWithValue(_marriagePlayerPortrait, "MarriagePlayerPortrait"); } }
        [DataSourceProperty] public MBBindingList<CalendarMarriageCandidateVM> MarriageCandidates { get { return _marriageCandidates; } }
        [DataSourceProperty] public bool HasMarriageCandidates { get { return _marriageCandidates.Count > 0; } }
        [DataSourceProperty] public bool ShowMarriageEmptyState { get { return _marriageCandidates.Count == 0; } }
        [DataSourceProperty] public bool IsStorySectionVisible { get { return _isStorySectionVisible; } private set { if (_isStorySectionVisible == value) return; _isStorySectionVisible = value; OnPropertyChangedWithValue(value, "IsStorySectionVisible"); } }
        [DataSourceProperty] public bool IsCharacterStoryVisible { get { return _isCharacterStoryVisible; } private set { if (_isCharacterStoryVisible == value) return; _isCharacterStoryVisible = value; OnPropertyChangedWithValue(value, "IsCharacterStoryVisible"); } }
        [DataSourceProperty] public bool IsCompanionsVisible { get { return _isCompanionsVisible; } private set { if (_isCompanionsVisible == value) return; _isCompanionsVisible = value; OnPropertyChangedWithValue(value, "IsCompanionsVisible"); } }
        [DataSourceProperty] public bool IsDiplomacyVisible { get { return _isDiplomacyVisible; } private set { if (_isDiplomacyVisible == value) return; _isDiplomacyVisible = value; OnPropertyChangedWithValue(value, "IsDiplomacyVisible"); OnPropertyChangedWithValue(IsRealmLedgerVisible, "IsRealmLedgerVisible"); } }
        [DataSourceProperty] public bool IsRealmLedgerVisible { get { return _isDiplomacyVisible && !_isMarriagesPage; } }
        [DataSourceProperty] public bool IsStrategicSectionVisible { get { return _isStrategicSectionVisible; } private set { if (_isStrategicSectionVisible == value) return; _isStrategicSectionVisible = value; OnPropertyChangedWithValue(value, "IsStrategicSectionVisible"); } }
        [DataSourceProperty] public bool IsWarStatisticsVisible { get { return _isWarStatisticsVisible; } private set { if (_isWarStatisticsVisible == value) return; _isWarStatisticsVisible = value; OnPropertyChangedWithValue(value, "IsWarStatisticsVisible"); } }
        [DataSourceProperty] public MBBindingList<CalendarWorldLedgerTabVM> Tabs { get { return _tabs; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldCalendarDayVM> Days { get { return _days; } }
        [DataSourceProperty] public bool HasSelectedCalendarDay { get { return _selectedCalendarDay != long.MinValue; } }
        [DataSourceProperty] public bool CanPreviousCalendarMonth { get { return CanMoveDisplayedCalendarMonth(-1); } }
        [DataSourceProperty] public bool CanNextCalendarMonth { get { return CanMoveDisplayedCalendarMonth(1); } }
        [DataSourceProperty] public MBBindingList<CalendarWorldCalendarMonthVM> CalendarMonths { get { return _calendarMonths; } }
        [DataSourceProperty] public MBBindingList<CalendarDiplomacyRelationVM> DiplomacyRelations { get { return _diplomacyRelations; } }
        [DataSourceProperty] public MBBindingList<CalendarKingdomFinanceRowVM> KingdomFinanceRows { get { return _kingdomFinanceRows; } }
        [DataSourceProperty] public bool HasKingdomFinanceRows { get { return _kingdomFinanceRows.Count > 0; } }
        [DataSourceProperty] public bool ShowKingdomFinanceEmptyState { get { return _kingdomFinanceRows.Count == 0; } }
        [DataSourceProperty] public bool IsKingdomFinanceLedgerVisible { get { return _isKingdomFinancesPage && _kingdomFinanceRows.Count > 0; } }
        [DataSourceProperty] public bool IsKingdomFinanceEmptyVisible { get { return _isKingdomFinancesPage && _kingdomFinanceRows.Count == 0; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldSavedSummaryVM> SavedSummaries { get { return _savedSummaries; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicProvinceVM> StrategicProvinces { get { return _strategicProvinces; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicProvinceVM> StrategicContestedProvinces { get { return _strategicContestedProvinces; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicMarkerVM> StrategicMarkers { get { return _strategicMarkers; } }
        [DataSourceProperty] public MBBindingList<CalendarStrategicKingdomSummaryVM> StrategicKingdomRows { get { return _strategicKingdomRows; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicVillageVM> SelectedStrategicVillages { get { return _selectedStrategicVillages; } }
        [DataSourceProperty] public MBBindingList<CalendarWorldStrategicVillageVM> TrackedStrategicSettlements { get { return _trackedStrategicSettlements; } }
        [DataSourceProperty] public bool HasTrackedStrategicSettlements { get { return _trackedStrategicSettlements.Count > 0; } }
        [DataSourceProperty] public bool ShowTrackedStrategicSettlements
        {
            get { return HasTrackedStrategicSettlements && !HasSelectedStrategicSettlement; }
        }
        [DataSourceProperty] public bool ShowStrategicMapLegend { get { return CalendarSettingsState.StrategicMapShowLegend; } }
        [DataSourceProperty] public int StrategicMapLegendWidth { get { return CalendarSettingsState.StrategicMapLegendWidth; } }
        [DataSourceProperty] public int StrategicMapLegendHeight { get { return Math.Max(StrategicPartyLegendMinimumHeight, CalendarSettingsState.StrategicMapLegendHeight); } }
        [DataSourceProperty] public int StrategicMapLegendMarginTop { get { return CalendarSettingsState.StrategicMapLegendMarginTop; } }
        [DataSourceProperty] public int StrategicMapLegendContentTop { get { return StrategicMapLegendMarginTop + 34; } }
        [DataSourceProperty] public int StrategicMapLegendSeparatorTop { get { return StrategicMapLegendContentTop + StrategicMapLegendHeight + 2; } }
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
        [DataSourceProperty] public bool ShowStrategicKingdomRows { get { return !HasSelectedStrategicSettlement; } }
        [DataSourceProperty] public bool ShowStrategicSettlementDetails { get { return HasSelectedStrategicSettlement; } }
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
            get
            {
                int legendExpansion = StrategicMapLegendHeight - StrategicPartyLegendBaseHeight;
                return Math.Max(180, (HasSelectedStrategicSettlement ? 348 : 390) - legendExpansion);
            }
        }
        [DataSourceProperty] public int StrategicSummaryScrollerMarginBottom
        {
            get { return HasSelectedStrategicSettlement ? 102 : 60; }
        }
        [DataSourceProperty] public int StrategicSummaryContentTop
        {
            get { return StrategicMapLegendSeparatorTop + 29; }
        }

        public void ExecuteClose() { if (_close != null) _close(); }
        public void ExecuteRefresh() { RefreshCalendar(); }
        public void ExecuteShowCharacterStory()
        {
            Diagnostics.Info("World Events page tab selected: My Story / Personal Chronicle.");
            IsCharacterStoryVisible = true;
            IsCompanionsVisible = false;
            RefreshCharacterStory();
        }
        public void ExecuteShowCalendarPage()
        {
            Diagnostics.Info("World Events page tab selected: Realm Chronicle / Calendar.");
            IsCalendarVisible = true;
            IsSummariesVisible = false;
            IsSavedSummariesPage = false;
        }
        public void ExecuteShowSavedSummaries()
        {
            Diagnostics.Info("World Events page tab selected: Realm Chronicle / Summaries.");
            IsCalendarVisible = false;
            IsSummariesVisible = true;
            IsSavedSummariesPage = true;
            IsMarriagesPage = false;
        }
        public void ExecuteShowMarriagesPage()
        {
            Diagnostics.Info("World Events page tab selected: Realm Affairs / Marriages.");
            IsSavedSummariesPage = false;
            IsDiplomacyRelationsPage = false;
            IsKingdomFinancesPage = false;
            IsMarriagesPage = true;
            OnPropertyChangedWithValue(IsRealmLedgerVisible, "IsRealmLedgerVisible");
            RefreshMarriages();
        }
        public void ExecuteSortMarriagesByName() { SetMarriageSortMode("Name"); }
        public void ExecuteSortMarriagesByAge() { SetMarriageSortMode("Age"); }
        public void ExecuteSortMarriagesByKingdom() { SetMarriageSortMode("Kingdom"); }
        public void ExecuteSortMarriagesByAll() { SetMarriageSortMode("All"); }

        private void SetMarriageSortMode(string mode)
        {
            if (string.Equals(_marriageSortMode, mode, StringComparison.Ordinal)) return;
            _marriageSortMode = mode;
            OnPropertyChangedWithValue(IsMarriageSortName, "IsMarriageSortName");
            OnPropertyChangedWithValue(IsMarriageSortAge, "IsMarriageSortAge");
            OnPropertyChangedWithValue(IsMarriageSortKingdom, "IsMarriageSortKingdom");
            OnPropertyChangedWithValue(IsMarriageSortAll, "IsMarriageSortAll");
            RefreshMarriages();
        }
        public void ExecuteShowCompanionsPage()
        {
            Diagnostics.Info("World Events page tab selected: My Story / Companions.");
            IsCharacterStoryVisible = false;
            IsCompanionsVisible = true;
            RefreshCompanions();
        }
        public void ExecuteShowDiplomacyRelations()
        {
            Diagnostics.Info("World Events page tab selected: Realm Affairs / Diplomacy.");
            IsDiplomacyRelationsPage = true;
            IsKingdomFinancesPage = false;
            IsMarriagesPage = false;
            OnPropertyChangedWithValue(IsRealmLedgerVisible, "IsRealmLedgerVisible");
            RefreshDiplomacy();
        }
        public void ExecuteShowKingdomFinances()
        {
            Diagnostics.Info("World Events page tab selected: Realm Affairs / Kingdom Finances.");
            IsDiplomacyRelationsPage = false;
            IsKingdomFinancesPage = true;
            IsMarriagesPage = false;
            OnPropertyChangedWithValue(IsRealmLedgerVisible, "IsRealmLedgerVisible");
            RefreshDiplomacy();
        }
        public void ExecuteShowStrategicMapPage()
        {
            Diagnostics.Info("World Events page tab selected: Military Affairs / Strategic Map.");
            IsStrategicMap = true;
            IsWarStatisticsVisible = false;
            BuildStrategicMapLayers();
            RefreshStrategicKingdomRows();
            RefreshSelectedStrategicVillages();
            RefreshTrackedStrategicSettlements();
            StrategicText = BuildStrategicPanelText();
            NotifyStrategicSettlementSelectionChanged();
        }
        public void ExecuteShowStrategicWarStatistics()
        {
            Diagnostics.Info("World Events page tab selected: Military Affairs / War Statistics.");
            IsStrategicMap = false;
            IsWarStatisticsVisible = true;
            _isPointerOverStrategicMap = false;
            RefreshWarStatistics();
        }
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
            RefreshStrategicKingdomRows();
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
        private void AddTab(string filter, string label)
        {
            _tabs.Add(new CalendarWorldLedgerTabVM(
                filter,
                label,
                SelectTab));
        }
        private void SelectTab(CalendarWorldLedgerTabVM tab)
        {
            if (tab == null) return;
            Diagnostics.Info("World Events main tab selected: " + tab.Filter + ".");
            _selectedFilter = "All";
            IsStrategicSectionVisible = string.Equals(tab.Filter, "Strategic", StringComparison.Ordinal);
            IsStrategicMap = IsStrategicSectionVisible;
            IsCalendarSectionVisible = string.Equals(tab.Filter, "Calendar", StringComparison.Ordinal);
            IsCalendarVisible = IsCalendarSectionVisible;
            IsSummariesVisible = false;
            IsSavedSummariesPage = false;
            IsMarriagesPage = false;
            OnPropertyChangedWithValue(IsRealmLedgerVisible, "IsRealmLedgerVisible");
            IsStorySectionVisible = string.Equals(tab.Filter, "Character", StringComparison.Ordinal);
            IsCharacterStoryVisible = IsStorySectionVisible;
            IsCompanionsVisible = false;
            IsDiplomacyVisible = string.Equals(tab.Filter, "Diplomacy", StringComparison.Ordinal);
            IsWarStatisticsVisible = false;
            if (IsDiplomacyVisible)
            {
                // Kingdom Finances is the section's landing page. Diplomacy
                // remains available as the second internal page without
                // changing any campaign data or save-state contracts.
                IsDiplomacyRelationsPage = false;
                IsKingdomFinancesPage = true;
            }
            if (!IsStrategicMap) _isPointerOverStrategicMap = false;
            foreach (CalendarWorldLedgerTabVM entry in _tabs) entry.IsSelected = ReferenceEquals(entry, tab);
            OnPropertyChangedWithValue(IsRealmLedgerVisible, "IsRealmLedgerVisible");
            RefreshCalendar();
        }

        private void RefreshCalendar()
        {
            RefreshCalendarNotes();
            RefreshCharacterStory();
            RefreshCompanions();
            RefreshMarriages();
            RefreshDiplomacy();
            RefreshWarStatistics();
            BuildStrategicMapLayers();
            RefreshStrategicKingdomRows();
            RefreshSelectedStrategicVillages();
            RefreshTrackedStrategicSettlements();
            StrategicText = BuildStrategicPanelText();
            NotifyStrategicSettlementSelectionChanged();
            BuildCalendarHistory();
        }

        private void RefreshCharacterStory()
        {
            Hero hero = Hero.MainHero;
            if (hero == null)
            {
                CharacterStoryTitle = "CHARACTER CHRONICLE";
                CharacterStorySubtitle = "No active campaign hero";
                CharacterStoryText = "Load a campaign to view the current character's story.";
                CharacterMilestoneText = "No later milestones have been recorded yet.";
                CharacterKillsText = "0";
                CharacterKnockoutsText = "0";
                CharacterRecordNote = "No combat record is available.";
                CharacterPortrait = new CharacterImageIdentifierVM(null);
                CharacterClanBanner = new BannerImageIdentifierVM(null, true);
                return;
            }

            string culture = hero.Culture == null || hero.Culture.Name == null ? "Unknown culture" : hero.Culture.Name.ToString();
            string clan = hero.Clan == null || hero.Clan.Name == null ? "No clan" : hero.Clan.Name.ToString();
            CharacterStoryTitle = hero.Name == null ? "CHARACTER CHRONICLE" : hero.Name.ToString().ToUpperInvariant();
            CharacterStorySubtitle = culture + "  •  " + clan + "  •  Age " + hero.Age.ToString("0");

            CharacterPortrait = new CharacterImageIdentifierVM(CharacterCode.CreateFrom(hero.CharacterObject));
            CharacterClanBanner = new BannerImageIdentifierVM(hero.Clan == null ? null : hero.Clan.Banner, true);
            string origin = CalendarWorldLedgerBehavior.GetCharacterOriginStory();
            string story = string.IsNullOrWhiteSpace(origin)
                ? "This campaign predates the origin recorder, so Bannerlord no longer exposes the exact choices made during character creation.\n\nThe next character created with Ages of Calradia enabled will have every final narrative choice preserved here."
                : origin;
            string milestones = CalendarWorldLedgerBehavior.GetCharacterMilestoneStory(hero);
            int kills;
            int knockouts;
            CalendarWorldLedgerBehavior.GetCombatStatistics(hero, out kills, out knockouts);
            CharacterStoryText = story;
            CharacterMilestoneText = string.IsNullOrWhiteSpace(milestones)
                ? "No later milestones have been recorded yet."
                : milestones;
            CharacterKillsText = kills.ToString("N0");
            CharacterKnockoutsText = knockouts.ToString("N0");
            CharacterRecordNote = "Combat records begin when this Ages of Calradia version is installed.";
        }

        private void RefreshWarStatistics()
        {
            CalendarWarStatisticsSnapshot snapshot = CalendarWorldLedgerBehavior.GetWarStatisticsSnapshot();
            WarActiveWarsText = snapshot.Wars.Count.ToString("N0");
            WarTroopsFieldedText = snapshot.TotalTroopsFielded.ToString("N0");
            WarLossesText = snapshot.TotalLosses.ToString("N0");
            WarDiplomacyRecordText = snapshot.WarDeclarations.ToString("N0") + " declarations  •  "
                + snapshot.PeaceAgreements.ToString("N0") + " peace accords";
            WarPlayerStatusText = snapshot.PlayerStatus;
            WarStatisticsFootnote = "Troops and ships are live faction strength. Troop losses use Bannerlord's war record; ships lost count vessels sunk in that war.";

            _warStatisticsRows.Clear();
            for (int index = 0; index < snapshot.Wars.Count; index++)
            {
                _warStatisticsRows.Add(new CalendarWarStatisticsRowVM(snapshot.Wars[index], index, ConcludeWar));
            }
            HasWarStatisticsRows = _warStatisticsRows.Count > 0;
            WarStatisticsText = HasWarStatisticsRows ? string.Empty : "No kingdom wars are active. Calradia is presently at peace.";
        }

        private void ConcludeWar(CalendarWarStatisticsRecord record, bool surrender)
        {
            if (record == null) return;
            string message;
            if (CalendarWorldLedgerBehavior.TryConcludeWar(record.LeftKingdom, record.RightKingdom, surrender, out message))
            {
                InformationManager.DisplayMessage(new InformationMessage(message));
                RefreshCalendar();
                return;
            }
            WarStatisticsText = message;
        }

        private void RefreshCompanions()
        {
            _companionRows.Clear();
            List<Hero> companions = new List<Hero>();
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                if (hero != null && hero.IsPlayerCompanion) companions.Add(hero);
            }
            companions.Sort(delegate(Hero left, Hero right)
            {
                string leftName = left == null || left.Name == null ? string.Empty : left.Name.ToString();
                string rightName = right == null || right.Name == null ? string.Empty : right.Name.ToString();
                return string.Compare(leftName, rightName, StringComparison.Ordinal);
            });

            if (companions.Count == 0)
            {
                CompanionsText = "No companions currently serve your clan. Recruit wanderers to build a company whose histories and combat records can be followed here.";
                HasCompanionRows = false;
                return;
            }

            for (int index = 0; index < companions.Count; index++)
            {
                _companionRows.Add(new CalendarCompanionRecordVM(companions[index], index));
            }
            CompanionsText = string.Empty;
            HasCompanionRows = _companionRows.Count > 0;
        }

        private void RefreshMarriages()
        {
            _marriageCandidates.Clear();
            Hero player = Hero.MainHero;
            if (player == null || player.CharacterObject == null || Campaign.Current == null)
            {
                MarriagePlayerName = "NO ACTIVE CHARACTER";
                MarriagePlayerDetails = "Load a campaign to open the marriage ledger.";
                MarriagePlayerStatus = string.Empty;
                MarriagePlayerPortrait = new CharacterImageIdentifierVM(null);
                MarriageStatusText = "No marriage candidates are available outside a campaign.";
                NotifyMarriageCandidateStateChanged();
                return;
            }

            MarriagePlayerName = player.Name == null ? "YOUR CHARACTER" : player.Name.ToString().ToUpperInvariant();
            string clanName = player.Clan == null || player.Clan.Name == null ? "No clan" : player.Clan.Name.ToString();
            string kingdomName = player.Clan == null || player.Clan.Kingdom == null || player.Clan.Kingdom.Name == null
                ? "Independent" : player.Clan.Kingdom.Name.ToString();
            MarriagePlayerDetails = clanName + "  •  " + kingdomName + "  •  Age " + player.Age.ToString("0");
            MarriagePlayerStatus = player.Spouse == null
                ? "UNMARRIED  •  Messengers arrange audiences; Bannerlord's native courtship and family approval decide the match."
                : "MARRIED TO " + (player.Spouse.Name == null ? "an unnamed spouse" : player.Spouse.Name.ToString().ToUpperInvariant());
            MarriagePlayerPortrait = new CharacterImageIdentifierVM(CharacterCode.CreateFrom(player.CharacterObject));

            List<Hero> candidates = new List<Hero>();
            MarriageModel marriageModel = Campaign.Current.Models == null ? null : Campaign.Current.Models.MarriageModel;
            foreach (Hero candidate in Hero.AllAliveHeroes)
            {
                if (candidate == null || candidate == player || !candidate.IsAlive || !candidate.IsActive || !candidate.IsLord
                    || candidate.IsFemale == player.IsFemale || candidate.CharacterObject == null
                    || candidate.Spouse != null || candidate.IsMinorFactionHero) continue;
                int minimumAge = marriageModel == null
                    ? 18
                    : (candidate.IsFemale ? marriageModel.MinimumMarriageAgeFemale : marriageModel.MinimumMarriageAgeMale);
                if (candidate.Age < minimumAge) continue;
                string candidateName = candidate.Name == null ? string.Empty : candidate.Name.ToString();
                if (!string.IsNullOrWhiteSpace(_marriageNameSearchText)
                    && candidateName.IndexOf(_marriageNameSearchText.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                candidates.Add(candidate);
            }
            candidates.Sort(delegate(Hero left, Hero right)
            {
                return CompareMarriageCandidates(left, right, _marriageSortMode);
            });

            for (int index = 0; index < candidates.Count; index++)
            {
                _marriageCandidates.Add(new CalendarMarriageCandidateVM(
                    player,
                    candidates[index],
                    index,
                    SendMarriageMessenger));
            }
            MarriageStatusText = _marriageCandidates.Count == 0
                ? (string.IsNullOrWhiteSpace(_marriageNameSearchText)
                    ? "No unmarried opposite-sex adult nobles are currently available."
                    : "No eligible marriage candidates match ‘" + _marriageNameSearchText.Trim() + "’.")
                : _marriageCandidates.Count.ToString("N0") + " unmarried " + (player.IsFemale ? "men" : "women")
                    + " listed  •  Sorted by " + MarriageSortDescription(_marriageSortMode)
                    + (string.IsNullOrWhiteSpace(_marriageNameSearchText) ? string.Empty : "  •  Name contains ‘" + _marriageNameSearchText.Trim() + "’");
            NotifyMarriageCandidateStateChanged();
        }

        private static int CompareMarriageCandidates(Hero left, Hero right, string mode)
        {
            if (string.Equals(mode, "Age", StringComparison.Ordinal))
            {
                int age = left.Age.CompareTo(right.Age);
                return age != 0 ? age : CompareMarriageCandidateNames(left, right);
            }
            if (string.Equals(mode, "Kingdom", StringComparison.Ordinal))
            {
                int kingdom = string.Compare(MarriageCandidateKingdom(left), MarriageCandidateKingdom(right), StringComparison.Ordinal);
                return kingdom != 0 ? kingdom : CompareMarriageCandidateNames(left, right);
            }
            if (string.Equals(mode, "All", StringComparison.Ordinal))
            {
                int kingdom = string.Compare(MarriageCandidateKingdom(left), MarriageCandidateKingdom(right), StringComparison.Ordinal);
                if (kingdom != 0) return kingdom;
                int age = left.Age.CompareTo(right.Age);
                return age != 0 ? age : CompareMarriageCandidateNames(left, right);
            }
            return CompareMarriageCandidateNames(left, right);
        }

        private static int CompareMarriageCandidateNames(Hero left, Hero right)
        {
            string leftName = left == null || left.Name == null ? string.Empty : left.Name.ToString();
            string rightName = right == null || right.Name == null ? string.Empty : right.Name.ToString();
            return string.Compare(leftName, rightName, StringComparison.Ordinal);
        }

        private static string MarriageCandidateKingdom(Hero hero)
        {
            return hero == null || hero.Clan == null || hero.Clan.Kingdom == null || hero.Clan.Kingdom.Name == null
                ? "Independent" : hero.Clan.Kingdom.Name.ToString();
        }

        private static string MarriageSortDescription(string mode)
        {
            if (string.Equals(mode, "Name", StringComparison.Ordinal)) return "name";
            if (string.Equals(mode, "Age", StringComparison.Ordinal)) return "age (youngest first)";
            if (string.Equals(mode, "Kingdom", StringComparison.Ordinal)) return "kingdom, then name";
            return "kingdom, age, then name";
        }

        private static bool IsMarriageProspectCompatible(Hero player, Hero candidate)
        {
            if (player == null || candidate == null || player.Spouse != null || candidate.Spouse != null
                || Campaign.Current == null || Campaign.Current.Models == null || Campaign.Current.Models.MarriageModel == null) return false;
            try
            {
                return Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(player, candidate)
                    && !FactionManager.IsAtWarAgainstFaction(player.MapFaction, candidate.MapFaction);
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Marriage prospect compatibility check failed.", exception);
                return false;
            }
        }

        private void NotifyMarriageCandidateStateChanged()
        {
            OnPropertyChangedWithValue(HasMarriageCandidates, "HasMarriageCandidates");
            OnPropertyChangedWithValue(ShowMarriageEmptyState, "ShowMarriageEmptyState");
        }

        private void SendMarriageMessenger(Hero candidate, string purpose)
        {
            Hero target = string.Equals(purpose, "ClanLeader", StringComparison.Ordinal)
                ? (candidate == null || candidate.Clan == null ? null : candidate.Clan.Leader)
                : candidate;
            string audienceName = target == null || target.Name == null ? "that court" : target.Name.ToString();
            if (candidate == null || target == null || target.CharacterObject == null || !target.IsAlive || target.IsPrisoner)
            {
                MarriageStatusText = "That court cannot receive a messenger at present.";
                return;
            }
            if (!IsMarriageProspectCompatible(Hero.MainHero, candidate))
            {
                MarriageStatusText = "Bannerlord's native marriage rules do not currently permit this match.";
                return;
            }
            if (CharacterObject.PlayerCharacter == null || PartyBase.MainParty == null || Campaign.Current == null)
            {
                MarriageStatusText = "A campaign party is required before a messenger can be dispatched.";
                return;
            }

            int daysRemaining = CalendarWorldLedgerBehavior.GetMarriageMessengerDaysRemaining(target, purpose);
            if (daysRemaining < 0)
            {
                int travelDays;
                if (CalendarWorldLedgerBehavior.DispatchMarriageMessenger(target, purpose, out travelDays))
                {
                    RefreshMarriages();
                    MarriageStatusText = "A messenger has departed for " + audienceName + ". The campaign route should take "
                        + travelDays.ToString("N0") + (travelDays == 1 ? " day." : " days.");
                    Diagnostics.Info("Marriage messenger dispatched to " + audienceName + "; purpose=" + purpose
                        + "; campaign-distance ETA=" + travelDays.ToString("N0") + " days.");
                }
                return;
            }
            if (daysRemaining > 0)
            {
                MarriageStatusText = "Your messenger is still travelling to " + audienceName + ". Expected arrival in "
                    + daysRemaining.ToString("N0") + (daysRemaining == 1 ? " day." : " days.");
                return;
            }

            try
            {
                if (_close != null) _close();
                CampaignMapConversation.OpenConversation(
                    new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty),
                    new ConversationCharacterData(target.CharacterObject));
                CalendarWorldLedgerBehavior.ConsumeArrivedMarriageMessenger(target, purpose);
                Diagnostics.Info("Marriage audience opened with " + audienceName + "; purpose=" + purpose + ".");
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Marriage messenger could not open the arranged audience.", exception);
                InformationManager.DisplayMessage(new InformationMessage("The arranged audience with " + audienceName + " could not be opened."));
            }
        }

        private void RefreshDiplomacy()
        {
            _diplomacyRelations.Clear();
            _kingdomFinanceRows.Clear();
            Kingdom playerKingdom = Clan.PlayerClan == null ? null : Clan.PlayerClan.Kingdom;
            List<Kingdom> kingdoms = new List<Kingdom>(Kingdom.All);
            kingdoms.Sort(delegate(Kingdom left, Kingdom right)
            {
                if (left == playerKingdom && right != playerKingdom) return -1;
                if (right == playerKingdom && left != playerKingdom) return 1;
                bool leftAtWar = playerKingdom != null && left != null && playerKingdom.IsAtWarWith(left);
                bool rightAtWar = playerKingdom != null && right != null && playerKingdom.IsAtWarWith(right);
                if (leftAtWar != rightAtWar) return leftAtWar ? -1 : 1;
                string leftName = left == null || left.Name == null ? string.Empty : left.Name.ToString();
                string rightName = right == null || right.Name == null ? string.Empty : right.Name.ToString();
                return string.Compare(leftName, rightName, StringComparison.Ordinal);
            });

            int activeWarCount = 0;
            HashSet<Kingdom> kingdomsAtWar = new HashSet<Kingdom>();
            int observedDailyIncome = 0;
            int rowIndex = 0;
            foreach (Kingdom kingdom in kingdoms)
            {
                if (kingdom != null && !kingdom.IsEliminated)
                {
                    int dailyIncome = CalculateKingdomDailyIncome(kingdom);
                    observedDailyIncome += dailyIncome;
                    _diplomacyRelations.Add(new CalendarDiplomacyRelationVM(
                        playerKingdom,
                        kingdom,
                        dailyIncome,
                        rowIndex++,
                        SendMessenger));
                }
            }

            for (int first = 0; first < kingdoms.Count; first++)
            {
                Kingdom left = kingdoms[first];
                if (left == null || left.IsEliminated) continue;
                for (int second = first + 1; second < kingdoms.Count; second++)
                {
                    Kingdom right = kingdoms[second];
                    if (right == null || right.IsEliminated || !left.IsAtWarWith(right)) continue;
                    activeWarCount++;
                    kingdomsAtWar.Add(left);
                    kingdomsAtWar.Add(right);
                }
            }

            ForeignOfficePeaceText = activeWarCount.ToString("N0");
            ForeignOfficeWarText = kingdomsAtWar.Count.ToString("N0");
            ForeignOfficeIncomeText = observedDailyIncome.ToString("N0") + " / day";
            ForeignOfficeRealmText = playerKingdom == null
                ? "INDEPENDENT CLAN  •  Global wars remain visible; bilateral peace clocks appear after you join or found a realm."
                : "YOUR REALM: " + (playerKingdom.Name == null ? "Unnamed kingdom" : playerKingdom.Name.ToString())
                    + "  •  Foreign courts observed: " + Math.Max(0, _diplomacyRelations.Count - 1).ToString("N0");
            DiplomacyText = _diplomacyRelations.Count == 0
                ? "No active foreign kingdoms could be found."
                : string.Empty;
            RefreshKingdomFinances(playerKingdom);
        }

        private void RefreshKingdomFinances(Kingdom playerKingdom)
        {
            if (playerKingdom == null || Campaign.Current == null || Campaign.Current.Models == null
                || Campaign.Current.Models.ClanFinanceModel == null)
            {
                KingdomFinanceRealmText = "KINGDOM FINANCES";
                KingdomTreasuryText = "0";
                KingdomIncomeText = "0 / day";
                KingdomExpensesText = "0 / day";
                KingdomNetText = "0 / day";
                KingdomFinanceStatusText = "Join or found a kingdom to open its treasury ledger.";
                NotifyKingdomFinanceRowStateChanged();
                return;
            }

            int totalTreasury = 0;
            int totalIncome = 0;
            int totalExpenses = 0;
            int totalNet = 0;
            int index = 0;
            List<Clan> clans = new List<Clan>(playerKingdom.Clans);
            clans.Sort(delegate(Clan left, Clan right)
            {
                if (left == Clan.PlayerClan && right != Clan.PlayerClan) return -1;
                if (right == Clan.PlayerClan && left != Clan.PlayerClan) return 1;
                string leftName = left == null || left.Name == null ? string.Empty : left.Name.ToString();
                string rightName = right == null || right.Name == null ? string.Empty : right.Name.ToString();
                return string.Compare(leftName, rightName, StringComparison.Ordinal);
            });

            foreach (Clan clan in clans)
            {
                if (clan == null || clan.IsEliminated) continue;
                int income = (int)Math.Round(Campaign.Current.Models.ClanFinanceModel.CalculateClanIncome(clan).ResultNumber);
                int expenses = Math.Abs((int)Math.Round(Campaign.Current.Models.ClanFinanceModel.CalculateClanExpenses(clan).ResultNumber));
                int net = (int)Math.Round(Campaign.Current.Models.ClanFinanceModel.CalculateClanGoldChange(clan).ResultNumber);
                totalTreasury += clan.Gold;
                totalIncome += income;
                totalExpenses += expenses;
                totalNet += net;
                _kingdomFinanceRows.Add(new CalendarKingdomFinanceRowVM(clan, income, expenses, net, index++));
            }

            string realmName = playerKingdom.Name == null ? "Unnamed kingdom" : playerKingdom.Name.ToString();
            KingdomFinanceRealmText = realmName.ToUpperInvariant() + "  •  " + _kingdomFinanceRows.Count.ToString("N0") + " clans in the royal ledger";
            KingdomTreasuryText = totalTreasury.ToString("N0") + " denars";
            KingdomIncomeText = "+" + totalIncome.ToString("N0") + " / day";
            KingdomExpensesText = "-" + totalExpenses.ToString("N0") + " / day";
            KingdomNetText = (totalNet >= 0 ? "+" : string.Empty) + totalNet.ToString("N0") + " / day";
            KingdomFinanceStatusText = _kingdomFinanceRows.Count == 0 ? "No active clans could be found in this kingdom." : string.Empty;
            NotifyKingdomFinanceRowStateChanged();
        }

        private void NotifyKingdomFinanceRowStateChanged()
        {
            OnPropertyChangedWithValue(HasKingdomFinanceRows, "HasKingdomFinanceRows");
            OnPropertyChangedWithValue(ShowKingdomFinanceEmptyState, "ShowKingdomFinanceEmptyState");
            OnPropertyChangedWithValue(IsKingdomFinanceLedgerVisible, "IsKingdomFinanceLedgerVisible");
            OnPropertyChangedWithValue(IsKingdomFinanceEmptyVisible, "IsKingdomFinanceEmptyVisible");
        }

        private static int CalculateKingdomDailyIncome(Kingdom kingdom)
        {
            if (kingdom == null || Campaign.Current == null || Campaign.Current.Models == null
                || Campaign.Current.Models.ClanFinanceModel == null) return 0;
            float total = 0f;
            foreach (Clan clan in kingdom.Clans)
            {
                if (clan == null || clan.IsEliminated) continue;
                total += Campaign.Current.Models.ClanFinanceModel.CalculateClanIncome(clan).ResultNumber;
            }
            return (int)Math.Round(total);
        }

        private void SendMessenger(Kingdom targetKingdom)
        {
            Hero ruler = targetKingdom == null ? null : targetKingdom.Leader;
            if (ruler == null || ruler.CharacterObject == null || !ruler.IsAlive || ruler.IsPrisoner)
            {
                DiplomacyText = "That court cannot receive a messenger at present.";
                return;
            }
            if (CharacterObject.PlayerCharacter == null || PartyBase.MainParty == null || Campaign.Current == null)
            {
                DiplomacyText = "A campaign party is required before a messenger can be dispatched.";
                return;
            }

            int daysRemaining = CalendarWorldLedgerBehavior.GetMessengerDaysRemaining(targetKingdom);
            if (daysRemaining < 0)
            {
                int travelDays;
                if (CalendarWorldLedgerBehavior.DispatchMessenger(targetKingdom, out travelDays))
                {
                    RefreshDiplomacy();
                    DiplomacyText = "A messenger has departed for "
                        + (ruler.Name == null ? "the foreign court" : ruler.Name.ToString())
                        + ". The campaign route is expected to take " + travelDays.ToString("N0")
                        + (travelDays == 1 ? " day." : " days.");
                    Diagnostics.Info("Diplomacy messenger dispatched to "
                        + (ruler.Name == null ? "an unnamed ruler" : ruler.Name.ToString())
                        + "; campaign-distance ETA=" + travelDays.ToString("N0") + " days.");
                }
                return;
            }
            if (daysRemaining > 0)
            {
                DiplomacyText = "Your messenger is still travelling. Expected arrival in "
                    + daysRemaining.ToString("N0") + (daysRemaining == 1 ? " day." : " days.");
                return;
            }
            try
            {
                Diagnostics.Info("Diplomacy audience opened after messenger arrival for "
                    + (ruler.Name == null ? "an unnamed ruler" : ruler.Name.ToString()) + ".");
                if (_close != null) _close();
                CampaignMapConversation.OpenConversation(
                    new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty),
                    new ConversationCharacterData(ruler.CharacterObject));
                CalendarWorldLedgerBehavior.ConsumeArrivedMessenger(targetKingdom);
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Diplomacy could not open the remote conversation.", exception);
                InformationManager.DisplayMessage(new InformationMessage(
                    "The audience could not be opened with "
                    + (ruler.Name == null ? "that ruler" : ruler.Name.ToString()) + "."));
            }
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
            BuildStrategicKingdomLabels(markerPoints);
            BuildStrategicMarkers(markerPoints);
            RefreshStrategicFriendlyArmies();
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
            float minimumSeparation = Math.Max(
                StrategicMarkerMinimumSeparation,
                CalendarSettingsState.StrategicMapMarkerSpacing);
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
                float minimumSeparation = Math.Max(
                    StrategicMarkerMinimumSeparation,
                    CalendarSettingsState.StrategicMapMarkerSpacing);
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

        internal static bool TryGetReferenceAnchor(string settlementId, out Vec2 referenceAnchor)
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
            double[] xCoefficients;
            double[] yCoefficients;
            if (!TryGetCampaignToReferenceProjection(out xCoefficients, out yCoefficients))
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

        internal static bool TryGetCampaignToReferenceProjection(out double[] xCoefficients, out double[] yCoefficients)
        {
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

            if (samples < 3
                || !TrySolveThreeByThree(normal, nativeX, out xCoefficients)
                || !TrySolveThreeByThree(normal, nativeY, out yCoefficients))
            {
                xCoefficients = null;
                yCoefficients = null;
                return false;
            }
            return true;
        }

        internal static Vec2 ProjectCampaignPositionToStrategicMap(Vec2 campaignPosition)
        {
            Vec2 projected = ProjectNativeSettlementPosition(campaignPosition);
            return new Vec2(
                Math.Max(0f, Math.Min(CalendarStrategicMapLayout.SourceWidth,
                    projected.x - CalendarStrategicMapLayout.CropLeft)),
                Math.Max(0f, Math.Min(CalendarStrategicMapLayout.SourceHeight,
                    projected.y - CalendarStrategicMapLayout.CropTop)));
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
                    true,
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

        private void RefreshStrategicKingdomRows()
        {
            _strategicKingdomRows.Clear();
            if (Campaign.Current == null) return;

            List<Kingdom> kingdoms = new List<Kingdom>();
            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom != null && !kingdom.IsEliminated) kingdoms.Add(kingdom);
            }
            kingdoms.Sort(delegate(Kingdom left, Kingdom right)
            {
                return string.Compare(
                    left.Name == null ? string.Empty : left.Name.ToString(),
                    right.Name == null ? string.Empty : right.Name.ToString(),
                    StringComparison.CurrentCultureIgnoreCase);
            });

            for (int index = 0; index < kingdoms.Count; index++)
            {
                Kingdom kingdom = kingdoms[index];
                long wealth = 0L;
                int clanCount = 0;
                foreach (Clan clan in Clan.All)
                {
                    if (clan == null || !ReferenceEquals(clan.Kingdom, kingdom)) continue;
                    clanCount++;
                    wealth += clan.Gold;
                }

                int fieldedTroops = 0;
                foreach (MobileParty party in MobileParty.All)
                {
                    if (party == null || !ReferenceEquals(party.MapFaction, kingdom) || party.MemberRoster == null) continue;
                    fieldedTroops += party.MemberRoster.TotalManCount;
                }

                int townCount = 0;
                int castleCount = 0;
                foreach (Settlement settlement in Settlement.All)
                {
                    if (settlement == null || settlement.Town == null) continue;
                    IFaction owner = CalendarWorldLedgerBehavior.GetLiveSettlementFaction(settlement);
                    if (!ReferenceEquals(owner, kingdom)) continue;
                    if (settlement.IsTown) townCount++;
                    else if (settlement.IsCastle) castleCount++;
                }

                _strategicKingdomRows.Add(new CalendarStrategicKingdomSummaryVM(
                    kingdom,
                    wealth,
                    fieldedTroops,
                    clanCount,
                    townCount,
                    castleCount,
                    index));
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
            OnPropertyChangedWithValue(ShowStrategicKingdomRows, "ShowStrategicKingdomRows");
            OnPropertyChangedWithValue(ShowStrategicSettlementDetails, "ShowStrategicSettlementDetails");
            OnPropertyChangedWithValue(IsSelectedStrategicSettlementTracked, "IsSelectedStrategicSettlementTracked");
            OnPropertyChangedWithValue(TrackSelectedSettlementText, "TrackSelectedSettlementText");
            OnPropertyChangedWithValue(StrategicSummaryScrollerHeight, "StrategicSummaryScrollerHeight");
            OnPropertyChangedWithValue(StrategicSummaryScrollerMarginBottom, "StrategicSummaryScrollerMarginBottom");
            OnPropertyChangedWithValue(StrategicSummaryContentTop, "StrategicSummaryContentTop");
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
            CalendarStrategicCampaignAtlasTextureProvider.UpdateMapState(ownerColorsBySettlementId, markerPoints);
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
            if (!CanMoveDisplayedCalendarMonth(direction))
            {
                Diagnostics.Info("Calendar month navigation ignored: direction=" + direction.ToString("N0")
                    + "; displayed=" + _displayCalendarYear.ToString("N0") + "/" + _displayCalendarMonth.ToString("N0") + ".");
                return;
            }
            // A record belongs to one absolute day. Do not carry that record
            // into another month after its grid has changed beneath it.
            if (_selectedCalendarDay != long.MinValue)
            {
                _selectedCalendarDay = long.MinValue;
                OnPropertyChangedWithValue(HasSelectedCalendarDay, "HasSelectedCalendarDay");
                RefreshCalendarNotes();
            }
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
            Diagnostics.Info("Calendar month displayed: year=" + _displayCalendarYear.ToString("N0")
                + "; month=" + _displayCalendarMonth.ToString("N0") + ".");
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
            // The visible page binds to Days, not the historical CalendarMonths
            // archive. Updating every archived month made one click walk years
            // of cells while leaving the 42 live cells visually stale.
            foreach (CalendarWorldCalendarDayVM entry in _days)
            {
                entry.IsSelected = entry.IsSelectable && entry.AbsoluteDay == _selectedCalendarDay;
            }
            OnPropertyChangedWithValue(HasSelectedCalendarDay, "HasSelectedCalendarDay");
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
            // The screen refreshes its live campaign data once per second. Do
            // not rebuild an unchanged archive: clearing this binding list
            // destroys the user's expanded-record state and resets the
            // scroller, which made a record appear to close immediately after
            // OPEN RECORD was clicked.
            Dictionary<string, bool> expandedByTitle = new Dictionary<string, bool>(StringComparer.Ordinal);
            for (int index = 0; index < _savedSummaries.Count; index++)
            {
                CalendarWorldSavedSummaryVM existing = _savedSummaries[index];
                if (existing != null && existing.IsExpanded)
                {
                    expandedByTitle[GetSavedSummaryIdentity(existing.Title)] = true;
                }
            }

            List<CalendarWorldSavedSummaryVM> refreshed = new List<CalendarWorldSavedSummaryVM>();
            for (int year = currentYear; year >= firstYear; year--)
            {
                int startMonth = year == firstYear ? firstMonth : FirstCalendarMonth;
                int endMonth = year == currentYear ? currentMonth : LastCalendarMonth;
                int yearImportantCount;
                int monthsWithEvents;
                string yearText = BuildYearImportantSummary(year, startMonth, endMonth, out yearImportantCount, out monthsWithEvents);
                int yearBodyHeight = yearImportantCount == 0
                    ? 86
                    : Math.Max(150, 70 + (yearImportantCount * 20) + (monthsWithEvents * 28));
                string yearTitle = year + " YEARLY SUMMARY — " + yearImportantCount + "/120 important events";
                refreshed.Add(new CalendarWorldSavedSummaryVM(
                    yearTitle,
                    yearText,
                    yearBodyHeight,
                    expandedByTitle.ContainsKey(GetSavedSummaryIdentity(yearTitle))));

                for (int month = endMonth; month >= startMonth; month--)
                {
                    bool leapYear = CalendarTimeMath.IsLeapYear(year);
                    int monthLength = CalendarTimeMath.GetMonthLength(month, leapYear);
                    long monthStart = CalendarTimeMath.DaysBeforeYear(year) + CalendarTimeMath.GetMonthStart(month, leapYear);
                    int eventCount = CalendarWorldLedgerBehavior.CountRecordedEntries(monthStart, monthStart + monthLength);
                    string monthText = CalendarWorldLedgerBehavior.GetImportantEventsText(monthStart, monthStart + monthLength, monthLength, true);
                    int monthBodyHeight = eventCount == 0
                        ? 86
                        : Math.Max(130, 70 + (Math.Min(monthLength, eventCount) * 22));
                    string monthTitle = CalendarSettingsState.GetMonthName(month) + " " + year + " MONTHLY SUMMARY — " + Math.Min(monthLength, eventCount) + "/" + monthLength + " important events";
                    refreshed.Add(new CalendarWorldSavedSummaryVM(
                        monthTitle,
                        monthText,
                        monthBodyHeight,
                        expandedByTitle.ContainsKey(GetSavedSummaryIdentity(monthTitle))));
                }
            }

            bool changed = refreshed.Count != _savedSummaries.Count;
            for (int index = 0; !changed && index < refreshed.Count; index++)
            {
                CalendarWorldSavedSummaryVM current = _savedSummaries[index];
                CalendarWorldSavedSummaryVM next = refreshed[index];
                changed = current == null ||
                    !string.Equals(current.Title, next.Title, StringComparison.Ordinal) ||
                    !string.Equals(current.SummaryText, next.SummaryText, StringComparison.Ordinal) ||
                    current.BodyHeight != next.BodyHeight;
            }

            if (!changed) return;

            _savedSummaries.Clear();
            for (int index = 0; index < refreshed.Count; index++)
            {
                _savedSummaries.Add(refreshed[index]);
            }
        }

        private static string GetSavedSummaryIdentity(string title)
        {
            if (string.IsNullOrEmpty(title)) return string.Empty;
            int countSeparator = title.IndexOf(" — ", StringComparison.Ordinal);
            return countSeparator < 0 ? title : title.Substring(0, countSeparator);
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

    internal static class CalendarDiplomacyDecisionFactory
    {
        internal static KingdomDecision Create(Clan proposer, Kingdom targetKingdom, bool makePeace)
        {
            if (proposer == null || targetKingdom == null) return null;
            if (!makePeace) return new DeclareWarDecision(proposer, targetKingdom);

            int durationInDays = 0;
            int dailyTributeToPay = 0;
            if (Campaign.Current != null && Campaign.Current.Models != null
                && Campaign.Current.Models.DiplomacyModel != null
                && targetKingdom.Leader != null && targetKingdom.Leader.Clan != null)
            {
                dailyTributeToPay = Campaign.Current.Models.DiplomacyModel.GetDailyTributeToPay(
                    proposer,
                    targetKingdom.Leader.Clan,
                    out durationInDays);
                dailyTributeToPay = 10 * (dailyTributeToPay / 10);
            }

            return new MakePeaceKingdomDecision(
                proposer,
                targetKingdom,
                dailyTributeToPay,
                durationInDays);
        }

        internal static bool HasPendingDecision(Kingdom playerKingdom, Kingdom targetKingdom, bool makePeace)
        {
            if (playerKingdom == null || targetKingdom == null) return false;
            foreach (KingdomDecision pending in playerKingdom.UnresolvedDecisions)
            {
                if (pending == null || pending.ShouldBeCancelled()) continue;
                MakePeaceKingdomDecision peace = pending as MakePeaceKingdomDecision;
                if (makePeace && peace != null && peace.FactionToMakePeaceWith == targetKingdom) return true;
                DeclareWarDecision war = pending as DeclareWarDecision;
                if (!makePeace && war != null && war.FactionToDeclareWarOn == targetKingdom) return true;
            }
            return false;
        }
    }

    internal sealed class CalendarMarriageCandidateVM : ViewModel
    {
        private readonly Hero _candidate;
        private readonly Hero _clanLeader;
        private readonly bool _isCompatible;
        private readonly bool _canPursueMatch;
        private readonly Action<Hero, string> _sendMessenger;

        internal CalendarMarriageCandidateVM(Hero player, Hero candidate, int index, Action<Hero, string> sendMessenger)
        {
            _candidate = candidate;
            _clanLeader = candidate == null || candidate.Clan == null ? null : candidate.Clan.Leader;
            _sendMessenger = sendMessenger;
            _isCompatible = player != null && candidate != null && player.Spouse == null && candidate.Spouse == null
                && Campaign.Current != null && Campaign.Current.Models != null && Campaign.Current.Models.MarriageModel != null
                && Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(player, candidate)
                && !FactionManager.IsAtWarAgainstFaction(player.MapFaction, candidate.MapFaction);
            _canPursueMatch = player != null && candidate != null && player.Spouse == null && candidate.Spouse == null
                && !FactionManager.IsAtWarAgainstFaction(player.MapFaction, candidate.MapFaction);
            IsEven = index % 2 == 0;
            Portrait = candidate == null || candidate.CharacterObject == null
                ? new CharacterImageIdentifierVM(null)
                : new CharacterImageIdentifierVM(CharacterCode.CreateFrom(candidate.CharacterObject));
        }

        [DataSourceProperty] public string Name { get { return _candidate == null || _candidate.Name == null ? "Unknown noble" : _candidate.Name.ToString(); } }
        [DataSourceProperty] public string GenderText { get { return _candidate != null && _candidate.IsFemale ? "WOMAN" : "MAN"; } }
        [DataSourceProperty] public string DetailsText
        {
            get
            {
                string clan = _candidate == null || _candidate.Clan == null || _candidate.Clan.Name == null ? "No clan" : _candidate.Clan.Name.ToString();
                string kingdom = _candidate == null || _candidate.Clan == null || _candidate.Clan.Kingdom == null || _candidate.Clan.Kingdom.Name == null
                    ? "Independent" : _candidate.Clan.Kingdom.Name.ToString();
                return clan + "  •  " + kingdom + "  •  Age " + (_candidate == null ? "?" : _candidate.Age.ToString("0"));
            }
        }
        [DataSourceProperty] public string ClanLeaderText { get { return "Clan leader: " + (_clanLeader == null || _clanLeader.Name == null ? "None" : _clanLeader.Name.ToString()); } }
        [DataSourceProperty] public string EligibilityText
        {
            get
            {
                if (_isCompatible) return "NATIVE COURTSHIP AVAILABLE";
                if (_candidate != null && (_candidate.PartyBelongedTo == null || (_candidate.PartyBelongedTo.MapEvent == null && _candidate.PartyBelongedTo.Army == null)))
                    return "MESSENGER AVAILABLE • COURTSHIP CHECKED AT AUDIENCE";
                return "CAMPAIGNING • MESSENGER CAN STILL TRAVEL";
            }
        }
        [DataSourceProperty] public string EligibilityColor { get { return _isCompatible ? "#87BD82FF" : "#C9A55EFF"; } }
        [DataSourceProperty] public CharacterImageIdentifierVM Portrait { get; private set; }
        [DataSourceProperty] public bool IsEven { get; private set; }
        [DataSourceProperty] public bool CanContactCandidate { get { return _canPursueMatch && IsAvailable(_candidate); } }
        [DataSourceProperty] public bool CanContactClanLeader { get { return _canPursueMatch && IsAvailable(_clanLeader); } }
        [DataSourceProperty] public string CandidateButtonText { get { return MessengerButtonText(_candidate, "Candidate", "SEND TO CANDIDATE"); } }
        [DataSourceProperty] public string ClanLeaderButtonText { get { return MessengerButtonText(_clanLeader, "ClanLeader", "SEND TO CLAN LEADER"); } }

        private static bool IsAvailable(Hero hero)
        {
            return hero != null && hero.IsAlive && !hero.IsPrisoner && hero.CharacterObject != null;
        }

        private static string MessengerButtonText(Hero target, string purpose, string idleText)
        {
            int days = CalendarWorldLedgerBehavior.GetMarriageMessengerDaysRemaining(target, purpose);
            if (days < 0) return idleText;
            if (days == 0) return "OPEN ARRANGED AUDIENCE";
            return "ARRIVES IN " + days.ToString("N0") + (days == 1 ? " DAY" : " DAYS");
        }

        public void ExecuteContactCandidate()
        {
            if (CanContactCandidate && _sendMessenger != null) _sendMessenger(_candidate, "Candidate");
        }

        public void ExecuteContactClanLeader()
        {
            if (CanContactClanLeader && _sendMessenger != null) _sendMessenger(_clanLeader, "ClanLeader");
        }
    }

    internal sealed class CalendarCompanionRecordVM : ViewModel
    {
        internal CalendarCompanionRecordVM(Hero hero, int index)
        {
            string culture = hero == null || hero.Culture == null || hero.Culture.Name == null
                ? "Unknown culture" : hero.Culture.Name.ToString();
            Name = hero == null || hero.Name == null ? "UNNAMED COMPANION" : hero.Name.ToString().ToUpperInvariant();
            Subtitle = culture + "  •  Age " + (hero == null ? "?" : hero.Age.ToString("0"));
            string background = hero == null || hero.EncyclopediaText == null ? string.Empty : hero.EncyclopediaText.ToString();
            BackgroundText = string.IsNullOrWhiteSpace(background)
                ? "No encyclopedia backstory is available for this companion."
                : background.Trim();
            int kills;
            int knockouts;
            CalendarWorldLedgerBehavior.GetCombatStatistics(hero, out kills, out knockouts);
            KillsText = kills.ToString("N0");
            KnockoutsText = knockouts.ToString("N0");
            Portrait = hero == null || hero.CharacterObject == null
                ? new CharacterImageIdentifierVM(null)
                : new CharacterImageIdentifierVM(CharacterCode.CreateFrom(hero.CharacterObject));
            IsEven = index % 2 == 0;
        }

        [DataSourceProperty] public string Name { get; private set; }
        [DataSourceProperty] public string Subtitle { get; private set; }
        [DataSourceProperty] public string BackgroundText { get; private set; }
        [DataSourceProperty] public string KillsText { get; private set; }
        [DataSourceProperty] public string KnockoutsText { get; private set; }
        [DataSourceProperty] public CharacterImageIdentifierVM Portrait { get; private set; }
        [DataSourceProperty] public bool IsEven { get; private set; }
    }

    internal sealed class CalendarStrategicKingdomSummaryVM : ViewModel
    {
        private readonly BannerImageIdentifierVM _banner;

        internal CalendarStrategicKingdomSummaryVM(
            Kingdom kingdom,
            long wealth,
            int fieldedTroops,
            int clanCount,
            int townCount,
            int castleCount,
            int index)
        {
            _banner = new BannerImageIdentifierVM(kingdom == null ? null : kingdom.Banner, true);
            Name = kingdom == null || kingdom.Name == null ? "Unknown kingdom" : kingdom.Name.ToString();
            Hero leader = kingdom == null ? null : kingdom.Leader;
            Clan rulingClan = kingdom == null ? null : kingdom.RulingClan;
            LeaderText = "Leader: " + (leader == null || leader.Name == null ? "Unknown" : leader.Name.ToString());
            RulingClanText = "Ruling clan: " + (rulingClan == null || rulingClan.Name == null ? "Unknown" : rulingClan.Name.ToString());
            StrengthText = "Wealth " + wealth.ToString("N0") + "  •  Fielded " + fieldedTroops.ToString("N0");
            HoldingsText = "Clans " + clanCount.ToString("N0") + "  •  Towns " + townCount.ToString("N0") + "  •  Castles " + castleCount.ToString("N0");
            IsEven = index % 2 == 0;
        }

        [DataSourceProperty] public BannerImageIdentifierVM Banner { get { return _banner; } }
        [DataSourceProperty] public string Name { get; private set; }
        [DataSourceProperty] public string LeaderText { get; private set; }
        [DataSourceProperty] public string RulingClanText { get; private set; }
        [DataSourceProperty] public string StrengthText { get; private set; }
        [DataSourceProperty] public string HoldingsText { get; private set; }
        [DataSourceProperty] public bool IsEven { get; private set; }
    }

    internal sealed class CalendarKingdomFinanceRowVM : ViewModel
    {
        internal CalendarKingdomFinanceRowVM(Clan clan, int income, int expenses, int net, int index)
        {
            Name = clan == null || clan.Name == null ? "Unknown clan" : clan.Name.ToString();
            Hero leader = clan == null ? null : clan.Leader;
            LeaderText = "Leader: " + (leader == null || leader.Name == null ? "Unknown" : leader.Name.ToString());
            TreasuryText = (clan == null ? 0 : clan.Gold).ToString("N0") + " denars";
            IncomeText = "+" + income.ToString("N0") + " / day";
            ExpensesText = "-" + expenses.ToString("N0") + " / day";
            NetText = (net >= 0 ? "+" : string.Empty) + net.ToString("N0") + " / day";
            NetColor = net >= 0 ? "#88C98AFF" : "#D27C6FFF";
            FiefsText = (clan == null ? 0 : clan.Fiefs.Count).ToString("N0") + " fiefs";
            IsPlayerClan = clan == Clan.PlayerClan;
            IsEven = index % 2 == 0;
        }

        [DataSourceProperty] public string Name { get; private set; }
        [DataSourceProperty] public string LeaderText { get; private set; }
        [DataSourceProperty] public string TreasuryText { get; private set; }
        [DataSourceProperty] public string IncomeText { get; private set; }
        [DataSourceProperty] public string ExpensesText { get; private set; }
        [DataSourceProperty] public string NetText { get; private set; }
        [DataSourceProperty] public string NetColor { get; private set; }
        [DataSourceProperty] public string FiefsText { get; private set; }
        [DataSourceProperty] public bool IsPlayerClan { get; private set; }
        [DataSourceProperty] public bool IsEven { get; private set; }
    }

    internal sealed class CalendarDiplomacyRelationVM : ViewModel
    {
        private readonly Kingdom _playerKingdom;
        private readonly Kingdom _otherKingdom;
        private readonly Action<Kingdom> _sendMessenger;
        private readonly BannerImageIdentifierVM _banner;

        internal CalendarDiplomacyRelationVM(Kingdom playerKingdom, Kingdom otherKingdom, int dailyIncome, int index, Action<Kingdom> sendMessenger)
        {
            _playerKingdom = playerKingdom;
            _otherKingdom = otherKingdom;
            _sendMessenger = sendMessenger;
            _banner = new BannerImageIdentifierVM(otherKingdom == null ? null : otherKingdom.Banner, true);
            DailyIncomeText = (dailyIncome >= 0 ? "+" : string.Empty) + dailyIncome.ToString("N0") + " denars / day";
            DailyIncomeColor = dailyIncome >= 0 ? "#8CC47CFF" : "#D36C5CFF";
            IsEven = index % 2 == 0;
        }

        [DataSourceProperty] public BannerImageIdentifierVM Banner { get { return _banner; } }
        [DataSourceProperty] public string Name { get { return _otherKingdom == null || _otherKingdom.Name == null ? "Unknown kingdom" : _otherKingdom.Name.ToString(); } }
        [DataSourceProperty] public bool IsAtWar { get { return _playerKingdom != null && _otherKingdom != null && _playerKingdom.IsAtWarWith(_otherKingdom); } }
        [DataSourceProperty] public bool IsEven { get; private set; }
        [DataSourceProperty] public string DailyIncomeText { get; private set; }
        [DataSourceProperty] public string DailyIncomeColor { get; private set; }
        [DataSourceProperty] public string RelationColor { get { return IsAtWar ? "#A63D31FF" : "#4B9C2FFF"; } }
        [DataSourceProperty] public string RulerText
        {
            get
            {
                Hero ruler = _otherKingdom == null ? null : _otherKingdom.Leader;
                return "Ruler: " + (ruler == null || ruler.Name == null ? "Unknown" : ruler.Name.ToString());
            }
        }
        [DataSourceProperty] public string StatusText { get { return _otherKingdom == _playerKingdom ? "YOUR KINGDOM" : (_playerKingdom == null ? "FOREIGN KINGDOM" : (IsAtWar ? "AT WAR" : "AT PEACE")); } }
        [DataSourceProperty] public string ActiveWarsText
        {
            get
            {
                if (_otherKingdom == null) return "No war information";
                List<string> enemies = new List<string>();
                foreach (Kingdom kingdom in Kingdom.All)
                {
                    if (kingdom == null || kingdom.IsEliminated || kingdom == _otherKingdom) continue;
                    if (_otherKingdom.IsAtWarWith(kingdom))
                    {
                        enemies.Add(kingdom.Name == null ? "Unknown kingdom" : kingdom.Name.ToString());
                    }
                }
                enemies.Sort(StringComparer.Ordinal);
                return enemies.Count == 0
                    ? "No active wars"
                    : "At war with: " + string.Join(", ", enemies.ToArray());
            }
        }
        [DataSourceProperty] public string PeaceDurationText
        {
            get
            {
                if (_otherKingdom == _playerKingdom) return "Seat of your realm";
                if (_playerKingdom == null || _otherKingdom == null) return ActiveWarsText;
                StanceLink stance = _playerKingdom.GetStanceWith(_otherKingdom);
                if (stance == null) return "No relation record";
                CampaignTime date = IsAtWar ? stance.WarStartDate : stance.PeaceDeclarationDate;
                int days = date.ToDays <= 0d ? 0 : Math.Max(0, (int)Math.Floor(date.ElapsedDaysUntilNow));
                return IsAtWar
                    ? "War for " + days.ToString("N0") + (days == 1 ? " day" : " days")
                    : "Peace held for " + days.ToString("N0") + (days == 1 ? " day" : " days");
            }
        }
        [DataSourceProperty] public string TributeText
        {
            get
            {
                if (_playerKingdom == null || _otherKingdom == null) return string.Empty;
                StanceLink stance = _playerKingdom.GetStanceWith(_otherKingdom);
                int tribute = stance == null ? 0 : stance.GetDailyTributeToPay(_playerKingdom);
                if (tribute > 0) return "Tribute owed: " + tribute.ToString("N0") + "/day";
                if (tribute < 0) return "Tribute received: " + (-tribute).ToString("N0") + "/day";
                return "No current tribute";
            }
        }
        [DataSourceProperty] public bool CanSendMessenger
        {
            get
            {
                Hero ruler = _otherKingdom == null ? null : _otherKingdom.Leader;
                return _otherKingdom != _playerKingdom && ruler != null && ruler != Hero.MainHero && ruler.IsAlive && !ruler.IsPrisoner
                    && ruler.CharacterObject != null && !_otherKingdom.IsEliminated;
            }
        }

        [DataSourceProperty] public string MessengerButtonText
        {
            get
            {
                int daysRemaining = CalendarWorldLedgerBehavior.GetMessengerDaysRemaining(_otherKingdom);
                if (daysRemaining < 0) return "SEND MESSENGER";
                if (daysRemaining == 0) return "OPEN AUDIENCE";
                return "ARRIVES IN " + daysRemaining.ToString("N0") + (daysRemaining == 1 ? " DAY" : " DAYS");
            }
        }

        public void ExecuteSendMessenger()
        {
            if (CanSendMessenger && _sendMessenger != null) _sendMessenger(_otherKingdom);
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
        [DataSourceProperty] public bool IsToday { get { return _isToday; } }
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
                if (_isSelected) return "#75562EAA";
                return "#FFFFFF00";
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
        [DataSourceProperty] public bool IsExpanded { get { return _isExpanded; } private set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChangedWithValue(value, "IsExpanded"); OnPropertyChangedWithValue(_isExpanded ? "-" : "+", "ExpandGlyph"); OnPropertyChangedWithValue(ActionText, "ActionText"); } }
        [DataSourceProperty] public string ExpandGlyph { get { return _isExpanded ? "-" : "+"; } }
        [DataSourceProperty] public string ActionText { get { return _isExpanded ? "CLOSE RECORD  «" : "OPEN RECORD  »"; } }
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
        private readonly bool _showLabel;
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
            bool showLabel,
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
            _showLabel = showLabel && isTown;
            _isUnderSiege = isUnderSiege;
            _isSelected = isSelected;
            _select = select;
        }
        internal Settlement Settlement { get { return _settlement; } }
        [DataSourceProperty] public int X { get { return _x; } }
        [DataSourceProperty] public int Y { get { return _y; } }
        [DataSourceProperty] public string Color { get { return _color; } }
        [DataSourceProperty] public string Label { get { return _settlement == null ? string.Empty : _settlement.Name.ToString(); } }
        [DataSourceProperty] public bool ShowLabel { get { return _showLabel; } }
        [DataSourceProperty] public bool IsTown { get { return _isTown; } }
        [DataSourceProperty] public bool IsCastle { get { return !_isTown; } }
        [DataSourceProperty] public bool IsUnderSiege { get { return _isUnderSiege; } }
        [DataSourceProperty] public int Size { get { return _size; } }
        [DataSourceProperty] public int IconSize { get { return _iconSize; } }
        [DataSourceProperty] public string BorderColor { get { return _isSelected ? "#FFE3A3FF" : "#100D0BFF"; } }
        [DataSourceProperty] public string GlowColor { get { return _isSelected ? "#FFE3A3A8" : "#00000000"; } }
        [DataSourceProperty] public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChangedWithValue(value, "IsSelected");
                OnPropertyChangedWithValue(BorderColor, "BorderColor");
                OnPropertyChangedWithValue(GlowColor, "GlowColor");
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

    internal sealed class CalendarWarStatisticsRowVM : ViewModel
    {
        private readonly CalendarWarStatisticsRecord _record;
        private readonly Action<CalendarWarStatisticsRecord, bool> _concludeWar;

        internal CalendarWarStatisticsRowVM(CalendarWarStatisticsRecord record, int index, Action<CalendarWarStatisticsRecord, bool> concludeWar)
        {
            _record = record;
            _concludeWar = concludeWar;
            LeftName = record.LeftName.ToUpperInvariant();
            RightName = record.RightName.ToUpperInvariant();
            LeftTroopsText = record.LeftTroops.ToString("N0") + " troops  •  " + record.LeftShips.ToString("N0") + " ships";
            RightTroopsText = record.RightTroops.ToString("N0") + " troops  •  " + record.RightShips.ToString("N0") + " ships";
            LeftLossesText = record.LeftLosses.ToString("N0") + " troop losses  •  " + record.LeftShipLosses.ToString("N0") + " ships lost";
            RightLossesText = record.RightLosses.ToString("N0") + " troop losses  •  " + record.RightShipLosses.ToString("N0") + " ships lost";
            ConflictTotalText = record.TotalLosses.ToString("N0") + " total casualties";
            WarScoreText = "WAR SCORE  " + record.WarScore.ToString("+0;-0;0") + "%";
            int magnitude = Math.Abs(record.WarScore);
            LeftAdvantageWidth = record.WarScore > 0 ? Math.Min(76, record.WarScore * 76 / 100) : 0;
            RightAdvantageWidth = record.WarScore < 0 ? Math.Min(76, -record.WarScore * 76 / 100) : 0;
            WarScoreStatusText = magnitude >= 100
                ? "SURRENDER AVAILABLE"
                : (magnitude >= 50 ? "WHITE PEACE AVAILABLE" : (50 - magnitude).ToString("N0") + "% TO WHITE PEACE");
            ResolutionText = magnitude >= 100 ? "ENFORCE SURRENDER" : "WHITE PEACE";
            CanResolveWar = record.IsPlayerWar && magnitude >= 50;
            IsPlayerWar = record.IsPlayerWar;
            IsEven = index % 2 == 0;
        }

        [DataSourceProperty] public string LeftName { get; private set; }
        [DataSourceProperty] public string RightName { get; private set; }
        [DataSourceProperty] public string LeftTroopsText { get; private set; }
        [DataSourceProperty] public string RightTroopsText { get; private set; }
        [DataSourceProperty] public string LeftLossesText { get; private set; }
        [DataSourceProperty] public string RightLossesText { get; private set; }
        [DataSourceProperty] public string ConflictTotalText { get; private set; }
        [DataSourceProperty] public string WarScoreText { get; private set; }
        [DataSourceProperty] public string WarScoreStatusText { get; private set; }
        [DataSourceProperty] public int LeftAdvantageWidth { get; private set; }
        [DataSourceProperty] public int RightAdvantageWidth { get; private set; }
        [DataSourceProperty] public string ResolutionText { get; private set; }
        [DataSourceProperty] public bool CanResolveWar { get; private set; }
        [DataSourceProperty] public bool IsPlayerWar { get; private set; }
        [DataSourceProperty] public bool IsEven { get; private set; }
        public void ExecuteResolveWar()
        {
            if (CanResolveWar && _concludeWar != null) _concludeWar(_record, Math.Abs(_record.WarScore) >= 100);
        }
    }

    internal sealed class CalendarWorldLedgerTabVM : ViewModel
    {
        private readonly Action<CalendarWorldLedgerTabVM> _select;
        private readonly string _baseLabel;
        private bool _isSelected;
        private string _label;
        internal CalendarWorldLedgerTabVM(
            string filter,
            string label,
            Action<CalendarWorldLedgerTabVM> select)
        {
            Filter = filter;
            _baseLabel = label;
            _label = label;
            _select = select;
        }
        internal string Filter { get; private set; }
        [DataSourceProperty] public string Label { get { return _label; } private set { if (_label == value) return; _label = value; OnPropertyChangedWithValue(value, "Label"); } }
        [DataSourceProperty] public int TabWidth
        {
            get
            {
                // Four equal slots fill the authored 1328-pixel tab rail.
                // Keeping this single source of truth prevents cumulative
                // drift between hit targets and selected artwork.
                return 332;
            }
        }
        [DataSourceProperty] public string SelectedTabTextureProvider
        {
            get
            {
                switch (Filter)
                {
                    case "Character": return "WorldEventsExactSelectedStoryTextureProvider";
                    case "Diplomacy": return "WorldEventsExactSelectedRealmTextureProvider";
                    case "Strategic": return "WorldEventsExactSelectedStrategicTextureProvider";
                    default: return "WorldEventsExactSelectedCalendarTextureProvider";
                }
            }
        }
        [DataSourceProperty] public string InactiveTabTextureProvider
        {
            get
            {
                switch (Filter)
                {
                    case "Character": return "WorldEventsExactInactiveStoryTextureProvider";
                    case "Diplomacy": return "WorldEventsExactInactiveRealmTextureProvider";
                    case "Strategic": return "WorldEventsExactInactiveStrategicTextureProvider";
                    default: return "WorldEventsExactInactiveCalendarTextureProvider";
                }
            }
        }
        [DataSourceProperty] public string SelectedTabGoldFrameProvider
        {
            get
            {
                switch (Filter)
                {
                    case "Character": return "WorldEventsGoldFrameStoryTextureProvider";
                    case "Companions": return "WorldEventsGoldFrameCompanionsTextureProvider";
                    case "Diplomacy": return "WorldEventsGoldFrameDiplomacyTextureProvider";
                    case "Wars": return "WorldEventsGoldFrameWarTextureProvider";
                    case "Summaries": return "WorldEventsGoldFrameSummariesTextureProvider";
                    case "Strategic": return "WorldEventsGoldFrameMapTextureProvider";
                    default: return "WorldEventsGoldFrameCalendarTextureProvider";
                }
            }
        }
        [DataSourceProperty] public string SkinIconProvider
        {
            get
            {
                switch (Filter)
                {
                    case "Character": return "WorldEventsStoryIconTextureProvider";
                    case "Companions": return "WorldEventsCompanionsIconTextureProvider";
                    case "Diplomacy": return "WorldEventsDiplomacyIconTextureProvider";
                    case "Wars": return "WorldEventsWarIconTextureProvider";
                    case "Summaries": return "WorldEventsSummariesIconTextureProvider";
                    case "Strategic": return "WorldEventsMapIconTextureProvider";
                    default: return "WorldEventsCalendarIconTextureProvider";
                }
            }
        }
        [DataSourceProperty] public string SelectedTabForegroundProvider
        {
            get
            {
                switch (Filter)
                {
                    case "Character": return "WorldEventsForegroundStoryTextureProvider";
                    case "Companions": return "WorldEventsForegroundCompanionsTextureProvider";
                    case "Diplomacy": return "WorldEventsForegroundDiplomacyTextureProvider";
                    case "Wars": return "WorldEventsForegroundWarTextureProvider";
                    case "Summaries": return "WorldEventsForegroundSummariesTextureProvider";
                    case "Strategic": return "WorldEventsForegroundMapTextureProvider";
                    default: return "WorldEventsForegroundCalendarTextureProvider";
                }
            }
        }
        [DataSourceProperty] public bool IsSelected { get { return _isSelected; } set { if (_isSelected == value) return; _isSelected = value; Label = _baseLabel; OnPropertyChangedWithValue(value, "IsSelected"); } }
        public void ExecuteSelect() { if (_select != null) _select(this); }
    }
}
