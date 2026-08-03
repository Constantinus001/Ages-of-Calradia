using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Extends a new quest's deadline once, at the stable public StartQuest
    /// boundary. Existing saves are untouched and no issue-specific/private
    /// quest fields are accessed.
    /// </summary>
    [HarmonyPatch]
    internal static class QuestDeadlineBalancePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase target = AccessTools.Method(typeof(QuestBase), nameof(QuestBase.StartQuest), Type.EmptyTypes);
            if (target == null)
            {
                Diagnostics.Info("Quest-deadline balance target was not found; quest deadlines remain native.");
                return new MethodBase[0];
            }

            return new[] { target };
        }

        [HarmonyPostfix]
        private static void Postfix(QuestBase __instance)
        {
            if (!CalendarSettingsState.BalanceQuestDeadlines || __instance == null)
            {
                return;
            }

            try
            {
                CampaignTime now = CampaignTime.Now;
                CampaignTime dueTime = __instance.QuestDueTime;
                double nativeRemainingDays = dueTime.ToDays - now.ToDays;
                if (nativeRemainingDays <= 0d)
                {
                    return;
                }

                double annualRemainingDays = nativeRemainingDays * CalendarAnnualBalance.DurationFactor;
                __instance.ChangeQuestDueTime(now + CampaignTime.Days((float)annualRemainingDays));
                CalendarAnnualBalanceDiagnostics.RecordQuestDeadline(nativeRemainingDays, annualRemainingDays);
            }
            catch (Exception exception)
            {
                // Quest timing is a quality-of-life balance feature. It must
                // never prevent Bannerlord from starting a quest.
                CalendarAnnualBalanceDiagnostics.RecordException("QuestDeadline.StartQuest", exception);
            }
        }
    }

    /// <summary>
    /// Observes Bannerlord's native hideout day/night rule. This target is
    /// resolved dynamically so a future engine update simply disables this
    /// optional diagnostic instead of breaking campaign startup.
    /// </summary>
    [HarmonyPatch]
    internal static class HideoutDayNightDiagnosticsPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase target = AccessTools.Method(typeof(HideoutCampaignBehavior), "IsItNighttimeNow", Type.EmptyTypes);
            if (target == null)
            {
                Diagnostics.Info("Hideout day/night diagnostic target was not found; the native behavior remains untouched.");
                return new MethodBase[0];
            }

            return new[] { target };
        }

        [HarmonyPostfix]
        private static void Postfix(bool __result)
        {
            CalendarAnnualBalanceDiagnostics.RecordHideoutDayNightCheck(__result);
        }
    }

    /// <summary>
    /// Records that the native player-romance behavior is running. Romance
    /// cooldown state is deliberately not modified because Bannerlord exposes
    /// no public setter for its persisted courtship-attempt data.
    /// </summary>
    [HarmonyPatch]
    internal static class PlayerRomanceDiagnosticsPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase target = AccessTools.Method(typeof(RomanceCampaignBehavior), "DailyTick", Type.EmptyTypes);
            if (target == null)
            {
                Diagnostics.Info("Player-romance diagnostic target was not found; the native behavior remains untouched.");
                return new MethodBase[0];
            }

            return new[] { target };
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            CalendarAnnualBalanceDiagnostics.RecordRomanceDailyTick();
        }
    }
}
