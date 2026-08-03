using System.Reflection;
using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace TwelveMonthCalendar
{
    internal static class SettlementBalanceMath
    {
        private static readonly FieldInfo BaseNumberField = AccessTools.Field(
            typeof(ExplainedNumber), "<BaseNumber>k__BackingField");

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
            // ExplainedNumber evaluates as BaseNumber * (1 + SumOfFactors).
            // Scale its base only: scaling factors too changes native
            // modifiers (especially negative finance modifiers) instead of
            // simply converting a daily result to the Gregorian cadence.
            // Limits remain intentionally untouched.
            if (BaseNumberField != null)
            {
                BaseNumberField.SetValueDirect(
                    __makeref(value),
                    value.BaseNumber * factor);
                return;
            }

            value = new ExplainedNumber(
                value.ResultNumber * factor,
                value.IncludeDescriptions,
                null);
        }

    }

    // Food is a coordinated system. The settlement-food wrapper scales direct
    // town sources and consumption; food goods entering markets are scaled at
    // their village/workshop sources and must not be scaled again there.

    [HarmonyPatch(typeof(DefaultVillageProductionCalculatorModel), "CalculateDailyFoodProductionAmount")]
    internal static class VillageFoodProductionBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultVillageProductionCalculatorModel), "CalculateDailyProductionAmount")]
    internal static class VillageProductionBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemObject item, ref ExplainedNumber __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return;
            }

            // Food goods enter the market before the settlement wrapper adds
            // its market component, so they receive their one annual scale
            // here.
            if (item != null
                && item.ItemCategory != null
                && item.ItemCategory.Properties == ItemCategory.Property.BonusToFoodStores)
            {
                SettlementBalanceMath.Scale(ref __result);
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

            // WorkshopModel does not expose its output category. The scoped
            // context is set only while Bannerlord runs a specific workshop.
            if (WorkshopFoodContext.ProducesFood(WorkshopFoodContext.ActiveWorkshop))
            {
                SettlementBalanceMath.Scale(ref __result);
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
