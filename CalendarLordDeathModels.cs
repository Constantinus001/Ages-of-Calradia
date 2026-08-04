using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Shared mortality math for the two public Bannerlord model seams used by
    /// this module. Executions, scripted deaths, and non-lord heroes do not
    /// pass through either scaled branch.
    /// </summary>
    internal static class CalendarLordDeathBalance
    {
        internal static bool IsEligibleLord(Hero hero)
        {
            return hero != null
                && hero.IsAlive
                && hero.IsLord
                && !hero.IsHumanPlayerCharacter;
        }

        /// <summary>
        /// The age model is evaluated once per campaign day. Convert that day
        /// chance to an annual chance, scale the annual chance, then convert it
        /// back. This keeps a 0.20 setting meaning 20% of native yearly old-age
        /// mortality even at the game's final-age probability cap.
        /// </summary>
        internal static float ScaleDailyDeathProbability(float nativeDailyProbability)
        {
            if (!IsFinite(nativeDailyProbability))
            {
                return nativeDailyProbability;
            }

            float nativeProbability = ClampUnit(nativeDailyProbability);
            float multiplier = CalendarSettingsState.LordDeathRateMultiplier;
            if (multiplier >= 0.9999f || nativeProbability <= 0f)
            {
                return nativeProbability;
            }

            if (multiplier <= 0f)
            {
                return 0f;
            }

            double daysInYear = Math.Max(1d, CalendarTimeMath.DaysInYear);
            double nativeAnnualProbability = 1d - Math.Pow(1d - nativeProbability, daysInYear);
            double targetAnnualProbability = nativeAnnualProbability * multiplier;
            return (float)(1d - Math.Pow(1d - targetAnnualProbability, 1d / daysInYear));
        }

        /// <summary>
        /// Battle survival is evaluated per casualty rather than per day, so
        /// scale only its direct death component and retain all native surgery,
        /// medicine, age, armor, and damage-type calculations.
        /// </summary>
        internal static float ScaleBattleSurvivalChance(float nativeSurvivalChance)
        {
            if (!IsFinite(nativeSurvivalChance))
            {
                return nativeSurvivalChance;
            }

            float nativeSurvival = ClampUnit(nativeSurvivalChance);
            float nativeDeathChance = 1f - nativeSurvival;
            return ClampUnit(
                1f - nativeDeathChance * CalendarSettingsState.LordDeathRateMultiplier);
        }

        private static float ClampUnit(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Decorates Bannerlord's public old-age probability model. It never calls
    /// private default-model code and delegates unchanged behavior for every
    /// hero outside the eligible noble-lord group.
    /// </summary>
    internal sealed class CalendarHeroDeathProbabilityModel : HeroDeathProbabilityCalculationModel
    {
        private readonly HeroDeathProbabilityCalculationModel _native;

        internal CalendarHeroDeathProbabilityModel(HeroDeathProbabilityCalculationModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override float CalculateHeroDeathProbability(Hero hero)
        {
            try
            {
                float nativeProbability = _native.CalculateHeroDeathProbability(hero);
                if (!CalendarLordDeathBalance.IsEligibleLord(hero))
                {
                    return nativeProbability;
                }

                float adjustedProbability = CalendarLordDeathBalance.ScaleDailyDeathProbability(nativeProbability);
                CalendarAnnualBalanceDiagnostics.RecordLordOldAgeDeath(
                    nativeProbability,
                    adjustedProbability);
                return adjustedProbability;
            }
            catch (Exception exception)
            {
                CalendarAnnualBalanceDiagnostics.RecordException(
                    "LordDeath.CalculateHeroDeathProbability",
                    exception);
                throw;
            }
        }
    }

    /// <summary>
    /// Decorates Bannerlord's public battle-survival model. Only noble lords'
    /// death component is reduced; all other PartyHealingModel results remain
    /// delegated directly to the model selected by the rest of the load order.
    /// </summary>
    internal sealed class CalendarLordBattleSurvivalModel : PartyHealingModel
    {
        private readonly PartyHealingModel _native;

        internal CalendarLordBattleSurvivalModel(PartyHealingModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override float GetSurgeryChance(PartyBase party)
        {
            return _native.GetSurgeryChance(party);
        }

        public override float GetSurvivalChance(
            PartyBase party,
            CharacterObject agentCharacter,
            DamageTypes damageType,
            bool canDamageKillEvenIfBlunt,
            PartyBase enemyParty)
        {
            try
            {
                float nativeSurvivalChance = _native.GetSurvivalChance(
                    party,
                    agentCharacter,
                    damageType,
                    canDamageKillEvenIfBlunt,
                    enemyParty);
                Hero hero = agentCharacter == null ? null : agentCharacter.HeroObject;
                if (!CalendarLordDeathBalance.IsEligibleLord(hero))
                {
                    return nativeSurvivalChance;
                }

                float adjustedSurvivalChance = CalendarLordDeathBalance.ScaleBattleSurvivalChance(
                    nativeSurvivalChance);
                CalendarAnnualBalanceDiagnostics.RecordLordBattleSurvival(
                    nativeSurvivalChance,
                    adjustedSurvivalChance);
                return adjustedSurvivalChance;
            }
            catch (Exception exception)
            {
                CalendarAnnualBalanceDiagnostics.RecordException(
                    "LordDeath.GetSurvivalChance",
                    exception);
                throw;
            }
        }

        public override int GetSkillXpFromHealingTroop(PartyBase party)
        {
            return _native.GetSkillXpFromHealingTroop(party);
        }

        public override ExplainedNumber GetDailyHealingForRegulars(
            PartyBase partyBase,
            bool isPrisoner,
            bool includeDescriptions = false)
        {
            return _native.GetDailyHealingForRegulars(partyBase, isPrisoner, includeDescriptions);
        }

        public override ExplainedNumber GetDailyHealingHpForHeroes(
            PartyBase partyBase,
            bool isPrisoners,
            bool includeDescriptions = false)
        {
            return _native.GetDailyHealingHpForHeroes(partyBase, isPrisoners, includeDescriptions);
        }

        public override int GetHeroesEffectedHealingAmount(Hero hero, float healingRate)
        {
            return _native.GetHeroesEffectedHealingAmount(hero, healingRate);
        }

        public override float GetSiegeBombardmentHitSurgeryChance(PartyBase party)
        {
            return _native.GetSiegeBombardmentHitSurgeryChance(party);
        }

        public override ExplainedNumber GetBattleEndHealingAmount(PartyBase partyBase, Hero hero)
        {
            return _native.GetBattleEndHealingAmount(partyBase, hero);
        }
    }
}
