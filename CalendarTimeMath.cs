using System;
using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    internal static class CalendarTimeMath
    {
        internal static int DaysInYear
        {
            get { return CalendarSettingsState.CommonDaysInYear; }
        }

        internal static int LeapYearDays
        {
            get { return DaysInYear + 1; }
        }

        internal static int NativeDaysInYear
        {
            get { return CalendarSettingsState.NativeDaysInYear; }
        }
        internal const int SeasonsInYear = 4;
        internal static float PregnancyDurationInDays
        {
            get { return CalendarSettingsState.PregnancyDurationInDays; }
        }

        internal static CampaignTime GetPregnancyDueDate(CampaignTime conceptionTime)
        {
            if (CalendarSettingsState.UseCalendarMonthPregnancy)
            {
                return AddCalendarMonths(
                    conceptionTime,
                    CalendarSettingsState.PregnancyDurationMonths);
            }

            return conceptionTime + CampaignTime.Days(PregnancyDurationInDays);
        }
        internal static double AverageDaysInYear
        {
            get { return CalendarSettingsState.UseLeapYears ? DaysInYear + 0.2425 : DaysInYear; }
        }

        internal static double NativeCampaignStartDay
        {
            // Native Bannerlord starts in year 1084 after one 21-day season.
            get { return 1084d * NativeDaysInYear + NativeDaysInYear / SeasonsInYear; }
        }

        internal static double GregorianCampaignStartDay
        {
            get
            {
                return DaysBeforeYear(1084)
                    + GetMonthStart(3, IsLeapYear(1084));
            }
        }

        internal static double LegacyCalendarDayOffset
        {
            get { return GregorianCampaignStartDay - NativeCampaignStartDay; }
        }

        internal static bool LooksLikeNativeTimeBasis(CampaignTime time)
        {
            double rawDay = time.ToDays;
            if (double.IsNaN(rawDay) || double.IsInfinity(rawDay))
            {
                return false;
            }

            return Math.Abs(rawDay - NativeCampaignStartDay)
                < Math.Abs(rawDay - GregorianCampaignStartDay);
        }

        internal static double ToCalendarAbsoluteDays(CampaignTime time)
        {
            return ToCalendarAbsoluteDays(time.ToDays);
        }

        internal static double ToCalendarAbsoluteDays(double rawDay)
        {
            return rawDay + (CalendarSettingsState.IsLegacySaveAgeCompatibility
                ? LegacyCalendarDayOffset
                : 0d);
        }

        internal static CampaignTime FromCalendarAbsoluteDays(double calendarDay)
        {
            double rawDay = calendarDay - (CalendarSettingsState.IsLegacySaveAgeCompatibility
                ? LegacyCalendarDayOffset
                : 0d);
            return CampaignTime.Days((float)rawDay);
        }

        internal static CampaignTime GetCampaignStartTime()
        {
            double startDay = CalendarSettingsState.IsLegacySaveAgeCompatibility
                ? NativeCampaignStartDay
                : GregorianCampaignStartDay;
            return CampaignTime.Days((float)startDay) + CampaignTime.Hours(9f);
        }

        internal static double DaysPerSeason
        {
            get { return AverageDaysInYear / SeasonsInYear; }
        }

        internal static int GetYearLength(int year)
        {
            return DaysInYear + (IsLeapYear(year) ? 1 : 0);
        }

        internal static float CampaignTimeMultiplier
        {
            get { return CalendarSettingsState.CampaignTimeScale; }
        }

        /// <summary>
        /// Identifies every built-in fast-forward mode. Normal map time remains
        /// fixed at the Gregorian base cadence; CampaignPacingPatch changes only
        /// Bannerlord's built-in fast-forward speed property for these modes.
        /// </summary>
        internal static bool IsFastForwardMode(CampaignTimeControlMode mode)
        {
            switch (mode)
            {
                case CampaignTimeControlMode.UnstoppableFastForward:
                case CampaignTimeControlMode.UnstoppableFastForwardForPartyWaitTime:
                case CampaignTimeControlMode.StoppableFastForward:
                    return true;
                default:
                    return false;
            }
        }


        internal static bool IsLeapYear(int year)
        {
            return CalendarSettingsState.UseLeapYears
                && (year % 400 == 0 || (year % 4 == 0 && year % 100 != 0));
        }

        internal static long DaysBeforeYear(int year)
        {
            if (year <= 0)
            {
            return year * (long)DaysInYear;
            }

            int lastYear = year - 1;
            int leapYears = CalendarSettingsState.UseLeapYears
                ? lastYear / 4 - lastYear / 100 + lastYear / 400
                : 0;
            return year * (long)DaysInYear + leapYears;
        }

        internal static int GetYear(CampaignTime time)
        {
            double absoluteDays = ToCalendarAbsoluteDays(time);
            int year = (int)Math.Floor(absoluteDays / AverageDaysInYear);

            while (absoluteDays < DaysBeforeYear(year))
            {
                year--;
            }

            while (absoluteDays >= DaysBeforeYear(year + 1))
            {
                year++;
            }

            return year;
        }

        internal static int GetDayOfYear(CampaignTime time)
        {
            int year = GetYear(time);
            long absoluteDay = (long)Math.Floor(ToCalendarAbsoluteDays(time));
            return (int)(absoluteDay - DaysBeforeYear(year));
        }

        internal static int GetMonth(CampaignTime time)
        {
            int year = GetYear(time);
            int dayOfYear = GetDayOfYear(time);
            bool leap = IsLeapYear(year);

            for (int month = 11; month >= 0; month--)
            {
                if (dayOfYear >= GetMonthStart(month, leap))
                {
                    return month;
                }
            }

            return 0;
        }

        internal static int GetMonthStart(int month, bool leapYear)
        {
            return CalendarSettingsState.GetMonthStart(month)
                + (leapYear && month >= 2 ? 1 : 0);
        }

        internal static int GetMonthLength(int month, bool leapYear)
        {
            return CalendarSettingsState.GetMonthLength(month)
                + (month == 1 && leapYear ? 1 : 0);
        }

        internal static int GetSeasonLength(int year, int season)
        {
            int start = GetSeasonStartDayOfYear(year, season);
            if (season == (int)CampaignTime.Seasons.Winter)
            {
                return GetYearLength(year) - start
                    + GetSeasonStartDayOfYear(year + 1, (int)CampaignTime.Seasons.Spring);
            }

            int end = GetSeasonStartDayOfYear(year, season + 1);
            return end - start;
        }

        internal static CampaignTime AddCalendarMonths(CampaignTime time, int months)
        {
            int sourceYear = GetYear(time);
            int sourceMonth = GetMonth(time);
            int sourceDay = GetDayOfYear(time) - GetMonthStart(
                sourceMonth,
                IsLeapYear(sourceYear)) + 1;

            int absoluteMonth = sourceYear * 12 + sourceMonth + months;
            int targetYear = absoluteMonth / 12;
            int targetMonth = absoluteMonth % 12;
            if (targetMonth < 0)
            {
                targetMonth += 12;
                targetYear--;
            }

            bool targetLeapYear = IsLeapYear(targetYear);
            int targetDay = Math.Min(
                sourceDay,
                GetMonthLength(targetMonth, targetLeapYear));
            long targetAbsoluteDay = DaysBeforeYear(targetYear)
                + GetMonthStart(targetMonth, targetLeapYear)
                + targetDay - 1;
            double calendarAbsoluteDays = ToCalendarAbsoluteDays(time);
            double fractionalDay = calendarAbsoluteDays - Math.Floor(calendarAbsoluteDays);

            return FromCalendarAbsoluteDays(targetAbsoluteDay + fractionalDay);
        }

        internal static int GetSeason(CampaignTime time)
        {
            int year = GetYear(time);
            int dayOfYear = GetDayOfYear(time);

            if (dayOfYear >= GetSeasonStartDayOfYear(year, (int)CampaignTime.Seasons.Winter))
            {
                return (int)CampaignTime.Seasons.Winter;
            }

            if (dayOfYear >= GetSeasonStartDayOfYear(year, (int)CampaignTime.Seasons.Autumn))
            {
                return (int)CampaignTime.Seasons.Autumn;
            }

            if (dayOfYear >= GetSeasonStartDayOfYear(year, (int)CampaignTime.Seasons.Summer))
            {
                return (int)CampaignTime.Seasons.Summer;
            }

            if (dayOfYear >= GetSeasonStartDayOfYear(year, (int)CampaignTime.Seasons.Spring))
            {
                return (int)CampaignTime.Seasons.Spring;
            }

            return (int)CampaignTime.Seasons.Winter;
        }

        internal static int GetDayOfSeason(CampaignTime time)
        {
            int year = GetYear(time);
            int dayOfYear = GetDayOfYear(time);
            int season = GetSeason(time);
            int seasonStart = GetSeasonStartDayOfYear(year, season);

            // Winter begins in December and continues through March 20 of the
            // next calendar year. January through March therefore belong to
            // the winter that started in the previous year.
            if (season == (int)CampaignTime.Seasons.Winter && dayOfYear < seasonStart)
            {
                int previousYear = year - 1;
                return dayOfYear
                    + GetYearLength(previousYear)
                    - GetSeasonStartDayOfYear(previousYear, season);
            }

            return dayOfYear - seasonStart;
        }

        internal static float ElapsedYearsUntilNow(CampaignTime time)
        {
            if (CalendarSettingsState.IsLegacySaveAgeCompatibility)
            {
                return GetLegacyCompatibleElapsedYearsAt(time, CampaignTime.Now);
            }

            return (float)(ToYears(CampaignTime.Now) - ToYears(time));
        }

        /// <summary>
        /// Calculates a hero's age across the native-to-Gregorian
        /// compatibility boundary. Existing saves contain birth timestamps
        /// measured with Bannerlord's 84-day year; after the first load, raw
        /// CampaignTime continues forward while the calendar uses a 365-day
        /// year. No hero data is rewritten, so the save remains loadable by
        /// the game and by versions of the mod that predate this fix.
        /// </summary>
        internal static float GetLegacyCompatibleHeroAgeAt(
            CampaignTime birthDay,
            CampaignTime referenceTime)
        {
            return GetLegacyCompatibleElapsedYearsAt(birthDay, referenceTime);
        }

        /// <summary>
        /// Preserves native 84-day elapsed-year history up to the first
        /// compatible load, then advances the same span using calendar years.
        /// This also covers engine systems that query elapsed years directly
        /// instead of going through Hero.Age.
        /// </summary>
        internal static float GetLegacyCompatibleElapsedYearsAt(
            CampaignTime startTime,
            CampaignTime referenceTime)
        {
            double nowDay = referenceTime.ToDays;
            double cutoverDay = CalendarSettingsState.LegacySaveAgeCutoverDay;
            double startDay = startTime.ToDays;

            if (double.IsNaN(nowDay) || double.IsInfinity(nowDay)
                || double.IsNaN(cutoverDay) || double.IsInfinity(cutoverDay)
                || double.IsNaN(startDay) || double.IsInfinity(startDay))
            {
                return 0f;
            }

            double ageInYears;
            if (nowDay <= cutoverDay)
            {
                ageInYears = (nowDay - startDay) / NativeDaysInYear;
            }
            else if (startDay <= cutoverDay)
            {
                ageInYears = (cutoverDay - startDay) / NativeDaysInYear
                    + (nowDay - cutoverDay) / AverageDaysInYear;
            }
            else
            {
                // A timestamp created after the compatibility boundary is
                // already entirely in the Gregorian portion of the timeline.
                ageInYears = (nowDay - startDay) / AverageDaysInYear;
            }

            if (double.IsNaN(ageInYears) || double.IsInfinity(ageInYears))
            {
                return 0f;
            }

            return (float)Math.Max(0d, ageInYears);
        }

        internal static float RemainingYearsFromNow(CampaignTime time)
        {
            return (float)(ToYears(time) - ToYears(CampaignTime.Now));
        }

        internal static double ToYears(CampaignTime time)
        {
            int year = GetYear(time);
            double dayWithinYear = ToCalendarAbsoluteDays(time) - DaysBeforeYear(year);
            return year + dayWithinYear / GetYearLength(year);
        }

        internal static double DurationToYears(CampaignTime duration)
        {
            return duration.ToDays / AverageDaysInYear;
        }

        internal static float ElapsedSeasonsUntilNow(CampaignTime time)
        {
            return (float)(ToSeasons(CampaignTime.Now) - ToSeasons(time));
        }

        internal static float RemainingSeasonsFromNow(CampaignTime time)
        {
            return (float)(ToSeasons(time) - ToSeasons(CampaignTime.Now));
        }

        internal static double ToSeasons(CampaignTime time)
        {
            int year = GetYear(time);
            int season = GetSeason(time);
            int seasonYear = GetSeasonYear(time, year, season);
            int seasonStart = GetSeasonStartDayOfYear(seasonYear, season);
            double dayWithinSeason = ToCalendarAbsoluteDays(time)
                - (DaysBeforeYear(seasonYear) + seasonStart);
            return seasonYear * SeasonsInYear
                + season
                + dayWithinSeason / GetSeasonLength(seasonYear, season);
        }

        internal static double DurationToSeasons(CampaignTime duration)
        {
            return duration.ToDays / DaysPerSeason;
        }

        internal static int GetSeasonYear(CampaignTime time)
        {
            int year = GetYear(time);
            return GetSeasonYear(time, year, GetSeason(time));
        }

        internal static int GetSeasonStartDayOfYear(int year, int season)
        {
            bool leapYear = IsLeapYear(year);
            int month;
            switch (season)
            {
                case (int)CampaignTime.Seasons.Spring:
                    month = 2; // March 21
                    break;
                case (int)CampaignTime.Seasons.Summer:
                    month = 5; // June 21
                    break;
                case (int)CampaignTime.Seasons.Autumn:
                    month = 8; // September 21
                    break;
                case (int)CampaignTime.Seasons.Winter:
                    month = 11; // December 21
                    break;
                default:
                    throw new ArgumentOutOfRangeException("season");
            }

            // Day values are zero-based internally, so the 21st is +20.
            int dayInMonth = Math.Min(20, GetMonthLength(month, leapYear) - 1);
            return GetMonthStart(month, leapYear) + Math.Max(0, dayInMonth);
        }

        private static int GetSeasonYear(CampaignTime time, int year, int season)
        {
            if (season == (int)CampaignTime.Seasons.Winter
                && GetDayOfYear(time) < GetSeasonStartDayOfYear(year, season))
            {
                return year - 1;
            }

            return year;
        }
    }
}
