using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// A bounded in-memory event trail. It deliberately does no disk I/O in
    /// normal play; the trail is written only when an exception reaches the
    /// diagnostic hooks. This keeps the recorder safe to leave enabled.
    /// </summary>
    internal static class CrashFlightRecorder
    {
        private const int Capacity = 256;
        private static readonly ConcurrentQueue<string> Events = new ConcurrentQueue<string>();
        private static int _eventCount;
        private static int _flushInProgress;

        internal static void Record(string category, string detail)
        {
            try
            {
                string entry = string.Format(
                    "{0:O} [{1}] {2}: {3}",
                    DateTime.UtcNow,
                    Thread.CurrentThread.ManagedThreadId,
                    category ?? "Event",
                    detail ?? string.Empty);
                Events.Enqueue(entry);
                Interlocked.Increment(ref _eventCount);

                while (Volatile.Read(ref _eventCount) > Capacity && Events.TryDequeue(out _))
                {
                    Interlocked.Decrement(ref _eventCount);
                }
            }
            catch
            {
                // A recorder must never affect the game it is observing.
            }
        }

        internal static void RecordCampaignCheckpoint(string trigger)
        {
            try
            {
                CampaignTime now = CampaignTime.Now;
                Record(
                    "Campaign",
                    string.Format(
                        "{0}; NowDays={1:F3}; Year={2}; DayOfYear={3}; Season={4}; DayOfSeason={5}",
                        trigger,
                        now.ToDays,
                        now.GetYear,
                        now.GetDayOfYear,
                        now.GetSeasonOfYear,
                        now.GetDayOfSeason));
            }
            catch (Exception exception)
            {
                Record("Campaign", trigger + " checkpoint failed: " + exception.GetType().FullName);
            }
        }

        internal static void Flush(string reason, Exception exception)
        {
            if (Interlocked.CompareExchange(ref _flushInProgress, 1, 0) != 0)
            {
                return;
            }

            try
            {
                StringBuilder report = new StringBuilder();
                report.AppendLine("Realistic Calendar Tweaks crash flight recorder");
                report.AppendLine("CapturedUtc=" + DateTime.UtcNow.ToString("O"));
                report.AppendLine("Reason=" + (reason ?? "Unknown"));
                if (exception != null)
                {
                    report.AppendLine("Exception:");
                    report.AppendLine(exception.ToString());
                }

                report.AppendLine("Recent events (oldest to newest):");
                foreach (string entry in Events)
                {
                    report.AppendLine(entry);
                }

                Diagnostics.WriteCrashSnapshot(report.ToString());
            }
            catch
            {
                // Process termination can leave locks or files unavailable.
            }
            finally
            {
                Volatile.Write(ref _flushInProgress, 0);
            }
        }
    }
}
