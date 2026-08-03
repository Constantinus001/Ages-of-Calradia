using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Patrol queues are updated daily. Expand their native spawn duration so
    /// settlements do not create patrols 4.35 times as often in a calendar year.
    /// </summary>
    internal sealed class CalendarSettlementPatrolModel : SettlementPatrolModel
    {
        private readonly SettlementPatrolModel _native;

        internal CalendarSettlementPatrolModel(SettlementPatrolModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override CampaignTime GetPatrolPartySpawnDuration(Settlement settlement, bool naval)
        {
            CampaignTime nativeDuration = _native.GetPatrolPartySpawnDuration(settlement, naval);
            if (!CalendarSettingsState.ExtendedCalendarEnabled || nativeDuration == CampaignTime.Never)
            {
                return nativeDuration;
            }

            return CampaignTime.Days((float)(nativeDuration.ToDays * CalendarAnnualBalance.DurationFactor));
        }

        public override bool CanSettlementHavePatrolParties(Settlement settlement, bool naval) => _native.CanSettlementHavePatrolParties(settlement, naval);
        public override PartyTemplateObject GetPartyTemplateForPatrolParty(Settlement settlement, bool naval) => _native.GetPartyTemplateForPatrolParty(settlement, naval);
    }
}
