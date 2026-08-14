using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using Helpers;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    internal sealed partial class CalendarWorldLedgerVM
    {
        private readonly MBBindingList<CalendarStrategicFriendlyArmyVM> _strategicFriendlyArmies =
            new MBBindingList<CalendarStrategicFriendlyArmyVM>();

        [DataSourceProperty]
        public MBBindingList<CalendarStrategicFriendlyArmyVM> StrategicFriendlyArmies
        {
            get { return _strategicFriendlyArmies; }
        }

        internal void RefreshStrategicFriendlyArmies()
        {
            _strategicFriendlyArmies.Clear();
            IFaction playerFaction = Clan.PlayerClan == null
                ? null
                : Clan.PlayerClan.MapFaction ?? Clan.PlayerClan;
            if (playerFaction == null) return;

            List<MobileParty> visibleParties = new List<MobileParty>();
            foreach (MobileParty party in MobileParty.All)
            {
                if (party == null || !party.IsActive)
                {
                    continue;
                }
                bool isPlayerParty = ReferenceEquals(party, MobileParty.MainParty);
                bool isClanParty = Clan.PlayerClan != null && ReferenceEquals(party.ActualClan, Clan.PlayerClan);
                bool isFriendlyArmyLeader = party.Army != null
                    && ReferenceEquals(party.Army.LeaderParty, party)
                    && IsFriendlyArmyFaction(playerFaction, party.MapFaction);
                if (isPlayerParty || isClanParty || isFriendlyArmyLeader) visibleParties.Add(party);
            }
            visibleParties.Sort(delegate(MobileParty left, MobileParty right)
            {
                string leftId = left == null ? string.Empty : left.StringId;
                string rightId = right == null ? string.Empty : right.StringId;
                return string.CompareOrdinal(leftId, rightId);
            });

            foreach (MobileParty party in visibleParties)
            {
                CampaignVec2 position = party.Position;
                Vec2 source = ProjectCampaignPositionToStrategicMap(new Vec2(position.X, position.Y));
                bool isPlayerParty = ReferenceEquals(party, MobileParty.MainParty);
                bool isClanParty = Clan.PlayerClan != null && ReferenceEquals(party.ActualClan, Clan.PlayerClan);
                string partyName = isPlayerParty
                    ? "Your Party"
                    : party.Name.ToString();
                string glyph = isPlayerParty ? "P" : isClanParty ? "C" : "A";
                string markerColor = isPlayerParty ? "#72B7FFFF" : isClanParty ? "#7DE8B0FF" : "#65E89AFF";
                _strategicFriendlyArmies.Add(new CalendarStrategicFriendlyArmyVM(
                    (int)Math.Round((source.x * StrategicMapScale) - 10f),
                    (int)Math.Round((source.y * StrategicMapScale) - 15f),
                    partyName,
                    glyph,
                    markerColor));
            }
        }

        private static bool IsFriendlyArmyFaction(IFaction playerFaction, IFaction candidateFaction)
        {
            return candidateFaction != null
                && (ReferenceEquals(playerFaction, candidateFaction)
                    || DiplomacyHelper.HasAllianceWithFaction(playerFaction, candidateFaction));
        }
    }

    internal sealed class CalendarStrategicFriendlyArmyVM : ViewModel
    {
        internal CalendarStrategicFriendlyArmyVM(int x, int y, string name, string glyph, string markerColor)
        { X = x; Y = y; Name = name ?? "Friendly Party"; Glyph = glyph ?? "A"; MarkerColor = markerColor ?? "#65E89AFF"; }
        [DataSourceProperty] public int X { get; private set; }
        [DataSourceProperty] public int Y { get; private set; }
        [DataSourceProperty] public string Name { get; private set; }
        [DataSourceProperty] public string Glyph { get; private set; }
        [DataSourceProperty] public string MarkerColor { get; private set; }
        [DataSourceProperty] public string GoldColor { get { return "#FFD66FFF"; } }
        [DataSourceProperty] public string ShadowColor { get { return "#100C08D8"; } }
    }
}
