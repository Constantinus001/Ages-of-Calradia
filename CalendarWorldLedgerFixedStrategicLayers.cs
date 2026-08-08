using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    // Generated fixed data sources for the strategic map. Each property maps to
    // one known province sprite, avoiding Gauntlet's unreliable dynamic-sprite
    // binding inside an ItemTemplate.
    internal sealed partial class CalendarWorldLedgerVM
    {
        private static readonly string[] FixedStrategicProvincePropertyNames = new string[]
        {
            "StrategicProvince001",
            "StrategicContestedProvince001",
            "StrategicProvince002",
            "StrategicContestedProvince002",
            "StrategicProvince003",
            "StrategicContestedProvince003",
            "StrategicProvince004",
            "StrategicContestedProvince004",
            "StrategicProvince005",
            "StrategicContestedProvince005",
            "StrategicProvince006",
            "StrategicContestedProvince006",
            "StrategicProvince007",
            "StrategicContestedProvince007",
            "StrategicProvince008",
            "StrategicContestedProvince008",
            "StrategicProvince009",
            "StrategicContestedProvince009",
            "StrategicProvince010",
            "StrategicContestedProvince010",
            "StrategicProvince011",
            "StrategicContestedProvince011",
            "StrategicProvince012",
            "StrategicContestedProvince012",
            "StrategicProvince013",
            "StrategicContestedProvince013",
            "StrategicProvince014",
            "StrategicContestedProvince014",
            "StrategicProvince015",
            "StrategicContestedProvince015",
            "StrategicProvince016",
            "StrategicContestedProvince016",
            "StrategicProvince017",
            "StrategicContestedProvince017",
            "StrategicProvince018",
            "StrategicContestedProvince018",
            "StrategicProvince019",
            "StrategicContestedProvince019",
            "StrategicProvince020",
            "StrategicContestedProvince020",
            "StrategicProvince021",
            "StrategicContestedProvince021",
            "StrategicProvince022",
            "StrategicContestedProvince022",
            "StrategicProvince023",
            "StrategicContestedProvince023",
            "StrategicProvince024",
            "StrategicContestedProvince024",
            "StrategicProvince025",
            "StrategicContestedProvince025",
            "StrategicProvince026",
            "StrategicContestedProvince026",
            "StrategicProvince027",
            "StrategicContestedProvince027",
            "StrategicProvince028",
            "StrategicContestedProvince028",
            "StrategicProvince029",
            "StrategicContestedProvince029",
            "StrategicProvince030",
            "StrategicContestedProvince030",
            "StrategicProvince031",
            "StrategicContestedProvince031",
            "StrategicProvince032",
            "StrategicContestedProvince032",
            "StrategicProvince033",
            "StrategicContestedProvince033",
            "StrategicProvince034",
            "StrategicContestedProvince034",
            "StrategicProvince035",
            "StrategicContestedProvince035",
            "StrategicProvince036",
            "StrategicContestedProvince036",
            "StrategicProvince037",
            "StrategicContestedProvince037",
            "StrategicProvince038",
            "StrategicContestedProvince038",
            "StrategicProvince039",
            "StrategicContestedProvince039",
            "StrategicProvince040",
            "StrategicContestedProvince040",
            "StrategicProvince041",
            "StrategicContestedProvince041",
            "StrategicProvince042",
            "StrategicContestedProvince042",
            "StrategicProvince043",
            "StrategicContestedProvince043",
            "StrategicProvince044",
            "StrategicContestedProvince044",
            "StrategicProvince045",
            "StrategicContestedProvince045",
            "StrategicProvince046",
            "StrategicContestedProvince046",
            "StrategicProvince047",
            "StrategicContestedProvince047",
            "StrategicProvince048",
            "StrategicContestedProvince048",
            "StrategicProvince049",
            "StrategicContestedProvince049",
            "StrategicProvince050",
            "StrategicContestedProvince050",
            "StrategicProvince051",
            "StrategicContestedProvince051",
            "StrategicProvince052",
            "StrategicContestedProvince052",
            "StrategicProvince053",
            "StrategicContestedProvince053",
            "StrategicProvince054",
            "StrategicContestedProvince054",
            "StrategicProvince055",
            "StrategicContestedProvince055",
            "StrategicProvince056",
            "StrategicContestedProvince056",
            "StrategicProvince057",
            "StrategicContestedProvince057",
            "StrategicProvince058",
            "StrategicContestedProvince058",
            "StrategicProvince059",
            "StrategicContestedProvince059",
            "StrategicProvince060",
            "StrategicContestedProvince060",
            "StrategicProvince061",
            "StrategicContestedProvince061",
            "StrategicProvince062",
            "StrategicContestedProvince062",
            "StrategicProvince063",
            "StrategicContestedProvince063",
            "StrategicProvince064",
            "StrategicContestedProvince064",
            "StrategicProvince065",
            "StrategicContestedProvince065",
            "StrategicProvince066",
            "StrategicContestedProvince066",
            "StrategicProvince067",
            "StrategicContestedProvince067",
            "StrategicProvince068",
            "StrategicContestedProvince068",
            "StrategicProvince069",
            "StrategicContestedProvince069",
            "StrategicProvince070",
            "StrategicContestedProvince070",
            "StrategicProvince071",
            "StrategicContestedProvince071",
            "StrategicProvince072",
            "StrategicContestedProvince072",
            "StrategicProvince073",
            "StrategicContestedProvince073",
            "StrategicProvince074",
            "StrategicContestedProvince074",
            "StrategicProvince075",
            "StrategicContestedProvince075",
            "StrategicProvince076",
            "StrategicContestedProvince076",
            "StrategicProvince077",
            "StrategicContestedProvince077",
            "StrategicProvince078",
            "StrategicContestedProvince078",
            "StrategicProvince079",
            "StrategicContestedProvince079",
            "StrategicProvince080",
            "StrategicContestedProvince080",
            "StrategicProvince081",
            "StrategicContestedProvince081",
            "StrategicProvince082",
            "StrategicContestedProvince082",
            "StrategicProvince083",
            "StrategicContestedProvince083",
            "StrategicProvince084",
            "StrategicContestedProvince084",
            "StrategicProvince085",
            "StrategicContestedProvince085",
            "StrategicProvince086",
            "StrategicContestedProvince086",
            "StrategicProvince087",
            "StrategicContestedProvince087",
            "StrategicProvince088",
            "StrategicContestedProvince088",
            "StrategicProvince089",
            "StrategicContestedProvince089",
            "StrategicProvince090",
            "StrategicContestedProvince090",
            "StrategicProvince091",
            "StrategicContestedProvince091",
            "StrategicProvince092",
            "StrategicContestedProvince092",
            "StrategicProvince093",
            "StrategicContestedProvince093",
            "StrategicProvince094",
            "StrategicContestedProvince094",
            "StrategicProvince095",
            "StrategicContestedProvince095",
            "StrategicProvince096",
            "StrategicContestedProvince096",
            "StrategicProvince097",
            "StrategicContestedProvince097",
            "StrategicProvince098",
            "StrategicContestedProvince098",
            "StrategicProvince099",
            "StrategicContestedProvince099",
            "StrategicProvince100",
            "StrategicContestedProvince100",
            "StrategicProvince101",
            "StrategicContestedProvince101",
            "StrategicProvince102",
            "StrategicContestedProvince102",
            "StrategicProvince103",
            "StrategicContestedProvince103",
            "StrategicProvince104",
            "StrategicContestedProvince104",
            "StrategicProvince105",
            "StrategicContestedProvince105",
            "StrategicProvince106",
            "StrategicContestedProvince106",
            "StrategicProvince107",
            "StrategicContestedProvince107",
            "StrategicProvince108",
            "StrategicContestedProvince108",
            "StrategicProvince109",
            "StrategicContestedProvince109",
            "StrategicProvince110",
            "StrategicContestedProvince110",
            "StrategicProvince111",
            "StrategicContestedProvince111",
            "StrategicProvince112",
            "StrategicContestedProvince112",
            "StrategicProvince113",
            "StrategicContestedProvince113",
            "StrategicProvince114",
            "StrategicContestedProvince114",
            "StrategicProvince115",
            "StrategicContestedProvince115",
            "StrategicProvince116",
            "StrategicContestedProvince116",
            "StrategicProvince117",
            "StrategicContestedProvince117",
            "StrategicProvince118",
            "StrategicContestedProvince118",
            "StrategicProvince119",
            "StrategicContestedProvince119",
            "StrategicProvince120",
            "StrategicContestedProvince120",
            "StrategicProvince121",
            "StrategicContestedProvince121",
            "StrategicProvince122",
            "StrategicContestedProvince122",
            "StrategicProvince123",
            "StrategicContestedProvince123",
            "StrategicProvince124",
            "StrategicContestedProvince124",
            "StrategicProvince125",
            "StrategicContestedProvince125",
            "StrategicProvince126",
            "StrategicContestedProvince126",
            "StrategicProvince127",
            "StrategicContestedProvince127",
            "StrategicProvince128",
            "StrategicContestedProvince128",
            "StrategicProvince129",
            "StrategicContestedProvince129",
            "StrategicProvince130",
            "StrategicContestedProvince130",
            "StrategicProvince131",
            "StrategicContestedProvince131",
            "StrategicProvince132",
            "StrategicContestedProvince132",
            "StrategicProvince133",
            "StrategicContestedProvince133"
        };

        private void NotifyFixedStrategicProvince(int index, bool contested)
        {
            OnPropertyChangedWithValue(
                contested ? _fixedStrategicContestedProvinces[index] : _fixedStrategicProvinces[index],
                FixedStrategicProvincePropertyNames[index * 2 + (contested ? 1 : 0)]);
        }

        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince001 { get { return _fixedStrategicProvinces[0]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince001 { get { return _fixedStrategicContestedProvinces[0]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince002 { get { return _fixedStrategicProvinces[1]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince002 { get { return _fixedStrategicContestedProvinces[1]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince003 { get { return _fixedStrategicProvinces[2]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince003 { get { return _fixedStrategicContestedProvinces[2]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince004 { get { return _fixedStrategicProvinces[3]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince004 { get { return _fixedStrategicContestedProvinces[3]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince005 { get { return _fixedStrategicProvinces[4]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince005 { get { return _fixedStrategicContestedProvinces[4]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince006 { get { return _fixedStrategicProvinces[5]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince006 { get { return _fixedStrategicContestedProvinces[5]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince007 { get { return _fixedStrategicProvinces[6]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince007 { get { return _fixedStrategicContestedProvinces[6]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince008 { get { return _fixedStrategicProvinces[7]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince008 { get { return _fixedStrategicContestedProvinces[7]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince009 { get { return _fixedStrategicProvinces[8]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince009 { get { return _fixedStrategicContestedProvinces[8]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince010 { get { return _fixedStrategicProvinces[9]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince010 { get { return _fixedStrategicContestedProvinces[9]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince011 { get { return _fixedStrategicProvinces[10]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince011 { get { return _fixedStrategicContestedProvinces[10]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince012 { get { return _fixedStrategicProvinces[11]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince012 { get { return _fixedStrategicContestedProvinces[11]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince013 { get { return _fixedStrategicProvinces[12]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince013 { get { return _fixedStrategicContestedProvinces[12]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince014 { get { return _fixedStrategicProvinces[13]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince014 { get { return _fixedStrategicContestedProvinces[13]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince015 { get { return _fixedStrategicProvinces[14]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince015 { get { return _fixedStrategicContestedProvinces[14]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince016 { get { return _fixedStrategicProvinces[15]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince016 { get { return _fixedStrategicContestedProvinces[15]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince017 { get { return _fixedStrategicProvinces[16]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince017 { get { return _fixedStrategicContestedProvinces[16]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince018 { get { return _fixedStrategicProvinces[17]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince018 { get { return _fixedStrategicContestedProvinces[17]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince019 { get { return _fixedStrategicProvinces[18]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince019 { get { return _fixedStrategicContestedProvinces[18]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince020 { get { return _fixedStrategicProvinces[19]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince020 { get { return _fixedStrategicContestedProvinces[19]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince021 { get { return _fixedStrategicProvinces[20]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince021 { get { return _fixedStrategicContestedProvinces[20]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince022 { get { return _fixedStrategicProvinces[21]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince022 { get { return _fixedStrategicContestedProvinces[21]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince023 { get { return _fixedStrategicProvinces[22]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince023 { get { return _fixedStrategicContestedProvinces[22]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince024 { get { return _fixedStrategicProvinces[23]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince024 { get { return _fixedStrategicContestedProvinces[23]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince025 { get { return _fixedStrategicProvinces[24]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince025 { get { return _fixedStrategicContestedProvinces[24]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince026 { get { return _fixedStrategicProvinces[25]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince026 { get { return _fixedStrategicContestedProvinces[25]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince027 { get { return _fixedStrategicProvinces[26]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince027 { get { return _fixedStrategicContestedProvinces[26]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince028 { get { return _fixedStrategicProvinces[27]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince028 { get { return _fixedStrategicContestedProvinces[27]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince029 { get { return _fixedStrategicProvinces[28]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince029 { get { return _fixedStrategicContestedProvinces[28]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince030 { get { return _fixedStrategicProvinces[29]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince030 { get { return _fixedStrategicContestedProvinces[29]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince031 { get { return _fixedStrategicProvinces[30]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince031 { get { return _fixedStrategicContestedProvinces[30]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince032 { get { return _fixedStrategicProvinces[31]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince032 { get { return _fixedStrategicContestedProvinces[31]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince033 { get { return _fixedStrategicProvinces[32]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince033 { get { return _fixedStrategicContestedProvinces[32]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince034 { get { return _fixedStrategicProvinces[33]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince034 { get { return _fixedStrategicContestedProvinces[33]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince035 { get { return _fixedStrategicProvinces[34]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince035 { get { return _fixedStrategicContestedProvinces[34]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince036 { get { return _fixedStrategicProvinces[35]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince036 { get { return _fixedStrategicContestedProvinces[35]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince037 { get { return _fixedStrategicProvinces[36]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince037 { get { return _fixedStrategicContestedProvinces[36]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince038 { get { return _fixedStrategicProvinces[37]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince038 { get { return _fixedStrategicContestedProvinces[37]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince039 { get { return _fixedStrategicProvinces[38]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince039 { get { return _fixedStrategicContestedProvinces[38]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince040 { get { return _fixedStrategicProvinces[39]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince040 { get { return _fixedStrategicContestedProvinces[39]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince041 { get { return _fixedStrategicProvinces[40]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince041 { get { return _fixedStrategicContestedProvinces[40]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince042 { get { return _fixedStrategicProvinces[41]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince042 { get { return _fixedStrategicContestedProvinces[41]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince043 { get { return _fixedStrategicProvinces[42]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince043 { get { return _fixedStrategicContestedProvinces[42]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince044 { get { return _fixedStrategicProvinces[43]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince044 { get { return _fixedStrategicContestedProvinces[43]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince045 { get { return _fixedStrategicProvinces[44]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince045 { get { return _fixedStrategicContestedProvinces[44]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince046 { get { return _fixedStrategicProvinces[45]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince046 { get { return _fixedStrategicContestedProvinces[45]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince047 { get { return _fixedStrategicProvinces[46]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince047 { get { return _fixedStrategicContestedProvinces[46]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince048 { get { return _fixedStrategicProvinces[47]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince048 { get { return _fixedStrategicContestedProvinces[47]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince049 { get { return _fixedStrategicProvinces[48]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince049 { get { return _fixedStrategicContestedProvinces[48]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince050 { get { return _fixedStrategicProvinces[49]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince050 { get { return _fixedStrategicContestedProvinces[49]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince051 { get { return _fixedStrategicProvinces[50]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince051 { get { return _fixedStrategicContestedProvinces[50]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince052 { get { return _fixedStrategicProvinces[51]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince052 { get { return _fixedStrategicContestedProvinces[51]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince053 { get { return _fixedStrategicProvinces[52]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince053 { get { return _fixedStrategicContestedProvinces[52]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince054 { get { return _fixedStrategicProvinces[53]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince054 { get { return _fixedStrategicContestedProvinces[53]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince055 { get { return _fixedStrategicProvinces[54]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince055 { get { return _fixedStrategicContestedProvinces[54]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince056 { get { return _fixedStrategicProvinces[55]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince056 { get { return _fixedStrategicContestedProvinces[55]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince057 { get { return _fixedStrategicProvinces[56]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince057 { get { return _fixedStrategicContestedProvinces[56]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince058 { get { return _fixedStrategicProvinces[57]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince058 { get { return _fixedStrategicContestedProvinces[57]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince059 { get { return _fixedStrategicProvinces[58]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince059 { get { return _fixedStrategicContestedProvinces[58]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince060 { get { return _fixedStrategicProvinces[59]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince060 { get { return _fixedStrategicContestedProvinces[59]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince061 { get { return _fixedStrategicProvinces[60]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince061 { get { return _fixedStrategicContestedProvinces[60]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince062 { get { return _fixedStrategicProvinces[61]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince062 { get { return _fixedStrategicContestedProvinces[61]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince063 { get { return _fixedStrategicProvinces[62]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince063 { get { return _fixedStrategicContestedProvinces[62]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince064 { get { return _fixedStrategicProvinces[63]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince064 { get { return _fixedStrategicContestedProvinces[63]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince065 { get { return _fixedStrategicProvinces[64]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince065 { get { return _fixedStrategicContestedProvinces[64]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince066 { get { return _fixedStrategicProvinces[65]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince066 { get { return _fixedStrategicContestedProvinces[65]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince067 { get { return _fixedStrategicProvinces[66]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince067 { get { return _fixedStrategicContestedProvinces[66]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince068 { get { return _fixedStrategicProvinces[67]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince068 { get { return _fixedStrategicContestedProvinces[67]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince069 { get { return _fixedStrategicProvinces[68]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince069 { get { return _fixedStrategicContestedProvinces[68]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince070 { get { return _fixedStrategicProvinces[69]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince070 { get { return _fixedStrategicContestedProvinces[69]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince071 { get { return _fixedStrategicProvinces[70]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince071 { get { return _fixedStrategicContestedProvinces[70]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince072 { get { return _fixedStrategicProvinces[71]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince072 { get { return _fixedStrategicContestedProvinces[71]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince073 { get { return _fixedStrategicProvinces[72]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince073 { get { return _fixedStrategicContestedProvinces[72]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince074 { get { return _fixedStrategicProvinces[73]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince074 { get { return _fixedStrategicContestedProvinces[73]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince075 { get { return _fixedStrategicProvinces[74]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince075 { get { return _fixedStrategicContestedProvinces[74]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince076 { get { return _fixedStrategicProvinces[75]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince076 { get { return _fixedStrategicContestedProvinces[75]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince077 { get { return _fixedStrategicProvinces[76]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince077 { get { return _fixedStrategicContestedProvinces[76]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince078 { get { return _fixedStrategicProvinces[77]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince078 { get { return _fixedStrategicContestedProvinces[77]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince079 { get { return _fixedStrategicProvinces[78]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince079 { get { return _fixedStrategicContestedProvinces[78]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince080 { get { return _fixedStrategicProvinces[79]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince080 { get { return _fixedStrategicContestedProvinces[79]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince081 { get { return _fixedStrategicProvinces[80]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince081 { get { return _fixedStrategicContestedProvinces[80]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince082 { get { return _fixedStrategicProvinces[81]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince082 { get { return _fixedStrategicContestedProvinces[81]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince083 { get { return _fixedStrategicProvinces[82]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince083 { get { return _fixedStrategicContestedProvinces[82]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince084 { get { return _fixedStrategicProvinces[83]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince084 { get { return _fixedStrategicContestedProvinces[83]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince085 { get { return _fixedStrategicProvinces[84]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince085 { get { return _fixedStrategicContestedProvinces[84]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince086 { get { return _fixedStrategicProvinces[85]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince086 { get { return _fixedStrategicContestedProvinces[85]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince087 { get { return _fixedStrategicProvinces[86]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince087 { get { return _fixedStrategicContestedProvinces[86]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince088 { get { return _fixedStrategicProvinces[87]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince088 { get { return _fixedStrategicContestedProvinces[87]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince089 { get { return _fixedStrategicProvinces[88]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince089 { get { return _fixedStrategicContestedProvinces[88]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince090 { get { return _fixedStrategicProvinces[89]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince090 { get { return _fixedStrategicContestedProvinces[89]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince091 { get { return _fixedStrategicProvinces[90]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince091 { get { return _fixedStrategicContestedProvinces[90]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince092 { get { return _fixedStrategicProvinces[91]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince092 { get { return _fixedStrategicContestedProvinces[91]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince093 { get { return _fixedStrategicProvinces[92]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince093 { get { return _fixedStrategicContestedProvinces[92]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince094 { get { return _fixedStrategicProvinces[93]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince094 { get { return _fixedStrategicContestedProvinces[93]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince095 { get { return _fixedStrategicProvinces[94]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince095 { get { return _fixedStrategicContestedProvinces[94]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince096 { get { return _fixedStrategicProvinces[95]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince096 { get { return _fixedStrategicContestedProvinces[95]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince097 { get { return _fixedStrategicProvinces[96]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince097 { get { return _fixedStrategicContestedProvinces[96]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince098 { get { return _fixedStrategicProvinces[97]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince098 { get { return _fixedStrategicContestedProvinces[97]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince099 { get { return _fixedStrategicProvinces[98]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince099 { get { return _fixedStrategicContestedProvinces[98]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince100 { get { return _fixedStrategicProvinces[99]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince100 { get { return _fixedStrategicContestedProvinces[99]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince101 { get { return _fixedStrategicProvinces[100]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince101 { get { return _fixedStrategicContestedProvinces[100]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince102 { get { return _fixedStrategicProvinces[101]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince102 { get { return _fixedStrategicContestedProvinces[101]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince103 { get { return _fixedStrategicProvinces[102]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince103 { get { return _fixedStrategicContestedProvinces[102]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince104 { get { return _fixedStrategicProvinces[103]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince104 { get { return _fixedStrategicContestedProvinces[103]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince105 { get { return _fixedStrategicProvinces[104]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince105 { get { return _fixedStrategicContestedProvinces[104]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince106 { get { return _fixedStrategicProvinces[105]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince106 { get { return _fixedStrategicContestedProvinces[105]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince107 { get { return _fixedStrategicProvinces[106]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince107 { get { return _fixedStrategicContestedProvinces[106]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince108 { get { return _fixedStrategicProvinces[107]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince108 { get { return _fixedStrategicContestedProvinces[107]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince109 { get { return _fixedStrategicProvinces[108]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince109 { get { return _fixedStrategicContestedProvinces[108]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince110 { get { return _fixedStrategicProvinces[109]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince110 { get { return _fixedStrategicContestedProvinces[109]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince111 { get { return _fixedStrategicProvinces[110]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince111 { get { return _fixedStrategicContestedProvinces[110]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince112 { get { return _fixedStrategicProvinces[111]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince112 { get { return _fixedStrategicContestedProvinces[111]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince113 { get { return _fixedStrategicProvinces[112]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince113 { get { return _fixedStrategicContestedProvinces[112]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince114 { get { return _fixedStrategicProvinces[113]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince114 { get { return _fixedStrategicContestedProvinces[113]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince115 { get { return _fixedStrategicProvinces[114]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince115 { get { return _fixedStrategicContestedProvinces[114]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince116 { get { return _fixedStrategicProvinces[115]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince116 { get { return _fixedStrategicContestedProvinces[115]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince117 { get { return _fixedStrategicProvinces[116]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince117 { get { return _fixedStrategicContestedProvinces[116]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince118 { get { return _fixedStrategicProvinces[117]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince118 { get { return _fixedStrategicContestedProvinces[117]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince119 { get { return _fixedStrategicProvinces[118]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince119 { get { return _fixedStrategicContestedProvinces[118]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince120 { get { return _fixedStrategicProvinces[119]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince120 { get { return _fixedStrategicContestedProvinces[119]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince121 { get { return _fixedStrategicProvinces[120]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince121 { get { return _fixedStrategicContestedProvinces[120]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince122 { get { return _fixedStrategicProvinces[121]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince122 { get { return _fixedStrategicContestedProvinces[121]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince123 { get { return _fixedStrategicProvinces[122]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince123 { get { return _fixedStrategicContestedProvinces[122]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince124 { get { return _fixedStrategicProvinces[123]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince124 { get { return _fixedStrategicContestedProvinces[123]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince125 { get { return _fixedStrategicProvinces[124]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince125 { get { return _fixedStrategicContestedProvinces[124]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince126 { get { return _fixedStrategicProvinces[125]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince126 { get { return _fixedStrategicContestedProvinces[125]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince127 { get { return _fixedStrategicProvinces[126]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince127 { get { return _fixedStrategicContestedProvinces[126]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince128 { get { return _fixedStrategicProvinces[127]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince128 { get { return _fixedStrategicContestedProvinces[127]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince129 { get { return _fixedStrategicProvinces[128]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince129 { get { return _fixedStrategicContestedProvinces[128]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince130 { get { return _fixedStrategicProvinces[129]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince130 { get { return _fixedStrategicContestedProvinces[129]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince131 { get { return _fixedStrategicProvinces[130]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince131 { get { return _fixedStrategicContestedProvinces[130]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince132 { get { return _fixedStrategicProvinces[131]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince132 { get { return _fixedStrategicContestedProvinces[131]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicProvince133 { get { return _fixedStrategicProvinces[132]; } }
        [DataSourceProperty] public CalendarWorldStrategicProvinceVM StrategicContestedProvince133 { get { return _fixedStrategicContestedProvinces[132]; } }
    }
}
