using System.Reflection;
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
