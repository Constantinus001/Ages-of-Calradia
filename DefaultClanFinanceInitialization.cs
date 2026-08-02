using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TwelveMonthCalendar
{
    // The native finance model initializes its text fields eagerly. Probe the
    // exact text lookup path without invoking that type, so a bad native
    // initializer cannot poison the class before diagnostics are recorded.
    internal static class DefaultClanFinanceInitialization
    {
        private static readonly string[] FinanceTextIds =
        {
            "str_finance_projects_income",
            "str_finance_shop_expense",
            "str_finance_mercenary",
            "str_finance_tribute_expenses",
            "str_finance_settlement_income",
            "str_finance_main_party_wage"
        };

        private static Game _campaignGame;
        private static bool _nativeFinanceAvailable;

        internal static bool NativeFinanceAvailable
        {
            get { return _nativeFinanceAvailable; }
        }

        // Called by the static-constructor transpiler. It deliberately avoids
        // Game.Current because the native initializer's direct use of that
        // accessor is the failing code path in this Bannerlord/NavalDLC build.
        internal static TextObject FindFinanceText(string id)
        {
            Game game = _campaignGame;
            if (game != null && game.GameTextManager != null)
            {
                try
                {
                    return game.GameTextManager.FindText(id);
                }
                catch (Exception exception)
                {
                    Diagnostics.Error("Stored campaign finance-text lookup failed for '" + id + "'.", exception);
                }
            }

            return new TextObject("{=!}" + id);
        }

        internal static void InitializeAfterCampaignGameStart(Game game)
        {
            _campaignGame = game;

            if (game == null || game.GameTextManager == null)
            {
                Diagnostics.Info("Native finance text probe skipped because the campaign text manager is unavailable.");
                return;
            }

            try
            {
                List<string> missing = new List<string>();
                foreach (string id in FinanceTextIds)
                {
                    string value = game.GameTextManager.FindText(id).ToString();
                    if (value.IndexOf("ERROR: Text with id", StringComparison.Ordinal) >= 0)
                    {
                        missing.Add(id);
                    }
                }

                Diagnostics.Info(
                    "Native finance text probe completed. MissingTextCount=" + missing.Count
                    + (missing.Count == 0 ? "." : "; Missing=" + string.Join(",", missing) + "."));

                RuntimeHelpers.RunClassConstructor(typeof(DefaultClanFinanceModel).TypeHandle);
                _nativeFinanceAvailable = true;
                Diagnostics.Info("Native clan-finance initialization completed through the calendar compatibility provider.");
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Native finance text probe failed.", exception);
            }
        }
    }
}
