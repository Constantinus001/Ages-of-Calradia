using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace AgesOfCalradiaSuccession
{
    internal static class SuccessionDiagnostics
    {
        private static string _logPath;

        internal static void Initialize()
        {
            string module = ResolveModuleDirectory();
            string logs = Path.Combine(module, "Logs");
            Directory.CreateDirectory(logs);
            _logPath = Path.Combine(logs, "AgesOfCalradiaSuccession.log");
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
            try
            {
                if (!string.IsNullOrEmpty(_logPath))
                    File.AppendAllText(_logPath, string.Format(CultureInfo.InvariantCulture, "{0:O} [INFO] {1}{2}", DateTime.UtcNow, message, Environment.NewLine));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal static void Error(string message, Exception exception)
        {
            Info("ERROR: " + message + (exception == null ? string.Empty : " " + exception));
        }
    }
}
