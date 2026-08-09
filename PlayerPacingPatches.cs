using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Keeps heroes from appearing as children when a save created before the
    /// calendar compatibility marker is opened. The native Hero.Age getter
    /// uses CampaignTime.ElapsedYearsUntilNow, so it needs the same cutover
    /// used by the calendar's save migration layer.
    /// </summary>
    [HarmonyPatch]
    internal static class LegacySaveHeroAgePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase target = AccessTools.Method(typeof(Hero), "get_Age", Type.EmptyTypes);
            if (target == null)
            {
                Diagnostics.Info("Legacy-save hero-age target was not found; native hero ages remain active.");
                return new MethodBase[0];
            }

            return new[] { target };
        }

        [HarmonyPostfix]
        private static void Postfix(Hero __instance, ref float __result)
        {
            if (!CalendarSettingsState.IsLegacySaveAgeCompatibility
                || __instance == null
                || CampaignOptions.IsLifeDeathCycleDisabled)
            {
                return;
            }

            try
            {
                CampaignTime referenceTime = __instance.IsAlive
                    ? CampaignTime.Now
                    : __instance.DeathDay;
                if (referenceTime == CampaignTime.Never)
                {
                    return;
                }

                __result = CalendarTimeMath.GetLegacyCompatibleHeroAgeAt(
                    __instance.BirthDay,
                    referenceTime);
            }
            catch (Exception exception)
            {
                Diagnostics.Info("Legacy-save hero-age compatibility failed safely: " + exception.GetType().Name + ".");
            }
        }
    }

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
                // CampaignTime.Never is used by story quests with no deadline
                // (including Villagers in Need and Establish your Clan). It
                // is a sentinel, not a date; arithmetic on it overflows and
                // makes QuestManager time the quest out on its next tick.
                if (dueTime == CampaignTime.Never)
                {
                    return;
                }

                double nativeRemainingDays = dueTime.ToDays - now.ToDays;
                if (double.IsNaN(nativeRemainingDays)
                    || double.IsInfinity(nativeRemainingDays)
                    || nativeRemainingDays <= 0d)
                {
                    return;
                }

                double annualRemainingDays = nativeRemainingDays * CalendarAnnualBalance.DurationFactor;
                if (double.IsNaN(annualRemainingDays)
                    || double.IsInfinity(annualRemainingDays)
                    || annualRemainingDays > float.MaxValue)
                {
                    return;
                }

                float safeAnnualRemainingDays = (float)annualRemainingDays;
                if (float.IsNaN(safeAnnualRemainingDays)
                    || float.IsInfinity(safeAnnualRemainingDays)
                    || safeAnnualRemainingDays <= 0f)
                {
                    return;
                }

                __instance.ChangeQuestDueTime(now + CampaignTime.Days(safeAnnualRemainingDays));
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
