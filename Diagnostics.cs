using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace TwelveMonthCalendar
{
    internal static class Diagnostics
    {
        private static readonly object SyncRoot = new object();
        private static string _logPath;

        internal static string LogPath => _logPath;

        internal static void Initialize()
        {
            try
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrWhiteSpace(documentsPath))
                {
                    documentsPath = Path.GetDirectoryName(typeof(Diagnostics).Assembly.Location);
                }

                string directory = Path.Combine(
                    documentsPath,
                    "Mount and Blade II Bannerlord",
                    "Configs",
                    "ModLogs");

                Directory.CreateDirectory(directory);
                _logPath = Path.Combine(directory, "TwelveMonthCalendar.log");
                Write("=== Twelve Month Calendar diagnostics started " + DateTime.Now.ToString("O") + " ===");
            }
            catch
            {
                // Diagnostics must never prevent the game from loading.
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

        private static void Write(string message)
        {
            if (string.IsNullOrWhiteSpace(_logPath))
            {
                return;
            }

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
                // Diagnostics must never interfere with gameplay.
            }
        }
    }
}
