using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
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

}
