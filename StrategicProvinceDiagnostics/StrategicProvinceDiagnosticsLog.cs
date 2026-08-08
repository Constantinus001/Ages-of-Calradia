using System;
using System.IO;
using System.Linq;
using System.Text;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    internal static class StrategicProvinceDiagnosticsLog
    {
        private static readonly object SyncRoot = new object();
        private static string _directory;
        private static string _logPath;
        private static string _snapshotPath;

        internal static void Initialize()
        {
            try
            {
                // Use the same stable game-root resolution used by established Bannerlord
                // modules. Assembly.Location can point at a loader/cache path rather than
                // the installed module directory.
                string modulePath = Path.Combine(BasePath.Name, "Modules", "RealisticCalendarTweaks");
                if (!TrySetDirectory(Path.Combine(modulePath, "Logs")))
                {
                    string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    TrySetDirectory(Path.Combine(documentsPath, "Mount and Blade II Bannerlord", "Configs", "ModLogs"));
                }

                Write("=== Strategic Province Diagnostics started " + DateTime.Now.ToString("O") + " ===");
            }
            catch (Exception exception)
            {
                // Diagnostics must never prevent Bannerlord from loading. The main logger
                // is initialized independently, so mirror initialization failures there.
                try
                {
                    Diagnostics.Error("Strategic province diagnostics logger initialization failed.", exception);
                }
                catch
                {
                }
            }
        }

        internal static void Info(string message)
        {
            Write("INFO  " + message);
        }

        internal static void Error(string message, Exception exception)
        {
            Write("ERROR " + message + Environment.NewLine + exception);
        }

        internal static void SnapshotHeader()
        {
            if (string.IsNullOrWhiteSpace(_snapshotPath)) return;
            try
            {
                lock (SyncRoot)
                {
                    if (new FileInfo(_snapshotPath).Length > 0) return;
                    File.AppendAllText(_snapshotPath, string.Join("\t", new[]
                    {
                        "timestamp", "campaign_day", "capture_reason", "province_index", "sprite_name",
                        "mapped_settlement_id", "mapped_settlement_name", "settlement_type", "map_x", "map_y",
                        "map_width", "map_height", "center_x", "center_y", "settlement_found", "owner_source",
                        "owner_faction_id", "owner_faction_name", "owner_clan_id", "owner_clan_name", "owner_color_argb",
                        "under_siege", "siege_event_present", "besieger_camp_present", "besieger_faction_id",
                        "besieger_faction_name", "besieger_color_argb", "stripe_eligible", "fill_state", "mapping_issue"
                    }) + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception exception)
            {
                Error("Could not write the strategic province snapshot header.", exception);
            }
        }

        internal static void SnapshotRow(string line)
        {
            if (string.IsNullOrWhiteSpace(_snapshotPath) || string.IsNullOrWhiteSpace(line)) return;
            try
            {
                lock (SyncRoot)
                {
                    File.AppendAllText(_snapshotPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception exception)
            {
                Error("Could not write a strategic province snapshot row.", exception);
            }
        }

        internal static string Clean(string value)
        {
            return (value ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }

        private static bool TrySetDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return false;
            try
            {
                Directory.CreateDirectory(directory);
                _directory = directory;
                _logPath = Path.Combine(directory, "StrategicProvinceDiagnostics.log");
                _snapshotPath = Path.Combine(directory, "StrategicProvinceDiagnostics.tsv");
                File.AppendAllText(_logPath, string.Empty, Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void Write(string message)
        {
            if (string.IsNullOrWhiteSpace(_logPath)) return;
            try
            {
                lock (SyncRoot)
                {
                    File.AppendAllText(
                        _logPath,
                        DateTime.Now.ToString("O") + " " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must not interfere with gameplay.
            }
        }
    }
}
