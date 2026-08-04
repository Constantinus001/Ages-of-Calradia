using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Tournament creation and resolution chances are sampled from a daily
    /// settlement tick. Convert only those probabilities so a Gregorian year
    /// has the same expected tournament cadence as Bannerlord's 84-day year.
    /// </summary>
    internal sealed class CalendarTournamentModel : TournamentModel
    {
        private readonly TournamentModel _native;

        internal CalendarTournamentModel(TournamentModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override float GetTournamentStartChance(Town town)
        {
            return DailyRateBalance.ScaleDailyProbability(_native.GetTournamentStartChance(town));
        }

        public override float GetTournamentEndChance(TournamentGame tournament)
        {
            return DailyRateBalance.ScaleDailyProbability(_native.GetTournamentEndChance(tournament));
        }

        public override TournamentGame CreateTournament(Town town) => _native.CreateTournament(town);
        public override MBList<ItemObject> GetEliteRewardItems(Town town, int regularRewardMinValue, int regularRewardMaxValue) => _native.GetEliteRewardItems(town, regularRewardMinValue, regularRewardMaxValue);
        public override int GetInfluenceReward(Hero winner, Town town) => _native.GetInfluenceReward(winner, town);
        public override int GetNumLeaderboardVictoriesAtGameStart() => _native.GetNumLeaderboardVictoriesAtGameStart();
        public override Equipment GetParticipantArmor(CharacterObject participant) => _native.GetParticipantArmor(participant);
        public override MBList<ItemObject> GetRegularRewardItems(Town town, int regularRewardMinValue, int regularRewardMaxValue) => _native.GetRegularRewardItems(town, regularRewardMinValue, regularRewardMaxValue);
        public override int GetRenownReward(Hero winner, Town town) => _native.GetRenownReward(winner, town);
        public override ValueTuple<SkillObject, int> GetSkillXpGainFromTournament(Town town) => _native.GetSkillXpGainFromTournament(town);
        public override float GetTournamentSimulationScore(CharacterObject character) => _native.GetTournamentSimulationScore(character);
    }
}
