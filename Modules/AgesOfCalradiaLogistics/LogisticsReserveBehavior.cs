using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace AgesOfCalradiaLogistics
{
    /// <summary>
    /// Persistent, campaign-side reserve store. Battle behaviours will spend from
    /// this store instead of creating unlimited ammunition.
    /// </summary>
    public sealed class LogisticsReserveBehavior : CampaignBehaviorBase
    {
        public const int MaximumReserve = 100;
        public const int ReservePerSupplyCrate = 20;

        private const int StartingReserve = 60;
        private const string SupplyItemId = "aoc_logistics_supply";
        private Dictionary<string, int> _reservesByPartyId = new Dictionary<string, int>();
        public static LogisticsReserveBehavior Active { get; private set; }

        public LogisticsReserveBehavior()
        {
            Active = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, EnsureEligiblePartyHasReserve);
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(this, RemovePartyReserve);
            CampaignEvents.DailyTickTownEvent.AddNonSerializedListener(this, RestockTownMarket);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddTownMenuOptions);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("aoc_logistics_reserves", ref _reservesByPartyId);
            if (_reservesByPartyId == null)
            {
                _reservesByPartyId = new Dictionary<string, int>();
            }
        }

        public int GetReserve(MobileParty party)
        {
            if (!IsEligible(party))
            {
                return 0;
            }

            EnsureEligiblePartyHasReserve(party);
            return _reservesByPartyId[party.StringId];
        }

        /// <summary>
        /// Converts Supply crates in the party inventory into reserve capacity.
        /// The caller supplies the intended number of crates, enabling a later
        /// menu or quartermaster interaction to ask for player confirmation.
        /// </summary>
        public int LoadSupplyCrates(MobileParty party, int requestedCrates)
        {
            if (!IsEligible(party) || requestedCrates <= 0)
            {
                return 0;
            }

            ItemObject supply = MBObjectManager.Instance.GetObject<ItemObject>(SupplyItemId);
            if (supply == null)
            {
                return 0;
            }

            EnsureEligiblePartyHasReserve(party);
            int capacity = MaximumReserve - _reservesByPartyId[party.StringId];
            int cratesToUse = System.Math.Min(requestedCrates, party.ItemRoster.GetItemNumber(supply));
            cratesToUse = System.Math.Min(cratesToUse, capacity / ReservePerSupplyCrate);
            if (cratesToUse <= 0)
            {
                return 0;
            }

            party.ItemRoster.AddToCounts(supply, -cratesToUse);
            _reservesByPartyId[party.StringId] += cratesToUse * ReservePerSupplyCrate;
            LogisticsDiagnostics.Info(string.Format("Loaded {0} Supply crate(s) into {1}; reserve is now {2}/{3}.", cratesToUse, party.StringId, _reservesByPartyId[party.StringId], MaximumReserve));
            return cratesToUse;
        }

        public int LoadAllAvailableSupplyCrates(MobileParty party)
        {
            ItemObject supply = MBObjectManager.Instance.GetObject<ItemObject>(SupplyItemId);
            return supply == null ? 0 : LoadSupplyCrates(party, party.ItemRoster.GetItemNumber(supply));
        }

        public bool TryConsumeReserve(MobileParty party, int amount)
        {
            if (!IsEligible(party) || amount <= 0 || GetReserve(party) < amount)
            {
                return false;
            }

            _reservesByPartyId[party.StringId] -= amount;
            return true;
        }

        public static bool IsEligible(MobileParty party)
        {
            return party != null && party.IsActive && !party.IsBandit &&
                (party == MobileParty.MainParty || party.IsLordParty || party.IsCaravan);
        }

        private void EnsureEligiblePartyHasReserve(MobileParty party)
        {
            if (IsEligible(party) && !_reservesByPartyId.ContainsKey(party.StringId))
            {
                _reservesByPartyId.Add(party.StringId, StartingReserve);
                LogisticsDiagnostics.Info(string.Format("Created reserve for {0}: {1}/{2}.", party.StringId, StartingReserve, MaximumReserve));
            }
        }

        private void RemovePartyReserve(PartyBase party)
        {
            if (party != null && party.MobileParty != null)
            {
                _reservesByPartyId.Remove(party.MobileParty.StringId);
            }
        }

        private void RestockTownMarket(Town town)
        {
            if (town == null || !town.IsTown || town.Owner == null)
            {
                return;
            }

            ItemObject supply = MBObjectManager.Instance.GetObject<ItemObject>(SupplyItemId);
            if (supply == null)
            {
                return;
            }

            int desiredStock = town.Prosperity >= 5000f ? 5 : 3;
            int currentStock = town.Owner.ItemRoster.GetItemNumber(supply);
            if (currentStock < desiredStock)
            {
                town.Owner.ItemRoster.AddToCounts(supply, 1);
                LogisticsDiagnostics.Info(string.Format("Restocked Supply in {0}: {1}/{2}.", town.Name, currentStock + 1, desiredStock));
            }
        }

        private void AddTownMenuOptions(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption(
                "town",
                "aoc_logistics_load_baggage",
                "Load supplies into baggage train",
                CanLoadMainPartyBaggage,
                LoadMainPartyBaggage,
                isLeave: false,
                index: -1);
        }

        private bool CanLoadMainPartyBaggage(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            ItemObject supply = MBObjectManager.Instance.GetObject<ItemObject>(SupplyItemId);
            int reserve = GetReserve(MobileParty.MainParty);
            int crateCount = supply == null ? 0 : MobileParty.MainParty.ItemRoster.GetItemNumber(supply);
            int capacity = MaximumReserve - reserve;

            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            args.IsEnabled = settlement != null && settlement.IsTown && crateCount > 0 && capacity >= ReservePerSupplyCrate;
            args.Text = new TextObject(
                "Load supplies into baggage train ({RESERVE}/{MAXIMUM} reserve; {CRATES} crate(s) carried).");
            args.Text.SetTextVariable("RESERVE", reserve);
            args.Text.SetTextVariable("MAXIMUM", MaximumReserve);
            args.Text.SetTextVariable("CRATES", crateCount);
            if (!args.IsEnabled)
            {
                args.Tooltip = new TextObject(
                    crateCount == 0
                        ? "Buy or capture Supply crates before loading the baggage train."
                        : "The baggage train does not have enough free capacity for another crate.");
            }

            return settlement != null && settlement.IsTown;
        }

        private void LoadMainPartyBaggage(MenuCallbackArgs args)
        {
            LoadAllAvailableSupplyCrates(MobileParty.MainParty);
        }
    }
}
