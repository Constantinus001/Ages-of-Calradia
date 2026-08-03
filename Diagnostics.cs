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
                string assemblyDirectory = Path.GetDirectoryName(typeof(Diagnostics).Assembly.Location);
                string moduleDirectory = assemblyDirectory;

                // The assembly lives in <module>\bin\Win64_Shipping_Client.
                // Keep the primary diagnostic file beside the module so a crash
                // report and its matching calendar log can be collected together.
                if (!string.IsNullOrWhiteSpace(assemblyDirectory))
                {
                    DirectoryInfo binaryDirectory = Directory.GetParent(assemblyDirectory);
                    DirectoryInfo candidateModuleDirectory = binaryDirectory == null
                        ? null
                        : binaryDirectory.Parent;
                    if (candidateModuleDirectory != null)
                    {
                        moduleDirectory = candidateModuleDirectory.FullName;
                    }
                }

                string directory = Path.Combine(moduleDirectory, "Logs");
                if (!TrySetLogPath(directory))
                {
                    // Some Windows installations deny a non-elevated game
                    // process write access to Program Files. Preserve the
                    // previous safe location only as a fallback.
                    string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    directory = Path.Combine(
                        documentsPath,
                        "Mount and Blade II Bannerlord",
                        "Configs",
                        "ModLogs");

                    if (!TrySetLogPath(directory))
                    {
                        return;
                    }

                    Write("INFO  Module log directory was not writable; using the Documents fallback.");
                }

                Write("=== Twelve Month Calendar diagnostics started " + DateTime.Now.ToString("O") + " ===");
            }
            catch
            {
                // Diagnostics must never prevent the game from loading.
            }
        }

        private static bool TrySetLogPath(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(directory);
                string candidate = Path.Combine(directory, "TwelveMonthCalendar.log");
                File.AppendAllText(candidate, string.Empty, Encoding.UTF8);
                _logPath = candidate;
                return true;
            }
            catch
            {
                return false;
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

        internal static void WriteCrashSnapshot(string contents)
        {
            if (string.IsNullOrWhiteSpace(_logPath) || string.IsNullOrWhiteSpace(contents))
            {
                return;
            }

            try
            {
                string directory = Path.Combine(Path.GetDirectoryName(_logPath), "CrashReports");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(
                    directory,
                    "TwelveMonthCalendar-crash-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".log");
                File.WriteAllText(path, contents, Encoding.UTF8);
            }
            catch
            {
                // Failure to persist a report must never interfere with crash handling.
            }
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
