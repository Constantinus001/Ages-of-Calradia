using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    [HarmonyPatch]
    internal static class MapTimeTrackerPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type trackerType = typeof(Campaign).Assembly.GetType(
                "TaleWorlds.CampaignSystem.MapTimeTracker");
            MethodBase target = trackerType == null
                ? null
                : AccessTools.Method(trackerType, "Tick", new[] { typeof(float) });

            if (target == null)
            {
                Diagnostics.Info(
                    "MapTimeTracker.Tick(float) was not found; campaign-time scaling is disabled rather than patching an unknown Bannerlord version.");
                return new MethodBase[0];
            }

            if (!CalendarPatchSafetyAudit.ValidateMapTimeTrackerTarget(target))
            {
                Diagnostics.Info(
                    "MapTimeTracker.Tick(float) did not match the supported Bannerlord target; campaign-time scaling is disabled safely.");
                return new MethodBase[0];
            }

            return new[] { target };
        }

        [HarmonyPrefix]
        private static void Prefix(ref float seconds)
        {
            if (CalendarSettingsState.ExtendedCalendarEnabled)
            {
                seconds *= CalendarTimeMath.CampaignTimeMultiplier;
            }
        }
    }
}
