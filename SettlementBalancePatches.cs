using System.Reflection;
using System.Collections;
using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TwelveMonthCalendar
{
    internal static class SettlementBalanceMath
    {
        private static readonly FieldInfo BaseNumberField = AccessTools.Field(
            typeof(ExplainedNumber), "<BaseNumber>k__BackingField");
        private static readonly FieldInfo SumOfFactorsField = AccessTools.Field(
            typeof(ExplainedNumber), "<SumOfFactors>k__BackingField");
        private static readonly FieldInfo ExplainerField = AccessTools.Field(
            typeof(ExplainedNumber), "_explainer");
        private static readonly PropertyInfo ExplanationLinesProperty = ExplainerField == null
            ? null
            : AccessTools.Property(ExplainerField.FieldType, "Lines");
        private static readonly FieldInfo ExplanationLineNumberField = GetExplanationLineNumberField();

        internal static float DailyRateFactor
        {
            get { return (float)(CalendarTimeMath.NativeDaysInYear / CalendarTimeMath.AverageDaysInYear); }
        }

        internal static void Scale(ref ExplainedNumber value)
        {
            Scale(ref value, DailyRateFactor);
        }

        internal static void Scale(ref ExplainedNumber value, float factor)
        {
            // The game exposes these setters as non-public. Set the backing
            // fields directly on the ref struct so explanations and clamp
            // limits remain intact while both numeric components are scaled.
            if (BaseNumberField != null && SumOfFactorsField != null)
            {
                ScaleExplanationLines(value, factor);
                BaseNumberField.SetValueDirect(
                    __makeref(value),
                    value.BaseNumber * factor);
                SumOfFactorsField.SetValueDirect(
                    __makeref(value),
                    value.SumOfFactors * factor);
                return;
            }

            value = new ExplainedNumber(
                value.ResultNumber * factor,
                value.IncludeDescriptions,
                null);
        }

        /// <summary>
        /// ExplainedNumber keeps its visible breakdown in a separate reference
        /// object. Scaling only BaseNumber/SumOfFactors made the daily total
        /// correct but left finance tooltips at native 84-day values.
        /// </summary>
        private static void ScaleExplanationLines(ExplainedNumber value, float factor)
        {
            try
            {
                if (ExplainerField == null
                    || ExplanationLinesProperty == null
                    || ExplanationLineNumberField == null)
                {
                    return;
                }

                object explainer = ExplainerField.GetValue(value);
                IList lines = explainer == null
                    ? null
                    : ExplanationLinesProperty.GetValue(explainer, null) as IList;
                if (lines == null)
                {
                    return;
                }

                for (int index = 0; index < lines.Count; index++)
                {
                    object line = lines[index];
                    if (line == null)
                    {
                        continue;
                    }

                    float number = (float)ExplanationLineNumberField.GetValue(line);
                    ExplanationLineNumberField.SetValue(line, number * factor);
                    lines[index] = line;
                }
            }
            catch
            {
                // Explanation scaling is cosmetic. Never risk campaign
                // calculations if a future game version changes this layout.
            }
        }

        private static FieldInfo GetExplanationLineNumberField()
        {
            try
            {
                if (ExplanationLinesProperty == null)
                {
                    return null;
                }

                Type[] genericArguments = ExplanationLinesProperty.PropertyType.GetGenericArguments();
                return genericArguments.Length == 1
                    ? AccessTools.Field(genericArguments[0], "Number")
                    : null;
            }
            catch
            {
                return null;
            }
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementFoodModel), "CalculateTownFoodStocksChange")]
    internal static class SettlementFoodBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return;
            }

            SettlementBalanceMath.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultVillageProductionCalculatorModel), "CalculateDailyFoodProductionAmount")]
    internal static class VillageFoodProductionBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return;
            }

            __result *= SettlementBalanceMath.DailyRateFactor;
        }
    }

    [HarmonyPatch(typeof(DefaultVillageProductionCalculatorModel), "CalculateDailyProductionAmount")]
    internal static class VillageProductionBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return;
            }

            SettlementBalanceMath.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultWorkshopModel), "GetEffectiveConversionSpeedOfProduction")]
    internal static class WorkshopProductionBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return;
            }

            SettlementBalanceMath.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementProsperityModel), "CalculateProsperityChange")]
    internal static class SettlementProsperityBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return;
            }

            SettlementBalanceMath.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementProsperityModel), "CalculateHearthChange")]
    internal static class VillageHearthBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return;
            }

            SettlementBalanceMath.Scale(ref __result);
        }
    }
}
