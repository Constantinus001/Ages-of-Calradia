using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TwelveMonthCalendar;

namespace AgesOfCalradiaReligions
{
    public sealed class PopulationCampaignBehavior : CampaignBehaviorBase
    {
        private const string PopulationStateKey = "AgesOfCalradiaReligions.PopulationStateV1";
        private const string PopulationStateChunksKey = "AgesOfCalradiaReligions.PopulationStateV2Chunks";
        private const string LastMonthKey = "AgesOfCalradiaReligions.LastPopulationMonthV1";
        private const string PolicyMenuId = "aoc_population_policies";
        private const string DebugMenuId = "aoc_population_debug";
        private static readonly HashSet<string> MajorUrbanRegions = new HashSet<string>(StringComparer.Ordinal)
        {
            "town_A1", "town_A2", "town_A4", "town_A6", "town_A8",
            "town_EN1", "town_EN2", "town_EN6", "town_ES1", "town_ES4",
            "town_ES5", "town_EW1", "town_EW2", "town_EW3", "town_EW4",
            "town_V1", "town_V3", "town_V5", "town_V6", "town_V7"
        };

        private Dictionary<string, ProvincePopulationState> _states = new Dictionary<string, ProvincePopulationState>(StringComparer.Ordinal);
        private string _serializedState = string.Empty;
        private List<string> _serializedStateChunks = new List<string>();
        private int _lastProcessedMonth = -1;
        private int _mapRevision;

        internal int MapRevision { get { return _mapRevision; } }

        public PopulationCampaignBehavior()
        {
            PopulationService.ActiveBehavior = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, OnTroopRecruited);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                _serializedState = PopulationPersistence.Serialize(_states.Values.OrderBy(state => state.SettlementId, StringComparer.Ordinal));
                _serializedStateChunks = ChunkedSavePayload.Split(_serializedState);
            }
            dataStore.SyncData(PopulationStateChunksKey, ref _serializedStateChunks);
            if (dataStore.IsLoading)
            {
                if (ChunkedSavePayload.HasPayload(_serializedStateChunks))
                    _serializedState = ChunkedSavePayload.Join(_serializedStateChunks);
                else
                    dataStore.SyncData(PopulationStateKey, ref _serializedState);
            }
            dataStore.SyncData(LastMonthKey, ref _lastProcessedMonth);
            if (dataStore.IsLoading && !string.IsNullOrWhiteSpace(_serializedState))
            {
                Dictionary<string, ProvincePopulationState> loaded;
                if (PopulationPersistence.TryDeserialize(_serializedState, out loaded)) _states = loaded;
                else ReligionDiagnostics.Info("Population save payload was absent or invalid; a baseline will be created when the campaign session launches.");
            }
        }

        internal bool TryGetState(string settlementId, out ProvincePopulationState state)
        {
            return _states.TryGetValue(settlementId ?? string.Empty, out state);
        }

        internal bool TryGetStateForSettlement(Settlement settlement, out ProvincePopulationState state)
        {
            state = null;
            if (settlement == null) return false;
            if (settlement.IsVillage && settlement.Village != null && settlement.Village.Bound != null) settlement = settlement.Village.Bound;
            return TryGetState(settlement.StringId, out state);
        }

        internal long GetTotalPopulation()
        {
            return _states.Values.Sum(state => state.TotalPopulation);
        }

        internal string GetStrategicMapSnapshotPayload()
        {
            EnsureInitializedForStrategicMap();
            StringBuilder builder = new StringBuilder("AOCMAP1");
            foreach (ProvincePopulationState state in _states.Values.OrderBy(value => value.SettlementId, StringComparer.Ordinal))
            {
                Settlement settlement = FindSettlement(state.SettlementId);
                string cultureId = settlement == null || settlement.Culture == null ? string.Empty : settlement.Culture.StringId;
                builder.Append('\n').Append(Uri.EscapeDataString(state.SettlementId))
                    .Append('|').Append(state.TotalPopulation.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(Uri.EscapeDataString(cultureId))
                    .Append('|').Append(Uri.EscapeDataString(GetDominantFaithId(state)))
                    .Append('|').Append(state.Happiness.ToString("F3", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        internal string GetCensusSnapshotPayload()
        {
            EnsureInitializedForStrategicMap();
            StringBuilder builder = new StringBuilder("AOCCENSUS1|");
            builder.Append(string.Join(",", ReligionCatalog.FaithIds));
            foreach (ProvincePopulationState state in _states.Values.OrderBy(value => value.SettlementId, StringComparer.Ordinal))
            {
                Settlement settlement = FindSettlement(state.SettlementId);
                string cultureId = settlement == null || settlement.Culture == null ? string.Empty : settlement.Culture.StringId;
                Clan ownerClan = settlement == null ? null : settlement.OwnerClan;
                Kingdom kingdom = ownerClan == null ? null : ownerClan.Kingdom;
                builder.Append('\n').Append(Uri.EscapeDataString(state.SettlementId))
                    .Append('|').Append(state.TotalPopulation.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(Uri.EscapeDataString(cultureId))
                    .Append('|').Append(Uri.EscapeDataString(kingdom == null ? string.Empty : kingdom.StringId))
                    .Append('|').Append(Uri.EscapeDataString(kingdom == null || kingdom.Name == null ? string.Empty : kingdom.Name.ToString()))
                    .Append('|').Append(Uri.EscapeDataString(ownerClan == null ? string.Empty : ownerClan.StringId))
                    .Append('|').Append(Uri.EscapeDataString(ownerClan == null || ownerClan.Name == null ? string.Empty : ownerClan.Name.ToString()))
                    .Append('|').Append(state.Happiness.ToString("F3", CultureInfo.InvariantCulture))
                    .Append('|');
                for (int faithIndex = 0; faithIndex < state.FaithPopulations.Length; faithIndex++)
                {
                    if (faithIndex > 0) builder.Append(',');
                    builder.Append(state.FaithPopulations[faithIndex].ToString(CultureInfo.InvariantCulture));
                }
            }
            return builder.ToString();
        }

        private void EnsureInitializedForStrategicMap()
        {
            // Old saves may not contain this module's population payload yet.
            // The map is a safe late initialization point because the campaign
            // settlement collection is fully available when World Events opens.
            if (_states.Count == 0 && Campaign.Current != null)
            {
                InitializeBaseline();
            }
        }

        internal bool SetTaxPolicy(string settlementId, TaxPolicy policy)
        {
            ProvincePopulationState state = null;
            if (!Enum.IsDefined(typeof(TaxPolicy), policy) || !TryGetState(settlementId, out state)) return false;
            state.TaxPolicy = policy;
            return true;
        }

        internal bool SetConscriptionPolicy(string settlementId, ConscriptionPolicy policy)
        {
            ProvincePopulationState state;
            if (!Enum.IsDefined(typeof(ConscriptionPolicy), policy) || !TryGetState(settlementId, out state)) return false;
            long oldCeiling = PopulationMath.GetMobilizationCeiling(state);
            state.ConscriptionPolicy = policy;
            long newCeiling = PopulationMath.GetMobilizationCeiling(state);
            state.AvailableManpower = newCeiling > oldCeiling
                ? Math.Min(newCeiling, state.AvailableManpower + newCeiling - oldCeiling)
                : Math.Min(state.AvailableManpower, newCeiling);
            return true;
        }

        internal float GetArmySupportFactor(IFaction faction)
        {
            Kingdom kingdom = faction as Kingdom;
            Clan clan = faction as Clan;
            if (kingdom == null && clan != null) kingdom = clan.Kingdom;
            if (kingdom == null) return 1f;

            long population = 0L;
            long available = 0L;
            long ceiling = 0L;
            foreach (ProvincePopulationState state in _states.Values)
            {
                Settlement settlement = FindSettlement(state.SettlementId);
                if (settlement == null || settlement.MapFaction != kingdom) continue;
                population += state.TotalPopulation;
                available += (long)Math.Floor(state.AvailableManpower * PopulationMath.FieldArmyMobilizationShare);
                ceiling += (long)Math.Floor(PopulationMath.GetMobilizationCeiling(state) * PopulationMath.FieldArmyMobilizationShare);
            }

            int activeLordParties = MobileParty.AllLordParties.Count(party => party != null && party.IsActive && party.MapFaction == kingdom);
            return PopulationMath.GetArmySupportFactor(population, Math.Min(available, ceiling), ceiling, activeLordParties);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            if (_states.Count == 0) InitializeBaseline();
            AddPolicyMenus(starter);
        }

        private void OnDailyTick()
        {
            if (_states.Count == 0) return;
            int currentMonth = GetCalendarMonthKey(CampaignTime.Now);
            if (_lastProcessedMonth < 0)
            {
                _lastProcessedMonth = currentMonth;
                return;
            }

            if (currentMonth == _lastProcessedMonth) return;
            _lastProcessedMonth = currentMonth;
            AdvanceMonth();
        }

        private void OnTroopRecruited(Hero recruiter, Settlement settlement, Hero recruitmentSource, CharacterObject troop, int amount)
        {
            ProvincePopulationState state;
            if (amount <= 0 || !TryGetStateForSettlement(settlement, out state)) return;
            long demographicCost = amount;
            state.AvailableManpower = Math.Max(0L, state.AvailableManpower - demographicCost);
        }

        private void AdvanceMonth()
        {
            UpdateAiPolicies();
            foreach (ProvincePopulationState state in _states.Values)
            {
                Settlement settlement = FindSettlement(state.SettlementId);
                bool foodCrisis = settlement != null && (settlement.IsStarving || (settlement.Town != null && settlement.Town.FoodStocks <= 0f));
                bool warDamage = settlement != null && (settlement.IsUnderSiege || settlement.IsUnderRaid || settlement.IsRaided);
                PopulationMonthResult result = PopulationMath.AdvanceMonth(state, foodCrisis, warDamage);
                state.LastMonthlyBirths = result.Births;
                state.LastMonthlyDeaths = result.Deaths;
                state.LastMonthlyMigrationNet = 0L;
                if (settlement != null && settlement.IsTown) RefreshTownRecruitReserve(state);
            }

            ApplyMigration();
            ReligionService.ProcessMonthly(_states.Values);
            _mapRevision++;
            StrategicMapModeIntegration.RefreshForMonthlyUpdate();
            ReligionDiagnostics.Info(string.Format(CultureInfo.InvariantCulture, "Monthly population update completed. Provinces={0}; Population={1}.", _states.Count, GetTotalPopulation()));
        }

        private void UpdateAiPolicies()
        {
            foreach (ProvincePopulationState state in _states.Values)
            {
                Settlement settlement = FindSettlement(state.SettlementId);
                Clan owner = settlement == null ? null : settlement.OwnerClan;
                if (owner == null || owner == Clan.PlayerClan || (owner.Kingdom != null && owner.Kingdom.RulingClan == Clan.PlayerClan)) continue;
                Kingdom kingdom = owner.Kingdom;
                bool atWar = kingdom != null && Kingdom.All.Any(other => other != null && other != kingdom && FactionManager.IsAtWarAgainstFaction(kingdom, other));
                ConscriptionPolicy desired = settlement.IsUnderSiege
                    ? ConscriptionPolicy.HeavyLevy
                    : atWar ? ConscriptionPolicy.WartimeLevy : ConscriptionPolicy.LimitedLevy;
                SetConscriptionPolicy(state.SettlementId, desired);
                state.TaxPolicy = state.Happiness < 35f ? TaxPolicy.Relief : TaxPolicy.Standard;
            }
        }

        private void ApplyMigration()
        {
            List<ProvincePopulationState> sources = _states.Values.Where(state => state.Happiness < 30f && state.TotalPopulation > 1000L).ToList();
            foreach (ProvincePopulationState source in sources)
            {
                Settlement sourceSettlement = FindSettlement(source.SettlementId);
                if (sourceSettlement == null || sourceSettlement.MapFaction == null) continue;
                ProvincePopulationState destination = _states.Values
                    .Where(candidate => candidate != source && candidate.Happiness >= source.Happiness + 15f)
                    .Where(candidate =>
                    {
                        Settlement settlement = FindSettlement(candidate.SettlementId);
                        return settlement != null && settlement.MapFaction == sourceSettlement.MapFaction;
                    })
                    .OrderByDescending(candidate => candidate.Happiness)
                    .ThenBy(candidate => candidate.SettlementId, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (destination == null) continue;
                TransferPopulation(source, destination, Math.Max(1L, source.TotalPopulation / 1000L));
            }
        }

        private static void TransferPopulation(ProvincePopulationState source, ProvincePopulationState destination, long amount)
        {
            amount = Math.Min(amount, source.TotalPopulation);
            if (amount <= 0) return;
            long sourceTotal = source.TotalPopulation;
            long urban = amount * source.UrbanPopulation / sourceTotal;
            long pastoral = amount * source.PastoralPopulation / sourceTotal;
            long institutional = amount * source.InstitutionalPopulation / sourceTotal;
            long rural = amount - urban - pastoral - institutional;
            source.UrbanPopulation -= urban; destination.UrbanPopulation += urban;
            source.RuralPopulation -= rural; destination.RuralPopulation += rural;
            source.PastoralPopulation -= pastoral; destination.PastoralPopulation += pastoral;
            source.InstitutionalPopulation -= institutional; destination.InstitutionalPopulation += institutional;
            source.LastMonthlyMigrationNet -= amount;
            destination.LastMonthlyMigrationNet += amount;
            long remaining = amount;
            for (int index = 0; index < source.FaithPopulations.Length; index++)
            {
                long moved = index == source.FaithPopulations.Length - 1
                    ? Math.Min(remaining, source.FaithPopulations[index])
                    : Math.Min(source.FaithPopulations[index], amount * source.FaithPopulations[index] / sourceTotal);
                source.FaithPopulations[index] -= moved;
                destination.FaithPopulations[index] += moved;
                remaining -= moved;
            }

            if (remaining > 0)
            {
                int dominant = 0;
                for (int index = 1; index < source.FaithPopulations.Length; index++)
                {
                    if (source.FaithPopulations[index] > source.FaithPopulations[dominant]) dominant = index;
                }

                long moved = Math.Min(remaining, source.FaithPopulations[dominant]);
                source.FaithPopulations[dominant] -= moved;
                destination.FaithPopulations[dominant] += moved;
            }
        }

        private void InitializeBaseline()
        {
            List<Settlement> regions = Settlement.All.Where(settlement => settlement != null && settlement.IsFortification).OrderBy(settlement => settlement.StringId, StringComparer.Ordinal).ToList();
            if (regions.Count == 0) throw new InvalidOperationException("No fortified settlements were available for population initialization.");
            _states.Clear();
            foreach (Settlement region in regions) _states.Add(region.StringId, new ProvincePopulationState(region.StringId));

            List<Settlement> majorCities = regions.Where(region => MajorUrbanRegions.Contains(region.StringId)).ToList();
            List<Settlement> otherTowns = regions.Where(region => region.IsTown && !MajorUrbanRegions.Contains(region.StringId)).ToList();
            Allocate(PopulationMath.MajorUrbanPopulation, majorCities, UrbanWeight, (state, value) => state.UrbanPopulation += value);
            Allocate(PopulationMath.OtherUrbanPopulation, otherTowns, UrbanWeight, (state, value) => state.UrbanPopulation += value);
            Allocate(PopulationMath.RuralPopulation, regions, RuralWeight, (state, value) => state.RuralPopulation += value);
            Allocate(PopulationMath.PastoralPopulation, regions, PastoralWeight, (state, value) => state.PastoralPopulation += value);
            Allocate(PopulationMath.InstitutionalPopulation, regions, region => region.IsTown ? 3d : 2d, (state, value) => state.InstitutionalPopulation += value);

            foreach (ProvincePopulationState state in _states.Values)
            {
                state.CarryingCapacity = Math.Max(1L, (long)Math.Floor(state.TotalPopulation * 1.18d));
                state.AvailableManpower = PopulationMath.GetMobilizationCeiling(state);
                InitializeFaithPopulation(state);
                Settlement settlement = FindSettlement(state.SettlementId);
                if (settlement != null && settlement.IsTown)
                {
                    state.TownRecruitReserve = PopulationMath.GetTownRecruitReserveCapacity(state) / 2L;
                }
            }

            AccountForStartingForces();

            long total = GetTotalPopulation();
            if (total != PopulationMath.CalradiaBaselinePopulation) throw new InvalidOperationException("Population baseline did not reconcile to 61,000,000; actual=" + total + ".");
            _mapRevision++;
            ReligionDiagnostics.Info("Initialized " + _states.Count + " strategic population regions at the 61,000,000 baseline.");
        }

        private static void RefreshTownRecruitReserve(ProvincePopulationState state)
        {
            long capacity = PopulationMath.GetTownRecruitReserveCapacity(state);
            long recovery = PopulationMath.GetMonthlyTownRecruitRecovery(state);
            state.TownRecruitReserve = Math.Min(capacity, Math.Max(0L, state.TownRecruitReserve + recovery));
        }

        private void AccountForStartingForces()
        {
            IEnumerable<MobileParty> forces = MobileParty.AllLordParties.Concat(MobileParty.AllGarrisonParties);
            foreach (MobileParty party in forces)
            {
                if (party == null || !party.IsActive || party.MemberRoster == null) continue;
                Settlement source = party.HomeSettlement ?? party.CurrentSettlement;
                ProvincePopulationState state;
                if (!TryGetStateForSettlement(source, out state)) continue;
                long demographicCost = party.MemberRoster.TotalManCount;
                state.AvailableManpower = Math.Max(0L, state.AvailableManpower - demographicCost);
            }
        }

        private void Allocate(long pool, IList<Settlement> regions, Func<Settlement, double> weightSelector, Action<ProvincePopulationState, long> assign)
        {
            if (regions.Count == 0) throw new InvalidOperationException("A required population allocation group was empty.");
            double totalWeight = regions.Sum(region => Math.Max(0.0001d, weightSelector(region)));
            long assigned = 0L;
            for (int index = 0; index < regions.Count; index++)
            {
                Settlement region = regions[index];
                long value = index == regions.Count - 1 ? pool - assigned : (long)Math.Floor(pool * Math.Max(0.0001d, weightSelector(region)) / totalWeight);
                assign(_states[region.StringId], value);
                assigned += value;
            }
        }

        private static double UrbanWeight(Settlement settlement)
        {
            return settlement.Town == null ? 1d : Math.Max(1000d, settlement.Town.Prosperity);
        }

        private static double RuralWeight(Settlement settlement)
        {
            double hearths = 0d;
            foreach (Village village in settlement.BoundVillages) hearths += Math.Max(50f, village.Hearth);
            return (500d + hearths + (settlement.Town == null ? 0d : settlement.Town.Prosperity * 0.3d)) * NorthDensityFactor(settlement.StringId);
        }

        private static double PastoralWeight(Settlement settlement)
        {
            string id = settlement.StringId;
            if (id.IndexOf("_K", StringComparison.Ordinal) >= 0) return 4d;
            if (id.IndexOf("_A", StringComparison.Ordinal) >= 0) return 2.4d;
            if (id.IndexOf("_N", StringComparison.Ordinal) >= 0 || id.IndexOf("_S", StringComparison.Ordinal) >= 0) return 1.5d;
            if (id.IndexOf("_B", StringComparison.Ordinal) >= 0) return 0.8d;
            return 0.45d;
        }

        private static double NorthDensityFactor(string id)
        {
            if (id.IndexOf("_N", StringComparison.Ordinal) >= 0) return 0.30d;
            if (id.IndexOf("_S", StringComparison.Ordinal) >= 0) return 0.58d;
            if (id.IndexOf("_K", StringComparison.Ordinal) >= 0) return 0.62d;
            if (id.IndexOf("_B", StringComparison.Ordinal) >= 0) return 0.72d;
            return 1d;
        }

        private static void InitializeFaithPopulation(ProvincePopulationState state)
        {
            Dictionary<string, double> mix = new Dictionary<string, double>(StringComparer.Ordinal);
            string id = state.SettlementId;
            if (string.Equals(id, "town_ES1", StringComparison.Ordinal))
            {
                mix["valeronism"] = 0.52d; mix["asharim"] = 0.33d; mix["mazirism"] = 0.10d; mix["calradic_old_faith"] = 0.05d;
            }
            else if (id.IndexOf("_A", StringComparison.Ordinal) >= 0)
            {
                mix["mazirism"] = 0.75d; mix["isharan_way"] = 0.15d; mix["asharim"] = 0.05d; mix["valeronism"] = 0.05d;
            }
            else if (id.IndexOf("_B", StringComparison.Ordinal) >= 0)
            {
                mix["caerwydd"] = 0.85d; mix["valeronism"] = 0.10d; mix["calradic_old_faith"] = 0.05d;
            }
            else if (id.IndexOf("_K", StringComparison.Ordinal) >= 0)
            {
                mix["kok_orun_way"] = 0.88d; mix["mazirism"] = 0.07d; mix["isharan_way"] = 0.05d;
            }
            else if (id.IndexOf("_N", StringComparison.Ordinal) >= 0 || id.IndexOf("_S", StringComparison.Ordinal) >= 0)
            {
                mix["veyrhold"] = 0.85d; mix["valeronism"] = 0.10d; mix["calradic_old_faith"] = 0.05d;
            }
            else
            {
                mix["valeronism"] = 0.84d; mix["calradic_old_faith"] = 0.10d; mix["asharim"] = 0.04d; mix["mazirism"] = 0.02d;
            }

            long assigned = 0L;
            string largestFaith = mix.OrderByDescending(pair => pair.Value).First().Key;
            foreach (KeyValuePair<string, double> pair in mix)
            {
                int index = ReligionCatalog.IndexOf(pair.Key);
                long value = (long)Math.Floor(state.TotalPopulation * pair.Value);
                state.FaithPopulations[index] = value;
                assigned += value;
            }

            state.FaithPopulations[ReligionCatalog.IndexOf(largestFaith)] += state.TotalPopulation - assigned;
            state.ReligiousTension = string.Equals(id, "town_ES1", StringComparison.Ordinal) ? 25f : 10f;
            for (int index = 0; index < state.FaithPopulations.Length; index++)
            {
                float share = state.TotalPopulation <= 0L ? 0f : state.FaithPopulations[index] / (float)state.TotalPopulation;
                state.FaithInstitutionStrengths[index] = Math.Max(5f, Math.Min(90f, 15f + share * 70f));
            }
            int dominantIndex = ReligionCatalog.IndexOf(largestFaith);
            state.FaithInstitutionStrengths[dominantIndex] = Math.Min(100f, state.FaithInstitutionStrengths[dominantIndex] + 10f);
            for (int index = 0; index < state.FaithInstitutionStrengths.Length; index++)
            {
                float strength = state.FaithInstitutionStrengths[index];
                state.FaithInstitutionTiers[index] = strength >= 90f ? ReligiousInstitutionTier.GreatSanctuary
                    : strength >= 60f ? ReligiousInstitutionTier.Temple
                    : strength >= 25f ? ReligiousInstitutionTier.Shrine : ReligiousInstitutionTier.None;
            }
            if (ReligionCatalog.HolySites.Any(site => site.SettlementId == state.SettlementId))
                state.FaithInstitutionTiers[dominantIndex] = ReligiousInstitutionTier.GreatSanctuary;
        }

        private static int GetCalendarMonthKey(CampaignTime time)
        {
            int year = time.GetYear;
            int dayOfYear = time.GetDayOfYear;
            int month = 0;
            for (int candidate = 11; candidate >= 0; candidate--)
            {
                int start = CalendarSettingsState.GetMonthStart(candidate) + (IsLeapYear(year) && candidate >= 2 ? 1 : 0);
                if (dayOfYear >= start) { month = candidate; break; }
            }

            return checked(year * 12 + month);
        }

        private static bool IsLeapYear(int year)
        {
            return CalendarSettingsState.UseLeapYears && (year % 400 == 0 || (year % 4 == 0 && year % 100 != 0));
        }

        private static Settlement FindSettlement(string settlementId)
        {
            return Settlement.All.FirstOrDefault(settlement => string.Equals(settlement.StringId, settlementId, StringComparison.Ordinal));
        }

        private void AddPolicyMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenu(PolicyMenuId, "{POPULATION_POLICY_SUMMARY}", OnPolicyMenuInit, GameMenu.MenuOverlayType.None);
            starter.AddGameMenu(DebugMenuId, "{POPULATION_DEBUG_SUMMARY}", OnDebugMenuInit, GameMenu.MenuOverlayType.None);
            starter.AddGameMenuOption("town", "aoc_manage_population", "Manage population, taxation, and conscription", CanManageCurrentProvince, OpenPolicyMenu, false, -1);
            starter.AddGameMenuOption("castle", "aoc_manage_population_castle", "Manage population, taxation, and conscription", CanManageCurrentProvince, OpenPolicyMenu, false, -1);
            starter.AddGameMenuOption("town", "aoc_recruit_urban_reserve", "Recruit from the urban volunteer reserve", CanRecruitUrbanVolunteers, RecruitUrbanVolunteers, false, -1);
            starter.AddGameMenuOption("town", "aoc_population_debug_town", "[DEBUG] Inspect population and fiscal data", CanOpenDebugMenu, OpenDebugMenu, false, -1);
            starter.AddGameMenuOption("castle", "aoc_population_debug_castle", "[DEBUG] Inspect population and fiscal data", CanOpenDebugMenu, OpenDebugMenu, false, -1);
            starter.AddGameMenuOption("village", "aoc_population_debug_village", "[DEBUG] Inspect population and fiscal data", CanOpenDebugMenu, OpenDebugMenu, false, -1);
            starter.AddGameMenuOption(PolicyMenuId, "aoc_cycle_tax", "Change tax policy", AlwaysPolicyOption, CycleTaxPolicy, false, -1);
            starter.AddGameMenuOption(PolicyMenuId, "aoc_cycle_conscription", "Change conscription policy", AlwaysPolicyOption, CycleConscriptionPolicy, false, -1);
            starter.AddGameMenuOption(PolicyMenuId, "aoc_leave_population", "Return", LeavePolicyMenuCondition, LeavePolicyMenu, true, -1);
            starter.AddGameMenuOption(DebugMenuId, "aoc_debug_open_population_management", "[DEBUG] Open population, tax, and conscription management", AlwaysPolicyOption, OpenPolicyMenu, false, -1);
            starter.AddGameMenuOption(DebugMenuId, "aoc_debug_open_religion_management", "[DEBUG] Open religion and holy-place management", AlwaysPolicyOption, OpenReligionMenu, false, -1);
            starter.AddGameMenuOption(DebugMenuId, "aoc_debug_refresh_population_report", "[DEBUG] Refresh settlement report", AlwaysPolicyOption, OpenDebugMenu, false, -1);
            starter.AddGameMenuOption(DebugMenuId, "aoc_leave_population_debug", "Return", LeavePolicyMenuCondition, LeavePolicyMenu, true, -1);
        }

        private void OnDebugMenuInit(MenuCallbackArgs args)
        {
            Settlement current = Settlement.CurrentSettlement;
            ProvincePopulationState state;
            if (current == null || !TryGetStateForSettlement(current, out state))
            {
                MBTextManager.SetTextVariable("POPULATION_DEBUG_SUMMARY", new TextObject("[DEBUG] No population state is available for this settlement."), false);
                return;
            }

            Settlement region = FindSettlement(state.SettlementId) ?? current;
            double taxMultiplier = PopulationMath.GetTaxMultiplier(state.TaxPolicy);
            double conscriptionShare = PopulationMath.GetMobilizationShare(state.ConscriptionPolicy);
            double taxPressure = PopulationMath.GetTaxHappinessPressure(state.TaxPolicy);
            double conscriptionPressure = PopulationMath.GetConscriptionHappinessPressure(state.ConscriptionPolicy);
            long mobilizationCeiling = PopulationMath.GetMobilizationCeiling(state);
            long garrisonManpower = (long)Math.Floor(mobilizationCeiling * PopulationMath.GarrisonMobilizationShare);
            long fieldManpower = mobilizationCeiling - garrisonManpower;
            long reserveCapacity = PopulationMath.GetTownRecruitReserveCapacity(state);
            long reserveRecovery = PopulationMath.GetMonthlyTownRecruitRecovery(state);
            long monthlyChange = state.LastMonthlyBirths - state.LastMonthlyDeaths + state.LastMonthlyMigrationNet;
            bool foodCrisis = region.IsStarving || (region.Town != null && region.Town.FoodStocks <= 0f);
            bool warDamage = region.IsUnderSiege || region.IsUnderRaid || region.IsRaided;
            double happinessTarget = 64d + taxPressure + conscriptionPressure - (foodCrisis ? 24d : 0d) - (warDamage ? 18d : 0d);
            double capacityUse = state.CarryingCapacity <= 0L ? 0d : state.TotalPopulation * 100d / state.CarryingCapacity;
            double manpowerUse = mobilizationCeiling <= 0L ? 0d : state.AvailableManpower * 100d / mobilizationCeiling;

            StringBuilder report = new StringBuilder();
            report.Append("[DEBUG] SETTLEMENT DEMOGRAPHICS — ").Append(current.Name).AppendLine();
            report.Append("Settlement ID: ").Append(current.StringId).Append(" | Population region: ").Append(region.Name).Append(" (").Append(state.SettlementId).AppendLine(")");
            report.Append("Owner: ").Append(region.OwnerClan == null ? "None" : region.OwnerClan.Name.ToString())
                .Append(" | Culture: ").Append(region.Culture == null ? "None" : region.Culture.Name.ToString()).AppendLine();
            report.Append("Population: ").Append(FormatCount(state.TotalPopulation))
                .Append(" | Urban ").Append(FormatCount(state.UrbanPopulation))
                .Append(" | Rural ").Append(FormatCount(state.RuralPopulation))
                .Append(" | Pastoral ").Append(FormatCount(state.PastoralPopulation))
                .Append(" | Institutional ").Append(FormatCount(state.InstitutionalPopulation)).AppendLine();
            report.Append("Carrying capacity: ").Append(FormatCount(state.CarryingCapacity)).Append(" | Utilization: ").Append(FormatPercent(capacityUse)).AppendLine();
            report.Append("Last month: births ").Append(FormatCount(state.LastMonthlyBirths))
                .Append(" | deaths ").Append(FormatCount(state.LastMonthlyDeaths))
                .Append(" | migration ").Append(FormatSigned(state.LastMonthlyMigrationNet))
                .Append(" | net ").Append(FormatSigned(monthlyChange)).AppendLine();
            report.Append("Happiness: ").Append(state.Happiness.ToString("F1", CultureInfo.InvariantCulture))
                .Append("/100 | monthly target ").Append(happinessTarget.ToString("F1", CultureInfo.InvariantCulture))
                .Append(" | crisis flags: food=").Append(foodCrisis ? "YES" : "no").Append(", war=").Append(warDamage ? "YES" : "no").AppendLine();
            report.Append("Tax: ").Append(state.TaxPolicy).Append(" | native tax multiplier ").Append(FormatPercent(taxMultiplier * 100d))
                .Append(" | happiness pressure ").Append(FormatSigned(taxPressure)).AppendLine();
            report.Append("Conscription: ").Append(state.ConscriptionPolicy).Append(" | rate ").Append(FormatPercent(conscriptionShare * 100d))
                .Append(" | happiness pressure ").Append(FormatSigned(conscriptionPressure)).AppendLine();
            report.Append("Mobilization: available ").Append(FormatCount(state.AvailableManpower)).Append(" / ceiling ").Append(FormatCount(mobilizationCeiling))
                .Append(" (").Append(FormatPercent(manpowerUse)).AppendLine(")");
            report.Append("Manpower allocation ceiling: garrison 65% = ").Append(FormatCount(garrisonManpower))
                .Append(" | field armies 35% = ").Append(FormatCount(fieldManpower)).AppendLine();
            report.Append("Garrison game capacity: ").Append(PopulationMath.GetGarrisonCapacityGameTroops(state).ToString("N0", CultureInfo.InvariantCulture)).AppendLine();
            report.Append("Urban recruits: ").Append(FormatCount(state.TownRecruitReserve)).Append(" / ").Append(FormatCount(reserveCapacity))
                .Append(" | monthly recovery ").Append(FormatCount(reserveRecovery))
                .Append(" | volunteer factor ").Append(PopulationService.GetVolunteerFactor(current).ToString("0.000", CultureInfo.InvariantCulture)).AppendLine();
            report.Append("Faith cohorts: ").Append(BuildFaithDebugText(state)).AppendLine();
            report.Append("Religious tension: ").Append(state.ReligiousTension.ToString("0.0", CultureInfo.InvariantCulture))
                .Append("/100 | last monthly converts: ").Append(FormatCount(state.LastMonthlyConverts))
                .Append(" | incident: ").Append(state.LastReligiousIncident).AppendLine();
            report.Append("Clergy institutions: ").Append(BuildInstitutionDebugText(state));
            MBTextManager.SetTextVariable("POPULATION_DEBUG_SUMMARY", new TextObject(report.ToString()), false);
        }

        private bool CanOpenDebugMenu(MenuCallbackArgs args)
        {
            ProvincePopulationState state;
            bool available = Settlement.CurrentSettlement != null && TryGetStateForSettlement(Settlement.CurrentSettlement, out state);
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            args.IsEnabled = available;
            if (!available) args.Tooltip = new TextObject("No population state is available for this settlement.");
            return Settlement.CurrentSettlement != null;
        }

        private static void OpenDebugMenu(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu(DebugMenuId);
        }

        private static void OpenReligionMenu(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu(ReligionCampaignBehavior.MenuId);
        }

        private static string BuildInstitutionDebugText(ProvincePopulationState state)
        {
            StringBuilder values = new StringBuilder();
            for (int index = 0; index < ReligionCatalog.FaithIds.Count && index < state.FaithInstitutionStrengths.Length; index++)
            {
                if (state.FaithPopulations[index] <= 0L && state.FaithInstitutionStrengths[index] <= 0f) continue;
                if (values.Length > 0) values.Append("; ");
                values.Append(ReligionCatalog.GetName(ReligionCatalog.FaithIds[index])).Append(' ')
                    .Append(state.FaithInstitutionTiers[index]).Append(" strength ")
                    .Append(state.FaithInstitutionStrengths[index].ToString("0.0", CultureInfo.InvariantCulture));
            }
            return values.Length == 0 ? "none" : values.ToString();
        }

        private static string BuildFaithDebugText(ProvincePopulationState state)
        {
            if (state == null || state.TotalPopulation <= 0L) return "none";
            StringBuilder faiths = new StringBuilder();
            for (int index = 0; index < ReligionCatalog.FaithIds.Count && index < state.FaithPopulations.Length; index++)
            {
                long count = state.FaithPopulations[index];
                if (count <= 0L) continue;
                if (faiths.Length > 0) faiths.Append("; ");
                faiths.Append(FormatFaithName(ReligionCatalog.FaithIds[index])).Append(' ')
                    .Append(FormatCount(count)).Append(" (")
                    .Append(FormatPercent(count * 100d / state.TotalPopulation)).Append(')');
            }
            return faiths.Length == 0 ? "none" : faiths.ToString();
        }

        private static string FormatFaithName(string id)
        {
            return id == "kok_orun_way" ? "Kok-Orun Way"
                : id == "isharan_way" ? "Isharan Way"
                : id == "calradic_old_faith" ? "Calradic Old Faith"
                : CultureInfo.InvariantCulture.TextInfo.ToTitleCase((id ?? string.Empty).Replace('_', ' '));
        }

        private static string FormatCount(long value) { return value.ToString("N0", CultureInfo.InvariantCulture); }
        private static string FormatPercent(double value) { return value.ToString("0.##", CultureInfo.InvariantCulture) + "%"; }
        private static string FormatSigned(double value) { return value.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture); }

        private void OnPolicyMenuInit(MenuCallbackArgs args)
        {
            ProvincePopulationState state;
            if (!TryGetStateForSettlement(Settlement.CurrentSettlement, out state)) return;
            TextObject summary = new TextObject("Population: {POP} ({CHANGE} last month)\nBirths: {BIRTHS} | Deaths: {DEATHS} | Migration: {MIGRATION}\nHappiness: {HAPPY}\nDominant faith: {FAITH}\nAvailable manpower: {MANPOWER} people\nUrban volunteer reserve: {RESERVE}/{RESERVE_CAP}\nTax policy: {TAX}\nConscription: {CONSCRIPTION}\nGarrison capacity: {GARRISON}; 65% of mobilized manpower is reserved locally and 35% supports field forces.");
            summary.SetTextVariable("POP", state.TotalPopulation.ToString("N0", CultureInfo.InvariantCulture));
            long monthlyChange = state.LastMonthlyBirths - state.LastMonthlyDeaths + state.LastMonthlyMigrationNet;
            summary.SetTextVariable("CHANGE", monthlyChange.ToString("+0;-0;0", CultureInfo.InvariantCulture));
            summary.SetTextVariable("BIRTHS", state.LastMonthlyBirths.ToString("N0", CultureInfo.InvariantCulture));
            summary.SetTextVariable("DEATHS", state.LastMonthlyDeaths.ToString("N0", CultureInfo.InvariantCulture));
            summary.SetTextVariable("MIGRATION", state.LastMonthlyMigrationNet.ToString("+0;-0;0", CultureInfo.InvariantCulture));
            summary.SetTextVariable("HAPPY", state.Happiness.ToString("F1", CultureInfo.InvariantCulture));
            summary.SetTextVariable("FAITH", GetDominantFaithName(state));
            summary.SetTextVariable("MANPOWER", state.AvailableManpower.ToString("N0", CultureInfo.InvariantCulture));
            summary.SetTextVariable("RESERVE", state.TownRecruitReserve.ToString("N0", CultureInfo.InvariantCulture));
            summary.SetTextVariable("RESERVE_CAP", PopulationMath.GetTownRecruitReserveCapacity(state).ToString("N0", CultureInfo.InvariantCulture));
            summary.SetTextVariable("GARRISON", PopulationMath.GetGarrisonCapacityGameTroops(state).ToString("N0", CultureInfo.InvariantCulture));
            summary.SetTextVariable("TAX", state.TaxPolicy.ToString());
            summary.SetTextVariable("CONSCRIPTION", state.ConscriptionPolicy + " (" + (PopulationMath.GetMobilizationShare(state.ConscriptionPolicy) * 100d).ToString("0.##", CultureInfo.InvariantCulture) + "%)");
            MBTextManager.SetTextVariable("POPULATION_POLICY_SUMMARY", summary, false);
        }

        private bool CanRecruitUrbanVolunteers(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            ProvincePopulationState state = null;
            int count = settlement != null && settlement.IsTown && TryGetStateForSettlement(settlement, out state) ? GetUrbanRecruitCount(state) : 0;
            args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
            args.IsEnabled = count > 0;
            args.Text = new TextObject("Recruit {COUNT} urban volunteers ({AVAILABLE} waiting; {COST} denars)");
            args.Text.SetTextVariable("COUNT", count);
            args.Text.SetTextVariable("AVAILABLE", state == null ? 0L : state.TownRecruitReserve);
            args.Text.SetTextVariable("COST", count * 30);
            if (!args.IsEnabled) args.Tooltip = new TextObject("No eligible urban volunteers are currently available, or your party lacks space or recruitment gold.");
            return settlement != null && settlement.IsTown;
        }

        private void RecruitUrbanVolunteers(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            ProvincePopulationState state;
            if (settlement == null || settlement.Culture == null || !TryGetStateForSettlement(settlement, out state)) return;
            int count = GetUrbanRecruitCount(state);
            CharacterObject troop = settlement.Culture.BasicTroop;
            if (count <= 0 || troop == null) return;
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, count * 30, true);
            MobileParty.MainParty.AddElementToMemberRoster(troop, count, false);
            state.TownRecruitReserve -= count;
            state.AvailableManpower -= count;
            InformationManager.DisplayMessage(new InformationMessage(count + " urban volunteers joined your party from " + settlement.Name + "."));
        }

        private static int GetUrbanRecruitCount(ProvincePopulationState state)
        {
            if (state == null || MobileParty.MainParty == null || Hero.MainHero == null) return 0;
            int partySpace = Math.Max(0, MobileParty.MainParty.Party.PartySizeLimit - MobileParty.MainParty.Party.NumberOfAllMembers);
            long affordable = Hero.MainHero.Gold / 30;
            long count = Math.Min(25L, Math.Min(state.TownRecruitReserve, Math.Min(state.AvailableManpower, Math.Min(partySpace, affordable))));
            return (int)Math.Max(0L, count);
        }

        private static string GetDominantFaithName(ProvincePopulationState state)
        {
            return FormatFaithName(GetDominantFaithId(state));
        }

        private static string GetDominantFaithId(ProvincePopulationState state)
        {
            int dominant = 0;
            for (int index = 1; index < state.FaithPopulations.Length; index++)
            {
                if (state.FaithPopulations[index] > state.FaithPopulations[dominant]) dominant = index;
            }

            return ReligionCatalog.FaithIds[dominant];
        }

        private bool CanManageCurrentProvince(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            bool authorized = settlement != null && settlement.OwnerClan != null && (settlement.OwnerClan == Clan.PlayerClan || (settlement.OwnerClan.Kingdom != null && settlement.OwnerClan.Kingdom.RulingClan == Clan.PlayerClan));
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            args.IsEnabled = authorized;
            if (!authorized) args.Tooltip = new TextObject("Only the owning clan or the kingdom's ruling clan may set provincial policy.");
            return settlement != null && settlement.IsFortification;
        }

        private static void OpenPolicyMenu(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu(PolicyMenuId);
        }

        private static bool AlwaysPolicyOption(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return true;
        }

        private void CycleTaxPolicy(MenuCallbackArgs args)
        {
            ProvincePopulationState state;
            if (TryGetStateForSettlement(Settlement.CurrentSettlement, out state)) state.TaxPolicy = (TaxPolicy)(((int)state.TaxPolicy + 1) % 4);
            GameMenu.SwitchToMenu(PolicyMenuId);
        }

        private void CycleConscriptionPolicy(MenuCallbackArgs args)
        {
            ProvincePopulationState state;
            if (TryGetStateForSettlement(Settlement.CurrentSettlement, out state)) SetConscriptionPolicy(state.SettlementId, (ConscriptionPolicy)(((int)state.ConscriptionPolicy + 1) % 5));
            GameMenu.SwitchToMenu(PolicyMenuId);
        }

        private static bool LeavePolicyMenuCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private static void LeavePolicyMenu(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            GameMenu.SwitchToMenu(settlement != null && settlement.IsVillage ? "village" : settlement != null && settlement.IsCastle ? "castle" : "town");
        }
    }
}
