using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    internal static class CalendarAnnualBalance
    {
        internal static float DurationFactor
        {
            get { return (float)(CalendarTimeMath.AverageDaysInYear / CalendarTimeMath.NativeDaysInYear); }
        }

        internal static float ScaleDuration(float nativeDuration)
        {
            return CalendarSettingsState.ExtendedCalendarEnabled
                ? nativeDuration * DurationFactor
                : nativeDuration;
        }

        internal static void ScaleDuration(ref ExplainedNumber value)
        {
            if (CalendarSettingsState.ExtendedCalendarEnabled)
            {
                SettlementBalanceMath.Scale(ref value, DurationFactor);
            }
        }
    }
}
