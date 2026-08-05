using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// The map scene consumes this blend for its sky and terrain exposure.
    /// Anchor it to CampaignTime's hour so the Gregorian clock cannot display
    /// night while the visual map remains in a cached daytime state.
    /// </summary>
    [HarmonyPatch(typeof(DefaultMapWeatherModel), "GetNightTimeFactor")]
    internal static class MapWeatherNightFactorPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref float __result)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return true;
            }

            float hour = CampaignTime.Now.CurrentHourInDay;
            float sunrise = 6f;
            float sunset = 18f;
            try
            {
                if (Campaign.Current != null && Campaign.Current.Models != null && Campaign.Current.Models.CampaignTimeModel != null)
                {
                    sunrise = Campaign.Current.Models.CampaignTimeModel.SunRise;
                    sunset = Campaign.Current.Models.CampaignTimeModel.SunSet;
                }
            }
            catch
            {
                // Use the native default daylight span if another module has
                // not yet initialized its campaign-time model.
            }

            sunrise = Math.Max(1f, Math.Min(11f, sunrise));
            sunset = Math.Max(sunrise + 2f, Math.Min(23f, sunset));
            if (hour < sunrise - 1f || hour > sunset + 1f)
            {
                __result = 1f;
                return false;
            }

            if (hour >= sunrise + 1f && hour <= sunset - 1f)
            {
                __result = 0f;
                return false;
            }

            if (hour < sunrise + 1f)
            {
                __result = Math.Max(0f, Math.Min(1f, (sunrise + 1f - hour) / 2f));
                return false;
            }

            __result = Math.Max(0f, Math.Min(1f, (hour - (sunset - 1f)) / 2f));
            return false;
        }
    }
}
