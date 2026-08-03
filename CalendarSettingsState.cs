using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Runtime-safe settings bridge. The optional MCM adapter updates this
    /// class when MCM is installed; the calendar keeps these defaults without
    /// requiring MCM.
    /// </summary>
    public static class CalendarSettingsState
    {
        private static readonly object SyncRoot = new object();

        public static event Action SettingsChanged;

        // This module always owns a 365-day Gregorian-style calendar. Keep a
        // stable value for legacy adapters/configuration, but do not expose a
        // selectable native-calendar mode.
        private const string FixedCalendarSystem = "Gregorian12Month";
        private const string DefaultDateFormat = "{Month} {Day} {Year}";
        private const bool DefaultUseOrdinalDaySuffixes = true;
        internal const int DefaultNativeDaysInYear = 84;
        internal const float DefaultPregnancyDurationInDays = 273.75f;
        internal const int DefaultPregnancyDurationMonths = 9;
        internal const float DefaultRenownGainMultiplier = 0.5f;
        private const int MaximumConfiguredMonthLength = 1000;
        private const int MaximumConfiguredMonthNameLength = 24;

        private static readonly string[] DefaultMonthNames =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        private static readonly string[] DefaultSeasonNames =
        {
            "Spring", "Summer", "Autumn", "Winter"
        };

        private static readonly int[] DefaultMonthLengths =
        {
            31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31
        };

        private static bool _useLeapYears = true;
        private static bool _showDayLabel;
        private static bool _showYearLabel;
        private static bool _useOrdinalDaySuffixes = DefaultUseOrdinalDaySuffixes;
        private static float _campaignTimeScale = 84f / 365.2425f;
        private static bool _autoCampaignTimeScale = true;
        private static string _dateFormat = DefaultDateFormat;
        private static int _nativeDaysInYear = DefaultNativeDaysInYear;
        private static float _pregnancyDurationInDays = DefaultPregnancyDurationInDays;
        private static int _pregnancyDurationMonths = DefaultPregnancyDurationMonths;
        private static bool _useCalendarMonthPregnancy = true;
        private static float _renownGainMultiplier = DefaultRenownGainMultiplier;
        private static bool _balancePartyImpairment = true;
        private static bool _balancePrisonerRecruitment = true;
        private static bool _balanceNpcMarriage = true;
        private static bool _balanceMapTracks = true;
        private static bool _balanceQuestDeadlines = true;
        private static bool _annualBalanceDiagnosticsEnabled = true;
        private static bool _campaignSessionStarted;
        private static readonly string[] _monthNames = (string[])DefaultMonthNames.Clone();
        private static readonly string[] _seasonNames = (string[])DefaultSeasonNames.Clone();
        private static readonly int[] _monthLengths = (int[])DefaultMonthLengths.Clone();
        private static readonly int[] _monthStarts = new int[12];
        private static int _commonDaysInYear;

        static CalendarSettingsState()
        {
            RebuildMonthCache();
        }

        public static bool ExtendedCalendarEnabled
        {
            get { return true; }
        }

        public static bool UseLeapYears
        {
            get { lock (SyncRoot) return _useLeapYears; }
        }

        public static bool ShowDayLabel
        {
            get { lock (SyncRoot) return _showDayLabel; }
        }

        public static bool ShowYearLabel
        {
            get { lock (SyncRoot) return _showYearLabel; }
        }

        public static bool UseOrdinalDaySuffixes
        {
            get { lock (SyncRoot) return _useOrdinalDaySuffixes; }
        }

        public static float CampaignTimeScale
        {
            get { lock (SyncRoot) return _campaignTimeScale; }
        }

        public static bool AutoCampaignTimeScale
        {
            get { lock (SyncRoot) return _autoCampaignTimeScale; }
        }

        public static string DateFormat
        {
            get { lock (SyncRoot) return _dateFormat; }
        }

        public static int NativeDaysInYear
        {
            get { lock (SyncRoot) return _nativeDaysInYear; }
        }

        public static float PregnancyDurationInDays
        {
            get { lock (SyncRoot) return _pregnancyDurationInDays; }
        }

        public static int PregnancyDurationMonths
        {
            get { lock (SyncRoot) return _pregnancyDurationMonths; }
        }

        public static bool UseCalendarMonthPregnancy
        {
            get { lock (SyncRoot) return _useCalendarMonthPregnancy; }
        }

        public static float RenownGainMultiplier
        {
            get { lock (SyncRoot) return _renownGainMultiplier; }
        }

        public static bool BalancePartyImpairment
        {
            get { lock (SyncRoot) return _balancePartyImpairment; }
        }

        public static bool BalancePrisonerRecruitment
        {
            get { lock (SyncRoot) return _balancePrisonerRecruitment; }
        }

        public static bool BalanceNpcMarriage
        {
            get { lock (SyncRoot) return _balanceNpcMarriage; }
        }

        public static bool BalanceMapTracks
        {
            get { lock (SyncRoot) return _balanceMapTracks; }
        }

        public static bool BalanceQuestDeadlines
        {
            get { lock (SyncRoot) return _balanceQuestDeadlines; }
        }

        public static bool AnnualBalanceDiagnosticsEnabled
        {
            get { lock (SyncRoot) return _annualBalanceDiagnosticsEnabled; }
        }

        public static string GetMonthName(int month)
        {
            lock (SyncRoot)
            {
                return _monthNames[month];
            }
        }

        public static string GetSeasonName(int season)
        {
            lock (SyncRoot)
            {
                return _seasonNames[season];
            }
        }

        public static int GetMonthLength(int month)
        {
            lock (SyncRoot)
            {
                return _monthLengths[month];
            }
        }

        public static int CommonDaysInYear
        {
            get
            {
                lock (SyncRoot)
                {
                    return _commonDaysInYear;
                }
            }
        }

        public static int GetMonthStart(int month)
        {
            lock (SyncRoot)
            {
                return _monthStarts[month];
            }
        }

        public static string[] MonthNamesSnapshot()
        {
            lock (SyncRoot) return (string[])_monthNames.Clone();
        }

        public static string[] SeasonNamesSnapshot()
        {
            lock (SyncRoot) return (string[])_seasonNames.Clone();
        }

        public static int[] MonthLengthsSnapshot()
        {
            lock (SyncRoot) return (int[])_monthLengths.Clone();
        }

        public static string CalendarSystem
        {
            get { return FixedCalendarSystem; }
        }

        public static string ConfigPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord",
                    "Configs",
                    "TwelveMonthCalendar",
                    "settings.xml");
            }
        }

        public static void Apply(
            string calendarSystem,
            bool useLeapYears,
            bool showDayLabel,
            bool showYearLabel,
            float campaignTimeScale,
            string dateFormat,
            string[] monthNames = null,
            int[] monthLengths = null,
            string[] seasonNames = null,
            int? nativeDaysInYear = null,
            float? pregnancyDurationInDays = null,
            int? pregnancyDurationMonths = null,
            bool? useCalendarMonthPregnancy = null,
            bool? autoCampaignTimeScale = null,
            float? renownGainMultiplier = null,
            bool? useOrdinalDaySuffixes = null,
            bool? balancePartyImpairment = null,
            bool? balancePrisonerRecruitment = null,
            bool? balanceNpcMarriage = null,
            bool? balanceMapTracks = null,
            bool? balanceQuestDeadlines = null,
            bool? annualBalanceDiagnosticsEnabled = null)
        {
            lock (SyncRoot)
            {
                if (_campaignSessionStarted
                    && useLeapYears != _useLeapYears)
                {
                    Diagnostics.Info("Leap-year setting change ignored after campaign session start; restart the campaign to apply it.");
                }
                else
                {
                    _useLeapYears = useLeapYears;
                }

                if (!string.IsNullOrWhiteSpace(calendarSystem)
                    && !string.Equals(calendarSystem, FixedCalendarSystem, StringComparison.OrdinalIgnoreCase))
                {
                    Diagnostics.Info(
                        "Ignoring legacy calendar-system setting '" + calendarSystem
                        + "'; Twelve Month Calendar is always Gregorian12Month.");
                }
                _showDayLabel = showDayLabel;
                _showYearLabel = showYearLabel;
                _useOrdinalDaySuffixes = useOrdinalDaySuffixes ?? _useOrdinalDaySuffixes;
                _dateFormat = NormalizeDateFormat(dateFormat);

                ApplyMonthNames(monthNames);
                ApplySeasonNames(seasonNames);
                ApplyMonthLengths(monthLengths);
                _nativeDaysInYear = Math.Max(1, nativeDaysInYear ?? _nativeDaysInYear);
                float requestedPregnancyDays = pregnancyDurationInDays ?? _pregnancyDurationInDays;
                _pregnancyDurationInDays = IsFinite(requestedPregnancyDays)
                    ? Math.Max(0.1f, Math.Min(10000f, requestedPregnancyDays))
                    : DefaultPregnancyDurationInDays;
                _pregnancyDurationMonths = Math.Max(1, pregnancyDurationMonths ?? _pregnancyDurationMonths);
                _useCalendarMonthPregnancy = useCalendarMonthPregnancy ?? _useCalendarMonthPregnancy;
                float requestedRenownMultiplier = renownGainMultiplier ?? _renownGainMultiplier;
                _renownGainMultiplier = IsFinite(requestedRenownMultiplier)
                    ? Math.Max(0f, Math.Min(1f, requestedRenownMultiplier))
                    : DefaultRenownGainMultiplier;
                ApplyCampaignStartSetting(ref _balancePartyImpairment, balancePartyImpairment, "BalancePartyImpairment");
                ApplyCampaignStartSetting(ref _balancePrisonerRecruitment, balancePrisonerRecruitment, "BalancePrisonerRecruitment");
                ApplyCampaignStartSetting(ref _balanceNpcMarriage, balanceNpcMarriage, "BalanceNpcMarriage");
                ApplyCampaignStartSetting(ref _balanceMapTracks, balanceMapTracks, "BalanceMapTracks");
                ApplyCampaignStartSetting(ref _balanceQuestDeadlines, balanceQuestDeadlines, "BalanceQuestDeadlines");
                _annualBalanceDiagnosticsEnabled = annualBalanceDiagnosticsEnabled ?? _annualBalanceDiagnosticsEnabled;
                _autoCampaignTimeScale = autoCampaignTimeScale ?? _autoCampaignTimeScale;
                _campaignTimeScale = _autoCampaignTimeScale
                    ? GetAutomaticCampaignTimeScale()
                    : IsFinite(campaignTimeScale)
                        ? Math.Max(0.01f, Math.Min(1.0f, campaignTimeScale))
                        : GetAutomaticCampaignTimeScale();
            }

            Diagnostics.Info(
                string.Format(
                    "Settings applied. CalendarSystem={0}; LeapYears={1}; ShowDayLabel={2}; ShowYearLabel={3}; OrdinalDays={4}; TimeScale={5:F6}; DateFormat={6}",
                    FixedCalendarSystem,
                    UseLeapYears,
                    ShowDayLabel,
                    ShowYearLabel,
                    UseOrdinalDaySuffixes,
                    CampaignTimeScale,
                    DateFormat));

            Action changed = SettingsChanged;
            if (changed != null)
            {
                try
                {
                    changed();
                }
                catch (Exception exception)
                {
                    Diagnostics.Error("A settings synchronization listener failed.", exception);
                }
            }
        }

        public static void ResetToDefaults()
        {
            Apply(
                FixedCalendarSystem,
                true,
                false,
                false,
                GetAutomaticCampaignTimeScale(),
                DefaultDateFormat,
                (string[])DefaultMonthNames.Clone(),
                (int[])DefaultMonthLengths.Clone(),
                (string[])DefaultSeasonNames.Clone(),
                DefaultNativeDaysInYear,
                DefaultPregnancyDurationInDays,
                DefaultPregnancyDurationMonths,
                true,
                true,
                DefaultRenownGainMultiplier,
                useOrdinalDaySuffixes: DefaultUseOrdinalDaySuffixes,
                balancePartyImpairment: true,
                balancePrisonerRecruitment: true,
                balanceNpcMarriage: true,
                balanceMapTracks: true,
                balanceQuestDeadlines: true,
                annualBalanceDiagnosticsEnabled: true);
            Save();
            Diagnostics.Info("Calendar settings reset to defaults.");
        }

        internal static void MarkCampaignSessionStarted()
        {
            lock (SyncRoot)
            {
                _campaignSessionStarted = true;
            }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Diagnostics.Info("No standalone settings file found; using defaults.");
                    Save();
                    return;
                }

                XmlDocument document = new XmlDocument();
                document.Load(ConfigPath);
                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != "TwelveMonthCalendar")
                {
                    throw new InvalidDataException("The standalone settings file has an invalid root element.");
                }

                Apply(
                    ReadAttribute(root, "CalendarSystem", FixedCalendarSystem),
                    ReadBoolean(root, "UseLeapYears", true),
                    ReadBoolean(root, "ShowDayLabel", false),
                    ReadBoolean(root, "ShowYearLabel", false),
                    ReadFloat(root, "CampaignTimeScale", 84f / 365.2425f),
                    ReadAttribute(root, "DateFormat", DefaultDateFormat),
                    ReadMonthNames(root),
                    ReadMonthLengths(root),
                    ReadSeasonNames(root),
                    ReadInt(root, "NativeDaysInYear", DefaultNativeDaysInYear),
                    ReadFloat(root, "PregnancyDurationDays", DefaultPregnancyDurationInDays),
                    ReadInt(root, "PregnancyDurationMonths", DefaultPregnancyDurationMonths),
                    ReadBoolean(root, "UseCalendarMonthPregnancy", true),
                    ReadBoolean(root, "AutoCampaignTimeScale", true),
                    ReadFloat(root, "RenownGainMultiplier", DefaultRenownGainMultiplier),
                    useOrdinalDaySuffixes: ReadBoolean(root, "UseOrdinalDaySuffixes", DefaultUseOrdinalDaySuffixes),
                    balancePartyImpairment: ReadBoolean(root, "BalancePartyImpairment", true),
                    balancePrisonerRecruitment: ReadBoolean(root, "BalancePrisonerRecruitment", true),
                    balanceNpcMarriage: ReadBoolean(root, "BalanceNpcMarriage", true),
                    balanceMapTracks: ReadBoolean(root, "BalanceMapTracks", true),
                    balanceQuestDeadlines: ReadBoolean(root, "BalanceQuestDeadlines", true),
                    annualBalanceDiagnosticsEnabled: ReadBoolean(root, "AnnualBalanceDiagnosticsEnabled", true));

                Diagnostics.Info(string.Format("Standalone settings loaded from {0}.", ConfigPath));
                // Rewrite the file after loading so newly added configurable
                // fields (such as custom month names) are added automatically.
                Save();
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Standalone settings could not be loaded; defaults remain active.", exception);
            }
        }

        public static void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                XmlDocument document = new XmlDocument();
                XmlElement root = document.CreateElement("TwelveMonthCalendar");
                document.AppendChild(root);
                root.SetAttribute("UseLeapYears", UseLeapYears.ToString());
                root.SetAttribute("ShowDayLabel", ShowDayLabel.ToString());
                root.SetAttribute("ShowYearLabel", ShowYearLabel.ToString());
                root.SetAttribute("UseOrdinalDaySuffixes", UseOrdinalDaySuffixes.ToString());
                root.SetAttribute("CampaignTimeScale", CampaignTimeScale.ToString("R", CultureInfo.InvariantCulture));
                root.SetAttribute("AutoCampaignTimeScale", AutoCampaignTimeScale.ToString());
                root.SetAttribute("DateFormat", DateFormat);
                root.SetAttribute("NativeDaysInYear", NativeDaysInYear.ToString(CultureInfo.InvariantCulture));
                root.SetAttribute("PregnancyDurationDays", PregnancyDurationInDays.ToString("R", CultureInfo.InvariantCulture));
                root.SetAttribute("PregnancyDurationMonths", PregnancyDurationMonths.ToString(CultureInfo.InvariantCulture));
                root.SetAttribute("UseCalendarMonthPregnancy", UseCalendarMonthPregnancy.ToString());
                root.SetAttribute("RenownGainMultiplier", RenownGainMultiplier.ToString("R", CultureInfo.InvariantCulture));
                root.SetAttribute("BalancePartyImpairment", BalancePartyImpairment.ToString());
                root.SetAttribute("BalancePrisonerRecruitment", BalancePrisonerRecruitment.ToString());
                root.SetAttribute("BalanceNpcMarriage", BalanceNpcMarriage.ToString());
                root.SetAttribute("BalanceMapTracks", BalanceMapTracks.ToString());
                root.SetAttribute("BalanceQuestDeadlines", BalanceQuestDeadlines.ToString());
                root.SetAttribute("AnnualBalanceDiagnosticsEnabled", AnnualBalanceDiagnosticsEnabled.ToString());
                string[] monthNames = MonthNamesSnapshot();
                string[] seasonNames = SeasonNamesSnapshot();
                int[] monthLengths = MonthLengthsSnapshot();
                for (int i = 0; i < 12; i++)
                {
                    root.SetAttribute(string.Format("Month{0}Name", i + 1), monthNames[i]);
                    root.SetAttribute(string.Format("Month{0}Days", i + 1), monthLengths[i].ToString(CultureInfo.InvariantCulture));
                }
                for (int i = 0; i < seasonNames.Length; i++)
                {
                    root.SetAttribute(string.Format("Season{0}Name", i + 1), seasonNames[i]);
                }
                document.Save(ConfigPath);

                Diagnostics.Info(string.Format("Standalone settings saved to {0}.", ConfigPath));
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Standalone settings could not be saved.", exception);
            }
        }

        private static string ReadAttribute(XmlElement root, string name, string fallback)
        {
            string value = root.GetAttribute(name);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static void ApplyCampaignStartSetting(ref bool currentValue, bool? requestedValue, string name)
        {
            if (!requestedValue.HasValue || requestedValue.Value == currentValue)
            {
                return;
            }

            if (_campaignSessionStarted)
            {
                Diagnostics.Info(name + " change ignored after campaign session start; restart the campaign to apply it.");
                return;
            }

            currentValue = requestedValue.Value;
        }

        private static bool ReadBoolean(XmlElement root, string name, bool fallback)
        {
            bool value;
            return bool.TryParse(root.GetAttribute(name), out value) ? value : fallback;
        }

        private static float ReadFloat(XmlElement root, string name, float fallback)
        {
            float value;
            if (!float.TryParse(
                root.GetAttribute(name),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
            {
                return fallback;
            }

            return IsFinite(value) ? value : fallback;
        }

        private static int ReadInt(XmlElement root, string name, int fallback)
        {
            int value;
            return int.TryParse(root.GetAttribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }

        private static string[] ReadMonthNames(XmlElement root)
        {
            string[] values = (string[])DefaultMonthNames.Clone();
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = ReadAttribute(root, string.Format("Month{0}Name", i + 1), values[i]);
            }

            return values;
        }

        private static string[] ReadSeasonNames(XmlElement root)
        {
            string[] values = (string[])DefaultSeasonNames.Clone();
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = ReadAttribute(root, string.Format("Season{0}Name", i + 1), values[i]);
            }

            return values;
        }

        private static int[] ReadMonthLengths(XmlElement root)
        {
            int[] values = (int[])DefaultMonthLengths.Clone();
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Math.Max(
                    1,
                    Math.Min(
                        MaximumConfiguredMonthLength,
                        ReadInt(root, string.Format("Month{0}Days", i + 1), values[i])));
            }

            return values;
        }

        private static void ApplyMonthNames(string[] values)
        {
            if (values == null || values.Length != _monthNames.Length)
            {
                return;
            }

            for (int i = 0; i < _monthNames.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    string name = values[i].Trim();
                    _monthNames[i] = name.Length > MaximumConfiguredMonthNameLength
                        ? name.Substring(0, MaximumConfiguredMonthNameLength)
                        : name;
                }
            }
        }

        private static void ApplySeasonNames(string[] values)
        {
            if (values == null || values.Length != _seasonNames.Length)
            {
                return;
            }

            for (int i = 0; i < _seasonNames.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    string name = values[i].Trim();
                    _seasonNames[i] = name.Length > MaximumConfiguredMonthNameLength
                        ? name.Substring(0, MaximumConfiguredMonthNameLength)
                        : name;
                }
            }
        }

        private static void ApplyMonthLengths(int[] values)
        {
            if (values == null || values.Length != _monthLengths.Length)
            {
                return;
            }

            for (int i = 0; i < _monthLengths.Length; i++)
            {
                _monthLengths[i] = Math.Max(1, Math.Min(MaximumConfiguredMonthLength, values[i]));
            }

            RebuildMonthCache();
        }

        private static void RebuildMonthCache()
        {
            int runningTotal = 0;
            for (int i = 0; i < _monthLengths.Length; i++)
            {
                _monthStarts[i] = runningTotal;
                runningTotal += _monthLengths[i];
            }

            _commonDaysInYear = runningTotal;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string NormalizeDateFormat(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultDateFormat;
            }

            string normalized = value;
            string[] tokens = { "Month", "Season", "Day", "Year", "MonthNumber", "DayOfYear" };
            foreach (string token in tokens)
            {
                normalized = Regex.Replace(
                    normalized,
                    "\\{" + token + "\\}",
                    "{" + token + "}",
                    RegexOptions.IgnoreCase);
            }

            return normalized;
        }

        private static float GetAutomaticCampaignTimeScale()
        {
            double averageDays = _useLeapYears
                ? _commonDaysInYear + 0.2425
                : _commonDaysInYear;
            return (float)Math.Max(0.01, Math.Min(1.0, _nativeDaysInYear / averageDays));
        }
    }
}
