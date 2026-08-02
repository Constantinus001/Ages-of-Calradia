using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    [HarmonyPatch]
    internal static class MapTimeTrackerPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(Campaign).Assembly.GetType("TaleWorlds.CampaignSystem.MapTimeTracker"),
                "Tick");
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
