using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>Adds a separately positionable season property to the native map-time VM.</summary>
    internal sealed class CalendarMapTimeControlVM : MapTimeControlVM
    {
        private bool _seasonRefreshFailureLogged;
        private string _season = string.Empty;
        private string _timeOfDay = string.Empty;
        private string _calendarDateLine = string.Empty;
        private string _seasonYearLine = string.Empty;
        private double _lastFastForwardDisplayHours = double.NaN;

        internal CalendarMapTimeControlVM(
            Func<MapBarShortcuts> getMapBarShortcuts,
            Action onTimeFlowStateChange,
            Action onCameraResetted)
            : base(getMapBarShortcuts, onTimeFlowStateChange, onCameraResetted)
        {
            CalendarSettingsState.SettingsChanged += OnCalendarSettingsChanged;
            RefreshSeason();
        }

        [DataSourceProperty]
        public string Season
        {
            get { return _season; }
            private set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(_season, normalized, StringComparison.Ordinal)) return;
                _season = normalized;
                OnPropertyChangedWithValue(_season, "Season");
            }
        }

        [DataSourceProperty]
        public string TimeOfDay
        {
            get { return _timeOfDay; }
            private set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(_timeOfDay, normalized, StringComparison.Ordinal)) return;
                _timeOfDay = normalized;
                OnPropertyChangedWithValue(_timeOfDay, "TimeOfDay");
            }
        }

        [DataSourceProperty]
        public string CalendarDateLine
        {
            get { return _calendarDateLine; }
            private set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(_calendarDateLine, normalized, StringComparison.Ordinal)) return;
                _calendarDateLine = normalized;
                OnPropertyChangedWithValue(_calendarDateLine, "CalendarDateLine");
            }
        }

        [DataSourceProperty]
        public string SeasonYearLine
        {
            get { return _seasonYearLine; }
            private set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(_seasonYearLine, normalized, StringComparison.Ordinal)) return;
                _seasonYearLine = normalized;
                OnPropertyChangedWithValue(_seasonYearLine, "SeasonYearLine");
            }
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            RefreshSeason();
        }

        internal void RefreshFastForwardDisplay()
        {
            if (Campaign.Current == null
                || !CalendarTimeMath.IsFastForwardMode(Campaign.Current.TimeControlMode))
            {
                _lastFastForwardDisplayHours = double.NaN;
                return;
            }

            // MapBarVM intentionally refreshes its native clock less often
            // during fast-forward. Keep the custom date, season, and numeric
            // clock aligned with the same CampaignTime source while time is
            // moving quickly, without issuing a property update every frame.
            double totalHours = CampaignTime.Now.ToHours;
            if (double.IsNaN(_lastFastForwardDisplayHours)
                || Math.Abs(totalHours - _lastFastForwardDisplayHours) >= 0.05d)
            {
                _lastFastForwardDisplayHours = totalHours;
                RefreshCalendarDisplay();
            }
        }

        public override void OnFinalize()
        {
            CalendarSettingsState.SettingsChanged -= OnCalendarSettingsChanged;
            base.OnFinalize();
        }

        public void ExecuteToggleWorldCalendar()
        {
            try
            {
                WorldCalendarScreen.Toggle();
            }
            catch (Exception exception)
            {
                Diagnostics.Error("World Events could not be toggled from the map bar.", exception);
            }
        }

        // Retained for compatibility with any cached or third-party prefab
        // that still invokes the former command name.
        public void ExecuteOpenWorldCalendar() { ExecuteToggleWorldCalendar(); }

        public void ExecuteOpenCamp()
        {
            try
            {
                if (!CalendarSettingsState.RefugeSystemEnabled
                    || Campaign.Current == null
                    || MobileParty.MainParty == null)
                {
                    return;
                }

                GameMenu.ActivateGameMenu(CalendarCampBehavior.CampMenuId);
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Camp menu could not be opened from the map bar.", exception);
            }
        }

        [DataSourceProperty]
        public bool IsRefugeSystemEnabled
        {
            get { return CalendarSettingsState.RefugeSystemEnabled; }
        }

        internal void RefreshSeason()
        {
            try
            {
                Season = CalendarSettingsState.GetSeasonName(CalendarTimeMath.GetSeason(CampaignTime.Now));
            }
            catch (Exception exception)
            {
                Season = string.Empty;
                if (_seasonRefreshFailureLogged) return;
                _seasonRefreshFailureLogged = true;
                Diagnostics.Error("Map-bar season refresh failed.", exception);
            }
        }

        internal void RefreshCalendarDisplay()
        {
            try
            {
                Date = CalendarFormatter.Format(CampaignTime.Now);
                CalendarDateLine = CalendarFormatter.FormatMapDateLine(CampaignTime.Now);
                SeasonYearLine = CalendarFormatter.FormatMapSeasonYearLine(CampaignTime.Now);
                RefreshSeason();
                RefreshClock();
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Map-bar calendar display refresh failed.", exception);
            }
        }

        private void OnCalendarSettingsChanged() { RefreshCalendarDisplay(); }

        internal void RefreshClock()
        {
            try
            {
                // Keep the custom numeric display on the same hour source as
                // vanilla MapTimeControlVM.Time and its native time-of-day
                // tooltip (Morning/Noon/Afternoon/Evening/Night).
                double hourInDay = CampaignTime.Now.ToHours % CampaignTime.HoursInDay;
                if (hourInDay < 0d)
                {
                    hourInDay += CampaignTime.HoursInDay;
                }

                int totalMinutes = Math.Max(0, Math.Min(1439, (int)Math.Floor(hourInDay * 60d)));
                int hour = totalMinutes / 60;
                int minute = totalMinutes % 60;
                // The native sundial widget is bound to MapTimeControlVM.Time;
                // explicitly update it beside the custom text so both visuals
                // use the exact same hour value during accelerated time.
                Time = (float)hourInDay;
                if (CalendarSettingsState.Use24HourClock)
                {
                    TimeOfDay = string.Format("{0:00}:{1:00}", hour, minute);
                    return;
                }

                string meridiem = hour < 12 ? "AM" : "PM";
                int twelveHour = hour % 12;
                TimeOfDay = string.Format("{0}:{1:00} {2}", twelveHour == 0 ? 12 : twelveHour, minute, meridiem);
            }
            catch (Exception exception)
            {
                TimeOfDay = string.Empty;
                Diagnostics.Error("Map-bar clock refresh failed.", exception);
            }
        }

    }

    /// <summary>
    /// Replaces the temporary native VM only after construction succeeds, so a
    /// changed Bannerlord field leaves the original UI intact rather than
    /// interrupting startup.
    /// </summary>
    [HarmonyPatch(typeof(MapBarVM), nameof(MapBarVM.Initialize))]
    internal static class MapBarSeasonDataSourcePatch
    {
        private static readonly FieldInfo GetMapBarShortcutsField = AccessTools.Field(typeof(MapTimeControlVM), "_getMapBarShortcuts");
        private static readonly FieldInfo OnTimeFlowStateChangeField = AccessTools.Field(typeof(MapTimeControlVM), "_onTimeFlowStateChange");
        private static readonly FieldInfo OnCameraResetField = AccessTools.Field(typeof(MapTimeControlVM), "_onCameraReset");
        private static bool _installationLogged;

        [HarmonyPostfix]
        private static void Postfix(MapBarVM __instance)
        {
            if (__instance == null) return;
            try
            {
                MapTimeControlVM original = __instance.MapTimeControl;
                if (original == null || original is CalendarMapTimeControlVM) return;
                if (GetMapBarShortcutsField == null || OnTimeFlowStateChangeField == null || OnCameraResetField == null)
                {
                    Diagnostics.Info("Map-bar season label was not installed because a required native callback field was not found.");
                    return;
                }

                Func<MapBarShortcuts> getMapBarShortcuts = GetMapBarShortcutsField.GetValue(original) as Func<MapBarShortcuts>;
                Action onTimeFlowStateChange = OnTimeFlowStateChangeField.GetValue(original) as Action;
                Action onCameraReset = OnCameraResetField.GetValue(original) as Action;
                if (getMapBarShortcuts == null || onTimeFlowStateChange == null || onCameraReset == null)
                {
                    Diagnostics.Info("Map-bar season label was not installed because a required native callback was unavailable.");
                    return;
                }

                CalendarMapTimeControlVM replacement = new CalendarMapTimeControlVM(getMapBarShortcuts, onTimeFlowStateChange, onCameraReset);
                original.OnFinalize();
                __instance.MapTimeControl = replacement;
                replacement.Refresh();
                replacement.RefreshCalendarDisplay();
                if (!_installationLogged)
                {
                    _installationLogged = true;
                    Diagnostics.Info("Dedicated map-bar season data source installed.");
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Map-bar season data-source installation failed; native map-time UI was retained where possible.", exception);
            }
        }
    }

    [HarmonyPatch(typeof(MapTimeControlVM), nameof(MapTimeControlVM.Refresh))]
    internal static class MapBarSeasonRefreshPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MapTimeControlVM __instance)
        {
            CalendarMapTimeControlVM calendarTimeControl = __instance as CalendarMapTimeControlVM;
            if (calendarTimeControl != null)
            {
                calendarTimeControl.RefreshSeason();
                calendarTimeControl.RefreshClock();
            }
        }
    }

    [HarmonyPatch(typeof(MapTimeControlVM), nameof(MapTimeControlVM.Tick))]
    internal static class MapBarFastForwardTickPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MapTimeControlVM __instance)
        {
            CalendarMapTimeControlVM calendarTimeControl = __instance as CalendarMapTimeControlVM;
            if (calendarTimeControl != null)
            {
                calendarTimeControl.RefreshFastForwardDisplay();
            }
        }
    }
}
