using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace TwelveMonthCalendar
{
    [HarmonyPatch(typeof(Clan), "AddRenown")]
    internal static class ClanRenownBalancePatch
    {
        [HarmonyPrefix]
        private static void Prefix([HarmonyArgument(0)] ref float value)
        {
            if (CalendarSettingsState.ExtendedCalendarEnabled && value > 0f)
            {
                value *= CalendarSettingsState.RenownGainMultiplier;
            }
        }
    }

    [HarmonyPatch(typeof(MobileParty), "get_TotalWage")]
    internal static class PartyWageBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled
                || DailyRateBalance.IsFinanceEvaluation)
            {
                return;
            }

            __result = (int)Math.Round(
                __result * CalendarTimeMath.NativeDaysInYear / CalendarTimeMath.AverageDaysInYear);
        }
    }

    [HarmonyPatch(typeof(PregnancyCampaignBehavior), "ChildConceived")]
    internal static class PregnancyDueDatePatch
    {
        private static readonly FieldInfo PregnancyListField = AccessTools.Field(
            typeof(PregnancyCampaignBehavior),
            "_heroPregnancies");

        [HarmonyPostfix]
        private static void Postfix(
            PregnancyCampaignBehavior __instance,
            Hero mother)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled || PregnancyListField == null)
            {
                return;
            }

            try
            {
                IEnumerable pregnancies = PregnancyListField.GetValue(__instance) as IEnumerable;
                if (pregnancies == null)
                {
                    return;
                }

                foreach (object pregnancy in pregnancies)
                {
                    if (pregnancy == null)
                    {
                        continue;
                    }

                    FieldInfo motherField = AccessTools.Field(pregnancy.GetType(), "Mother");
                    FieldInfo dueDateField = AccessTools.Field(pregnancy.GetType(), "DueDate");
                    if (motherField == null || dueDateField == null || motherField.GetValue(pregnancy) != mother)
                    {
                        continue;
                    }

                    dueDateField.SetValue(
                        pregnancy,
                        CalendarTimeMath.GetPregnancyDueDate(CampaignTime.Now));
                    Diagnostics.Info(
                        string.Format(
                            "Pregnancy due date adjusted using {0} calendar months ({1:F2} fixed days fallback).",
                            CalendarSettingsState.PregnancyDurationMonths,
                            CalendarSettingsState.PregnancyDurationInDays));
                    return;
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Pregnancy due-date adjustment failed.", exception);
            }
        }
    }
}
