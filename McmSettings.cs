using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace TwelveMonthCalendar.MCM
{
    internal sealed class CalendarMcmSettings : AttributeGlobalSettings<CalendarMcmSettings>
    {
        private bool _useLeapYears = CalendarSettingsState.UseLeapYears;
        private bool _showDayLabel = CalendarSettingsState.ShowDayLabel;
        private bool _showYearLabel = CalendarSettingsState.ShowYearLabel;
        private bool _useOrdinalDaySuffixes = CalendarSettingsState.UseOrdinalDaySuffixes;
        private float _campaignTimeScale = CalendarSettingsState.CampaignTimeScale;
        private bool _autoCampaignTimeScale = CalendarSettingsState.AutoCampaignTimeScale;
        private string _dateFormat = CalendarSettingsState.DateFormat;
        private bool _useCalendarMonthPregnancy = CalendarSettingsState.UseCalendarMonthPregnancy;
        private int _pregnancyDurationMonths = CalendarSettingsState.PregnancyDurationMonths;
        private float _pregnancyDurationInDays = CalendarSettingsState.PregnancyDurationInDays;
        private float _renownGainMultiplier = CalendarSettingsState.RenownGainMultiplier;
        private bool _balancePartyImpairment = CalendarSettingsState.BalancePartyImpairment;
        private bool _balancePrisonerRecruitment = CalendarSettingsState.BalancePrisonerRecruitment;
        private bool _balanceNpcMarriage = CalendarSettingsState.BalanceNpcMarriage;
        private bool _balanceMapTracks = CalendarSettingsState.BalanceMapTracks;
        private bool _balanceQuestDeadlines = CalendarSettingsState.BalanceQuestDeadlines;
        private bool _annualBalanceDiagnosticsEnabled = CalendarSettingsState.AnnualBalanceDiagnosticsEnabled;
        private bool _synchronizing;

        public override string Id => "TwelveMonthCalendar_MCM";
        public override string DisplayName => "Twelve Month Calendar";
        public override string FolderName => "TwelveMonthCalendar";
        public override string FormatType => "json";

        [SettingPropertyBool("Use Leap Years", Order = 0, RequireRestart = true,
            HintText = "Adds February 29 using the Gregorian leap-year rule.")]
        [SettingPropertyGroup("Calendar")]
        public bool UseLeapYears
        {
            get { return _useLeapYears; }
            set
            {
                _useLeapYears = value;
                Apply();
                OnPropertyChanged();
            }
        }

        [SettingPropertyBool("Show Day Label", Order = 1, RequireRestart = false,
            HintText = "Displays Day before the day number.")]
        [SettingPropertyGroup("Display")]
        public bool ShowDayLabel
        {
            get { return _showDayLabel; }
            set
            {
                _showDayLabel = value;
                Apply();
                OnPropertyChanged();
            }
        }

        [SettingPropertyBool("Use Ordinal Day Suffixes", Order = 2, RequireRestart = false,
            HintText = "Displays dates as 1st, 2nd, 3rd, and so on.")]
        [SettingPropertyGroup("Display")]
        public bool UseOrdinalDaySuffixes
        {
            get { return _useOrdinalDaySuffixes; }
            set
            {
                _useOrdinalDaySuffixes = value;
                Apply();
                OnPropertyChanged();
            }
        }

        [SettingPropertyBool("Show Year Label", Order = 3, RequireRestart = false,
            HintText = "Displays Year before the year number.")]
        [SettingPropertyGroup("Display")]
        public bool ShowYearLabel
        {
            get { return _showYearLabel; }
            set
            {
                _showYearLabel = value;
                Apply();
                OnPropertyChanged();
            }
        }

        [SettingPropertyFloatingInteger("Campaign Time Scale", 0.01f, 1.0f, "0.000", Order = 4,
            RequireRestart = true, HintText = "Controls how quickly campaign time advances. Default is 0.230.")]
        [SettingPropertyGroup("Economy")]
        public float CampaignTimeScale
        {
            get { return _campaignTimeScale; }
            set
            {
                _campaignTimeScale = value;
                Apply();
                OnPropertyChanged();
            }
        }

        [SettingPropertyBool("Automatic Campaign Time Scale", Order = 5, RequireRestart = true,
            HintText = "Derives pacing from the configured native and calendar year lengths.")]
        [SettingPropertyGroup("Economy")]
        public bool AutoCampaignTimeScale
        {
            get { return _autoCampaignTimeScale; }
            set
            {
                _autoCampaignTimeScale = value;
                Apply();
                OnPropertyChanged();
            }
        }

        [SettingPropertyText("Date Format", Order = 6, RequireRestart = false,
            HintText = "Tokens: {Month}, {Season}, {Day}, {Year}, {MonthNumber}, {DayOfYear}. Example: {Month} {Day} {Year}. The map bar shows the season separately to the right of the clock.")]
        [SettingPropertyGroup("Display")]
        public string DateFormat
        {
            get { return _dateFormat; }
            set
            {
                _dateFormat = value;
                Apply();
                OnPropertyChanged();
            }
        }

        [SettingPropertyBool("Use Calendar-Month Pregnancy", Order = 7, RequireRestart = false,
            HintText = "Uses the configured number of calendar months for future pregnancies.")]
        [SettingPropertyGroup("Life Cycle")]
        public bool UseCalendarMonthPregnancy
        {
            get { return _useCalendarMonthPregnancy; }
            set
            {
                _useCalendarMonthPregnancy = value;
                Apply();
                OnPropertyChanged();
            }
        }

        [SettingPropertyInteger("Pregnancy Duration (Months)", 1, 24, "0", Order = 8,
            RequireRestart = false, HintText = "Calendar months from conception to birth.")]
        [SettingPropertyGroup("Life Cycle")]
        public int PregnancyDurationMonths
        {
            get { return _pregnancyDurationMonths; }
            set
            {
                _pregnancyDurationMonths = value;
                Apply();
                OnPropertyChanged();
            }
        }

        [SettingPropertyFloatingInteger("Fixed Pregnancy Duration (Days)", 0.1f, 10000f, "0.00", Order = 9,
            RequireRestart = false, HintText = "Fallback duration when calendar-month pregnancy is disabled.")]
        [SettingPropertyGroup("Life Cycle")]
        public float PregnancyDurationInDays
        {
            get { return _pregnancyDurationInDays; }
            set
            {
                _pregnancyDurationInDays = value;
                Apply();
                OnPropertyChanged();
            }
        }

        [SettingPropertyFloatingInteger("Renown Gain Multiplier", 0f, 1f, "0.00", Order = 10,
            RequireRestart = false, HintText = "Scales positive renown awards. Default is 0.50.")]
        [SettingPropertyGroup("Progression")]
        public float RenownGainMultiplier
        {
            get { return _renownGainMultiplier; }
            set
            {
                _renownGainMultiplier = value;
                Apply();
                OnPropertyChanged();
            }
        }

        [SettingPropertyBool("Balance Party Impairment", Order = 20, RequireRestart = true,
            HintText = "Scales post-battle disorganization and vulnerability durations to the 365-day year.")]
        [SettingPropertyGroup("Annual Balance")]
        public bool BalancePartyImpairment
        {
            get { return _balancePartyImpairment; }
            set { _balancePartyImpairment = value; Apply(); OnPropertyChanged(); }
        }

        [SettingPropertyBool("Balance Prisoner Recruitment", Order = 21, RequireRestart = true,
            HintText = "Scales prisoner conformity gained per campaign hour for player and AI parties.")]
        [SettingPropertyGroup("Annual Balance")]
        public bool BalancePrisonerRecruitment
        {
            get { return _balancePrisonerRecruitment; }
            set { _balancePrisonerRecruitment = value; Apply(); OnPropertyChanged(); }
        }

        [SettingPropertyBool("Balance NPC Marriage", Order = 22, RequireRestart = true,
            HintText = "Converts NPC marriage chance to preserve its annual rate across the 365-day year.")]
        [SettingPropertyGroup("Annual Balance")]
        public bool BalanceNpcMarriage
        {
            get { return _balanceNpcMarriage; }
            set { _balanceNpcMarriage = value; Apply(); OnPropertyChanged(); }
        }

        [SettingPropertyBool("Balance Map Tracks", Order = 23, RequireRestart = true,
            HintText = "Scales track lifetime while preserving native track detection and spotting rules.")]
        [SettingPropertyGroup("Annual Balance")]
        public bool BalanceMapTracks
        {
            get { return _balanceMapTracks; }
            set { _balanceMapTracks = value; Apply(); OnPropertyChanged(); }
        }

        [SettingPropertyBool("Balance Quest Deadlines", Order = 24, RequireRestart = true,
            HintText = "Extends deadlines only for quests started while this setting is enabled.")]
        [SettingPropertyGroup("Annual Balance")]
        public bool BalanceQuestDeadlines
        {
            get { return _balanceQuestDeadlines; }
            set { _balanceQuestDeadlines = value; Apply(); OnPropertyChanged(); }
        }

        [SettingPropertyBool("Annual Balance Diagnostics", Order = 25, RequireRestart = false,
            HintText = "Writes sampled annual-balance checkpoints to crash reports.")]
        [SettingPropertyGroup("Diagnostics")]
        public bool AnnualBalanceDiagnosticsEnabled
        {
            get { return _annualBalanceDiagnosticsEnabled; }
            set { _annualBalanceDiagnosticsEnabled = value; Apply(); OnPropertyChanged(); }
        }

        public static bool RegisterSettings()
        {
            if (Instance == null)
            {
                return false;
            }

            Instance.SubscribeToCoreState();
            Instance.SyncFromCoreState();
            Instance.Apply();
            return true;
        }

        private void Apply()
        {
            if (_synchronizing)
            {
                return;
            }

            CalendarSettingsState.Apply(
                CalendarSettingsState.CalendarSystem,
                _useLeapYears,
                _showDayLabel,
                _showYearLabel,
                _campaignTimeScale,
                _dateFormat,
                autoCampaignTimeScale: _autoCampaignTimeScale,
                pregnancyDurationMonths: _pregnancyDurationMonths,
                pregnancyDurationInDays: _pregnancyDurationInDays,
                useCalendarMonthPregnancy: _useCalendarMonthPregnancy,
                renownGainMultiplier: _renownGainMultiplier,
                useOrdinalDaySuffixes: _useOrdinalDaySuffixes,
                balancePartyImpairment: _balancePartyImpairment,
                balancePrisonerRecruitment: _balancePrisonerRecruitment,
                balanceNpcMarriage: _balanceNpcMarriage,
                balanceMapTracks: _balanceMapTracks,
                balanceQuestDeadlines: _balanceQuestDeadlines,
                annualBalanceDiagnosticsEnabled: _annualBalanceDiagnosticsEnabled);
            CalendarSettingsState.Save();
        }

        private void SubscribeToCoreState()
        {
            CalendarSettingsState.SettingsChanged -= OnCoreStateChanged;
            CalendarSettingsState.SettingsChanged += OnCoreStateChanged;
        }

        private void SyncFromCoreState()
        {
            _synchronizing = true;
            try
            {
                _useLeapYears = CalendarSettingsState.UseLeapYears;
                _showDayLabel = CalendarSettingsState.ShowDayLabel;
                _showYearLabel = CalendarSettingsState.ShowYearLabel;
                _useOrdinalDaySuffixes = CalendarSettingsState.UseOrdinalDaySuffixes;
                _campaignTimeScale = CalendarSettingsState.CampaignTimeScale;
                _autoCampaignTimeScale = CalendarSettingsState.AutoCampaignTimeScale;
                _dateFormat = CalendarSettingsState.DateFormat;
                _useCalendarMonthPregnancy = CalendarSettingsState.UseCalendarMonthPregnancy;
                _pregnancyDurationMonths = CalendarSettingsState.PregnancyDurationMonths;
                _pregnancyDurationInDays = CalendarSettingsState.PregnancyDurationInDays;
                _renownGainMultiplier = CalendarSettingsState.RenownGainMultiplier;
                _balancePartyImpairment = CalendarSettingsState.BalancePartyImpairment;
                _balancePrisonerRecruitment = CalendarSettingsState.BalancePrisonerRecruitment;
                _balanceNpcMarriage = CalendarSettingsState.BalanceNpcMarriage;
                _balanceMapTracks = CalendarSettingsState.BalanceMapTracks;
                _balanceQuestDeadlines = CalendarSettingsState.BalanceQuestDeadlines;
                _annualBalanceDiagnosticsEnabled = CalendarSettingsState.AnnualBalanceDiagnosticsEnabled;
            }
            finally
            {
                _synchronizing = false;
            }
        }

        private void OnCoreStateChanged()
        {
            if (_synchronizing)
            {
                return;
            }

            SyncFromCoreState();
            OnPropertyChanged(nameof(UseLeapYears));
            OnPropertyChanged(nameof(ShowDayLabel));
            OnPropertyChanged(nameof(ShowYearLabel));
            OnPropertyChanged(nameof(UseOrdinalDaySuffixes));
            OnPropertyChanged(nameof(CampaignTimeScale));
            OnPropertyChanged(nameof(AutoCampaignTimeScale));
            OnPropertyChanged(nameof(DateFormat));
            OnPropertyChanged(nameof(UseCalendarMonthPregnancy));
            OnPropertyChanged(nameof(PregnancyDurationMonths));
            OnPropertyChanged(nameof(PregnancyDurationInDays));
            OnPropertyChanged(nameof(RenownGainMultiplier));
            OnPropertyChanged(nameof(BalancePartyImpairment));
            OnPropertyChanged(nameof(BalancePrisonerRecruitment));
            OnPropertyChanged(nameof(BalanceNpcMarriage));
            OnPropertyChanged(nameof(BalanceMapTracks));
            OnPropertyChanged(nameof(BalanceQuestDeadlines));
            OnPropertyChanged(nameof(AnnualBalanceDiagnosticsEnabled));
        }
    }
}
