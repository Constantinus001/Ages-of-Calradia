using System.Threading;
using TaleWorlds.Core;

namespace TwelveMonthCalendar
{
    // Bannerlord's DefaultClanFinanceModel static constructor assumes the
    // engine's own Game.Current is ready. It is not during the first map-bar
    // construction after character creation. Never alter that native state;
    // defer the optional UI calculation until Bannerlord makes it valid.
    internal static class DefaultClanFinanceInitialization
    {
        private static int _deferredMapFinanceLogged;

        internal static void InitializeAfterCampaignGameStart(Game game)
        {
            Diagnostics.Info(
                "Campaign game registered for finance diagnostics. LifecycleGameTextManager="
                + IsTextManagerReady(game) + ".");
        }

        internal static bool IsNativeFinanceContextReady()
        {
            try
            {
                return IsTextManagerReady(Game.Current);
            }
            catch
            {
                return false;
            }
        }

        internal static void LogDeferredMapFinance()
        {
            if (Interlocked.Exchange(ref _deferredMapFinanceLogged, 1) == 0)
            {
                Diagnostics.Info(
                    "Deferred the first map-bar clan-finance calculation because Bannerlord's native Game.Current was not ready. "
                    + "The calculation will run on a later map refresh after native initialization completes.");
            }
        }

        private static bool IsTextManagerReady(Game game)
        {
            if (game == null)
            {
                return false;
            }

            try
            {
                return game.GameTextManager != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
