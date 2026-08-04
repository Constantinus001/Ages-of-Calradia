using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TwelveMonthCalendar
{
    // NavalDLC creates its map-bar view during the character-creation transition.
    // On that transition the native finance model can be asked for localization
    // before Game.Current has finished initializing. Skipping only that early UI
    // refresh lets the normal refresh run once the campaign is ready.
    [HarmonyPatch]
    internal static class NavalDlcFinanceInitializationPatch
    {
        private const int CharacterCreationFinanceDelaySeconds = 15;
        private static long _deferMapInfoRefreshUntilUtcTicks;

        internal static void DeferMapInfoRefreshUntilMapStateIsReady()
        {
            DateTime untilUtc = DateTime.UtcNow.AddSeconds(CharacterCreationFinanceDelaySeconds);
            Interlocked.Exchange(ref _deferMapInfoRefreshUntilUtcTicks, untilUtc.Ticks);
            Diagnostics.Info(
                "NavalDLC map-finance refreshes deferred for "
                + CharacterCreationFinanceDelaySeconds
                + " seconds during the character-creation map transition.");
        }

        internal static void CompleteMapInfoRefreshDeferral()
        {
            long untilTicks = Interlocked.Read(ref _deferMapInfoRefreshUntilUtcTicks);
            if (untilTicks > DateTime.UtcNow.Ticks)
            {
                Diagnostics.Info(
                    "Map state is ready; NavalDLC map-finance refreshes remain deferred until the campaign UI settling period ends.");
            }
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("NavalDLC.ViewModelCollection.Map.MapBar.NavalMapInfoVM"))
                .FirstOrDefault(candidate => candidate != null);

            if (type == null)
            {
                try
                {
                    type = Assembly.Load("NavalDLC.ViewModelCollection")
                        .GetType("NavalDLC.ViewModelCollection.Map.MapBar.NavalMapInfoVM");
                }
                catch
                {
                    // NavalDLC is optional; leaving the target absent is safe.
                }
            }

            if (type == null)
            {
                Diagnostics.Info(
                    "NavalDLC map-finance compatibility target was not found; compatibility patch not needed.");
                return Enumerable.Empty<MethodBase>();
            }

            MethodBase target = AccessTools.Method(type, "UpdatePlayerInfo", new[] { typeof(bool) });
            if (target == null)
            {
                Diagnostics.Info(
                    "NavalDLC map-finance compatibility target did not expose UpdatePlayerInfo(bool); compatibility patch skipped.");
                return Enumerable.Empty<MethodBase>();
            }

            Diagnostics.Info("NavalDLC map-finance compatibility target found.");
            return new[] { target };
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            try
            {
                if (!DefaultClanFinanceInitialization.EnsureCampaignGameCurrent("NavalDLC map-bar refresh"))
                {
                    Diagnostics.Info("Skipped a NavalDLC map-finance refresh because the campaign game context was unavailable.");
                    return false;
                }

                long untilTicks = Interlocked.Read(ref _deferMapInfoRefreshUntilUtcTicks);
                if (untilTicks > DateTime.UtcNow.Ticks)
                {
                    Diagnostics.Info("Skipped a NavalDLC map-finance refresh while the campaign UI is still settling after character creation.");
                    return false;
                }

                if (untilTicks != 0
                    && Interlocked.CompareExchange(ref _deferMapInfoRefreshUntilUtcTicks, 0, untilTicks) == untilTicks)
                {
                    Diagnostics.Info("NavalDLC map-finance refresh deferral elapsed; normal refreshes are enabled.");
                }

                Game game = Game.Current;
                if (game == null || game.GameTextManager == null)
                {
                    Diagnostics.Info(
                        "Skipped an early NavalDLC map-finance refresh while the game text manager was not ready. "
                        + "GameCurrent=" + (game != null)
                        + "; GameTextManager=" + (game != null && game.GameTextManager != null) + ".");
                    return false;
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Info("Skipped a NavalDLC map-finance refresh because game initialization was incomplete: " + exception.Message);
                return false;
            }

            return true;
        }
    }

    // Bannerlord v1.4.7's NavalDLC delegates every clan-finance calculation to
    // a native class whose static initializer is failing before the first map
    // screen. This temporary compatibility guard skips only that delegated
    // calculation, allowing the campaign to start while the exact text-manager
    // state is captured in the diagnostics log.
    [HarmonyPatch]
    internal static class NavalDlcClanFinanceSafetyPatch
    {
        private static int _suppressionLogged;

        private static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("NavalDLC.GameComponents.NavalDLCClanFinanceModel"))
                .FirstOrDefault(candidate => candidate != null);

            if (type == null)
            {
                try
                {
                    type = Assembly.Load("NavalDLC")
                        .GetType("NavalDLC.GameComponents.NavalDLCClanFinanceModel");
                }
                catch
                {
                    // NavalDLC is optional.
                }
            }

            if (type == null)
            {
                Diagnostics.Info("NavalDLC clan-finance safety target was not found.");
                return Enumerable.Empty<MethodBase>();
            }

            Diagnostics.Info("NavalDLC clan-finance safety target found.");
            return new[]
            {
                AccessTools.Method(type, "CalculateClanGoldChange"),
                AccessTools.Method(type, "CalculateClanIncome"),
                AccessTools.Method(type, "CalculateClanExpenses")
            }.Where(method => method != null);
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(bool includeDescriptions, ref ExplainedNumber __result)
        {
            if (DefaultClanFinanceInitialization.NativeFinanceAvailable
                && DefaultClanFinanceInitialization.EnsureCampaignGameCurrent("NavalDLC clan-finance call"))
            {
                return true;
            }

            __result = new ExplainedNumber(0f, includeDescriptions);
            if (Interlocked.Exchange(ref _suppressionLogged, 1) == 0)
            {
                Diagnostics.Info(
                    "Temporarily suppressed NavalDLC's native clan-finance call because the campaign game context was unavailable."
                    + " Native finance will resume automatically when initialization succeeds.");
            }

            return false;
        }
    }
}
