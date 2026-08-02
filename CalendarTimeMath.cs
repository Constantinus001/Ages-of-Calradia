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
            double absoluteDays = time.ToDays;
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
            long absoluteDay = (long)Math.Floor(time.ToDays);
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
            double fractionalDay = time.ToDays - Math.Floor(time.ToDays);

            return CampaignTime.Days((float)(targetAbsoluteDay + fractionalDay));
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
            return (float)(ToYears(CampaignTime.Now) - ToYears(time));
        }

        internal static float RemainingYearsFromNow(CampaignTime time)
        {
            return (float)(ToYears(time) - ToYears(CampaignTime.Now));
        }

        internal static double ToYears(CampaignTime time)
        {
            int year = GetYear(time);
            double dayWithinYear = time.ToDays - DaysBeforeYear(year);
            return year + dayWithinYear / GetYearLength(year);
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
            double dayWithinSeason = time.ToDays - (DaysBeforeYear(seasonYear) + seasonStart);
            return seasonYear * SeasonsInYear
                + season
                + dayWithinSeason / GetSeasonLength(seasonYear, season);
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
