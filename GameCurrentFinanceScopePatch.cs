using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;

namespace TwelveMonthCalendar
{
    // This leaves Game.Current fully native except while the guarded map-info
    // call is actively initializing the finance model on the same thread.
    [HarmonyPatch(typeof(Game), "get_Current")]
    internal static class GameCurrentFinanceScopePatch
    {
        private static int _overrideUseLogged;

        [HarmonyPrefix]
        private static bool Prefix(ref Game __result)
        {
            Game scopedGame = DefaultClanFinanceInitialization.GetScopedGame();
            if (scopedGame == null)
            {
                return true;
            }

            __result = scopedGame;
            if (Interlocked.Exchange(ref _overrideUseLogged, 1) == 0)
            {
                Diagnostics.Info("Scoped Game.Current override supplied the campaign game to native finance initialization.");
            }

            return false;
        }
    }
}
