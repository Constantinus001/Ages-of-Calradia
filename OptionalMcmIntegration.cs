using System;
using System.IO;
using System.Reflection;

namespace TwelveMonthCalendar
{
    internal static class OptionalMcmIntegration
    {
        private static bool _initialized;
        private static bool _settingsRegistered;

        internal static bool IsSettingsRegistered
        {
            get { return _settingsRegistered; }
        }

        internal static void TryInitialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            try
            {
                Assembly mcmAssembly = FindLoadedAssembly("MCMv5");
                Type mcmType = mcmAssembly?.GetType(
                    "MCM.Abstractions.Base.Global.AttributeGlobalSettings`1") ??
                    Type.GetType(
                        "MCM.Abstractions.Base.Global.AttributeGlobalSettings`1, MCMv5");
                if (mcmType == null)
                {
                    Diagnostics.Info("MCM not detected. Calendar defaults remain active; MCM is optional.");
                    return;
                }

                Diagnostics.Info(
                    "MCM detected: " +
                    (mcmAssembly?.GetName().Version?.ToString() ?? "version unavailable") +
                    ". Loading the calendar settings adapter.");

                string moduleDirectory = Path.GetDirectoryName(
                    typeof(MySubModule).Assembly.Location);
                string adapterPath = Path.Combine(
                    moduleDirectory,
                    "AgesOfCalradia.MCM.dll");

                if (!File.Exists(adapterPath))
                {
                    Diagnostics.Info("MCM detected, but optional adapter DLL is missing.");
                    return;
                }

                Assembly adapter = FindLoadedAssembly("AgesOfCalradia.MCM") ??
                    Assembly.LoadFrom(adapterPath);
                Type settingsType = adapter.GetType(
                    "AgesOfCalradia.MCM.CalendarMcmSettings");
                MethodInfo registerMethod = settingsType?.GetMethod(
                    "RegisterSettings",
                    BindingFlags.Public | BindingFlags.Static);

                if (registerMethod == null)
                {
                    Diagnostics.Info("MCM adapter loaded without a Register method.");
                    return;
                }

                object result = registerMethod.Invoke(null, null);
                _settingsRegistered = result is bool && (bool)result;
                if (_settingsRegistered)
                {
                    Diagnostics.Info("Optional MCM settings registered; the native Calendar Options tab will be hidden.");
                }
                else
                {
                    Diagnostics.Info("MCM was detected but its calendar settings were not ready; the native Calendar Options tab remains available.");
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Error(
                    "Optional MCM integration failed; calendar will continue with defaults.",
                    exception);
            }
        }

        private static Assembly FindLoadedAssembly(string simpleName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (string.Equals(
                        assembly.GetName().Name,
                        simpleName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return assembly;
                    }
                }
                catch
                {
                    // Dynamic or partially loaded assemblies can reject metadata
                    // access. They are unrelated to this optional integration.
                }
            }

            return null;
        }
    }
}
