using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Gives Bannerlord's existing caravan destination score a small, bounded
    /// preference for towns whose market is short on liquid purchasing power or
    /// food. Native distance, ownership, siege, navigation, trade, and cargo
    /// rules remain responsible for the actual route; this only changes the
    /// ordering of otherwise valid destinations.
    /// </summary>
    [HarmonyPatch]
    internal static class CaravanTradePriorityPatch
    {
        private const int LowGoldThreshold = 2500;
        private const float MaximumPriorityBonus = 0.35f;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(CaravansCampaignBehavior), "GetTradeScoreForTown");
        }

        [HarmonyPostfix]
        private static void Postfix(Town town, ref float __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled || town == null || __result <= 0f)
            {
                return;
            }

            try
            {
                float shortagePressure = GetShortagePressure(town);
                if (shortagePressure > 0f)
                {
                    __result *= 1f + Math.Min(MaximumPriorityBonus, shortagePressure);
                }
            }
            catch (Exception exception)
            {
                // A missing/changed market API must never prevent native caravan
                // routing from running.
                Diagnostics.Error("Caravan shortage-priority calculation failed; native score retained.", exception);
            }
        }

        private static float GetShortagePressure(Town town)
        {
            Settlement settlement = town.Settlement;
            if (settlement == null || settlement.IsUnderSiege || settlement.IsStarving)
            {
                return 0f;
            }

            float pressure = 0f;
            if (town.FoodStocks < 80f)
            {
                pressure += Math.Min(0.16f, (80f - town.FoodStocks) / 500f);
            }

            if (town.Gold < LowGoldThreshold)
            {
                pressure += Math.Min(0.10f, (LowGoldThreshold - town.Gold) / 25000f);
            }

            return Math.Max(0f, Math.Min(MaximumPriorityBonus, pressure));
        }
    }
}
