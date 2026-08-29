using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace AgesOfCalradiaReligions
{
    internal static class ReligionDiagnostics
    {
        private static string _logPath;

        internal static void Initialize()
        {
            string moduleDirectory = ResolveModuleDirectory();
            string logDirectory = Path.Combine(moduleDirectory, "Logs");
            Directory.CreateDirectory(logDirectory);
            _logPath = Path.Combine(logDirectory, "AgesOfCalradiaReligions.log");
        }

        private static string ResolveModuleDirectory()
        {
            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            DirectoryInfo binaryDirectory = string.IsNullOrWhiteSpace(assemblyDirectory)
                ? null
                : Directory.GetParent(assemblyDirectory);
            DirectoryInfo moduleDirectory = binaryDirectory == null ? null : binaryDirectory.Parent;
            return moduleDirectory == null
                ? AppDomain.CurrentDomain.BaseDirectory
                : moduleDirectory.FullName;
        }

        internal static void Info(string message)
        {
            Write("INFO", message, null);
        }

        internal static void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        private static void Write(string level, string message, Exception exception)
        {
            try
            {
                if (string.IsNullOrEmpty(_logPath))
                {
                    return;
                }

                string line = string.Format(CultureInfo.InvariantCulture, "{0:O} [{1}] {2}{3}", DateTime.UtcNow, level, message, exception == null ? string.Empty : Environment.NewLine + exception);
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch (IOException)
            {
                // Diagnostics must never prevent the module from loading.
            }
            catch (UnauthorizedAccessException)
            {
                // A read-only module directory safely disables file logging.
            }
        }
    }
}
