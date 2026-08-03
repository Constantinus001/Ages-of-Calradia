using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;

namespace TwelveMonthCalendar
{
    internal static class GameCurrentFallbackState
    {
        private static Game _campaignGame;
        private static int _fallbackUseLogged;

        internal static void SetCampaignGame(Game game)
        {
            _campaignGame = IsTextManagerReady(game) ? game : null;
            Interlocked.Exchange(ref _fallbackUseLogged, 0);
            Diagnostics.Info(
                "Campaign game reference registered for finance compatibility. GameTextManager="
                + (_campaignGame != null) + ".");
        }

        internal static void ClearCampaignGame()
        {
            _campaignGame = null;
            Interlocked.Exchange(ref _fallbackUseLogged, 0);
        }

        internal static Game CampaignGame => _campaignGame;

        internal static bool IsTextManagerReady(Game game)
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
                // A partially initialized Game must never make a compatibility
                // fallback throw while Bannerlord is reading Game.Current.
                return false;
            }
        }

        internal static void LogFallbackUse(bool originalGameWasPresent, bool originalTextManagerWasPresent)
        {
            if (Interlocked.Exchange(ref _fallbackUseLogged, 1) == 0)
            {
                Diagnostics.Info(
                    "Provided the active campaign game to native finance initialization. "
                    + "OriginalGameCurrent=" + originalGameWasPresent
                    + "; OriginalGameTextManager=" + originalTextManagerWasPresent + ".");
            }
        }
    }

    // The NavalDLC finance model's static initializer reads
    // Game.Current.GameTextManager. During campaign startup Game.Current can
    // point to a partially initialized Game even while OnGameStart provides the
    // fully initialized campaign Game. Use that known-good instance only until
    // the native property is ready.
    [HarmonyPatch(typeof(Game), "get_Current")]
    internal static class GameCurrentFallbackPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref Game __result)
        {
            try
            {
                bool originalGameWasPresent = __result != null;
                bool originalTextManagerWasPresent = GameCurrentFallbackState.IsTextManagerReady(__result);

                if (originalTextManagerWasPresent)
                {
                    return;
                }

                Game campaignGame = GameCurrentFallbackState.CampaignGame;
                if (GameCurrentFallbackState.IsTextManagerReady(campaignGame))
                {
                    __result = campaignGame;
                    GameCurrentFallbackState.LogFallbackUse(
                        originalGameWasPresent,
                        originalTextManagerWasPresent);
                }
            }
            catch
            {
                // Game.Current is called by native startup code. Preserve its
                // original value if a compatibility probe cannot be completed.
            }
        }
    }
}
