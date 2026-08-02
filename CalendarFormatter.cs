using System;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    internal static class CalendarFormatter
    {
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

                // The season is a fixed leading component. This keeps it first
                // even when the user changes the month/day/year order.
                string format = CalendarSettingsState.DateFormat;
                format = Regex.Replace(format, "\\{Season\\}", string.Empty, RegexOptions.IgnoreCase);
                format = Regex.Replace(format, "\\s{2,}", " ").Trim();

                string date = format
                    .Replace("{Month}", CalendarSettingsState.GetMonthName(month))
                    .Replace("{Day}", dayText)
                    .Replace("{Year}", yearText)
                    .Replace("{MonthNumber}", (month + 1).ToString())
                    .Replace("{DayOfYear}", (dayOfYear + 1).ToString());

                return CalendarSettingsState.GetSeasonName(CalendarTimeMath.GetSeason(time)) + " " + date;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Calendar date formatting failed.", exception);
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
