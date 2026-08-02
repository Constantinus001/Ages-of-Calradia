using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Engine.Options;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.Options;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Adds a real Calendar page to Bannerlord's native Settings screen.
    /// The native Options movie is created with OptionsVM, so its data source
    /// is replaced with a proxy that exposes one additional category without
    /// requiring MCM. The hook is deliberately restricted to that one movie
    /// and the unmodified native OptionsVM type.
    /// </summary>
    [HarmonyPatch]
    internal static class CalendarOptionsMoviePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Constructor(
                typeof(GauntletMovie),
                new[]
                {
                    typeof(string),
                    typeof(UIContext),
                    typeof(WidgetFactory),
                    typeof(IViewModel),
                    typeof(bool)
                });
        }

        [HarmonyPrefix]
        private static void Prefix(
            [HarmonyArgument(0)] string movieName,
            [HarmonyArgument(3)] ref IViewModel viewModel)
        {
            if (!string.Equals(movieName, "Options", StringComparison.Ordinal))
            {
                return;
            }

            OptionsVM options = viewModel as OptionsVM;
            if (options == null
                || options is CalendarOptionsVM
                || options.GetType() != typeof(OptionsVM))
            {
                return;
            }

            try
            {
                Diagnostics.Info("Native Settings OptionsVM detected; creating the Calendar category.");
                viewModel = new CalendarOptionsVM(options);
                Diagnostics.Info("Native Settings screen extended with the Calendar tab.");
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Native Calendar Settings tab could not be created; original settings remain active.", exception);
            }
        }
    }

    [HarmonyPatch(typeof(GenericOptionDataVM), "RefreshValues")]
    internal static class CalendarOptionLabelRefreshPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GenericOptionDataVM __instance)
        {
            CalendarOptionDataBase calendarOption = __instance.GetOptionData() as CalendarOptionDataBase;
            if (calendarOption != null)
            {
                __instance.Name = calendarOption.Name;
                __instance.Description = calendarOption.Description;
            }
        }
    }

    // The native GauntletOptionsScreen retains its original OptionsVM and owns
    // the active-state registration used to close the screen. The Calendar VM
    // is only the movie's data source, so it must never close the screen itself.
    [HarmonyPatch(typeof(OptionsVM), nameof(OptionsVM.ExecuteCancel))]
    internal static class CalendarOptionsCancelPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(OptionsVM __instance)
        {
            CalendarOptionsVM calendarOptions = __instance as CalendarOptionsVM;
            return calendarOptions == null || !calendarOptions.TryCloseThroughNativeOptions(false);
        }
    }

    [HarmonyPatch(typeof(OptionsVM), nameof(OptionsVM.ExecuteDone))]
    internal static class CalendarOptionsDonePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(OptionsVM __instance)
        {
            CalendarOptionsVM calendarOptions = __instance as CalendarOptionsVM;
            return calendarOptions == null || !calendarOptions.TryCloseThroughNativeOptions(true);
        }
    }

    internal sealed class CalendarOptionsVM : OptionsVM
    {
        private readonly OptionsVM _nativeOptions;
        private readonly GroupedOptionCategoryVM _calendarOptions;

        [DataSourceProperty]
        public GroupedOptionCategoryVM CalendarOptions
        {
            get { return _calendarOptions; }
        }

        internal CalendarOptionsVM(OptionsVM source)
            : base(
                source.CurrentOptionsMode,
                delegate { },
                delegate(KeyOptionVM key) { },
                delegate { },
                delegate { })
        {
            _nativeOptions = source;
            CopyNativeState(source);
            DetachProxyGamepadHandler();

            bool nativeCalendarSettingsEnabled = !OptionalMcmIntegration.IsSettingsRegistered;

            List<IOptionData> options = new List<IOptionData>
            {
                new CalendarSelectionOptionData(
                    "Calendar System",
                    new[] { "Gregorian 12-Month", "Native 84-Day" },
                    delegate { return CalendarSettingsState.ExtendedCalendarEnabled ? 0f : 1f; },
                    delegate(float value)
                    {
                        Apply(calendarSystem: value < 0.5f ? "Gregorian12Month" : "Native84Day");
                    }),
                new CalendarBooleanOptionData(
                    "Use Leap Years",
                    delegate { return CalendarSettingsState.UseLeapYears; },
                    delegate(bool value) { Apply(useLeapYears: value); }),
                new CalendarBooleanOptionData(
                    "Show Day Label",
                    delegate { return CalendarSettingsState.ShowDayLabel; },
                    delegate(bool value) { Apply(showDayLabel: value); }),
                new CalendarBooleanOptionData(
                    "Show Year Label",
                    delegate { return CalendarSettingsState.ShowYearLabel; },
                    delegate(bool value) { Apply(showYearLabel: value); }),
                new CalendarBooleanOptionData(
                    "Use Ordinal Day Suffixes",
                    delegate { return CalendarSettingsState.UseOrdinalDaySuffixes; },
                    delegate(bool value) { Apply(useOrdinalDaySuffixes: value); }),
                new CalendarBooleanOptionData(
                    "Automatic Campaign Time Scale",
                    delegate { return CalendarSettingsState.AutoCampaignTimeScale; },
                    delegate(bool value) { Apply(autoCampaignTimeScale: value); }),
                new CalendarNumericOptionData(
                    "Campaign Time Scale",
                    0.01f,
                    1.0f,
                    false,
                    0,
                    delegate { return CalendarSettingsState.CampaignTimeScale; },
                    delegate(float value) { Apply(campaignTimeScale: value, autoCampaignTimeScale: false); }),
                new CalendarSelectionOptionData(
                    "Date Format",
                    new[] { "Day-Month-Year", "Month-Day-Year", "Year-Month-Day" },
                    GetDateFormatIndex,
                    delegate(float value) { Apply(dateFormat: GetDateFormats()[ClampIndex(value, GetDateFormats().Length)]); }),
                new CalendarBooleanOptionData(
                    "Use Calendar-Month Pregnancy",
                    delegate { return CalendarSettingsState.UseCalendarMonthPregnancy; },
                    delegate(bool value) { Apply(useCalendarMonthPregnancy: value); }),
                new CalendarNumericOptionData(
                    "Pregnancy Duration (Months)",
                    1f,
                    24f,
                    true,
                    1,
                    delegate { return CalendarSettingsState.PregnancyDurationMonths; },
                    delegate(float value) { Apply(pregnancyDurationMonths: Math.Max(1, (int)value)); }),
                new CalendarNumericOptionData(
                    "Fixed Pregnancy Duration (Days)",
                    0.1f,
                    10000f,
                    false,
                    0,
                    delegate { return CalendarSettingsState.PregnancyDurationInDays; },
                    delegate(float value) { Apply(pregnancyDurationInDays: value); }),
                new CalendarNumericOptionData(
                    "Renown Gain Multiplier",
                    0f,
                    1f,
                    false,
                    0,
                    delegate { return CalendarSettingsState.RenownGainMultiplier; },
                    delegate(float value) { Apply(renownGainMultiplier: value); }),
                new CalendarResetOptionData(
                    "Reset Calendar Settings",
                    delegate { CalendarSettingsState.ResetToDefaults(); })
            };

            OptionCategory category = new OptionCategory(
                new List<IOptionData>(),
                new[] { new OptionGroup(new TextObject("Calendar"), options) });
            _calendarOptions = new GroupedOptionCategoryVM(
                this,
                new TextObject("Calendar"),
                category,
                nativeCalendarSettingsEnabled,
                true);

            if (!nativeCalendarSettingsEnabled)
            {
                Diagnostics.Info("Native Calendar Options tab disabled because MCM settings are active.");
            }

            ApplyCalendarOptionLabels(options);

            AddCategoryToNativeLists(_calendarOptions);
        }

        internal bool TryCloseThroughNativeOptions(bool applyChanges)
        {
            if (_nativeOptions == null)
            {
                Diagnostics.Info("Calendar Settings proxy could not find the native OptionsVM while closing.");
                return false;
            }

            try
            {
                Diagnostics.Info(
                    "Calendar Settings close routed through the native OptionsVM. ApplyChanges="
                    + applyChanges + ".");
                if (applyChanges)
                {
                    _nativeOptions.ExecuteDone();
                }
                else
                {
                    _nativeOptions.ExecuteCancel();
                }

                return true;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Calendar Settings could not route screen close through the native OptionsVM.", exception);
                return false;
            }
        }

        private void DetachProxyGamepadHandler()
        {
            try
            {
                MethodInfo method = AccessTools.Method(typeof(OptionsVM), "OnGamepadActiveStateChanged");
                if (method == null)
                {
                    Diagnostics.Info("Calendar Settings proxy could not locate the native gamepad handler.");
                    return;
                }

                Action handler = (Action)Delegate.CreateDelegate(typeof(Action), this, method);
                Input.OnGamepadActiveStateChanged = (Action)Delegate.Remove(
                    Input.OnGamepadActiveStateChanged,
                    handler);
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Calendar Settings proxy could not detach its unused gamepad handler.", exception);
            }
        }

        private void ApplyCalendarOptionLabels(IList<IOptionData> options)
        {
            if (_calendarOptions.Groups == null || _calendarOptions.Groups.Count == 0)
            {
                return;
            }

            OptionGroupVM group = _calendarOptions.Groups[0];
            for (int i = 0; i < options.Count && i < group.Options.Count; i++)
            {
                CalendarOptionDataBase calendarOption = options[i] as CalendarOptionDataBase;
                if (calendarOption != null)
                {
                    group.Options[i].Name = calendarOption.Name;
                    group.Options[i].Description = calendarOption.Description;
                }
            }
        }

        private void CopyNativeState(OptionsVM source)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (FieldInfo field in typeof(OptionsVM).GetFields(flags))
            {
                field.SetValue(this, field.GetValue(source));
            }

            CloneListField("_categories");
            CloneListField("_groupedCategories");
        }

        private void CloneListField(string fieldName)
        {
            FieldInfo field = AccessTools.Field(typeof(OptionsVM), fieldName);
            if (field == null)
            {
                return;
            }

            IList original = field.GetValue(this) as IList;
            IList clone = Activator.CreateInstance(field.FieldType) as IList;
            if (original == null || clone == null)
            {
                return;
            }

            foreach (object value in original)
            {
                clone.Add(value);
            }

            field.SetValue(this, clone);
        }

        private void AddCategoryToNativeLists(GroupedOptionCategoryVM category)
        {
            AddToListField("_categories", category, 4);
            AddToListField("_groupedCategories", category, 4);
        }

        private void AddToListField(string fieldName, object value, int index)
        {
            FieldInfo field = AccessTools.Field(typeof(OptionsVM), fieldName);
            IList list = field == null ? null : field.GetValue(this) as IList;
            if (list == null)
            {
                return;
            }

            list.Insert(Math.Min(index, list.Count), value);
        }

        private static void Apply(
            string calendarSystem = null,
            bool? useLeapYears = null,
            bool? showDayLabel = null,
            bool? showYearLabel = null,
            bool? useOrdinalDaySuffixes = null,
            float? campaignTimeScale = null,
            string dateFormat = null,
            bool? autoCampaignTimeScale = null,
            bool? useCalendarMonthPregnancy = null,
            int? pregnancyDurationMonths = null,
            float? pregnancyDurationInDays = null,
            float? renownGainMultiplier = null)
        {
            string requestedCalendarSystem = calendarSystem ?? CalendarSettingsState.CalendarSystem;
            bool requestedLeapYears = useLeapYears ?? CalendarSettingsState.UseLeapYears;
            bool requestedShowDayLabel = showDayLabel ?? CalendarSettingsState.ShowDayLabel;
            bool requestedShowYearLabel = showYearLabel ?? CalendarSettingsState.ShowYearLabel;
            bool requestedOrdinalDaySuffixes = useOrdinalDaySuffixes ?? CalendarSettingsState.UseOrdinalDaySuffixes;
            float requestedCampaignTimeScale = campaignTimeScale ?? CalendarSettingsState.CampaignTimeScale;
            string requestedDateFormat = dateFormat ?? CalendarSettingsState.DateFormat;
            bool requestedAutoTimeScale = autoCampaignTimeScale ?? CalendarSettingsState.AutoCampaignTimeScale;
            bool requestedCalendarMonthPregnancy = useCalendarMonthPregnancy
                ?? CalendarSettingsState.UseCalendarMonthPregnancy;
            int requestedPregnancyMonths = pregnancyDurationMonths
                ?? CalendarSettingsState.PregnancyDurationMonths;
            float requestedPregnancyDays = pregnancyDurationInDays
                ?? CalendarSettingsState.PregnancyDurationInDays;
            float requestedRenownMultiplier = renownGainMultiplier
                ?? CalendarSettingsState.RenownGainMultiplier;

            // Bannerlord initializes option controls by writing their current
            // value back to the data source. Do not treat those UI refreshes as
            // genuine settings edits or repeatedly save/synchronize the file.
            if (string.Equals(requestedCalendarSystem, CalendarSettingsState.CalendarSystem, StringComparison.OrdinalIgnoreCase)
                && requestedLeapYears == CalendarSettingsState.UseLeapYears
                && requestedShowDayLabel == CalendarSettingsState.ShowDayLabel
                && requestedShowYearLabel == CalendarSettingsState.ShowYearLabel
                && requestedOrdinalDaySuffixes == CalendarSettingsState.UseOrdinalDaySuffixes
                && NearlyEqual(requestedCampaignTimeScale, CalendarSettingsState.CampaignTimeScale)
                && string.Equals(requestedDateFormat, CalendarSettingsState.DateFormat, StringComparison.Ordinal)
                && requestedAutoTimeScale == CalendarSettingsState.AutoCampaignTimeScale
                && requestedCalendarMonthPregnancy == CalendarSettingsState.UseCalendarMonthPregnancy
                && requestedPregnancyMonths == CalendarSettingsState.PregnancyDurationMonths
                && NearlyEqual(requestedPregnancyDays, CalendarSettingsState.PregnancyDurationInDays)
                && NearlyEqual(requestedRenownMultiplier, CalendarSettingsState.RenownGainMultiplier))
            {
                return;
            }

            CalendarSettingsState.Apply(
                requestedCalendarSystem,
                requestedLeapYears,
                requestedShowDayLabel,
                requestedShowYearLabel,
                requestedCampaignTimeScale,
                requestedDateFormat,
                autoCampaignTimeScale: requestedAutoTimeScale,
                useCalendarMonthPregnancy: requestedCalendarMonthPregnancy,
                pregnancyDurationMonths: requestedPregnancyMonths,
                pregnancyDurationInDays: requestedPregnancyDays,
                renownGainMultiplier: requestedRenownMultiplier,
                useOrdinalDaySuffixes: requestedOrdinalDaySuffixes);
            CalendarSettingsState.Save();
        }

        private static bool NearlyEqual(float first, float second)
        {
            return Math.Abs(first - second) < 0.0001f;
        }

        private static string[] GetDateFormats()
        {
            return new[]
            {
                "{Day} {Month} {Year}",
                "{Month} {Day} {Year}",
                "{Year} {Month} {Day}"
            };
        }

        private static float GetDateFormatIndex()
        {
            int index = Array.IndexOf(GetDateFormats(), CalendarSettingsState.DateFormat);
            return index < 0 ? 1f : index;
        }

        private static int ClampIndex(float value, int length)
        {
            return Math.Max(0, Math.Min(length - 1, (int)value));
        }
    }

    internal abstract class CalendarOptionDataBase : IOptionData
    {
        private readonly string _name;

        protected CalendarOptionDataBase(string name)
        {
            _name = name;
        }

        internal string Name
        {
            get { return _name; }
        }

        internal string Description
        {
            get
            {
                switch (_name)
                {
                    case "Calendar System":
                        return "Choose between the Gregorian 12-month calendar and Bannerlord's native 84-day calendar.";
                    case "Use Leap Years":
                        return "Adds February 29 in Gregorian leap years and keeps the calendar synchronized with leap-year rules.";
                    case "Show Day Label":
                        return "Displays the word 'Day' before the day number on the campaign map date.";
                    case "Show Year Label":
                        return "Displays the word 'Year' before the year number on the campaign map date.";
                    case "Use Ordinal Day Suffixes":
                        return "Displays dates as 1st, 2nd, 3rd, and so on. 11th, 12th, and 13th use the correct th suffix.";
                    case "Automatic Campaign Time Scale":
                        return "Automatically calculates campaign pacing from the configured calendar year length.";
                    case "Campaign Time Scale":
                        return "Controls how quickly campaign time advances when automatic pacing is disabled. Lower values are slower.";
                    case "Date Format":
                        return "Select the order of the month, day, and year. The season is always displayed first.";
                    case "Use Calendar-Month Pregnancy":
                        return "Uses calendar months for pregnancy duration instead of the fixed day value.";
                    case "Pregnancy Duration (Months)":
                        return "Sets how many calendar months a pregnancy lasts when calendar-month pregnancy is enabled.";
                    case "Fixed Pregnancy Duration (Days)":
                        return "Sets pregnancy length in days when calendar-month pregnancy is disabled. The default is 273.75 days.";
                    case "Renown Gain Multiplier":
                        return "Scales positive renown rewards. A value of 0.50 gives half the normal positive renown.";
                    case "Reset Calendar Settings":
                        return "Click to restore the mod's default calendar, pacing, display, pregnancy, and renown settings.";
                    default:
                        return string.Empty;
                }
            }
        }

        public abstract float GetDefaultValue();

        public virtual void Commit()
        {
            CalendarSettingsState.Save();
        }

        public abstract float GetValue(bool forceRefresh);

        public abstract void SetValue(float value);

        public abstract object GetOptionType();

        public bool IsNative()
        {
            return false;
        }

        public virtual bool IsAction()
        {
            return false;
        }

        public (string, bool) GetIsDisabledAndReasonID()
        {
            return (string.Empty, false);
        }
    }

    internal sealed class CalendarBooleanOptionData : CalendarOptionDataBase, IBooleanOptionData
    {
        private readonly Func<bool> _get;
        private readonly Action<bool> _set;
        private readonly bool _default;

        internal CalendarBooleanOptionData(string name, Func<bool> get, Action<bool> set)
            : base(name)
        {
            _get = get;
            _set = set;
            _default = get();
        }

        public override float GetDefaultValue()
        {
            return _default ? 1f : 0f;
        }

        public override float GetValue(bool forceRefresh)
        {
            return _get() ? 1f : 0f;
        }

        public override void SetValue(float value)
        {
            _set(value >= 0.5f);
        }

        public override object GetOptionType()
        {
            return OptionsVM.OptionsDataType.BooleanOption;
        }
    }

    internal sealed class CalendarNumericOptionData : CalendarOptionDataBase, INumericOptionData
    {
        private readonly float _min;
        private readonly float _max;
        private readonly bool _discrete;
        private readonly int _interval;
        private readonly Func<float> _get;
        private readonly Action<float> _set;
        private readonly float _default;

        internal CalendarNumericOptionData(
            string name,
            float min,
            float max,
            bool discrete,
            int interval,
            Func<float> get,
            Action<float> set)
            : base(name)
        {
            _min = min;
            _max = max;
            _discrete = discrete;
            _interval = interval;
            _get = get;
            _set = set;
            _default = get();
        }

        public override float GetDefaultValue()
        {
            return _default;
        }

        public override float GetValue(bool forceRefresh)
        {
            return Math.Max(_min, Math.Min(_max, _get()));
        }

        public override void SetValue(float value)
        {
            _set(Math.Max(_min, Math.Min(_max, value)));
        }

        public override object GetOptionType()
        {
            return OptionsVM.OptionsDataType.NumericOption;
        }

        public float GetMinValue()
        {
            return _min;
        }

        public float GetMaxValue()
        {
            return _max;
        }

        public bool GetIsDiscrete()
        {
            return _discrete;
        }

        public int GetDiscreteIncrementInterval()
        {
            return _interval;
        }

        public bool GetShouldUpdateContinuously()
        {
            return !_discrete;
        }
    }

    internal sealed class CalendarSelectionOptionData : CalendarOptionDataBase, ISelectionOptionData
    {
        private readonly string[] _names;
        private readonly Func<float> _get;
        private readonly Action<float> _set;

        internal CalendarSelectionOptionData(
            string name,
            string[] names,
            Func<float> get,
            Action<float> set)
            : base(name)
        {
            _names = names;
            _get = get;
            _set = set;
        }

        public override float GetDefaultValue()
        {
            return _get();
        }

        public override float GetValue(bool forceRefresh)
        {
            return _get();
        }

        public override void SetValue(float value)
        {
            _set(value);
        }

        public override object GetOptionType()
        {
            return OptionsVM.OptionsDataType.MultipleSelectionOption;
        }

        public int GetSelectableOptionsLimit()
        {
            return _names.Length;
        }

        public IEnumerable<SelectionData> GetSelectableOptionNames()
        {
            for (int i = 0; i < _names.Length; i++)
            {
                yield return new SelectionData(false, _names[i]);
            }
        }
    }

    internal sealed class CalendarResetOptionData : CalendarOptionDataBase, IBooleanOptionData
    {
        private readonly Action _action;

        internal CalendarResetOptionData(string name, Action action)
            : base(name)
        {
            _action = action;
        }

        public override float GetDefaultValue()
        {
            return 0f;
        }

        public override float GetValue(bool forceRefresh)
        {
            return 0f;
        }

        public override void SetValue(float value)
        {
            if (value >= 0.5f)
            {
                _action();
            }
        }

        public override object GetOptionType()
        {
            return OptionsVM.OptionsDataType.BooleanOption;
        }
    }
}
