using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Runs after Bannerlord has completed its private clan daily tick. This
    /// gives finance telemetry a deterministic point after GiveGoldAction has
    /// applied the scaled daily result, avoiding CampaignEvents listener-order
    /// ambiguity.
    /// </summary>
    [HarmonyPatch]
    internal static class ClanFinanceFlowAuditPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickClan");
        }

        [HarmonyPostfix]
        private static void Postfix(Clan clan)
        {
            try
            {
                CalendarFinanceTelemetry.VerifyAppliedCredit(clan);
            }
            catch (Exception exception)
            {
                Diagnostics.Error("AI finance-flow telemetry failed; no campaign state was changed.", exception);
            }
        }
    }
}
