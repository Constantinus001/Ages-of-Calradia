using System;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    internal static class CalendarFormatter
    {
        internal static string FormatMapBar(CampaignTime time)
        {
            string date = Format(time);
            if (string.IsNullOrWhiteSpace(date))
            {
                return date;
            }

            string season = CalendarSettingsState.GetSeasonName(CalendarTimeMath.GetSeason(time));
            return string.IsNullOrWhiteSpace(season)
                ? date
                : season + " " + date;
        }

        internal static string Format(CampaignTime time)
        {
            try
            {
                if (time == CampaignTime.Never || time.ToDays < 0.0)
                {
                    return null;
                }

                int year = CalendarTimeMath.GetYear(time);
                int dayOfYear = CalendarTimeMath.GetDayOfYear(time);
                int month = CalendarTimeMath.GetMonth(time);
                int dayOfMonth = dayOfYear - CalendarTimeMath.GetMonthStart(
                    month,
                    CalendarTimeMath.IsLeapYear(year));

                int dayNumber = dayOfMonth + 1;
                string dayNumberText = CalendarSettingsState.UseOrdinalDaySuffixes
                    ? FormatOrdinal(dayNumber)
                    : dayNumber.ToString();
                string dayText = CalendarSettingsState.ShowDayLabel
                    ? string.Format("Day {0}", dayNumberText)
                    : dayNumberText;
                string yearText = CalendarSettingsState.ShowYearLabel
                    ? string.Format("Year {0}", year)
                    : year.ToString();

                // The map bar renders the season in its own label to the right
                // of the clock. Keep accepting the old token for configuration
                // compatibility, but do not duplicate it in the date label.
                string format = CalendarSettingsState.DateFormat;
                format = Regex.Replace(format, "\\{Season\\}", string.Empty, RegexOptions.IgnoreCase);
                format = Regex.Replace(format, "\\s{2,}", " ").Trim();

                string date = format
                    .Replace("{Month}", CalendarSettingsState.GetMonthName(month))
                    .Replace("{Day}", dayText)
                    .Replace("{Year}", yearText)
                    .Replace("{MonthNumber}", (month + 1).ToString())
                    .Replace("{DayOfYear}", (dayOfYear + 1).ToString());

                return date;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Calendar date formatting failed.", exception);
                return null;
            }
        }

        internal static string FormatMapDateLine(CampaignTime time)
        {
            try
            {
                if (time == CampaignTime.Never || time.ToDays < 0.0)
                {
                    return null;
                }

                int year = CalendarTimeMath.GetYear(time);
                int dayOfYear = CalendarTimeMath.GetDayOfYear(time);
                int month = CalendarTimeMath.GetMonth(time);
                int dayOfMonth = dayOfYear - CalendarTimeMath.GetMonthStart(
                    month,
                    CalendarTimeMath.IsLeapYear(year));
                int dayNumber = dayOfMonth + 1;
                string dayText = CalendarSettingsState.UseOrdinalDaySuffixes
                    ? FormatOrdinal(dayNumber)
                    : dayNumber.ToString();
                if (CalendarSettingsState.ShowDayLabel)
                {
                    dayText = "Day " + dayText;
                }

                string monthText = CalendarSettingsState.GetMonthName(month);
                string yearText = CalendarSettingsState.ShowYearLabel
                    ? "Year " + year
                    : year.ToString();
                string format = CalendarSettingsState.DateFormat;
                if (string.Equals(format, "{Day} {Month} {Year}", StringComparison.Ordinal))
                {
                    return dayText + ", " + monthText;
                }

                if (string.Equals(format, "{Year} {Month} {Day}", StringComparison.Ordinal))
                {
                    return yearText + ", " + monthText;
                }

                // Month-Day-Year is the default compact map layout. For a
                // custom MCM format, preserve the configured order in full.
                if (string.Equals(format, "{Month} {Day} {Year}", StringComparison.Ordinal))
                {
                    return monthText + ", " + dayText;
                }

                return Format(time);
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Map-bar calendar date-line formatting failed.", exception);
                return null;
            }
        }

        internal static string FormatMapSeasonYearLine(CampaignTime time)
        {
            try
            {
                if (time == CampaignTime.Never || time.ToDays < 0.0)
                {
                    return null;
                }

                string season = CalendarSettingsState.GetSeasonName(CalendarTimeMath.GetSeason(time));
                int year = CalendarTimeMath.GetYear(time);
                string yearText = CalendarSettingsState.ShowYearLabel
                    ? "Year " + year
                    : year.ToString();
                bool yearIsOnDateLine = string.Equals(
                    CalendarSettingsState.DateFormat,
                    "{Year} {Month} {Day}",
                    StringComparison.Ordinal);
                if (yearIsOnDateLine)
                {
                    int dayOfYear = CalendarTimeMath.GetDayOfYear(time);
                    int month = CalendarTimeMath.GetMonth(time);
                    int dayOfMonth = dayOfYear - CalendarTimeMath.GetMonthStart(
                        month,
                        CalendarTimeMath.IsLeapYear(year));
                    int dayNumber = dayOfMonth + 1;
                    string dayText = CalendarSettingsState.UseOrdinalDaySuffixes
                        ? FormatOrdinal(dayNumber)
                        : dayNumber.ToString();
                    if (CalendarSettingsState.ShowDayLabel)
                    {
                        dayText = "Day " + dayText;
                    }

                    return string.IsNullOrWhiteSpace(season) ? dayText : dayText + ", " + season;
                }

                return string.IsNullOrWhiteSpace(season) ? yearText : yearText + ", " + season;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Map-bar season-year formatting failed.", exception);
                return null;
            }
        }

        private static string FormatOrdinal(int value)
        {
            int lastTwoDigits = value % 100;
            if (lastTwoDigits >= 11 && lastTwoDigits <= 13)
            {
                return value + "th";
            }

            switch (value % 10)
            {
                case 1:
                    return value + "st";
                case 2:
                    return value + "nd";
                case 3:
                    return value + "rd";
                default:
                    return value + "th";
            }
        }
    }
}
