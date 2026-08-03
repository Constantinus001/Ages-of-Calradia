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
        private static int _recordedCredits;
        private static int _verifiedCredits;
        private static int _kingdomBudgetTransferCount;
        private static long _nativeKingdomBudgetDelta;
        private static long _scaledKingdomBudgetDelta;

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
            _recordedCredits++;
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
            _verifiedCredits++;
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

        internal static void RecordKingdomBudgetTransfer(
            Kingdom kingdom,
            int nativeDelta,
            int scaledDelta)
        {
            if (!CalendarSettingsState.AnnualBalanceDiagnosticsEnabled
                || kingdom == null
                || nativeDelta == 0)
            {
                return;
            }

            _kingdomBudgetTransferCount++;
            _nativeKingdomBudgetDelta += nativeDelta;
            _scaledKingdomBudgetDelta += scaledDelta;
        }

        internal static void ReportMonthlyHealth()
        {
            if (!CalendarSettingsState.AnnualBalanceDiagnosticsEnabled)
            {
                return;
            }

            Diagnostics.Info(string.Format(
                "Finance hook health. RecordedCredits={0}; VerifiedCredits={1}; PendingCredits={2}; Mismatches={3}; KingdomBudgetTransfers={4}; NativeBudgetDelta={5}; ScaledBudgetDelta={6}.",
                _recordedCredits,
                _verifiedCredits,
                PendingCredits.Count,
                _mismatchCount,
                _kingdomBudgetTransferCount,
                _nativeKingdomBudgetDelta,
                _scaledKingdomBudgetDelta));
            _recordedCredits = 0;
            _verifiedCredits = 0;
            _mismatchCount = 0;
            _kingdomBudgetTransferCount = 0;
            _nativeKingdomBudgetDelta = 0;
            _scaledKingdomBudgetDelta = 0;
        }
    }
}
