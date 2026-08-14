using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Engine.Options;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.Options;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;

namespace AgesOfCalradia.PoliticalSettingsBridge
{
    [HarmonyPatch]
    internal static class NativePoliticalOptionsPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type calendarOptionsType = AccessTools.TypeByName(
                "TwelveMonthCalendar.CalendarOptionsVM");
            if (calendarOptionsType == null) return new MethodBase[0];
            return AccessTools.GetDeclaredConstructors(calendarOptionsType);
        }

        [HarmonyPostfix]
        private static void Postfix(OptionsVM __instance)
        {
            if (__instance == null) return;

            FieldInfo calendarField = AccessTools.Field(
                __instance.GetType(),
                "_calendarOptions");
            GroupedOptionCategoryVM calendar = calendarField == null
                ? null
                : calendarField.GetValue(__instance) as GroupedOptionCategoryVM;
            if (calendar == null || HasPoliticalGroup(calendar)) return;

            List<IOptionData> options = new List<IOptionData>
            {
                new BridgeNumericOptionData(
                    "Control Fill Opacity",
                    "Controls mainland political color opacity.",
                    10f,
                    100f,
                    delegate { return PoliticalSettingsBridgeSubModule.OpacityPercent; },
                    delegate(float value) { PoliticalSettingsBridgeSubModule.SetOpacityPercent((int)value); },
                    100f),
                new BridgeNumericOptionData(
                    "Control Color Brightness",
                    "Scales faction RGB brightness. Reload the campaign map after changing it.",
                    25f,
                    125f,
                    delegate { return PoliticalSettingsBridgeSubModule.BrightnessPercent; },
                    delegate(float value) { PoliticalSettingsBridgeSubModule.SetBrightnessPercent((int)value); },
                    100f),
                new BridgeBooleanOptionData(
                    "Keep Rivers and Lakes Solid",
                    "Keeps political water entities fully colored when mainland opacity is reduced.",
                    delegate { return PoliticalSettingsBridgeSubModule.SolidWater; },
                    delegate(bool value) { PoliticalSettingsBridgeSubModule.SetSolidWater(value); },
                    true)
            };

            OptionGroupVM politicalGroup = new OptionGroupVM(
                new TextObject("Political Map"),
                __instance,
                options);
            calendar.Groups.Add(politicalGroup);
            PoliticalSettingsBridgeSubModule.WriteLog(
                "Political Map group appended to the visible Calendar settings tab.");
        }

        private static bool HasPoliticalGroup(GroupedOptionCategoryVM calendar)
        {
            foreach (OptionGroupVM group in calendar.Groups)
            {
                if (group != null
                    && string.Equals(group.Name, "Political Map", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(GenericOptionDataVM), "RefreshValues")]
    internal static class NativePoliticalOptionLabelPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GenericOptionDataVM __instance)
        {
            BridgeOptionData option = __instance == null
                ? null
                : __instance.GetOptionData() as BridgeOptionData;
            if (option == null) return;
            __instance.Name = option.DisplayName;
            __instance.Description = option.Description;
        }
    }

    internal abstract class BridgeOptionData : IOptionData
    {
        internal string DisplayName { get; private set; }
        internal string Description { get; private set; }

        protected BridgeOptionData(string name, string description)
        {
            DisplayName = name;
            Description = description;
        }

        public abstract float GetDefaultValue();
        public abstract float GetValue(bool forceRefresh);
        public abstract void SetValue(float value);
        public abstract object GetOptionType();
        public void Commit() { }
        public bool IsNative() { return false; }
        public bool IsAction() { return false; }
        public (string, bool) GetIsDisabledAndReasonID() { return (string.Empty, false); }
    }

    internal sealed class BridgeNumericOptionData : BridgeOptionData, INumericOptionData
    {
        private readonly float _minimum;
        private readonly float _maximum;
        private readonly Func<float> _get;
        private readonly Action<float> _set;
        private readonly float _default;

        internal BridgeNumericOptionData(
            string name,
            string description,
            float minimum,
            float maximum,
            Func<float> get,
            Action<float> set,
            float defaultValue)
            : base(name, description)
        {
            _minimum = minimum;
            _maximum = maximum;
            _get = get;
            _set = set;
            _default = defaultValue;
        }

        public override float GetDefaultValue() { return _default; }
        public override float GetValue(bool forceRefresh)
        {
            return Math.Max(_minimum, Math.Min(_maximum, _get()));
        }
        public override void SetValue(float value)
        {
            _set(Math.Max(_minimum, Math.Min(_maximum, value)));
        }
        public override object GetOptionType() { return OptionsVM.OptionsDataType.NumericOption; }
        public float GetMinValue() { return _minimum; }
        public float GetMaxValue() { return _maximum; }
        public bool GetIsDiscrete() { return true; }
        public int GetDiscreteIncrementInterval() { return 1; }
        public bool GetShouldUpdateContinuously() { return false; }
        public int GetNumberOfDiscreteValues() { return (int)(_maximum - _minimum) + 1; }
        public int GetValueIndex() { return (int)(GetValue(false) - _minimum); }
        public float GetValueAtIndex(int index) { return _minimum + index; }
        public int GetCurrentValueIndex() { return GetValueIndex(); }
        public string GetValueAsString() { return ((int)GetValue(false)).ToString(); }
    }

    internal sealed class BridgeBooleanOptionData : BridgeOptionData, IBooleanOptionData
    {
        private readonly Func<bool> _get;
        private readonly Action<bool> _set;
        private readonly bool _default;

        internal BridgeBooleanOptionData(
            string name,
            string description,
            Func<bool> get,
            Action<bool> set,
            bool defaultValue)
            : base(name, description)
        {
            _get = get;
            _set = set;
            _default = defaultValue;
        }

        public override float GetDefaultValue() { return _default ? 1f : 0f; }
        public override float GetValue(bool forceRefresh) { return _get() ? 1f : 0f; }
        public override void SetValue(float value) { _set(value >= 0.5f); }
        public override object GetOptionType() { return OptionsVM.OptionsDataType.BooleanOption; }
    }
}
