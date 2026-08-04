using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>Adds a separately positionable season property to the native map-time VM.</summary>
    internal sealed class CalendarMapTimeControlVM : MapTimeControlVM
    {
        private bool _seasonRefreshFailureLogged;
        private string _season = string.Empty;

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

        public override void RefreshValues()
        {
            base.RefreshValues();
            RefreshSeason();
        }

        public override void OnFinalize()
        {
            CalendarSettingsState.SettingsChanged -= OnCalendarSettingsChanged;
            base.OnFinalize();
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
                RefreshSeason();
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Map-bar calendar display refresh failed.", exception);
            }
        }

        private void OnCalendarSettingsChanged() { RefreshCalendarDisplay(); }
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
            if (calendarTimeControl != null) calendarTimeControl.RefreshSeason();
        }
    }
}
