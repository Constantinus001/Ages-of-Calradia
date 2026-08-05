using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Uses Bannerlord's native movement calculation with a common base speed
    /// of four. Native troop count, prisoner, herd, terrain, skill, army, and
    /// encumbrance modifiers continue to determine final party movement.
    /// </summary>
    internal sealed class CalendarPartySpeedModel : PartySpeedModel
    {
        private const float CalendarBaseSpeed = 4f;
        private static readonly TextObject CalendarBaseSpeedText = new TextObject(
            "{=TMCCalendarBaseSpeed}Calendar base speed");
        private readonly PartySpeedModel _native;

        internal CalendarPartySpeedModel(PartySpeedModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override float BaseSpeed
        {
            get { return CalendarBaseSpeed; }
        }

        public override float MinimumSpeed
        {
            get { return _native.MinimumSpeed; }
        }

        public override ExplainedNumber CalculateBaseSpeed(
            MobileParty party,
            bool includeDescriptions = false,
            int additionalTroopOnFootCount = 0,
            int additionalTroopOnHorseCount = 0)
        {
            ExplainedNumber result = _native.CalculateBaseSpeed(
                party,
                includeDescriptions,
                additionalTroopOnFootCount,
                additionalTroopOnHorseCount);

            if (CalendarSettingsState.AnnualRateBalanceEnabled)
            {
                result.Add(CalendarBaseSpeed - _native.BaseSpeed, CalendarBaseSpeedText);
            }

            return result;
        }

        public override ExplainedNumber CalculateFinalSpeed(MobileParty party, ExplainedNumber finalSpeed)
        {
            return _native.CalculateFinalSpeed(party, finalSpeed);
        }
    }
}
