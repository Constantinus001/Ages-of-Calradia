using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Audits the actual AI-clan gold credit immediately after Bannerlord's
    /// daily finance tick. It is read-only and exposes any money source that
    /// bypasses the scaled ClanFinanceModel contract.
    /// </summary>
    internal static class CalendarFinanceTelemetry
    {
        private sealed class PendingCredit
        {
            internal int GoldBeforeCredit;
            internal float NativeResult;
            internal float ScaledResult;
        }

        private static readonly Dictionary<Clan, PendingCredit> PendingCredits =
            new Dictionary<Clan, PendingCredit>();
        private static int _mismatchCount;

        internal static void RecordFinanceResult(Clan clan, float nativeResult, float scaledResult, bool applyWithdrawals)
        {
            if (!CalendarSettingsState.AnnualBalanceDiagnosticsEnabled
                || !applyWithdrawals
                || clan == null
                || clan == Clan.PlayerClan)
            {
                return;
            }

            PendingCredits[clan] = new PendingCredit
            {
                GoldBeforeCredit = clan.Gold,
                NativeResult = nativeResult,
                ScaledResult = scaledResult
            };
        }

        internal static void VerifyAppliedCredit(Clan clan)
        {
            if (!CalendarSettingsState.AnnualBalanceDiagnosticsEnabled
                || clan == null
                || !PendingCredits.TryGetValue(clan, out PendingCredit pending))
            {
                return;
            }

            PendingCredits.Remove(clan);
            int actualDelta = clan.Gold - pending.GoldBeforeCredit;
            int expectedDelta = (int)Math.Round(pending.ScaledResult, MidpointRounding.AwayFromZero);
            int tolerance = Math.Max(2, (int)Math.Ceiling(Math.Abs(expectedDelta) * 0.05f));
            int difference = actualDelta - expectedDelta;
            if (Math.Abs(difference) <= tolerance)
            {
                return;
            }

            // A positive mismatch is money created during the daily AI-clan
            // finance tick outside the scaled result returned to Bannerlord.
            // Remove only that excess. Player money, losses, loot, trade and
            // all non-daily events are outside this postfix and untouched.
            if (difference > tolerance && clan.Leader != null)
            {
                int correction = Math.Min(difference, clan.Leader.Gold);
                if (correction > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(
                        clan.Leader,
                        null,
                        correction,
                        disableNotification: true);
                    Diagnostics.Info(string.Format(
                        "AI finance surplus corrected. Clan={0}; Removed={1}; ExpectedDaily={2}; ActualDaily={3}.",
                        clan.Name,
                        correction,
                        expectedDelta,
                        actualDelta));
                }
            }

            _mismatchCount++;
            Diagnostics.Info(string.Format(
                "Finance flow mismatch #{0}. Clan={1}; NativeDaily={2:F2}; ScaledDaily={3:F2}; ActualGoldDelta={4}; Difference={5}; GoldBefore={6}; GoldAfter={7}.",
                _mismatchCount,
                clan.Name,
                pending.NativeResult,
                pending.ScaledResult,
                actualDelta,
                difference,
                pending.GoldBeforeCredit,
                clan.Gold));
        }
    }
}
