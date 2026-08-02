using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TwelveMonthCalendar
{
    internal static class CrashDiagnostics
    {
        private static bool _unhandledExceptionHandlerRegistered;

        internal static void RegisterUnhandledExceptionHandler()
        {
            if (_unhandledExceptionHandlerRegistered)
            {
                return;
            }

            _unhandledExceptionHandlerRegistered = true;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Diagnostics.Info("Crash diagnostics registered: process-level and character-creation transition tracing enabled.");
        }

        internal static void Trace(string location)
        {
            Diagnostics.Info("Crash trace: " + location);
        }

        internal static Exception LogAndPreserve(string location, Exception exception)
        {
            if (exception != null)
            {
                Diagnostics.Error("Crash trace caught an exception at " + location + ".", exception);
            }

            return exception;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            try
            {
                Exception exception = args.ExceptionObject as Exception;
                if (exception != null)
                {
                    Diagnostics.Error(
                        "Process-level unhandled exception. IsTerminating=" + args.IsTerminating + ".",
                        exception);
                }
                else
                {
                    Diagnostics.Info(
                        "Process-level unhandled non-Exception object. IsTerminating="
                        + args.IsTerminating + "; Value=" + args.ExceptionObject);
                }
            }
            catch
            {
                // Never allow diagnostics to alter crash handling.
            }
        }
    }

    [HarmonyPatch]
    internal static class CharacterCreationFinalizeDiagnosticsPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(Campaign).Assembly.GetType(
                    "TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState"),
                "FinalizeCharacterCreationState");
        }

        [HarmonyPrefix]
        private static void Prefix()
        {
            CrashDiagnostics.Trace("CharacterCreationState.FinalizeCharacterCreationState entered.");
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            CrashDiagnostics.Trace("CharacterCreationState.FinalizeCharacterCreationState completed.");
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            return CrashDiagnostics.LogAndPreserve(
                "CharacterCreationState.FinalizeCharacterCreationState",
                __exception);
        }
    }

    [HarmonyPatch]
    internal static class GameStateCreationDiagnosticsPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(Game).Assembly.GetType("TaleWorlds.Core.GameStateManager"),
                "HandleCreateState");
        }

        [HarmonyPrefix]
        private static void Prefix(object __0)
        {
            CrashDiagnostics.Trace(
                "GameStateManager.HandleCreateState entered. State="
                + (__0 == null ? "<null>" : __0.GetType().FullName));
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            return CrashDiagnostics.LogAndPreserve("GameStateManager.HandleCreateState", __exception);
        }
    }

    [HarmonyPatch]
    internal static class MapScreenCreationDiagnosticsPatch
    {
        private static MethodBase TargetMethod()
        {
            Type type = FindType("SandBox.View", "SandBox.View.Map.MapScreen");
            return type == null
                ? null
                : AccessTools.GetDeclaredConstructors(type).FirstOrDefault();
        }

        [HarmonyPrefix]
        private static void Prefix()
        {
            CrashDiagnostics.Trace("SandBox.View.Map.MapScreen constructor entered.");
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            CrashDiagnostics.Trace("SandBox.View.Map.MapScreen constructor completed.");
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            return CrashDiagnostics.LogAndPreserve("SandBox.View.Map.MapScreen constructor", __exception);
        }

        private static Type FindType(string assemblyName, string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);

            if (type != null)
            {
                return type;
            }

            try
            {
                return Assembly.Load(assemblyName).GetType(typeName);
            }
            catch
            {
                return null;
            }
        }
    }
}
