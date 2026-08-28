using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace AgesOfCalradiaReligions
{
    /// <summary>Personal faith, state religion, clergy, holy access, tension, and conversion.</summary>
    public sealed class ReligionCampaignBehavior : CampaignBehaviorBase
    {
        internal const string MenuId = "aoc_religion_management";
        private const string HeroKey = "AgesOfCalradiaReligions.HeroFaithV1";
        private const string RealmKey = "AgesOfCalradiaReligions.RealmFaithV1";
        private const string SitesKey = "AgesOfCalradiaReligions.HolySitesV1";
        private const string ClergyOfficesKey = "AgesOfCalradiaReligions.ClergyOfficesV1";
        private Dictionary<string, HeroReligionState> _heroes = new Dictionary<string, HeroReligionState>(StringComparer.Ordinal);
        private Dictionary<string, RealmReligionState> _realms = new Dictionary<string, RealmReligionState>(StringComparer.Ordinal);
        private Dictionary<string, HolySiteState> _sites = new Dictionary<string, HolySiteState>(StringComparer.Ordinal);
        private Dictionary<string, ClergyOfficeState> _clergyOffices = new Dictionary<string, ClergyOfficeState>(StringComparer.Ordinal);
        private string _heroPayload = string.Empty;
        private string _realmPayload = string.Empty;
        private string _sitePayload = string.Empty;
        private string _clergyOfficePayload = string.Empty;
        private bool _initializingExistingHeroes;

        public ReligionCampaignBehavior()
        {
            ReligionService.ActiveBehavior = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                _heroPayload = ReligionPersistence.SerializeHeroes(_heroes.Values.OrderBy(value => value.HeroId, StringComparer.Ordinal));
                _realmPayload = ReligionPersistence.SerializeRealms(_realms.Values.OrderBy(value => value.KingdomId, StringComparer.Ordinal));
                _sitePayload = ReligionPersistence.SerializeHolySites(_sites.Values.OrderBy(value => value.SiteId, StringComparer.Ordinal));
                _clergyOfficePayload = ReligionPersistence.SerializeClergyOffices(_clergyOffices.Values.OrderBy(value => value.SettlementId, StringComparer.Ordinal));
            }
            dataStore.SyncData(HeroKey, ref _heroPayload);
            dataStore.SyncData(RealmKey, ref _realmPayload);
            dataStore.SyncData(SitesKey, ref _sitePayload);
            dataStore.SyncData(ClergyOfficesKey, ref _clergyOfficePayload);
            if (dataStore.IsLoading)
            {
                Dictionary<string, HeroReligionState> heroes;
                Dictionary<string, RealmReligionState> realms;
                Dictionary<string, HolySiteState> sites;
                Dictionary<string, ClergyOfficeState> offices;
                if (ReligionPersistence.TryDeserializeHeroes(_heroPayload, out heroes)) _heroes = heroes;
                if (ReligionPersistence.TryDeserializeRealms(_realmPayload, out realms)) _realms = realms;
                if (ReligionPersistence.TryDeserializeHolySites(_sitePayload, out sites)) _sites = sites;
                if (ReligionPersistence.TryDeserializeClergyOffices(_clergyOfficePayload, out offices)) _clergyOffices = offices;
            }
        }

        internal HeroReligionState GetHeroState(Hero hero)
        {
            if (hero == null) return null;
            HeroReligionState state;
            if (_heroes.TryGetValue(hero.StringId, out state)) return state;
            string culture = hero.Culture == null ? string.Empty : hero.Culture.StringId;
            string cultureFaith = ReligionCatalog.DefaultFaithForCulture(culture);
            string motherFaith = GetKnownParentFaith(hero.Mother);
            string fatherFaith = GetKnownParentFaith(hero.Father);
            string inheritedFaith = CharacterReligionMath.GetInheritedFaith(motherFaith, fatherFaith, cultureFaith, StableRoll(hero.StringId, 0));
            string initialFaith = _initializingExistingHeroes ? cultureFaith : inheritedFaith;
            state = new HeroReligionState(hero.StringId, initialFaith, 50f, -1) { BirthFaithId = initialFaith };
            _heroes[hero.StringId] = state;
            return state;
        }

        private string GetKnownParentFaith(Hero parent)
        {
            if (parent == null) return string.Empty;
            HeroReligionState known;
            if (_heroes.TryGetValue(parent.StringId, out known)) return known.FaithId;
            return ReligionCatalog.DefaultFaithForCulture(parent.Culture == null ? string.Empty : parent.Culture.StringId);
        }

        private void InitializeAllHeroFaiths()
        {
            _initializingExistingHeroes = true;
            try
            {
                foreach (Hero hero in Hero.AllAliveHeroes.Where(value => value != null).OrderByDescending(value => value.Age).ThenBy(value => value.StringId, StringComparer.Ordinal))
                    GetHeroState(hero);
            }
            finally
            {
                _initializingExistingHeroes = false;
            }
        }

        internal RealmReligionState GetRealmState(Kingdom kingdom)
        {
            if (kingdom == null) return null;
            RealmReligionState state;
            if (_realms.TryGetValue(kingdom.StringId, out state)) return state;
            string culture = kingdom.Culture == null ? string.Empty : kingdom.Culture.StringId;
            state = new RealmReligionState(kingdom.StringId, ReligionCatalog.DefaultFaithForCulture(culture));
            _realms[kingdom.StringId] = state;
            return state;
        }

        internal HolySiteAccess GetHolySiteAccess(string siteId, string faithId)
        {
            HolySiteState state;
            return _sites.TryGetValue(siteId ?? string.Empty, out state) ? state.GetAccess(faithId) : HolySiteAccess.Open;
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            EnsureDefinitions();
            foreach (Kingdom kingdom in Kingdom.All) GetRealmState(kingdom);
            InitializeAllHeroFaiths();
            GetHeroState(Hero.MainHero);
            AddMenus(starter);
            ReligionDiagnostics.Info("Religion state initialized: personal faith, realm policy, holy-site access, clergy institutions, tension, and monthly conversion are active.");
        }

        private void EnsureDefinitions()
        {
            foreach (HolySiteDefinition definition in ReligionCatalog.HolySites)
            {
                if (_sites.ContainsKey(definition.Id)) continue;
                HolySiteState state = new HolySiteState(definition.Id);
                for (int index = 0; index < state.AccessByFaith.Length; index++) state.AccessByFaith[index] = HolySiteAccess.Closed;
                foreach (string faithId in definition.FaithIds)
                {
                    int index = ReligionCatalog.IndexOf(faithId);
                    if (index >= 0) state.AccessByFaith[index] = HolySiteAccess.Open;
                }
                _sites.Add(definition.Id, state);
            }
        }

        internal void ProcessMonthly(IEnumerable<ProvincePopulationState> states)
        {
            EnsureDefinitions();
            bool playerNotified = false;
            Dictionary<string, long> realmPopulation = new Dictionary<string, long>(StringComparer.Ordinal);
            Dictionary<string, long> realmOfficial = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (ProvincePopulationState province in states)
            {
                Settlement settlement = Settlement.All.FirstOrDefault(value => value != null && value.StringId == province.SettlementId);
                Kingdom kingdom = settlement == null || settlement.OwnerClan == null ? null : settlement.OwnerClan.Kingdom;
                RealmReligionState realm = GetRealmState(kingdom);
                string targetFaith = realm == null ? DominantFaith(province) : realm.OfficialFaithId;
                ConvertProvince(province, targetFaith, realm == null ? CrownReligiousPolicy.TraditionalTolerance : realm.Policy, settlement, realm);
                UpdateClergyOffice(province, settlement, realm);
                ApplyGovernorFaithEffects(province, settlement, realm);
                ApplyMonthlyIncident(province, settlement, targetFaith, realm);
                if (!playerNotified && province.LastReligiousIncident != ReligiousIncidentType.None && IsPlayerRealm(settlement))
                {
                    InformationManager.DisplayMessage(new InformationMessage(settlement.Name + ": " + IncidentName(province.LastReligiousIncident) + "."));
                    playerNotified = true;
                }
                if (realm == null) continue;
                long total;
                realmPopulation.TryGetValue(realm.KingdomId, out total);
                realmPopulation[realm.KingdomId] = total + province.TotalPopulation;
                long official;
                realmOfficial.TryGetValue(realm.KingdomId, out official);
                realmOfficial[realm.KingdomId] = official + province.GetFaithPopulation(realm.OfficialFaithId);
            }
            foreach (RealmReligionState realm in _realms.Values)
            {
                long total;
                if (!realmPopulation.TryGetValue(realm.KingdomId, out total) || total <= 0L) continue;
                realm.ReligiousUnity = Clamp(realmOfficial[realm.KingdomId] * 100f / total, 0f, 100f);
                float clergyTarget = realm.ClergyGovernance == ClergyGovernancePolicy.ClericalAutonomy ? 65f
                    : realm.ClergyGovernance == ClergyGovernancePolicy.CrownConcordat ? 58f : 42f;
                realm.ClergyRelations = Clamp(realm.ClergyRelations + (clergyTarget - realm.ClergyRelations) * 0.04f, 0f, 100f);
            }
            ProcessHeroReligionMonth();
        }

        private void ProcessHeroReligionMonth()
        {
            int now = (int)Math.Floor(CampaignTime.Now.ToDays);
            int month = (int)Math.Floor(CampaignTime.Now.ToDays / 30d);
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero.Clan == null) continue;
                HeroReligionState state = GetHeroState(hero);
                Kingdom kingdom = hero.Clan.Kingdom;
                RealmReligionState realm = GetRealmState(kingdom);
                if (state == null || realm == null) continue;
                bool official = state.FaithId == realm.OfficialFaithId;
                bool related = ReligionCatalog.AreRelated(state.FaithId, realm.OfficialFaithId);
                string cultureFaith = ReligionCatalog.DefaultFaithForCulture(hero.Culture == null ? string.Empty : hero.Culture.StringId);
                state.ReligiousLegitimacy = CharacterReligionMath.GetReligiousLegitimacy(state.Piety, realm.ClergyRelations,
                    realm.ReligiousUnity, official, related, state.FaithId == cultureFaith, state.ConversionCount);
                state.Piety = Clamp(state.Piety + (official ? 0.05f : -0.02f), 0f, 100f);

                bool ruler = kingdom.Leader == hero;
                if (ruler)
                    realm.ClergyRelations = Clamp(realm.ClergyRelations + (official ? 0.15f : related ? -0.10f : -0.35f), 0f, 100f);

                if (hero == Hero.MainHero || hero.Age < 16f || official || (state.LastConversionDay >= 0 && now - state.LastConversionDay < 1825)) continue;
                HeroReligionState spouseState = hero.Spouse == null ? null : GetHeroState(hero.Spouse);
                bool spouseConverted = spouseState != null && spouseState.FaithId == realm.OfficialFaithId;
                double chance = CharacterReligionMath.GetMonthlyConversionChance(realm.Policy, realm.ClergyRelations, state.Zeal,
                    related, ruler, spouseConverted);
                if (StableRollTenThousand(hero.StringId, month) >= chance * 10000d) continue;
                state.FaithId = realm.OfficialFaithId;
                state.LastConversionDay = now;
                state.ConversionCount++;
                state.Zeal = 25f;
                state.Piety = Clamp(state.Piety - 5f, 0f, 100f);
            }
            ProcessAnnualFaithRelations(month);
        }

        private void ProcessAnnualFaithRelations(int month)
        {
            if (month % 12 != 0) return;
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                Hero spouse = hero == null ? null : hero.Spouse;
                if (spouse == null || string.CompareOrdinal(hero.StringId, spouse.StringId) >= 0) continue;
                HeroReligionState first = GetHeroState(hero);
                HeroReligionState second = GetHeroState(spouse);
                int change = first.FaithId == second.FaithId ? 1
                    : !ReligionCatalog.AreRelated(first.FaithId, second.FaithId) && first.Zeal + second.Zeal >= 120f ? -1 : 0;
                if (change != 0) ChangeRelationAction.ApplyRelationChangeBetweenHeroes(hero, spouse, change, true);
            }

            for (int firstIndex = 0; firstIndex < Kingdom.All.Count; firstIndex++)
            {
                Kingdom firstKingdom = Kingdom.All[firstIndex];
                if (firstKingdom == null || firstKingdom.Leader == null) continue;
                RealmReligionState first = GetRealmState(firstKingdom);
                for (int secondIndex = firstIndex + 1; secondIndex < Kingdom.All.Count; secondIndex++)
                {
                    Kingdom secondKingdom = Kingdom.All[secondIndex];
                    if (secondKingdom == null || secondKingdom.Leader == null) continue;
                    RealmReligionState second = GetRealmState(secondKingdom);
                    int change = first.OfficialFaithId == second.OfficialFaithId
                        && first.Policy <= CrownReligiousPolicy.TraditionalTolerance && second.Policy <= CrownReligiousPolicy.TraditionalTolerance ? 1
                        : !ReligionCatalog.AreRelated(first.OfficialFaithId, second.OfficialFaithId)
                            && (first.Policy == CrownReligiousPolicy.Suppression || second.Policy == CrownReligiousPolicy.Suppression) ? -1 : 0;
                    if (change != 0) ChangeRelationAction.ApplyRelationChangeBetweenHeroes(firstKingdom.Leader, secondKingdom.Leader, change, true);
                }
            }
        }

        private void ApplyGovernorFaithEffects(ProvincePopulationState province, Settlement settlement, RealmReligionState realm)
        {
            Hero governor = settlement == null || settlement.Town == null ? null : settlement.Town.Governor;
            if (governor == null) return;
            HeroReligionState governorFaith = GetHeroState(governor);
            if (governorFaith == null) return;
            string localFaith = DominantFaith(province);
            if (governorFaith.FaithId == localFaith)
                province.ReligiousTension = Clamp(province.ReligiousTension - 0.20f, 0f, 100f);
            else if (ReligionCatalog.AreRelated(governorFaith.FaithId, localFaith))
                province.ReligiousTension = Clamp(province.ReligiousTension - 0.05f, 0f, 100f);
            else
                province.ReligiousTension = Clamp(province.ReligiousTension + (realm != null && realm.Policy == CrownReligiousPolicy.Suppression ? 0.40f : 0.25f), 0f, 100f);
        }

        private void ApplyMonthlyIncident(ProvincePopulationState province, Settlement settlement, string officialFaith, RealmReligionState realm)
        {
            province.LastReligiousIncident = ReligiousIncidentType.None;
            if (province.TotalPopulation <= 0L) return;
            CrownReligiousPolicy policy = realm == null ? CrownReligiousPolicy.TraditionalTolerance : realm.Policy;
            int roll = StableRoll(province.SettlementId, (int)Math.Floor(CampaignTime.Now.ToDays / 30d));
            float dominantShare = province.FaithPopulations.Max() * 100f / province.TotalPopulation;
            bool diverse = dominantShare < 80f;
            int officialIndex = ReligionCatalog.IndexOf(officialFaith);
            float officialShare = officialIndex < 0 ? dominantShare : province.FaithPopulations[officialIndex] * 100f / province.TotalPopulation;
            float strongestClergy = province.FaithInstitutionStrengths.Length == 0 ? 0f : province.FaithInstitutionStrengths.Max();
            HolySiteDefinition site = settlement == null ? null : ReligionCatalog.HolySites.FirstOrDefault(value => value.SettlementId == settlement.StringId);

            if (province.ReligiousTension >= 72f && roll < 65)
            {
                province.LastReligiousIncident = ReligiousIncidentType.SectarianViolence;
                province.Happiness = Clamp(province.Happiness - 2f, 0f, 100f);
                province.ReligiousTension = Clamp(province.ReligiousTension + 3f, 0f, 100f);
            }
            else if (policy == CrownReligiousPolicy.Suppression && officialShare < 75f && roll < 55)
            {
                province.LastReligiousIncident = ReligiousIncidentType.SuppressionResistance;
                province.Happiness = Clamp(province.Happiness - 1.5f, 0f, 100f);
                province.ReligiousTension = Clamp(province.ReligiousTension + 2f, 0f, 100f);
            }
            else if (province.ReligiousTension >= 45f && roll < 38 + Math.Max(0, (int)((45f - strongestClergy) / 3f)))
            {
                province.LastReligiousIncident = ReligiousIncidentType.ClericalDispute;
                province.Happiness = Clamp(province.Happiness - 0.5f, 0f, 100f);
                province.ReligiousTension = Clamp(province.ReligiousTension + 1f, 0f, 100f);
            }
            else if (diverse && province.ReligiousTension <= 25f && policy <= CrownReligiousPolicy.TraditionalTolerance && roll < 20 + (int)(strongestClergy / 10f))
            {
                province.LastReligiousIncident = ReligiousIncidentType.InterfaithFestival;
                province.Happiness = Clamp(province.Happiness + 0.75f, 0f, 100f);
                province.ReligiousTension = Clamp(province.ReligiousTension - 1f, 0f, 100f);
            }
            else if (site != null && HasOpenRecognizedAccess(site) && roll < Math.Min(36, 12 + (int)(strongestClergy / 5f)))
            {
                province.LastReligiousIncident = ReligiousIncidentType.PilgrimMarket;
                province.Happiness = Clamp(province.Happiness + 0.5f, 0f, 100f);
                province.ReligiousTension = Clamp(province.ReligiousTension - 0.5f, 0f, 100f);
            }
        }

        private void ConvertProvince(ProvincePopulationState province, string targetFaith, CrownReligiousPolicy policy, Settlement settlement, RealmReligionState realm)
        {
            province.LastMonthlyConverts = 0L;
            int target = ReligionCatalog.IndexOf(targetFaith);
            if (target < 0 || province.TotalPopulation <= 0L) return;
            int source = LargestOtherFaith(province, target);
            double policyRate = ReligionSimulationMath.GetPolicyConversionMultiplier(policy);
            if (source >= 0 && province.FaithPopulations[source] > 0L)
            {
                float effectiveStrength = province.FaithInstitutionStrengths[target];
                if (realm != null)
                {
                    effectiveStrength *= 0.75f + realm.ClergyRelations / 200f;
                    effectiveStrength *= realm.ClergyGovernance == ClergyGovernancePolicy.CrownSupervision ? 1.10f
                        : realm.ClergyGovernance == ClergyGovernancePolicy.ClericalAutonomy ? 0.90f : 1f;
                }
                long moved = ReligionSimulationMath.GetMonthlyConversionCount(province.TotalPopulation, province.FaithPopulations[source],
                    ReligionCatalog.FaithIds[source], targetFaith, policy, effectiveStrength);
                province.FaithPopulations[source] -= moved;
                province.FaithPopulations[target] += moved;
                province.LastMonthlyConverts = moved;
            }

            float minority = 100f - province.FaithPopulations[target] * 100f / Math.Max(1L, province.TotalPopulation);
            float holyPressure = GetHolyAccessPressure(settlement, province);
            float tensionTarget = ReligionSimulationMath.GetTensionTarget(minority, policy, holyPressure, province.SettlementId == "town_ES1");
            province.ReligiousTension = Clamp(province.ReligiousTension + (tensionTarget - province.ReligiousTension) * 0.16f, 0f, 100f);
            province.Happiness = Clamp(province.Happiness - Math.Max(0f, province.ReligiousTension - 55f) * 0.012f, 0f, 100f);

            for (int index = 0; index < province.FaithInstitutionStrengths.Length; index++)
            {
                float share = province.FaithPopulations[index] * 100f / Math.Max(1L, province.TotalPopulation);
                float tierSupport = province.FaithInstitutionTiers[index] == ReligiousInstitutionTier.Shrine ? 5f
                    : province.FaithInstitutionTiers[index] == ReligiousInstitutionTier.Temple ? 12f
                    : province.FaithInstitutionTiers[index] == ReligiousInstitutionTier.GreatSanctuary ? 20f : 0f;
                float desired = Clamp(10f + share * 0.75f + tierSupport + (index == target ? (float)policyRate * 3f : 0f), 0f, 100f);
                province.FaithInstitutionStrengths[index] = Clamp(province.FaithInstitutionStrengths[index] + (desired - province.FaithInstitutionStrengths[index]) * 0.06f, 0f, 100f);
            }
        }

        private void UpdateClergyOffice(ProvincePopulationState province, Settlement settlement, RealmReligionState realm)
        {
            if (settlement == null) return;
            ClergyOfficeState office = GetOrCreateClergyOffice(settlement, province);
            if (office == null) return;
            int faith = ReligionCatalog.IndexOf(office.FaithId);
            int tier = faith < 0 ? 0 : (int)province.FaithInstitutionTiers[faith];
            long monthlyIncome = Math.Min(500L, 40L + tier * 60L + province.InstitutionalPopulation / 200000L);
            office.Treasury = Math.Min(50000L, office.Treasury + Math.Max(0L, monthlyIncome));
            if (realm == null) return;
            if (realm.ClergyGovernance == ClergyGovernancePolicy.CrownSupervision && DominantFaith(province) != realm.OfficialFaithId)
                province.ReligiousTension = Clamp(province.ReligiousTension + 0.35f, 0f, 100f);
            else if (realm.ClergyGovernance == ClergyGovernancePolicy.ClericalAutonomy && province.ReligiousTension < 60f)
                province.ReligiousTension = Clamp(province.ReligiousTension - 0.15f, 0f, 100f);
        }

        private float GetHolyAccessPressure(Settlement settlement, ProvincePopulationState province)
        {
            if (settlement == null) return 0f;
            HolySiteDefinition definition = ReligionCatalog.HolySites.FirstOrDefault(value => value.SettlementId == settlement.StringId);
            if (definition == null) return 0f;
            float pressure = 0f;
            foreach (string faithId in definition.FaithIds)
            {
                int index = ReligionCatalog.IndexOf(faithId);
                if (index < 0 || province.FaithPopulations[index] <= 0L) continue;
                float localShare = province.FaithPopulations[index] / (float)Math.Max(1L, province.TotalPopulation);
                HolySiteAccess access = GetHolySiteAccess(definition.Id, faithId);
                pressure += localShare * (access == HolySiteAccess.Closed ? 28f : access == HolySiteAccess.Restricted ? 10f : -3f);
            }
            return pressure;
        }

        private void AddMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenu(MenuId, "{AOC_RELIGION_SUMMARY}", OnMenuInit, GameMenu.MenuOverlayType.None);
            starter.AddGameMenuOption("town", "aoc_religion_town", "Religion and holy places", CanOpenMenu, OpenMenu, false, -1);
            starter.AddGameMenuOption("castle", "aoc_religion_castle", "Religion and holy places", CanOpenMenu, OpenMenu, false, -1);
            starter.AddGameMenuOption("village", "aoc_religion_village", "Religion and holy places", CanOpenMenu, OpenMenu, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_religion_policy", "Change crown religious policy", CanRuleReligion, CyclePolicy, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_religion_official", "Proclaim the local majority as the realm's official faith", CanRuleReligion, ProclaimLocalFaith, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_clergy_governance", "Change crown–clergy governance", CanRuleReligion, CycleClergyGovernance, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_institution_upgrade", "Build or upgrade the local religious institution", CanUpgradeInstitution, UpgradeInstitution, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_clergy_appoint", "Appoint the local clergy office", CanManageLocalClergy, AppointClergy, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_clergy_endow", "Endow the local clergy office (5,000 denars)", CanEndowClergy, EndowClergy, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_clergy_tax", "Levy the clergy treasury", CanTaxClergy, TaxClergy, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_religion_festival", "Sponsor the local clergy and a public festival (5,000 denars)", CanSponsor, SponsorFestival, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_religion_convert", "Adopt the local majority faith", CanConvert, ConvertPlayer, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_religion_pilgrimage", "Undertake a pilgrimage", CanPilgrimage, UndertakePilgrimage, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_religion_access", "Change access to this holy place", CanControlHolySite, CycleHolyAccess, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_religion_access_asharim", "Change Asharim access", CanControlAsharimAccess, CycleAsharimAccess, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_religion_access_valeronism", "Change Valeronist access", CanControlValeronistAccess, CycleValeronistAccess, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_religion_access_mazirism", "Change Mazirist access", CanControlMaziristAccess, CycleMaziristAccess, false, -1);
            starter.AddGameMenuOption(MenuId, "aoc_religion_leave", "Return", Always, LeaveMenu, true, -1);
        }

        private void OnMenuInit(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            ProvincePopulationState province;
            if (!PopulationService.TryGetState(settlement, out province)) return;
            Kingdom kingdom = settlement.OwnerClan == null ? null : settlement.OwnerClan.Kingdom;
            RealmReligionState realm = GetRealmState(kingdom);
            string dominant = DominantFaith(province);
            StringBuilder text = new StringBuilder();
            text.Append(settlement.Name).Append(" — Religion\nLocal majority: ").Append(ReligionCatalog.GetName(dominant));
            if (realm != null) text.Append("\nOfficial faith: ").Append(ReligionCatalog.GetName(realm.OfficialFaithId)).Append(" | Crown policy: ").Append(PolicyName(realm.Policy)).Append("\nRealm unity: ").Append(realm.ReligiousUnity.ToString("0.0", CultureInfo.InvariantCulture)).Append("% | Clergy relations: ").Append(realm.ClergyRelations.ToString("0.0", CultureInfo.InvariantCulture)).Append(" | Governance: ").Append(ClergyGovernanceName(realm.ClergyGovernance));
            if (kingdom != null && kingdom.Leader != null)
            {
                HeroReligionState rulerFaith = GetHeroState(kingdom.Leader);
                text.Append("\nRuler: ").Append(kingdom.Leader.Name).Append(" | Faith: ").Append(ReligionCatalog.GetName(rulerFaith.FaithId))
                    .Append(" | Religious legitimacy: ").Append(rulerFaith.ReligiousLegitimacy.ToString("0.0", CultureInfo.InvariantCulture)).Append("/100");
            }
            Hero governor = settlement.Town == null ? null : settlement.Town.Governor;
            if (governor != null)
            {
                HeroReligionState governorFaith = GetHeroState(governor);
                text.Append("\nGovernor: ").Append(governor.Name).Append(" | Faith: ").Append(ReligionCatalog.GetName(governorFaith.FaithId));
            }
            text.Append("\nReligious tension: ").Append(province.ReligiousTension.ToString("0.0", CultureInfo.InvariantCulture)).Append("/100 | Last month converted: ").Append(province.LastMonthlyConverts.ToString("N0", CultureInfo.InvariantCulture));
            text.Append("\nThis month's religious incident: ").Append(IncidentName(province.LastReligiousIncident));
            text.Append("\n\nCommunities:\n");
            for (int index = 0; index < province.FaithPopulations.Length; index++)
            {
                if (province.FaithPopulations[index] <= 0L) continue;
                text.Append(ReligionCatalog.GetName(ReligionCatalog.FaithIds[index])).Append(": ")
                    .Append((province.FaithPopulations[index] * 100d / province.TotalPopulation).ToString("0.0", CultureInfo.InvariantCulture)).Append("% | ")
                    .Append(InstitutionName(province.FaithInstitutionTiers[index])).Append(" | ")
                    .Append(ReligionCatalog.Get(ReligionCatalog.FaithIds[index]).ClergyTitle).Append(" strength ")
                    .Append(province.FaithInstitutionStrengths[index].ToString("0", CultureInfo.InvariantCulture)).Append("/100\n");
            }
            ClergyOfficeState office = GetOrCreateClergyOffice(settlement, province);
            if (office != null)
            {
                text.Append("\nClergy office: ").Append(ReligionCatalog.Get(office.FaithId).ClergyTitle)
                    .Append(" of ").Append(ReligionCatalog.GetName(office.FaithId))
                    .Append(" | Holder: ").Append(ClergyHolderName(office))
                    .Append(" | Treasury: ").Append(office.Treasury.ToString("N0", CultureInfo.InvariantCulture)).Append(" denars\n");
            }
            if (settlement.Notables != null && settlement.Notables.Count > 0)
            {
                text.Append("\nLocal notable faiths:\n");
                foreach (Hero notable in settlement.Notables.Where(value => value != null && value.IsAlive).Take(6))
                {
                    HeroReligionState notableFaith = GetHeroState(notable);
                    text.Append(notable.Name).Append(": ").Append(ReligionCatalog.GetName(notableFaith.FaithId))
                        .Append(" | Zeal ").Append(notableFaith.Zeal.ToString("0", CultureInfo.InvariantCulture))
                        .Append(" | Piety ").Append(notableFaith.Piety.ToString("0", CultureInfo.InvariantCulture)).Append('\n');
                }
            }
            HolySiteDefinition site = FindCurrentHolySite();
            if (site != null)
            {
                text.Append("\nHoly place: ").Append(site.Name).Append('\n');
                foreach (string faith in site.FaithIds) text.Append(ReligionCatalog.GetName(faith)).Append(" access: ").Append(GetHolySiteAccess(site.Id, faith)).Append("\n");
            }
            HeroReligionState hero = GetHeroState(Hero.MainHero);
            if (hero != null) text.Append("\nYour faith: ").Append(ReligionCatalog.GetName(hero.FaithId))
                .Append(" | Birth faith: ").Append(ReligionCatalog.GetName(hero.BirthFaithId))
                .Append(" | Conversions: ").Append(hero.ConversionCount)
                .Append(" | Zeal: ").Append(hero.Zeal.ToString("0", CultureInfo.InvariantCulture)).Append("/100 | Piety: ").Append(hero.Piety.ToString("0", CultureInfo.InvariantCulture))
                .Append("/100 | Religious legitimacy: ").Append(hero.ReligiousLegitimacy.ToString("0.0", CultureInfo.InvariantCulture)).Append("/100");
            if (Hero.MainHero != null && Hero.MainHero.Spouse != null)
            {
                HeroReligionState spouseFaith = GetHeroState(Hero.MainHero.Spouse);
                text.Append("\nYour spouse: ").Append(Hero.MainHero.Spouse.Name).Append(" | Faith: ").Append(ReligionCatalog.GetName(spouseFaith.FaithId));
            }
            MBTextManager.SetTextVariable("AOC_RELIGION_SUMMARY", new TextObject(text.ToString()), false);
        }

        private bool CanOpenMenu(MenuCallbackArgs args)
        {
            ProvincePopulationState state;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return Settlement.CurrentSettlement != null && PopulationService.TryGetState(Settlement.CurrentSettlement, out state);
        }

        private bool CanRuleReligion(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            bool allowed = settlement != null && settlement.OwnerClan != null && settlement.OwnerClan.Kingdom != null && settlement.OwnerClan.Kingdom.RulingClan == Clan.PlayerClan;
            args.IsEnabled = allowed;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            if (!allowed) args.Tooltip = new TextObject("Only the realm's ruling clan may change crown religious policy.");
            return true;
        }

        private bool CanSponsor(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            args.IsEnabled = Hero.MainHero != null && Hero.MainHero.Gold >= 5000;
            if (!args.IsEnabled) args.Tooltip = new TextObject("You need 5,000 denars.");
            return true;
        }

        private bool CanManageLocalClergy(MenuCallbackArgs args)
        {
            ProvincePopulationState province;
            bool hasProvince = PopulationService.TryGetState(Settlement.CurrentSettlement, out province);
            int faith = hasProvince ? ReligionCatalog.IndexOf(DominantFaith(province)) : -1;
            bool hasInstitution = faith >= 0 && province.FaithInstitutionTiers[faith] >= ReligiousInstitutionTier.Shrine;
            bool allowed = IsLocalPolicyOwner() && hasInstitution;
            args.IsEnabled = allowed;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            if (!IsLocalPolicyOwner()) args.Tooltip = new TextObject("Only the owning clan or the realm's ruling clan may manage the local clergy.");
            else if (!hasInstitution) args.Tooltip = new TextObject("Build a shrine before appointing a formal clergy office.");
            return Settlement.CurrentSettlement != null && !Settlement.CurrentSettlement.IsVillage;
        }

        private bool CanUpgradeInstitution(MenuCallbackArgs args)
        {
            ProvincePopulationState province;
            bool available = PopulationService.TryGetState(Settlement.CurrentSettlement, out province);
            int faith = available ? ReligionCatalog.IndexOf(DominantFaith(province)) : -1;
            ReligiousInstitutionTier tier = faith < 0 ? ReligiousInstitutionTier.None : province.FaithInstitutionTiers[faith];
            int cost = InstitutionUpgradeCost(tier);
            args.Text = new TextObject("Build or upgrade {FAITH} institution to {TIER} ({COST} denars)");
            args.Text.SetTextVariable("FAITH", faith < 0 ? "local" : ReligionCatalog.GetName(ReligionCatalog.FaithIds[faith]));
            args.Text.SetTextVariable("TIER", tier >= ReligiousInstitutionTier.GreatSanctuary ? "maximum tier" : InstitutionName((ReligiousInstitutionTier)((int)tier + 1)));
            args.Text.SetTextVariable("COST", cost);
            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            args.IsEnabled = available && IsLocalPolicyOwner() && tier < ReligiousInstitutionTier.GreatSanctuary && Hero.MainHero.Gold >= cost;
            if (!IsLocalPolicyOwner()) args.Tooltip = new TextObject("Only the owning clan or the realm's ruling clan may construct religious institutions.");
            else if (tier >= ReligiousInstitutionTier.GreatSanctuary) args.Tooltip = new TextObject("This institution is already a Great Sanctuary.");
            else if (Hero.MainHero.Gold < cost) args.Tooltip = new TextObject("You cannot afford this institution upgrade.");
            return available && Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsFortification;
        }

        private bool CanEndowClergy(MenuCallbackArgs args)
        {
            ProvincePopulationState province;
            ClergyOfficeState office = PopulationService.TryGetState(Settlement.CurrentSettlement, out province) ? GetOrCreateClergyOffice(Settlement.CurrentSettlement, province) : null;
            args.IsEnabled = office != null && Hero.MainHero.Gold >= 5000;
            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            if (office == null) args.Tooltip = new TextObject("This settlement has no recognized clergy office.");
            else if (Hero.MainHero.Gold < 5000) args.Tooltip = new TextObject("You need 5,000 denars.");
            return Settlement.CurrentSettlement != null && !Settlement.CurrentSettlement.IsVillage;
        }

        private bool CanTaxClergy(MenuCallbackArgs args)
        {
            ProvincePopulationState province;
            ClergyOfficeState office = PopulationService.TryGetState(Settlement.CurrentSettlement, out province) ? GetOrCreateClergyOffice(Settlement.CurrentSettlement, province) : null;
            int now = (int)Math.Floor(CampaignTime.Now.ToDays);
            int amount = office == null ? 0 : (int)Math.Min(5000L, office.Treasury);
            args.Text = new TextObject("Levy {AMOUNT} denars from the clergy treasury");
            args.Text.SetTextVariable("AMOUNT", amount);
            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            args.IsEnabled = IsLocalPolicyOwner() && office != null && amount > 0 && (office.LastClergyTaxDay < 0 || now - office.LastClergyTaxDay >= 365);
            if (!IsLocalPolicyOwner()) args.Tooltip = new TextObject("Only the owning clan or the realm's ruling clan may levy the clergy treasury.");
            else if (office == null || amount <= 0) args.Tooltip = new TextObject("The local clergy treasury is empty.");
            else if (now - office.LastClergyTaxDay < 365) args.Tooltip = new TextObject("The clergy treasury may be levied only once each year.");
            return Settlement.CurrentSettlement != null && !Settlement.CurrentSettlement.IsVillage;
        }

        private bool CanConvert(MenuCallbackArgs args)
        {
            ProvincePopulationState province;
            bool show = PopulationService.TryGetState(Settlement.CurrentSettlement, out province);
            HeroReligionState hero = GetHeroState(Hero.MainHero);
            args.IsEnabled = show && hero != null && hero.FaithId != DominantFaith(province);
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return show;
        }

        private bool CanControlHolySite(MenuCallbackArgs args)
        {
            HolySiteDefinition site = FindCurrentHolySite();
            bool allowed = site != null && site.FaithIds.Length == 1 && IsPlayerRuler();
            args.IsEnabled = allowed;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            if (!allowed) args.Tooltip = new TextObject("Only the ruling clan controlling this holy place may change access.");
            return site != null && site.FaithIds.Length == 1;
        }

        private bool CanControlAsharimAccess(MenuCallbackArgs args) { return CanControlFaithAccess(args, "asharim"); }
        private bool CanControlValeronistAccess(MenuCallbackArgs args) { return CanControlFaithAccess(args, "valeronism"); }
        private bool CanControlMaziristAccess(MenuCallbackArgs args) { return CanControlFaithAccess(args, "mazirism"); }

        private bool CanControlFaithAccess(MenuCallbackArgs args, string faithId)
        {
            HolySiteDefinition site = FindCurrentHolySite();
            bool visible = site != null && site.FaithIds.Length > 1 && site.FaithIds.Contains(faithId);
            args.IsEnabled = visible && IsPlayerRuler();
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            if (visible && !args.IsEnabled) args.Tooltip = new TextObject("Only the ruling clan controlling this holy place may change access.");
            return visible;
        }

        private bool CanPilgrimage(MenuCallbackArgs args)
        {
            HolySiteDefinition site = FindCurrentHolySite();
            HeroReligionState hero = GetHeroState(Hero.MainHero);
            HolySiteAccess access = site == null || hero == null ? HolySiteAccess.Closed : GetHolySiteAccess(site.Id, hero.FaithId);
            int cost = access == HolySiteAccess.Restricted ? 2000 : 1000;
            int elapsed = hero == null || hero.LastPilgrimageDay < 0 ? int.MaxValue : (int)Math.Floor(CampaignTime.Now.ToDays) - hero.LastPilgrimageDay;
            bool recognized = site != null && hero != null && site.FaithIds.Contains(hero.FaithId);
            args.Text = new TextObject("Undertake a pilgrimage ({COST} denars)");
            args.Text.SetTextVariable("COST", cost);
            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            args.IsEnabled = recognized && access != HolySiteAccess.Closed && elapsed >= 365 && Hero.MainHero.Gold >= cost;
            if (!recognized) args.Tooltip = new TextObject("This is not a holy place recognized by your faith.");
            else if (access == HolySiteAccess.Closed) args.Tooltip = new TextObject("Pilgrims of your faith are forbidden entry.");
            else if (elapsed < 365) args.Tooltip = new TextObject("A major pilgrimage may be undertaken only once each year.");
            else if (Hero.MainHero.Gold < cost) args.Tooltip = new TextObject("You cannot afford the journey and offering.");
            return site != null;
        }

        private void CyclePolicy(MenuCallbackArgs args)
        {
            RealmReligionState realm = CurrentRealm();
            if (realm != null) realm.Policy = (CrownReligiousPolicy)(((int)realm.Policy + 1) % 4);
            OpenMenu(args);
        }

        private void CycleClergyGovernance(MenuCallbackArgs args)
        {
            RealmReligionState realm = CurrentRealm();
            if (realm != null)
            {
                realm.ClergyGovernance = (ClergyGovernancePolicy)(((int)realm.ClergyGovernance + 1) % 3);
                realm.ClergyRelations = Clamp(realm.ClergyRelations - 2f, 0f, 100f);
            }
            OpenMenu(args);
        }

        private void UpgradeInstitution(MenuCallbackArgs args)
        {
            ProvincePopulationState province;
            if (!PopulationService.TryGetState(Settlement.CurrentSettlement, out province) || !IsLocalPolicyOwner()) return;
            int faith = ReligionCatalog.IndexOf(DominantFaith(province));
            ReligiousInstitutionTier tier = province.FaithInstitutionTiers[faith];
            int cost = InstitutionUpgradeCost(tier);
            if (tier >= ReligiousInstitutionTier.GreatSanctuary || Hero.MainHero.Gold < cost) return;
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, true);
            province.FaithInstitutionTiers[faith] = (ReligiousInstitutionTier)((int)tier + 1);
            province.FaithInstitutionStrengths[faith] = Clamp(province.FaithInstitutionStrengths[faith] + 10f, 0f, 100f);
            province.Happiness = Clamp(province.Happiness + 1f, 0f, 100f);
            GetOrCreateClergyOffice(Settlement.CurrentSettlement, province);
            RealmReligionState realm = CurrentRealm();
            if (realm != null) realm.ClergyRelations = Clamp(realm.ClergyRelations + 3f, 0f, 100f);
            OpenMenu(args);
        }

        private void AppointClergy(MenuCallbackArgs args)
        {
            ProvincePopulationState province;
            if (!PopulationService.TryGetState(Settlement.CurrentSettlement, out province) || !IsLocalPolicyOwner()) return;
            string faith = DominantFaith(province);
            int faithIndex = ReligionCatalog.IndexOf(faith);
            if (faithIndex < 0 || province.FaithInstitutionTiers[faithIndex] < ReligiousInstitutionTier.Shrine) return;
            string holder = SelectClergyHolderId(Settlement.CurrentSettlement);
            ClergyOfficeState office;
            if (!_clergyOffices.TryGetValue(province.SettlementId, out office))
            {
                office = new ClergyOfficeState(province.SettlementId, faith, holder);
                _clergyOffices.Add(province.SettlementId, office);
            }
            else
            {
                office.FaithId = faith;
                office.HolderHeroId = holder;
            }
            RealmReligionState realm = CurrentRealm();
            if (realm != null) realm.ClergyRelations = Clamp(realm.ClergyRelations + (faith == realm.OfficialFaithId ? 2f : -2f), 0f, 100f);
            OpenMenu(args);
        }

        private void EndowClergy(MenuCallbackArgs args)
        {
            ProvincePopulationState province;
            if (!PopulationService.TryGetState(Settlement.CurrentSettlement, out province) || Hero.MainHero.Gold < 5000) return;
            ClergyOfficeState office = GetOrCreateClergyOffice(Settlement.CurrentSettlement, province);
            if (office == null) return;
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 5000, true);
            office.Treasury = Math.Min(50000L, office.Treasury + 5000L);
            int faith = ReligionCatalog.IndexOf(office.FaithId);
            province.FaithInstitutionStrengths[faith] = Clamp(province.FaithInstitutionStrengths[faith] + 5f, 0f, 100f);
            province.ReligiousTension = Clamp(province.ReligiousTension - 1f, 0f, 100f);
            RealmReligionState realm = CurrentRealm();
            if (realm != null) realm.ClergyRelations = Clamp(realm.ClergyRelations + 4f, 0f, 100f);
            OpenMenu(args);
        }

        private void TaxClergy(MenuCallbackArgs args)
        {
            ProvincePopulationState province;
            ClergyOfficeState office;
            int now = (int)Math.Floor(CampaignTime.Now.ToDays);
            if (!PopulationService.TryGetState(Settlement.CurrentSettlement, out province) || !IsLocalPolicyOwner()
                || !_clergyOffices.TryGetValue(province.SettlementId, out office) || (office.LastClergyTaxDay >= 0 && now - office.LastClergyTaxDay < 365)) return;
            int amount = (int)Math.Min(5000L, office.Treasury);
            if (amount <= 0) return;
            office.Treasury -= amount;
            office.LastClergyTaxDay = now;
            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount, true);
            province.ReligiousTension = Clamp(province.ReligiousTension + 3f, 0f, 100f);
            province.Happiness = Clamp(province.Happiness - 1f, 0f, 100f);
            int faith = ReligionCatalog.IndexOf(office.FaithId);
            province.FaithInstitutionStrengths[faith] = Clamp(province.FaithInstitutionStrengths[faith] - 2f, 0f, 100f);
            RealmReligionState realm = CurrentRealm();
            if (realm != null) realm.ClergyRelations = Clamp(realm.ClergyRelations - 8f, 0f, 100f);
            OpenMenu(args);
        }

        private void ProclaimLocalFaith(MenuCallbackArgs args)
        {
            RealmReligionState realm = CurrentRealm();
            ProvincePopulationState province;
            if (realm != null && PopulationService.TryGetState(Settlement.CurrentSettlement, out province))
            {
                realm.OfficialFaithId = DominantFaith(province);
                realm.ClergyRelations = Clamp(realm.ClergyRelations - 8f, 0f, 100f);
            }
            OpenMenu(args);
        }

        private void SponsorFestival(MenuCallbackArgs args)
        {
            ProvincePopulationState province;
            if (Hero.MainHero != null && Hero.MainHero.Gold >= 5000 && PopulationService.TryGetState(Settlement.CurrentSettlement, out province))
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 5000, true);
                int faith = ReligionCatalog.IndexOf(DominantFaith(province));
                province.FaithInstitutionStrengths[faith] = Clamp(province.FaithInstitutionStrengths[faith] + 6f, 0f, 100f);
                province.ReligiousTension = Clamp(province.ReligiousTension - 8f, 0f, 100f);
                province.Happiness = Clamp(province.Happiness + 2f, 0f, 100f);
            }
            OpenMenu(args);
        }

        private void ConvertPlayer(MenuCallbackArgs args)
        {
            ProvincePopulationState province;
            HeroReligionState hero = GetHeroState(Hero.MainHero);
            if (hero != null && PopulationService.TryGetState(Settlement.CurrentSettlement, out province))
            {
                hero.FaithId = DominantFaith(province);
                hero.Zeal = 35f;
                hero.LastConversionDay = (int)Math.Floor(CampaignTime.Now.ToDays);
                hero.ConversionCount++;
                hero.Piety = Clamp(hero.Piety - 5f, 0f, 100f);
            }
            OpenMenu(args);
        }

        private void UndertakePilgrimage(MenuCallbackArgs args)
        {
            HolySiteDefinition site = FindCurrentHolySite();
            HeroReligionState hero = GetHeroState(Hero.MainHero);
            ProvincePopulationState province;
            if (site == null || hero == null || !site.FaithIds.Contains(hero.FaithId) || !PopulationService.TryGetState(Settlement.CurrentSettlement, out province)) return;
            HolySiteAccess access = GetHolySiteAccess(site.Id, hero.FaithId);
            int cost = access == HolySiteAccess.Restricted ? 2000 : 1000;
            int now = (int)Math.Floor(CampaignTime.Now.ToDays);
            if (access == HolySiteAccess.Closed || Hero.MainHero.Gold < cost || (hero.LastPilgrimageDay >= 0 && now - hero.LastPilgrimageDay < 365)) return;
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, true);
            hero.LastPilgrimageDay = now;
            int faith = ReligionCatalog.IndexOf(hero.FaithId);
            int institutionTier = (int)province.FaithInstitutionTiers[faith];
            hero.Piety = Clamp(hero.Piety + 8f + institutionTier * 2f, 0f, 100f);
            hero.Zeal = Clamp(hero.Zeal + 4f + institutionTier, 0f, 100f);
            province.ReligiousTension = Clamp(province.ReligiousTension - 3f, 0f, 100f);
            province.Happiness = Clamp(province.Happiness + 1f, 0f, 100f);
            province.FaithInstitutionStrengths[faith] = Clamp(province.FaithInstitutionStrengths[faith] + 2f, 0f, 100f);
            RealmReligionState realm = CurrentRealm();
            if (realm != null) realm.ClergyRelations = Clamp(realm.ClergyRelations + 2f, 0f, 100f);
            InformationManager.DisplayMessage(new InformationMessage("Your pilgrimage to " + site.Name + " is complete."));
            OpenMenu(args);
        }

        private void CycleHolyAccess(MenuCallbackArgs args)
        {
            HolySiteDefinition definition = FindCurrentHolySite();
            HolySiteState state;
            if (definition != null && _sites.TryGetValue(definition.Id, out state))
            {
                foreach (string faith in definition.FaithIds)
                {
                    int index = ReligionCatalog.IndexOf(faith);
                    state.AccessByFaith[index] = (HolySiteAccess)(((int)state.AccessByFaith[index] + 1) % 3);
                }
            }
            OpenMenu(args);
        }

        private void CycleAsharimAccess(MenuCallbackArgs args) { CycleFaithAccess(args, "asharim"); }
        private void CycleValeronistAccess(MenuCallbackArgs args) { CycleFaithAccess(args, "valeronism"); }
        private void CycleMaziristAccess(MenuCallbackArgs args) { CycleFaithAccess(args, "mazirism"); }

        private void CycleFaithAccess(MenuCallbackArgs args, string faithId)
        {
            HolySiteDefinition definition = FindCurrentHolySite();
            HolySiteState state;
            int index = ReligionCatalog.IndexOf(faithId);
            if (definition != null && definition.FaithIds.Contains(faithId) && index >= 0 && _sites.TryGetValue(definition.Id, out state))
                state.AccessByFaith[index] = (HolySiteAccess)(((int)state.AccessByFaith[index] + 1) % 3);
            OpenMenu(args);
        }

        private RealmReligionState CurrentRealm()
        {
            Settlement settlement = Settlement.CurrentSettlement;
            return GetRealmState(settlement == null || settlement.OwnerClan == null ? null : settlement.OwnerClan.Kingdom);
        }

        private bool IsPlayerRuler()
        {
            Settlement settlement = Settlement.CurrentSettlement;
            return settlement != null && settlement.OwnerClan != null && settlement.OwnerClan.Kingdom != null && settlement.OwnerClan.Kingdom.RulingClan == Clan.PlayerClan;
        }

        private static HolySiteDefinition FindCurrentHolySite()
        {
            Settlement settlement = Settlement.CurrentSettlement;
            return settlement == null ? null : ReligionCatalog.HolySites.FirstOrDefault(value => value.SettlementId == settlement.StringId);
        }

        private static int LargestOtherFaith(ProvincePopulationState province, int excluded)
        {
            int largest = -1;
            for (int index = 0; index < province.FaithPopulations.Length; index++)
                if (index != excluded && (largest < 0 || province.FaithPopulations[index] > province.FaithPopulations[largest])) largest = index;
            return largest;
        }

        private static string DominantFaith(ProvincePopulationState province)
        {
            int dominant = 0;
            for (int index = 1; index < province.FaithPopulations.Length; index++)
                if (province.FaithPopulations[index] > province.FaithPopulations[dominant]) dominant = index;
            return ReligionCatalog.FaithIds[dominant];
        }

        private static string PolicyName(CrownReligiousPolicy policy)
        {
            return policy == CrownReligiousPolicy.UniversalProtection ? "Universal Protection"
                : policy == CrownReligiousPolicy.TraditionalTolerance ? "Traditional Tolerance"
                : policy == CrownReligiousPolicy.OfficialSupremacy ? "Official Supremacy" : "Suppression";
        }

        private ClergyOfficeState GetOrCreateClergyOffice(Settlement settlement, ProvincePopulationState province)
        {
            if (settlement == null || province == null || settlement.IsVillage) return null;
            ClergyOfficeState office;
            if (_clergyOffices.TryGetValue(province.SettlementId, out office)) return office;
            string faith = DominantFaith(province);
            int index = ReligionCatalog.IndexOf(faith);
            if (index < 0 || province.FaithInstitutionTiers[index] < ReligiousInstitutionTier.Shrine) return null;
            office = new ClergyOfficeState(province.SettlementId, faith, SelectClergyHolderId(settlement));
            _clergyOffices.Add(province.SettlementId, office);
            return office;
        }

        private static string SelectClergyHolderId(Settlement settlement)
        {
            Hero notable = settlement == null ? null : settlement.Notables.FirstOrDefault(hero => hero != null && hero.IsAlive);
            if (notable != null) return notable.StringId;
            return settlement == null || settlement.OwnerClan == null || settlement.OwnerClan.Leader == null
                ? string.Empty : settlement.OwnerClan.Leader.StringId;
        }

        private static string ClergyHolderName(ClergyOfficeState office)
        {
            if (office == null || string.IsNullOrEmpty(office.HolderHeroId)) return "Vacant";
            Hero holder = Hero.AllAliveHeroes.FirstOrDefault(hero => hero != null && hero.StringId == office.HolderHeroId);
            return holder == null || holder.Name == null ? "Vacant" : holder.Name.ToString();
        }

        private static int InstitutionUpgradeCost(ReligiousInstitutionTier tier)
        {
            return tier == ReligiousInstitutionTier.None ? 5000
                : tier == ReligiousInstitutionTier.Shrine ? 15000
                : tier == ReligiousInstitutionTier.Temple ? 30000 : 0;
        }

        private static string InstitutionName(ReligiousInstitutionTier tier)
        {
            return tier == ReligiousInstitutionTier.Shrine ? "Shrine"
                : tier == ReligiousInstitutionTier.Temple ? "Temple"
                : tier == ReligiousInstitutionTier.GreatSanctuary ? "Great Sanctuary" : "No formal institution";
        }

        private static string ClergyGovernanceName(ClergyGovernancePolicy policy)
        {
            return policy == ClergyGovernancePolicy.ClericalAutonomy ? "Clerical Autonomy"
                : policy == ClergyGovernancePolicy.CrownConcordat ? "Crown Concordat" : "Crown Supervision";
        }

        private static bool IsLocalPolicyOwner()
        {
            Settlement settlement = Settlement.CurrentSettlement;
            return settlement != null && settlement.OwnerClan != null && (settlement.OwnerClan == Clan.PlayerClan
                || (settlement.OwnerClan.Kingdom != null && settlement.OwnerClan.Kingdom.RulingClan == Clan.PlayerClan));
        }

        private bool HasOpenRecognizedAccess(HolySiteDefinition site)
        {
            foreach (string faith in site.FaithIds) if (GetHolySiteAccess(site.Id, faith) == HolySiteAccess.Open) return true;
            return false;
        }

        private static bool IsPlayerRealm(Settlement settlement)
        {
            return settlement != null && settlement.OwnerClan != null && (settlement.OwnerClan == Clan.PlayerClan
                || (Clan.PlayerClan.Kingdom != null && settlement.OwnerClan.Kingdom == Clan.PlayerClan.Kingdom));
        }

        private static int StableRoll(string id, int month)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in id ?? string.Empty) hash = hash * 31 + character;
                hash = hash * 31 + month;
                return Math.Abs(hash % 100);
            }
        }

        private static int StableRollTenThousand(string id, int month)
        {
            unchecked
            {
                int hash = 23;
                foreach (char character in id ?? string.Empty) hash = hash * 37 + character;
                hash = hash * 37 + month;
                return Math.Abs(hash % 10000);
            }
        }

        private static string IncidentName(ReligiousIncidentType incident)
        {
            return incident == ReligiousIncidentType.PilgrimMarket ? "Pilgrim market"
                : incident == ReligiousIncidentType.InterfaithFestival ? "Interfaith festival"
                : incident == ReligiousIncidentType.ClericalDispute ? "Clerical dispute"
                : incident == ReligiousIncidentType.SuppressionResistance ? "Resistance to religious suppression"
                : incident == ReligiousIncidentType.SectarianViolence ? "Sectarian violence" : "None";
        }

        private static void OpenMenu(MenuCallbackArgs args) { GameMenu.SwitchToMenu(MenuId); }
        private static bool Always(MenuCallbackArgs args) { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; }
        private static void LeaveMenu(MenuCallbackArgs args) { GameMenu.ExitToLast(); }
        private static float Clamp(float value, float minimum, float maximum) { return Math.Max(minimum, Math.Min(maximum, value)); }
    }
}
