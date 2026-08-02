using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    [HarmonyPatch(typeof(CampaignTime), nameof(CampaignTime.ToString))]
    internal static class CampaignTimeToStringPatch
    {
        private static bool Prefix(CampaignTime __instance, ref string __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            string formattedDate = CalendarFormatter.Format(__instance);
            if (formattedDate == null)
            {
                return true;
            }

            __result = formattedDate;
            return false;
        }
    }
}
