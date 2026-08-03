using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Scales Bannerlord's public clan-finance contract without patching its
    /// private DefaultClanFinanceModel implementation. That implementation has
    /// a static initializer which is unsafe while the first map bar is being
    /// created, so delegation is deliberately skipped until the engine has a
    /// valid native Game.Current and GameTextManager.
    /// </summary>
    internal sealed class CalendarClanFinanceModel : ClanFinanceModel
    {
        private readonly ClanFinanceModel _native;
        private static int _deferredCallsLogged;

        internal CalendarClanFinanceModel(ClanFinanceModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override int PartyGoldLowerThreshold
        {
            get
            {
                return CanEvaluateNativeFinance()
                    ? Evaluate(() => _native.PartyGoldLowerThreshold, false)
                    : DeferredInteger();
            }
        }

        public override ExplainedNumber CalculateClanGoldChange(
            Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false, bool includeDetails = false)
        {
            if (!CanEvaluateNativeFinance())
            {
                return DeferredNumber(includeDescriptions);
            }

            ExplainedNumber result = Evaluate(
                () => _native.CalculateClanGoldChange(
                    clan, includeDescriptions, applyWithdrawals, includeDetails),
                applyWithdrawals);
            Scale(ref result);
            return result;
        }

        public override ExplainedNumber CalculateClanIncome(
            Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false, bool includeDetails = false)
        {
            if (!CanEvaluateNativeFinance())
            {
                return DeferredNumber(includeDescriptions);
            }

            ExplainedNumber result = Evaluate(
                () => _native.CalculateClanIncome(
                    clan, includeDescriptions, applyWithdrawals, includeDetails),
                applyWithdrawals);
            Scale(ref result);
            return result;
        }

        public override ExplainedNumber CalculateClanExpenses(
            Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false, bool includeDetails = false)
        {
            if (!CanEvaluateNativeFinance())
            {
                return DeferredNumber(includeDescriptions);
            }

            ExplainedNumber result = Evaluate(
                () => _native.CalculateClanExpenses(
                    clan, includeDescriptions, applyWithdrawals, includeDetails),
                applyWithdrawals);
            Scale(ref result);
            return result;
        }

        public override ExplainedNumber CalculateTownIncomeFromTariffs(Clan clan, Town town, bool applyWithdrawals = false)
        {
            if (!CanEvaluateNativeFinance())
            {
                return DeferredNumber(false);
            }

            ExplainedNumber result = Evaluate(
                () => _native.CalculateTownIncomeFromTariffs(clan, town, applyWithdrawals),
                applyWithdrawals);
            Scale(ref result);
            return result;
        }

        public override int CalculateTownIncomeFromProjects(Town town)
        {
            return CanEvaluateNativeFinance()
                ? Scale(Evaluate(() => _native.CalculateTownIncomeFromProjects(town), false))
                : DeferredInteger();
        }

        public override int CalculateNotableDailyGoldChange(Hero hero, bool applyWithdrawals = false)
        {
            return CanEvaluateNativeFinance()
                ? Scale(Evaluate(() => _native.CalculateNotableDailyGoldChange(hero, applyWithdrawals), applyWithdrawals))
                : DeferredInteger();
        }

        public override int CalculateVillageIncome(Clan clan, Village village, bool applyWithdrawals = false)
        {
            return CanEvaluateNativeFinance()
                ? Scale(Evaluate(() => _native.CalculateVillageIncome(clan, village, applyWithdrawals), applyWithdrawals))
                : DeferredInteger();
        }

        public override int CalculateOwnerIncomeFromCaravan(MobileParty caravan)
        {
            return CanEvaluateNativeFinance()
                ? Scale(Evaluate(() => _native.CalculateOwnerIncomeFromCaravan(caravan), false))
                : DeferredInteger();
        }

        public override int CalculateOwnerIncomeFromWorkshop(Workshop workshop)
        {
            return CanEvaluateNativeFinance()
                ? Scale(Evaluate(() => _native.CalculateOwnerIncomeFromWorkshop(workshop), false))
                : DeferredInteger();
        }

        public override float RevenueSmoothenFraction()
        {
            return CanEvaluateNativeFinance()
                ? Evaluate(() => _native.RevenueSmoothenFraction(), false)
                : DeferredFloat();
        }

        private static bool CanEvaluateNativeFinance()
        {
            try
            {
                Game current = Game.Current;
                return current != null && current.GameTextManager != null;
            }
            catch
            {
                return false;
            }
        }

        private static T Evaluate<T>(Func<T> calculation, bool applyWithdrawals)
        {
            DailyRateBalance.EnterFinanceEvaluation(applyWithdrawals);
            try
            {
                return calculation();
            }
            finally
            {
                DailyRateBalance.ExitFinanceEvaluation();
            }
        }

        private static ExplainedNumber DeferredNumber(bool includeDescriptions)
        {
            LogDeferredCall();
            return new ExplainedNumber(0f, includeDescriptions, null);
        }

        private static int DeferredInteger()
        {
            LogDeferredCall();
            return 0;
        }

        private static float DeferredFloat()
        {
            LogDeferredCall();
            return 0f;
        }

        private static void LogDeferredCall()
        {
            if (Interlocked.Exchange(ref _deferredCallsLogged, 1) == 0)
            {
                Diagnostics.Info(
                    "Deferred a clan-finance calculation while Bannerlord's native Game.Current was unavailable. "
                    + "No native finance code was invoked; normal scaled evaluation resumes automatically once the campaign context is ready.");
            }
        }

        private static void Scale(ref ExplainedNumber value)
        {
            if (CalendarSettingsState.ExtendedCalendarEnabled)
            {
                SettlementBalanceMath.Scale(ref value);
            }
        }

        private static int Scale(int value)
        {
            return DailyRateBalance.ScaleDiscreteDailyValue(value, "ClanFinance", null);
        }
    }
}
