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
            _campaignGame = game;
            Diagnostics.Info(
                "Campaign game reference registered for finance compatibility. GameTextManager="
                + (game != null && game.GameTextManager != null) + ".");
        }

        internal static Game CampaignGame => _campaignGame;

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
            bool originalGameWasPresent = __result != null;
            bool originalTextManagerWasPresent = originalGameWasPresent
                && __result.GameTextManager != null;

            if (originalTextManagerWasPresent)
            {
                return;
            }

            Game campaignGame = GameCurrentFallbackState.CampaignGame;
            if (campaignGame != null)
            {
                __result = campaignGame;
                GameCurrentFallbackState.LogFallbackUse(
                    originalGameWasPresent,
                    originalTextManagerWasPresent);
            }
        }
    }
}
