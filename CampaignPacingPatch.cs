using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Applies the selected fast-forward speed through Bannerlord's own
    /// Campaign.SpeedUpMultiplier property. The Gregorian base conversion is
    /// still applied at MapTimeTracker.Tick, so this never stacks a second
    /// multiplier on campaign time or alters annual balance factors.
    /// </summary>
    [HarmonyPatch]
    internal static class CampaignPacingPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase target = AccessTools.Method(
                typeof(Campaign),
                "TickMapTime",
                new[] { typeof(float) });
            if (target == null)
            {
                Diagnostics.Info(
                    "Campaign.TickMapTime(float) was not found; configurable fast-forward speed is disabled.");
                return new MethodBase[0];
            }

            if (!CalendarPatchSafetyAudit.ValidateCampaignPacingTarget(target))
            {
                return new MethodBase[0];
            }

            return new[] { target };
        }

        [HarmonyPrefix]
        private static void Prefix(Campaign __instance)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled
                || __instance == null
                || !CalendarTimeMath.IsFastForwardMode(__instance.TimeControlMode))
            {
                return;
            }

            float requestedSpeed = CalendarSettingsState.FastForwardTimeMultiplier;
            if (Math.Abs(__instance.SpeedUpMultiplier - requestedSpeed) > 0.0001f)
            {
                // Campaign.TickMapTime already applies SpeedUpMultiplier to its
                // fast-forward path. Updating this built-in property gives the
                // requested 1-128x speed without multiplying realDt again.
                __instance.SpeedUpMultiplier = requestedSpeed;
            }
        }
    }
}
