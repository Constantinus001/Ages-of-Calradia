using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Converts values that are evaluated once per campaign day from the
    /// native 84-day year to the Gregorian campaign year. The clock itself
    /// already advances at the matching time scale; these patches prevent
    /// daily accumulation from happening 4.35 times as often per year.
    /// </summary>
    internal static class DailyRateBalance
    {
        [ThreadStatic]
        private static int _financeEvaluationDepth;

        [ThreadStatic]
        private static bool _financeApplyWithdrawals;

        internal static bool IsExtendedCalendar
        {
            get { return CalendarSettingsState.ExtendedCalendarEnabled; }
        }

        internal static float Factor
        {
            get { return SettlementBalanceMath.DailyRateFactor; }
        }

        internal static bool IsFinanceEvaluation
        {
            get { return _financeEvaluationDepth > 0; }
        }

        internal static bool FinanceApplyWithdrawals
        {
            get { return _financeApplyWithdrawals; }
        }

        internal static void EnterFinanceEvaluation(bool applyWithdrawals)
        {
            if (_financeEvaluationDepth == 0)
            {
                _financeApplyWithdrawals = applyWithdrawals;
            }

            _financeEvaluationDepth++;
        }

        internal static void ExitFinanceEvaluation()
        {
            if (_financeEvaluationDepth > 0)
            {
                _financeEvaluationDepth--;
                if (_financeEvaluationDepth == 0)
                {
                    _financeApplyWithdrawals = false;
                }
            }
        }

        internal static int ScaleDailyInteger(int value)
        {
            return (int)Math.Round(value * Factor, MidpointRounding.AwayFromZero);
        }

        internal static int ScaleDiscreteDailyValue(int value, string channel, object scope)
        {
            if (!IsExtendedCalendar || value == 0)
            {
                return value;
            }

            int sign = value < 0 ? -1 : 1;
            float exact = Math.Abs(value) * Factor;
            int whole = (int)Math.Floor(exact);
            float fraction = exact - whole;
            if (fraction > 0f)
            {
                long day = (long)Math.Floor(CampaignTime.Now.ToDays);
                int scopeHash = scope == null ? 0 : RuntimeHelpers.GetHashCode(scope);
                long hash = day * 1103515245L + scopeHash * 31L + channel.GetHashCode();
                double unit = (hash & 0x7FFFFFFF) / 2147483648.0;
                if (unit < fraction)
                {
                    whole++;
                }
            }

            return sign * whole;
        }

        internal static void Scale(ref ExplainedNumber value)
        {
            if (IsExtendedCalendar)
            {
                SettlementBalanceMath.Scale(ref value);
            }
        }

        internal static void Scale(ref float value)
        {
            if (IsExtendedCalendar)
            {
                value *= Factor;
            }
        }

        internal static void Scale(ref int value)
        {
            if (IsExtendedCalendar)
            {
                value = (int)Math.Round(value * Factor, MidpointRounding.AwayFromZero);
            }
        }

        /// <summary>
        /// Converts a per-day probability so that repeating it over 365 days
        /// has the same annual probability as repeating the native value over
        /// 84 days.
        /// </summary>
        internal static float ScaleDailyProbability(float probability)
        {
            if (!IsExtendedCalendar)
            {
                return probability;
            }

            probability = Math.Max(0f, Math.Min(1f, probability));
            return 1f - (float)Math.Pow(1d - probability, Factor);
        }
    }

    [HarmonyPatch(typeof(DefaultBuildingConstructionModel), "CalculateDailyConstructionPower")]
    internal static class ConstructionPowerBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultBuildingConstructionModel), "CalculateDailyConstructionPowerWithoutBoost")]
    internal static class ConstructionPowerWithoutBoostBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Town town, ref int __result)
        {
            __result = DailyRateBalance.ScaleDiscreteDailyValue(
                __result,
                "construction",
                town);
        }
    }

    [HarmonyPatch(typeof(DefaultMobilePartyFoodConsumptionModel), "CalculateDailyFoodConsumptionf")]
    internal static class PartyFoodConsumptionBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementLoyaltyModel), "CalculateLoyaltyChange")]
    internal static class LoyaltyBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementSecurityModel), "CalculateSecurityChange")]
    internal static class SecurityBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementMilitiaModel), "CalculateMilitiaChange")]
    internal static class MilitiaBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementGarrisonModel), "CalculateBaseGarrisonChange")]
    internal static class GarrisonBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementTaxModel), "CalculateTownTax")]
    internal static class SettlementTaxBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            if (!DailyRateBalance.IsFinanceEvaluation)
            {
                DailyRateBalance.Scale(ref __result);
            }
        }
    }

    [HarmonyPatch(typeof(DefaultClanPoliticsModel), "CalculateInfluenceChange")]
    internal static class InfluenceBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), "CalculateDailyCohesionChange")]
    internal static class ArmyCohesionBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultPartyHealingModel), "GetDailyHealingForRegulars")]
    internal static class RegularHealingBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultPartyHealingModel), "GetDailyHealingHpForHeroes")]
    internal static class HeroHealingBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultPartyTrainingModel), "GetEffectiveDailyExperience")]
    internal static class PartyTrainingBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultPartyMoraleModel), "GetDailyStarvationMoralePenalty")]
    internal static class StarvationMoraleBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(TaleWorlds.CampaignSystem.Party.PartyBase party, ref int __result)
        {
            __result = DailyRateBalance.ScaleDiscreteDailyValue(
                __result,
                "starvation_morale",
                party);
        }
    }

    [HarmonyPatch(typeof(DefaultPartyMoraleModel), "GetDailyNoWageMoralePenalty")]
    internal static class NoWageMoraleBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix([HarmonyArgument(0)] MobileParty party, ref int __result)
        {
            __result = DailyRateBalance.ScaleDiscreteDailyValue(
                __result,
                "no_wage_morale",
                party);
        }
    }

    [HarmonyPatch(typeof(DefaultDailyTroopXpBonusModel), "CalculateDailyTroopXpBonus")]
    internal static class GarrisonTrainingBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Town town, ref int __result)
        {
            __result = DailyRateBalance.ScaleDiscreteDailyValue(
                __result,
                "garrison_xp",
                town);
        }
    }

    [HarmonyPatch(typeof(DefaultNotablePowerModel), "CalculateDailyPowerChangeForHero")]
    internal static class NotablePowerBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultCrimeModel), "GetDailyCrimeRatingChange")]
    internal static class CrimeRatingBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultVolunteerModel), "GetDailyVolunteerProductionProbability")]
    internal static class VolunteerProbabilityBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            __result = DailyRateBalance.ScaleDailyProbability(__result);
        }
    }

    [HarmonyPatch(typeof(DefaultPartyDesertionModel), "GetDesertionChanceForTroop")]
    internal static class DesertionProbabilityBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            __result = DailyRateBalance.ScaleDailyProbability(__result);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementMilitiaModel), "CalculateVeteranMilitiaSpawnChance")]
    internal static class VeteranMilitiaProbabilityBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result)
        {
            if (!DailyRateBalance.IsExtendedCalendar)
            {
                return;
            }

            __result = new ExplainedNumber(
                DailyRateBalance.ScaleDailyProbability(__result.ResultNumber),
                __result.IncludeDescriptions,
                null);
        }
    }

    [HarmonyPatch(typeof(DefaultMinorFactionsModel), "get_DailyMinorFactionHeroSpawnChance")]
    internal static class MinorFactionSpawnProbabilityBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            __result = DailyRateBalance.ScaleDailyProbability(__result);
        }
    }

    [HarmonyPatch(typeof(DefaultPregnancyModel), "GetDailyChanceOfPregnancyForHero")]
    internal static class PregnancyProbabilityBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            __result = DailyRateBalance.ScaleDailyProbability(__result);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementEconomyModel), "GetDailyDemandForCategory")]
    internal static class SettlementDemandBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementEconomyModel), "CalculateDailySettlementBudgetForItemCategory")]
    internal static class SettlementBudgetBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementGarrisonModel), "GetMaximumDailyAutoRecruitmentCount")]
    internal static class GarrisonAutoRecruitmentBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Town town, ref int __result)
        {
            __result = DailyRateBalance.ScaleDiscreteDailyValue(
                __result,
                "auto_recruitment",
                town);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementGarrisonModel), "GetMaximumDailyRepairAmount")]
    internal static class GarrisonRepairBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultSettlementEconomyModel), "GetTownGoldChange")]
    internal static class TownGoldChangeBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            if (!DailyRateBalance.IsFinanceEvaluation)
            {
                DailyRateBalance.Scale(ref __result);
            }
        }
    }

    [HarmonyPatch(typeof(DefaultWorkshopModel), "get_DailyExpense")]
    internal static class WorkshopDailyExpenseBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            if (!DailyRateBalance.IsFinanceEvaluation)
            {
                DailyRateBalance.Scale(ref __result);
            }
        }
    }

    internal sealed class FinanceTaxStockState
    {
        internal int Original;
    }

    [HarmonyPatch(typeof(DefaultClanFinanceModel), "CalculateTownIncomeFromTariffs")]
    internal static class TownTariffIncomeBalancePatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            TaleWorlds.CampaignSystem.Settlements.Town town,
            bool applyWithdrawals,
            out FinanceTaxStockState __state)
        {
            __state = null;
            if (CalendarSettingsState.ExtendedCalendarEnabled && applyWithdrawals)
            {
                __state = new FinanceTaxStockState { Original = town.TradeTaxAccumulated };
            }
        }

        [HarmonyPostfix]
        private static void Postfix(
            TaleWorlds.CampaignSystem.Settlements.Town town,
            ref TaleWorlds.CampaignSystem.ExplainedNumber __result,
            FinanceTaxStockState __state,
            bool applyWithdrawals)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled || !applyWithdrawals)
            {
                if (!DailyRateBalance.IsFinanceEvaluation)
                {
                    DailyRateBalance.Scale(ref __result);
                }

                return;
            }

            int nativeRemaining = town.TradeTaxAccumulated;
            int nativeWithdrawal = __state.Original - nativeRemaining;
            town.TradeTaxAccumulated = __state.Original
                - DailyRateBalance.ScaleDailyInteger(nativeWithdrawal);
        }
    }

    [HarmonyPatch(typeof(DefaultClanFinanceModel), "CalculateVillageIncome")]
    internal static class VillageTaxIncomeBalancePatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            TaleWorlds.CampaignSystem.Settlements.Village village,
            bool applyWithdrawals,
            out FinanceTaxStockState __state)
        {
            __state = null;
            if (CalendarSettingsState.ExtendedCalendarEnabled && applyWithdrawals)
            {
                __state = new FinanceTaxStockState { Original = village.TradeTaxAccumulated };
            }
        }

        [HarmonyPostfix]
        private static void Postfix(
            TaleWorlds.CampaignSystem.Settlements.Village village,
            ref int __result,
            FinanceTaxStockState __state,
            bool applyWithdrawals)
        {
            if (!CalendarSettingsState.ExtendedCalendarEnabled || !applyWithdrawals)
            {
                if (!DailyRateBalance.IsFinanceEvaluation)
                {
                    DailyRateBalance.Scale(ref __result);
                }

                return;
            }

            int nativeRemaining = village.TradeTaxAccumulated;
            int nativeWithdrawal = __state.Original - nativeRemaining;
            village.TradeTaxAccumulated = __state.Original
                - DailyRateBalance.ScaleDailyInteger(nativeWithdrawal);
        }
    }

    [HarmonyPatch(typeof(DefaultClanFinanceModel), "CalculateClanIncomeInternal")]
    internal static class ClanIncomeInternalBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __1, bool applyWithdrawals)
        {
            // During a real withdrawal this is the complete income
            // accumulator, including kingdom, trade, tribute, settlement,
            // caravan, workshop, and mercenary income. Scaling here prevents
            // small components from being missed or scaled twice. Preview
            // calls are scaled once by CalculateClanIncome's outer postfix.
            if (CalendarSettingsState.ExtendedCalendarEnabled && applyWithdrawals)
            {
                SettlementBalanceMath.Scale(ref __1);
            }
        }
    }

    [HarmonyPatch(typeof(DefaultClanFinanceModel), "CalculateClanExpensesInternal")]
    internal static class ClanExpensesInternalBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __1, bool applyWithdrawals)
        {
            // This accumulator contains party/garrison wages, recruitment,
            // tributes, mercenaries, debts, and the remaining expense paths.
            if (CalendarSettingsState.ExtendedCalendarEnabled && applyWithdrawals)
            {
                SettlementBalanceMath.Scale(ref __1);
            }
        }
    }

    [HarmonyPatch(typeof(DefaultClanFinanceModel), "CalculateTownIncomeFromProjects")]
    internal static class TownProjectIncomeBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            if (!DailyRateBalance.IsFinanceEvaluation)
            {
                DailyRateBalance.Scale(ref __result);
            }
        }
    }

    [HarmonyPatch(typeof(DefaultClanFinanceModel), "CalculateOwnerIncomeFromCaravan")]
    internal static class CaravanIncomeBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            if (!DailyRateBalance.IsFinanceEvaluation)
            {
                DailyRateBalance.Scale(ref __result);
            }
        }
    }

    [HarmonyPatch(typeof(DefaultClanFinanceModel), "CalculateOwnerIncomeFromWorkshop")]
    internal static class WorkshopIncomeBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            if (!DailyRateBalance.IsFinanceEvaluation)
            {
                DailyRateBalance.Scale(ref __result);
            }
        }
    }

    [HarmonyPatch(typeof(DefaultClanFinanceModel), "CalculateNotableDailyGoldChange")]
    internal static class NotableDailyGoldBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            if (!DailyRateBalance.IsFinanceEvaluation)
            {
                DailyRateBalance.Scale(ref __result);
            }
        }
    }

    [HarmonyPatch(typeof(DefaultClanFinanceModel), "CalculateClanGoldChange")]
    internal static class ClanGoldChangeBalancePatch
    {
        [HarmonyPrefix]
        private static void Prefix(bool applyWithdrawals, out bool __state)
        {
            DailyRateBalance.EnterFinanceEvaluation(applyWithdrawals);
            __state = true;
        }

        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result, ref bool __state)
        {
            try
            {
                if (!DailyRateBalance.FinanceApplyWithdrawals)
                {
                    DailyRateBalance.Scale(ref __result);
                }
            }
            finally
            {
                if (__state)
                {
                    DailyRateBalance.ExitFinanceEvaluation();
                    __state = false;
                }
            }
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, ref bool __state)
        {
            if (__state)
            {
                DailyRateBalance.ExitFinanceEvaluation();
                __state = false;
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(DefaultClanFinanceModel), "CalculateClanIncome")]
    internal static class ClanIncomeBalancePatch
    {
        [HarmonyPrefix]
        private static void Prefix(bool applyWithdrawals, out bool __state)
        {
            DailyRateBalance.EnterFinanceEvaluation(applyWithdrawals);
            __state = true;
        }

        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result, ref bool __state)
        {
            try
            {
                if (!DailyRateBalance.FinanceApplyWithdrawals)
                {
                    DailyRateBalance.Scale(ref __result);
                }
            }
            finally
            {
                if (__state)
                {
                    DailyRateBalance.ExitFinanceEvaluation();
                    __state = false;
                }
            }
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, ref bool __state)
        {
            if (__state)
            {
                DailyRateBalance.ExitFinanceEvaluation();
                __state = false;
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(DefaultClanFinanceModel), "CalculateClanExpenses")]
    internal static class ClanExpensesBalancePatch
    {
        [HarmonyPrefix]
        private static void Prefix(bool applyWithdrawals, out bool __state)
        {
            DailyRateBalance.EnterFinanceEvaluation(applyWithdrawals);
            __state = true;
        }

        [HarmonyPostfix]
        private static void Postfix(ref ExplainedNumber __result, ref bool __state)
        {
            try
            {
                if (!DailyRateBalance.FinanceApplyWithdrawals)
                {
                    DailyRateBalance.Scale(ref __result);
                }
            }
            finally
            {
                if (__state)
                {
                    DailyRateBalance.ExitFinanceEvaluation();
                    __state = false;
                }
            }
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, ref bool __state)
        {
            if (__state)
            {
                DailyRateBalance.ExitFinanceEvaluation();
                __state = false;
            }
            return __exception;
        }
    }
}
