using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Preserves Bannerlord's recruitment rules while reducing only the
    /// conformity accumulated per campaign hour. This applies to the player
    /// and AI through the shared public model contract.
    /// </summary>
    internal sealed class CalendarPrisonerRecruitmentModel : PrisonerRecruitmentCalculationModel
    {
        private readonly PrisonerRecruitmentCalculationModel _native;

        internal CalendarPrisonerRecruitmentModel(PrisonerRecruitmentCalculationModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override int GetConformityNeededToRecruitPrisoner(CharacterObject character)
        {
            return _native.GetConformityNeededToRecruitPrisoner(character);
        }

        public override ExplainedNumber GetConformityChangePerHour(PartyBase party, CharacterObject character)
        {
            try
            {
                ExplainedNumber result = _native.GetConformityChangePerHour(party, character);
                float nativeValue = result.ResultNumber;
                if (CalendarSettingsState.BalancePrisonerRecruitment)
                {
                    DailyRateBalance.Scale(ref result);
                }
                CalendarAnnualBalanceDiagnostics.RecordPrisonerConformity(nativeValue, result.ResultNumber);
                return result;
            }
            catch (Exception exception)
            {
                CalendarAnnualBalanceDiagnostics.RecordException("PrisonerRecruitment.GetConformityChangePerHour", exception);
                throw;
            }
        }

        public override int GetPrisonerRecruitmentMoraleEffect(PartyBase party, CharacterObject character, int num)
        {
            return _native.GetPrisonerRecruitmentMoraleEffect(party, character, num);
        }

        public override bool IsPrisonerRecruitable(PartyBase party, CharacterObject character, out int conformityNeeded)
        {
            return _native.IsPrisonerRecruitable(party, character, out conformityNeeded);
        }

        public override bool ShouldPartyRecruitPrisoners(PartyBase party)
        {
            return _native.ShouldPartyRecruitPrisoners(party);
        }

        public override int CalculateRecruitableNumber(PartyBase party, CharacterObject character)
        {
            return _native.CalculateRecruitableNumber(party, character);
        }
    }
}
