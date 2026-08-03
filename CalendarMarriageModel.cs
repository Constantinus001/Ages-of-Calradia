using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Delegates every marriage decision to Bannerlord and converts only the
    /// native NPC per-day marriage probability to its annual equivalent for a
    /// 365-day calendar. Player marriage rules are not changed.
    /// </summary>
    internal sealed class CalendarMarriageModel : MarriageModel
    {
        private readonly MarriageModel _native;

        internal CalendarMarriageModel(MarriageModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override int MinimumMarriageAgeMale
        {
            get { return _native.MinimumMarriageAgeMale; }
        }

        public override int MinimumMarriageAgeFemale
        {
            get { return _native.MinimumMarriageAgeFemale; }
        }

        public override bool IsCoupleSuitableForMarriage(Hero firstHero, Hero secondHero)
        {
            return _native.IsCoupleSuitableForMarriage(firstHero, secondHero);
        }

        public override int GetEffectiveRelationIncrease(Hero firstHero, Hero secondHero)
        {
            return _native.GetEffectiveRelationIncrease(firstHero, secondHero);
        }

        public override Clan GetClanAfterMarriage(Hero firstHero, Hero secondHero)
        {
            return _native.GetClanAfterMarriage(firstHero, secondHero);
        }

        public override bool IsSuitableForMarriage(Hero hero)
        {
            return _native.IsSuitableForMarriage(hero);
        }

        public override bool IsClanSuitableForMarriage(Clan clan)
        {
            return _native.IsClanSuitableForMarriage(clan);
        }

        public override float NpcCoupleMarriageChance(Hero firstHero, Hero secondHero)
        {
            try
            {
                float nativeValue = _native.NpcCoupleMarriageChance(firstHero, secondHero);
                float annualValue = CalendarSettingsState.BalanceNpcMarriage
                    ? DailyRateBalance.ScaleDailyProbability(nativeValue)
                    : nativeValue;
                CalendarAnnualBalanceDiagnostics.RecordMarriageChance(nativeValue, annualValue);
                return annualValue;
            }
            catch (Exception exception)
            {
                CalendarAnnualBalanceDiagnostics.RecordException("Marriage.NpcCoupleMarriageChance", exception);
                throw;
            }
        }

        public override bool ShouldNpcMarriageBetweenClansBeAllowed(Clan consideringClan, Clan targetClan)
        {
            return _native.ShouldNpcMarriageBetweenClansBeAllowed(consideringClan, targetClan);
        }

        public override List<Hero> GetAdultChildrenSuitableForMarriage(Hero hero)
        {
            return _native.GetAdultChildrenSuitableForMarriage(hero);
        }
    }
}
