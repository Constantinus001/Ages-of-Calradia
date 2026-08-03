using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Keeps native eligibility rules intact and expresses impairment durations
    /// as the same fraction of a Gregorian campaign year as native uses of its
    /// 84-day year. No DefaultPartyImpairmentModel members are referenced.
    /// </summary>
    internal sealed class CalendarPartyImpairmentModel : PartyImpairmentModel
    {
        private readonly PartyImpairmentModel _native;

        internal CalendarPartyImpairmentModel(PartyImpairmentModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override ExplainedNumber GetDisorganizedStateDuration(MobileParty party)
        {
            try
            {
                ExplainedNumber result = _native.GetDisorganizedStateDuration(party);
                float nativeValue = result.ResultNumber;
                if (CalendarSettingsState.BalancePartyImpairment)
                {
                    CalendarAnnualBalance.ScaleDuration(ref result);
                }
                CalendarAnnualBalanceDiagnostics.RecordImpairment(nativeValue, result.ResultNumber);
                return result;
            }
            catch (Exception exception)
            {
                CalendarAnnualBalanceDiagnostics.RecordException("PartyImpairment.GetDisorganizedStateDuration", exception);
                throw;
            }
        }

        public override float GetVulnerabilityStateDuration(PartyBase party)
        {
            try
            {
                float nativeValue = _native.GetVulnerabilityStateDuration(party);
                float annualValue = CalendarSettingsState.BalancePartyImpairment
                    ? CalendarAnnualBalance.ScaleDuration(nativeValue)
                    : nativeValue;
                CalendarAnnualBalanceDiagnostics.RecordImpairment(nativeValue, annualValue);
                return annualValue;
            }
            catch (Exception exception)
            {
                CalendarAnnualBalanceDiagnostics.RecordException("PartyImpairment.GetVulnerabilityStateDuration", exception);
                throw;
            }
        }

        public override float GetSiegeExpectedVulnerabilityTime()
        {
            try
            {
                float nativeValue = _native.GetSiegeExpectedVulnerabilityTime();
                float annualValue = CalendarSettingsState.BalancePartyImpairment
                    ? CalendarAnnualBalance.ScaleDuration(nativeValue)
                    : nativeValue;
                CalendarAnnualBalanceDiagnostics.RecordImpairment(nativeValue, annualValue);
                return annualValue;
            }
            catch (Exception exception)
            {
                CalendarAnnualBalanceDiagnostics.RecordException("PartyImpairment.GetSiegeExpectedVulnerabilityTime", exception);
                throw;
            }
        }

        public override bool CanGetDisorganized(PartyBase partyBase)
        {
            return _native.CanGetDisorganized(partyBase);
        }
    }
}
