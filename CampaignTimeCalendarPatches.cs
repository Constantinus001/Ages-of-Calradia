using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TwelveMonthCalendar
{
    [HarmonyPatch(typeof(CampaignTime))]
    internal static class CampaignTimeCalendarPatches
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return CalendarPatchSafetyAudit.ValidateCampaignTimeCalendarTargets();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DefaultCampaignTimeModel), "get_CampaignStartTime")]
        private static bool CampaignStartTime(ref CampaignTime __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            // Preserve Bannerlord's native campaign start as April 1 in 1084.
            // With real-world boundaries, that date falls in Spring.
            long startDay = CalendarTimeMath.DaysBeforeYear(1084)
                + CalendarTimeMath.GetMonthStart(3, CalendarTimeMath.IsLeapYear(1084));
            __result = CampaignTime.Days(startDay) + CampaignTime.Hours(9f);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_DaysInSeason")]
        private static bool DaysInSeason(ref int __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            CampaignTime now = CampaignTime.Now;
            int season = CalendarTimeMath.GetSeason(now);
            int seasonYear = CalendarTimeMath.GetSeasonYear(now);
            __result = CalendarTimeMath.GetSeasonLength(seasonYear, season);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_DaysInYear")]
        private static bool DaysInYear(ref int __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CalendarTimeMath.DaysInYear;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_ElapsedSeasonsUntilNow")]
        private static bool ElapsedSeasonsUntilNow(CampaignTime __instance, ref float __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CalendarTimeMath.ElapsedSeasonsUntilNow(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_ElapsedYearsUntilNow")]
        private static bool ElapsedYearsUntilNow(CampaignTime __instance, ref float __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CalendarTimeMath.ElapsedYearsUntilNow(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_RemainingSeasonsFromNow")]
        private static bool RemainingSeasonsFromNow(CampaignTime __instance, ref float __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CalendarTimeMath.RemainingSeasonsFromNow(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_RemainingYearsFromNow")]
        private static bool RemainingYearsFromNow(CampaignTime __instance, ref float __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CalendarTimeMath.RemainingYearsFromNow(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_ToSeasons")]
        private static bool ToSeasons(CampaignTime __instance, ref double __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CalendarTimeMath.ToSeasons(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_ToYears")]
        private static bool ToYears(CampaignTime __instance, ref double __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CalendarTimeMath.ToYears(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_GetDayOfSeason")]
        private static bool GetDayOfSeason(CampaignTime __instance, ref int __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CalendarTimeMath.GetDayOfSeason(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_GetDayOfYear")]
        private static bool GetDayOfYear(CampaignTime __instance, ref int __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CalendarTimeMath.GetDayOfYear(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_GetWeekOfSeason")]
        private static bool GetWeekOfSeason(CampaignTime __instance, ref int __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CalendarTimeMath.GetDayOfSeason(__instance) / 7;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_GetSeasonOfYear")]
        private static bool GetSeasonOfYear(CampaignTime __instance, ref CampaignTime.Seasons __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = (CampaignTime.Seasons)CalendarTimeMath.GetSeason(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_GetYear")]
        private static bool GetYear(CampaignTime __instance, ref int __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CalendarTimeMath.GetYear(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Years")]
        private static bool Years(float valueInYears, ref CampaignTime __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CampaignTime.Days(valueInYears * (float)CalendarTimeMath.AverageDaysInYear);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("YearsFromNow")]
        private static bool YearsFromNow(float valueInYears, ref CampaignTime __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            __result = CampaignTime.Now + CampaignTime.Days(valueInYears * (float)CalendarTimeMath.AverageDaysInYear);
            return false;
        }
    }
}
