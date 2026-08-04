using System;
using System.Threading;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Records enough context to diagnose the new model wrappers without
    /// performing disk I/O or filling the flight recorder on every campaign
    /// tick. The first evaluation and then every 512th evaluation are kept.
    /// </summary>
    internal static class CalendarAnnualBalanceDiagnostics
    {
        private static int _impairmentEvaluations;
        private static int _prisonerConformityEvaluations;
        private static int _marriageChanceEvaluations;
        private static int _mapTrackLifeEvaluations;
        private static int _questDeadlineAdjustments;
        private static int _hideoutDayNightChecks;
        private static int _romanceDailyTicks;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> LoggedExceptions =
            new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();

        internal static void RecordImpairment(float nativeValue, float annualValue)
        {
            RecordSample(
                ref _impairmentEvaluations,
                "Impairment",
                "NativeDuration=" + nativeValue.ToString("F3")
                + "; AnnualDuration=" + annualValue.ToString("F3"));
        }

        internal static void RecordPrisonerConformity(float nativeValue, float annualValue)
        {
            RecordSample(
                ref _prisonerConformityEvaluations,
                "PrisonerRecruitment",
                "NativeConformityPerHour=" + nativeValue.ToString("F5")
                + "; AnnualConformityPerHour=" + annualValue.ToString("F5"));
        }

        internal static void RecordMarriageChance(float nativeValue, float annualValue)
        {
            RecordSample(
                ref _marriageChanceEvaluations,
                "Marriage",
                "NativeDailyChance=" + nativeValue.ToString("F6")
                + "; AnnualDailyChance=" + annualValue.ToString("F6"));
        }

        internal static void RecordMapTrackLife(int nativeValue, int annualValue)
        {
            RecordSample(
                ref _mapTrackLifeEvaluations,
                "MapTracks",
                "NativeLifetime=" + nativeValue + "; AnnualLifetime=" + annualValue);
        }

        internal static void RecordQuestDeadline(double nativeRemainingDays, double annualRemainingDays)
        {
            RecordSample(
                ref _questDeadlineAdjustments,
                "QuestDeadline",
                "NativeRemainingDays=" + nativeRemainingDays.ToString("F3")
                + "; AnnualRemainingDays=" + annualRemainingDays.ToString("F3"));
        }

        internal static void RecordHideoutDayNightCheck(bool isNighttime)
        {
            RecordSample(
                ref _hideoutDayNightChecks,
                "HideoutDayNight",
                "NativeDayNightRulePreserved; IsNighttime=" + isNighttime);
        }

        internal static void RecordRomanceDailyTick()
        {
            RecordSample(
                ref _romanceDailyTicks,
                "PlayerRomance",
                "NativePlayerRomanceCooldownPreserved");
        }

        internal static void RecordException(string channel, Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            string detail = channel + " native-model evaluation threw " + exception.GetType().FullName;
            CrashFlightRecorder.Record("AnnualBalance", detail);
            if (LoggedExceptions.TryAdd(channel, 0))
            {
                Diagnostics.Error(detail, exception);
            }
        }

        private static void RecordSample(ref int counter, string channel, string detail)
        {
            if (!CalendarSettingsState.AnnualBalanceDiagnosticsEnabled)
            {
                return;
            }
            int count = Interlocked.Increment(ref counter);
            if (count == 1 || count % 512 == 0)
            {
                CrashFlightRecorder.Record("AnnualBalance", channel + " #" + count + "; " + detail);
            }
        }
    }
}
