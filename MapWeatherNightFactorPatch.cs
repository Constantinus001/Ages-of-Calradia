using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Applies the optional visual lighting profile after Bannerlord has
    /// calculated weather. Native CampaignTime.SunRise/SunSet are deliberately
    /// not changed because they also drive gameplay rules.
    /// </summary>
    [HarmonyPatch(typeof(DefaultMapWeatherModel), nameof(DefaultMapWeatherModel.GetAtmosphereModel))]
    internal static class MapWeatherNightFactorPatch
    {
        private static bool _failureLogged;

        [HarmonyPostfix]
        private static void Postfix(ref AtmosphereInfo __result)
        {
            if (!CalendarSettingsState.ClockSynchronizedLighting
                || Campaign.Current == null)
            {
                return;
            }

            try
            {
                float hour = GetHourOfDay();
                float customDaylight = GetDaylightFactor(
                    hour,
                    CalendarSettingsState.VisualSunriseHour,
                    CalendarSettingsState.VisualSunsetHour,
                    CalendarSettingsState.VisualLightingTransitionHours);
                float nativeDaylight = GetDaylightFactor(
                    hour,
                    CampaignTime.SunRise,
                    CampaignTime.SunSet,
                    2f);

                // Keep the weather model's colors and precipitation intact,
                // but move its luminous values to the selected visual clock.
                // The small floor prevents division spikes at native night.
                float nativeLuminous = 0.001f + 0.999f * nativeDaylight;
                float customLuminous = 0.001f + 0.999f * customDaylight;
                float lightRatio = Clamp(customLuminous / nativeLuminous, 0.001f, 3f);

                __result.SunInfo.Brightness = Scale(__result.SunInfo.Brightness, lightRatio);
                __result.SunInfo.MaxBrightness = Scale(__result.SunInfo.MaxBrightness, lightRatio);
                __result.SunInfo.Size = Scale(__result.SunInfo.Size, lightRatio);
                __result.SunInfo.RayStrength = Scale(__result.SunInfo.RayStrength, lightRatio);
                __result.AmbientInfo.EnvironmentMultiplier = Clamp(
                    Scale(__result.AmbientInfo.EnvironmentMultiplier, lightRatio),
                    0.001f,
                    1.5f);
                __result.SkyInfo.Brightness = Scale(__result.SkyInfo.Brightness, lightRatio);
                __result.TimeInfo.TimeOfDay = hour;
                __result.TimeInfo.NightTimeFactor = 1f - customDaylight;

                // Bannerlord's native profile spans roughly -3 at night to -2
                // in daylight. Preserve its weather-specific maximum exposure
                // while aligning the minimum exposure to the visual clock.
                __result.PostProInfo.MinExposure = -3f + customDaylight;
            }
            catch (Exception exception)
            {
                if (_failureLogged)
                {
                    return;
                }

                _failureLogged = true;
                Diagnostics.Error("Clock-synchronized campaign lighting failed; native atmosphere remains active.", exception);
            }
        }

        private static float GetHourOfDay()
        {
            double hour = CampaignTime.Now.ToHours % CampaignTime.HoursInDay;
            if (hour < 0d)
            {
                hour += CampaignTime.HoursInDay;
            }

            return (float)hour;
        }

        private static float GetDaylightFactor(float hour, float sunrise, float sunset, float transition)
        {
            float dayLength = sunset - sunrise;
            if (dayLength < 0f)
            {
                dayLength += 24f;
            }

            if (dayLength <= 0.25f || dayLength >= 23.75f)
            {
                return 0f;
            }

            float safeTransition = Math.Max(0.25f, Math.Min(transition, dayLength / 2f));
            float sinceSunrise = hour - sunrise;
            if (sinceSunrise < 0f)
            {
                sinceSunrise += 24f;
            }

            if (sinceSunrise >= dayLength)
            {
                return 0f;
            }

            if (sinceSunrise < safeTransition * 2f)
            {
                return SmoothStep(sinceSunrise / (safeTransition * 2f));
            }

            float duskStart = dayLength - safeTransition * 2f;
            if (sinceSunrise > duskStart)
            {
                return 1f - SmoothStep((sinceSunrise - duskStart) / (safeTransition * 2f));
            }

            return 1f;
        }

        private static float SmoothStep(float value)
        {
            float normalized = Clamp(value, 0f, 1f);
            return normalized * normalized * (3f - 2f * normalized);
        }

        private static float Scale(float value, float factor)
        {
            return Math.Max(0f, value * factor);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
