using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Scales only the lifetime of map tracks. Detection, visibility, and
    /// leave-track probability remain native tactical calculations.
    /// </summary>
    internal sealed class CalendarMapTrackModel : MapTrackModel
    {
        private readonly MapTrackModel _native;

        internal CalendarMapTrackModel(MapTrackModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override float MaxTrackLife
        {
            get
            {
                float nativeValue = _native.MaxTrackLife;
                return CalendarSettingsState.BalanceMapTracks
                    ? CalendarAnnualBalance.ScaleDuration(nativeValue)
                    : nativeValue;
            }
        }

        public override float GetSkipTrackChance(MobileParty mobileParty)
        {
            return _native.GetSkipTrackChance(mobileParty);
        }

        public override float GetMaxTrackSpottingDistanceForMainParty()
        {
            return _native.GetMaxTrackSpottingDistanceForMainParty();
        }

        public override bool CanPartyLeaveTrack(MobileParty mobileParty)
        {
            return _native.CanPartyLeaveTrack(mobileParty);
        }

        public override float GetTrackDetectionDifficultyForMainParty(Track track, float trackSpottingDistance)
        {
            return _native.GetTrackDetectionDifficultyForMainParty(track, trackSpottingDistance);
        }

        public override float GetSkillFromTrackDetected(Track track)
        {
            return _native.GetSkillFromTrackDetected(track);
        }

        public override int GetTrackLife(MobileParty mobileParty)
        {
            int nativeValue = _native.GetTrackLife(mobileParty);
            int annualValue = CalendarSettingsState.BalanceMapTracks
                ? Math.Max(1, (int)Math.Round(nativeValue * CalendarAnnualBalance.DurationFactor, MidpointRounding.AwayFromZero))
                : nativeValue;
            CalendarAnnualBalanceDiagnostics.RecordMapTrackLife(nativeValue, annualValue);
            return annualValue;
        }

        public override TextObject TrackTitle(Track track)
        {
            return _native.TrackTitle(track);
        }

        public override IEnumerable<ValueTuple<TextObject, string>> GetTrackDescription(Track track)
        {
            return _native.GetTrackDescription(track);
        }

        public override uint GetTrackColor(Track track)
        {
            return _native.GetTrackColor(track);
        }

        public override float GetTrackScale(Track track)
        {
            return _native.GetTrackScale(track);
        }
    }
}
