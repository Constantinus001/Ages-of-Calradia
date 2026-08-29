using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace AgesOfCalradiaSuccession
{
    /// <summary>
    /// Replaces only native kingdom ruler elections with deterministic hereditary
    /// resolution. It does not touch clan inheritance, politics UI, or map assets.
    /// </summary>
    public sealed class SuccessionCampaignBehavior : CampaignBehaviorBase
    {
        private const string StateKey = "AOC_Succession_State_v2";
        private const string PoliticsKey = "AOC_Succession_Politics_v1";
        private string _payload = string.Empty;
        private string _politicsPayload = string.Empty;
        private readonly Dictionary<string, string> _lawByKingdom = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _dynastyByKingdom = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _monarchByKingdom = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _minorHeirByKingdom = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _regentByKingdom = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _legitimacyByKingdom = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _coronatedByKingdom = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _accessionBasisByKingdom = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _accessionDayByKingdom = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _pretenderByKingdom = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _recognitionByRealmClan = new Dictionary<string, string>(StringComparer.Ordinal);

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.KingdomCreatedEvent.AddNonSerializedListener(this, OnKingdomCreated);
            CampaignEvents.KingdomDecisionAdded.AddNonSerializedListener(this, OnKingdomDecisionAdded);
            CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, OnHeroComesOfAge);
            SuccessionService.Attach(this);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
                _payload = SuccessionPersistence.Serialize(_lawByKingdom, _dynastyByKingdom, _monarchByKingdom, _minorHeirByKingdom, _regentByKingdom);
            dataStore.SyncData(StateKey, ref _payload);
            if (dataStore.IsLoading)
                SuccessionPersistence.Deserialize(_payload, _lawByKingdom, _dynastyByKingdom, _monarchByKingdom, _minorHeirByKingdom, _regentByKingdom);
            if (dataStore.IsSaving)
                _politicsPayload = SuccessionPoliticsPersistence.Serialize(_legitimacyByKingdom, _coronatedByKingdom,
                    _accessionBasisByKingdom, _accessionDayByKingdom, _pretenderByKingdom, _recognitionByRealmClan);
            dataStore.SyncData(PoliticsKey, ref _politicsPayload);
            if (dataStore.IsLoading)
                SuccessionPoliticsPersistence.Deserialize(_politicsPayload, _legitimacyByKingdom, _coronatedByKingdom,
                    _accessionBasisByKingdom, _accessionDayByKingdom, _pretenderByKingdom, _recognitionByRealmClan);
        }

        internal SuccessionLaw GetLaw(Kingdom kingdom)
        {
            if (kingdom == null) return SuccessionLaw.AbsolutePrimogeniture;
            string value;
            SuccessionLaw law;
            if (_lawByKingdom.TryGetValue(kingdom.StringId, out value) && Enum.TryParse(value, out law)) return law;
            law = SuccessionResolver.DefaultLawFor(kingdom);
            _lawByKingdom[kingdom.StringId] = law.ToString();
            return law;
        }

        internal IReadOnlyList<SuccessionClaim> GetClaimants(Kingdom kingdom)
        {
            Clan dynasty = FindClan(Get(_dynastyByKingdom, kingdom == null ? null : kingdom.StringId));
            Hero monarch = FindHero(Get(_monarchByKingdom, kingdom == null ? null : kingdom.StringId));
            return SuccessionResolver.Rank(kingdom, dynasty, monarch, GetLaw(kingdom));
        }

        internal Hero GetMinorHeir(Kingdom kingdom)
        {
            return FindHero(Get(_minorHeirByKingdom, kingdom == null ? null : kingdom.StringId));
        }

        internal Hero GetRegent(Kingdom kingdom)
        {
            return FindHero(Get(_regentByKingdom, kingdom == null ? null : kingdom.StringId));
        }

        internal float GetLegitimacy(Kingdom kingdom)
        {
            string value = Get(_legitimacyByKingdom, kingdom == null ? null : kingdom.StringId);
            float parsed;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : 50f;
        }

        internal bool IsCoronated(Kingdom kingdom)
        {
            return string.Equals(Get(_coronatedByKingdom, kingdom == null ? null : kingdom.StringId), "true", StringComparison.Ordinal);
        }

        internal Hero GetPretender(Kingdom kingdom)
        {
            return FindHero(Get(_pretenderByKingdom, kingdom == null ? null : kingdom.StringId));
        }

        internal ClanRecognition GetRecognition(Kingdom kingdom, Clan clan)
        {
            string value = Get(_recognitionByRealmClan, RecognitionKey(kingdom, clan));
            ClanRecognition parsed;
            return Enum.TryParse(value, out parsed) ? parsed : ClanRecognition.Neutral;
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            RunSafely("settlement debug menu registration", delegate { SuccessionDebugMenu.Register(starter, this); });
            RunSafely("coronation menu registration", delegate { SuccessionCoronationMenu.Register(starter, this); });
            RunSafely("kingdom snapshot initialization", EnsureKingdomSnapshots);
            RunSafely("political-state initialization", EnsurePoliticalStates);
            RunSafely("religious legitimacy startup audit", delegate
            {
                float playerLegitimacy = SuccessionReligionBridge.GetReligiousLegitimacy(Hero.MainHero);
                SuccessionDiagnostics.Info("Hereditary succession v0.4.3 initialized. Player religious legitimacy input="
                    + playerLegitimacy.ToString("0.0", CultureInfo.InvariantCulture) + ".");
            });
        }

        private void OnDailyTick()
        {
            RunSafely("daily regency audit", AuditRegencies);
            RunSafely("daily kingdom snapshot", EnsureKingdomSnapshots);
            RunSafely("daily political-state update", UpdatePoliticalStates);
        }

        private void OnKingdomCreated(Kingdom kingdom)
        {
            Snapshot(kingdom);
        }

        private void OnKingdomDecisionAdded(KingdomDecision decision, bool isPlayerInvolved)
        {
            KingSelectionKingdomDecision kingSelection = decision as KingSelectionKingdomDecision;
            Kingdom kingdom = kingSelection == null ? null : kingSelection.Kingdom;
            if (kingdom == null) return;

            string kingdomId = kingdom.StringId;
            Clan dynasty = FindClan(Get(_dynastyByKingdom, kingdomId));
            Hero previousMonarch = FindHero(Get(_monarchByKingdom, kingdomId));
            SuccessionLaw law = GetLaw(kingdom);
            Hero recordedMinor = FindHero(Get(_minorHeirByKingdom, kingdomId));
            if (IsUnderageHeir(recordedMinor))
            {
                AppointRegent(kingdom, recordedMinor, dynasty, kingSelection);
                return;
            }

            Hero dynasticHeir = SuccessionResolver.FindLawfulDynasticHeir(dynasty, previousMonarch, law);
            if (dynasticHeir != null && (dynasticHeir.Clan == null || dynasticHeir.Clan.Kingdom != kingdom))
            {
                SuccessionDiagnostics.Info("Dynastic claimant " + dynasticHeir.Name + " is outside " + kingdom.Name + "; foreign-clan transfer deferred.");
                dynasticHeir = null;
            }
            if (dynasticHeir != null)
            {
                if (dynasticHeir.Age < SuccessionResolver.AdultAge)
                {
                    _minorHeirByKingdom[kingdomId] = dynasticHeir.StringId;
                    BeginAccession(kingdom, dynasticHeir, "Regency", true);
                    AppointRegent(kingdom, dynasticHeir, dynasty, kingSelection);
                    return;
                }

                kingdom.RemoveDecision(kingSelection);
                CrownAdultHeir(kingdom, dynasticHeir, law, "lawful dynastic heir");
                return;
            }

            bool emergency = false;
            List<SuccessionClaim> claims = SuccessionResolver.Rank(kingdom, dynasty, previousMonarch, law);
            if (claims.Count == 0)
            {
                emergency = true;
                claims = SuccessionResolver.RankEmergency(kingdom, dynasty);
                SuccessionDiagnostics.Info("Normal claimant order exhausted for " + kingdom.Name + "; deterministic emergency order invoked.");
            }

            kingdom.RemoveDecision(kingSelection);
            if (claims.Count == 0)
            {
                SuccessionDiagnostics.Info("No living clan leader exists for " + kingdom.Name + "; ruler vote cancelled without a transfer target.");
                return;
            }

            SuccessionClaim heir = claims[0];
            if (kingdom.RulingClan != heir.Clan)
                ChangeRulingClanAction.Apply(kingdom, heir.Clan);

            _dynastyByKingdom[kingdomId] = heir.Clan.StringId;
            _monarchByKingdom[kingdomId] = heir.Hero.StringId;
            BeginAccession(kingdom, heir.Hero, emergency ? "Emergency" : "Collateral", false);
            string message = kingdom.Name + " passes by " + LawName(law) + " to " + heir.Hero.Name + ".";
            SuccessionDiagnostics.Info(message + " Basis: " + heir.Explanation + ". Native ruler vote cancelled.");
            InformationManager.DisplayMessage(new InformationMessage(message));
        }

        private void EnsureKingdomSnapshots()
        {
            foreach (Kingdom kingdom in Kingdom.All) Snapshot(kingdom);
        }

        private void Snapshot(Kingdom kingdom)
        {
            if (kingdom == null || kingdom.IsEliminated) return;
            string id = kingdom.StringId;
            if (!_lawByKingdom.ContainsKey(id)) _lawByKingdom[id] = SuccessionResolver.DefaultLawFor(kingdom).ToString();
            if (IsUnderageHeir(FindHero(Get(_minorHeirByKingdom, id)))) return;
            if (kingdom.RulingClan != null && kingdom.Leader != null && kingdom.Leader.IsAlive)
            {
                _dynastyByKingdom[id] = kingdom.RulingClan.StringId;
                _monarchByKingdom[id] = kingdom.Leader.StringId;
            }
        }

        private void AppointRegent(Kingdom kingdom, Hero heir, Clan dynasty, KingSelectionKingdomDecision decision)
        {
            List<SuccessionClaim> candidates = SuccessionResolver.Rank(kingdom, dynasty, FindHero(Get(_monarchByKingdom, kingdom.StringId)), GetLaw(kingdom));
            SuccessionClaim regentClaim = candidates.FirstOrDefault(c => c.Hero != heir && c.Hero.Age >= SuccessionResolver.AdultAge);
            if (regentClaim == null)
                regentClaim = SuccessionResolver.RankEmergency(kingdom, dynasty).FirstOrDefault(c => c.Hero != heir && c.Hero.Age >= SuccessionResolver.AdultAge);

            if (decision != null) kingdom.RemoveDecision(decision);
            if (regentClaim == null)
            {
                SuccessionDiagnostics.Info("No adult regent exists for underage heir " + heir.Name + " of " + kingdom.Name + ". Vote cancelled; regency remains vacant.");
                return;
            }

            if (kingdom.RulingClan != regentClaim.Clan) ChangeRulingClanAction.Apply(kingdom, regentClaim.Clan);
            _regentByKingdom[kingdom.StringId] = regentClaim.Hero.StringId;
            if (string.IsNullOrEmpty(Get(_accessionBasisByKingdom, kingdom.StringId)))
                BeginAccession(kingdom, heir, "Regency", true);
            else
                EvaluatePoliticalState(kingdom, heir);
            string message = regentClaim.Hero.Name + " becomes Regent of " + kingdom.Name + " for the underage heir " + heir.Name + ".";
            SuccessionDiagnostics.Info(message);
            InformationManager.DisplayMessage(new InformationMessage(message));
        }

        private void CrownAdultHeir(Kingdom kingdom, Hero heir, SuccessionLaw law, string basis)
        {
            if (kingdom == null || heir == null || heir.Clan == null || heir.Clan.Kingdom != kingdom || !heir.IsAlive || heir.Age < SuccessionResolver.AdultAge) return;
            if (heir.Clan.Leader != heir) ChangeClanLeaderAction.ApplyWithSelectedNewLeader(heir.Clan, heir);
            if (kingdom.RulingClan != heir.Clan) ChangeRulingClanAction.Apply(kingdom, heir.Clan);
            string id = kingdom.StringId;
            _dynastyByKingdom[id] = heir.Clan.StringId;
            _monarchByKingdom[id] = heir.StringId;
            _minorHeirByKingdom.Remove(id);
            _regentByKingdom.Remove(id);
            BeginAccession(kingdom, heir, "Dynastic", false);
            string message = heir.Name + " assumes the crown of " + kingdom.Name + " by " + LawName(law) + ".";
            SuccessionDiagnostics.Info(message + " Basis: " + basis + ".");
            InformationManager.DisplayMessage(new InformationMessage(message));
        }

        private void OnHeroComesOfAge(Hero hero)
        {
            if (hero == null) return;
            foreach (Kingdom kingdom in Kingdom.All.ToList())
            {
                if (Get(_minorHeirByKingdom, kingdom.StringId) == hero.StringId)
                {
                    CrownAdultHeir(kingdom, hero, GetLaw(kingdom), "completion of the lawful regency");
                    return;
                }
            }
        }

        private void AuditRegencies()
        {
            foreach (Kingdom kingdom in Kingdom.All.ToList())
            {
                string id = kingdom.StringId;
                string heirId = Get(_minorHeirByKingdom, id);
                if (string.IsNullOrEmpty(heirId)) continue;
                Hero heir = FindHero(heirId);
                if (heir != null && heir.IsAlive && heir.IsActive)
                {
                    if (heir.Age >= SuccessionResolver.AdultAge)
                        CrownAdultHeir(kingdom, heir, GetLaw(kingdom), "daily regency maturity audit");
                    else
                    {
                        Hero regent = FindHero(Get(_regentByKingdom, id));
                        if (regent == null || !regent.IsAlive || !regent.IsActive || kingdom.Leader != regent)
                            AppointRegent(kingdom, heir, FindClan(Get(_dynastyByKingdom, id)), null);
                    }
                    continue;
                }

                _minorHeirByKingdom.Remove(id);
                _regentByKingdom.Remove(id);
                Clan dynasty = FindClan(Get(_dynastyByKingdom, id));
                Hero next = SuccessionResolver.FindLawfulDynasticHeir(dynasty, FindHero(Get(_monarchByKingdom, id)), GetLaw(kingdom));
                if (next != null && (next.Clan == null || next.Clan.Kingdom != kingdom)) next = null;
                if (next != null && next.Age < SuccessionResolver.AdultAge)
                    _minorHeirByKingdom[id] = next.StringId;
                else if (next != null)
                    CrownAdultHeir(kingdom, next, GetLaw(kingdom), "replacement after the death of an underage heir");
            }
        }

        private static bool IsUnderageHeir(Hero hero)
        {
            return hero != null && hero.IsAlive && hero.IsActive && hero.Age < SuccessionResolver.AdultAge;
        }

        internal void HoldCoronation(Kingdom kingdom, Hero ruler, bool notify)
        {
            if (kingdom == null || ruler == null || kingdom.Leader != ruler || IsCoronated(kingdom)) return;
            _coronatedByKingdom[kingdom.StringId] = "true";
            EvaluatePoliticalState(kingdom, ruler);
            string message = ruler.Name + " is crowned ruler of " + kingdom.Name + ". Legitimacy is now "
                + GetLegitimacy(kingdom).ToString("0", CultureInfo.InvariantCulture) + ".";
            SuccessionDiagnostics.Info(message);
            if (notify) InformationManager.DisplayMessage(new InformationMessage(message));
        }

        internal void RegisterDebugCivilWar(Kingdom original, Kingdom claimantRealm, Hero pretender)
        {
            _dynastyByKingdom[claimantRealm.StringId] = pretender.Clan.StringId;
            _monarchByKingdom[claimantRealm.StringId] = pretender.StringId;
            _lawByKingdom[claimantRealm.StringId] = GetLaw(original).ToString();
            BeginAccession(claimantRealm, pretender, "Claimant", false);
            EvaluatePoliticalState(original, GetMinorHeir(original) ?? original.Leader);
        }

        internal List<Clan> GetCivilWarSupporters(Kingdom kingdom, Hero pretender)
        {
            List<Clan> supporters = kingdom == null ? new List<Clan>() : kingdom.Clans
                .Where(c => c != null && c != kingdom.RulingClan && !c.IsClanTypeMercenary && !c.IsMinorFaction
                    && GetRecognition(kingdom, c) == ClanRecognition.SupportsPretender)
                .ToList();
            if (pretender != null && pretender.Clan != null && pretender.Clan.Kingdom == kingdom && !supporters.Contains(pretender.Clan))
                supporters.Insert(0, pretender.Clan);
            return supporters;
        }

        private void EnsurePoliticalStates()
        {
            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom == null || kingdom.IsEliminated) continue;
                if (string.IsNullOrEmpty(Get(_accessionBasisByKingdom, kingdom.StringId)))
                {
                    _accessionBasisByKingdom[kingdom.StringId] = "Established";
                    _accessionDayByKingdom[kingdom.StringId] = CurrentDay.ToString(CultureInfo.InvariantCulture);
                    _coronatedByKingdom[kingdom.StringId] = "true";
                }
                EvaluatePoliticalState(kingdom, GetMinorHeir(kingdom) ?? kingdom.Leader);
            }
        }

        private void UpdatePoliticalStates()
        {
            foreach (Kingdom kingdom in Kingdom.All.ToList())
            {
                if (kingdom == null || kingdom.IsEliminated) continue;
                Hero subject = GetMinorHeir(kingdom) ?? kingdom.Leader;
                if (subject == null) continue;
                int accessionDay;
                int.TryParse(Get(_accessionDayByKingdom, kingdom.StringId), NumberStyles.Integer, CultureInfo.InvariantCulture, out accessionDay);
                if (GetMinorHeir(kingdom) == null && !IsCoronated(kingdom) && kingdom.Leader != Hero.MainHero && CurrentDay - accessionDay >= 7)
                    HoldCoronation(kingdom, kingdom.Leader, true);
                else
                    EvaluatePoliticalState(kingdom, subject);
            }
        }

        private void BeginAccession(Kingdom kingdom, Hero subject, string basis, bool regency)
        {
            if (kingdom == null || subject == null) return;
            string id = kingdom.StringId;
            _accessionBasisByKingdom[id] = basis;
            _accessionDayByKingdom[id] = CurrentDay.ToString(CultureInfo.InvariantCulture);
            _coronatedByKingdom[id] = "false";
            if (!regency) _regentByKingdom.Remove(id);
            EvaluatePoliticalState(kingdom, subject);
        }

        private void EvaluatePoliticalState(Kingdom kingdom, Hero subject)
        {
            if (kingdom == null || subject == null) return;
            string id = kingdom.StringId;
            string basis = Get(_accessionBasisByKingdom, id);
            float legitimacy;
            switch (basis)
            {
                case "Dynastic": legitimacy = 60f; break;
                case "Regency": legitimacy = 48f; break;
                case "Collateral": legitimacy = 42f; break;
                case "Claimant": legitimacy = 30f; break;
                case "Emergency": legitimacy = 25f; break;
                default: legitimacy = 65f; break;
            }

            if (subject.Culture == kingdom.Culture) legitimacy += 10f;
            string officialFaith = SuccessionReligionBridge.GetOfficialFaith(kingdom);
            string personalFaith = SuccessionReligionBridge.GetPersonalFaith(subject);
            if (!string.IsNullOrEmpty(officialFaith) && officialFaith == personalFaith) legitimacy += 10f;
            legitimacy += Math.Max(-10f, Math.Min(10f, (SuccessionReligionBridge.GetReligiousLegitimacy(subject) - 50f) * 0.2f));
            legitimacy += subject.Age < SuccessionResolver.AdultAge ? -15f : (subject.Age >= 25f ? 5f : 0f);
            if (IsCoronated(kingdom)) legitimacy += 15f;
            if (IsUnderageHeir(GetMinorHeir(kingdom))) legitimacy -= 5f;
            legitimacy = Math.Max(0f, Math.Min(100f, legitimacy));
            _legitimacyByKingdom[id] = legitimacy.ToString("0.0", CultureInfo.InvariantCulture);

            Hero pretender = SelectPretender(kingdom, subject, legitimacy);
            _pretenderByKingdom[id] = pretender == null ? string.Empty : pretender.StringId;
            foreach (Clan clan in kingdom.Clans)
            {
                if (clan == null || clan.IsClanTypeMercenary || clan.IsMinorFaction) continue;
                float support = legitimacy;
                if (subject.Clan == clan) support += 35f;
                if (clan.Culture == subject.Culture) support += 8f;
                Hero leader = clan.Leader;
                if (leader != null && !string.IsNullOrEmpty(officialFaith)
                    && SuccessionReligionBridge.GetPersonalFaith(leader) == officialFaith) support += 8f;
                if (subject.Clan != null)
                    support += Math.Max(-15f, Math.Min(15f, FactionManager.GetRelationBetweenClans(subject.Clan, clan) * 0.15f));
                support -= Math.Max(0, clan.Tier - 3) * 3f;
                ClanRecognition recognition = support >= 65f ? ClanRecognition.Recognized
                    : support >= 42f ? ClanRecognition.Neutral
                    : pretender != null ? ClanRecognition.SupportsPretender
                    : ClanRecognition.Opposed;
                _recognitionByRealmClan[RecognitionKey(kingdom, clan)] = recognition.ToString();
            }
        }

        private Hero SelectPretender(Kingdom kingdom, Hero subject, float legitimacy)
        {
            if (kingdom == null || legitimacy >= 80f) return null;
            return GetClaimants(kingdom)
                .Where(c => c.Hero != subject && c.Hero != GetRegent(kingdom) && c.Clan != kingdom.RulingClan && c.Clan.Tier >= 2)
                .Select(c => c.Hero)
                .FirstOrDefault();
        }

        internal Hero GetCivilWarPretender(Kingdom kingdom)
        {
            Hero pretender = GetPretender(kingdom);
            if (pretender != null && pretender.IsAlive && pretender.Clan != null && pretender.Clan.Kingdom == kingdom
                && pretender.Clan != kingdom.RulingClan) return pretender;
            return GetClaimants(kingdom).Where(c => c.Hero != kingdom.Leader && c.Clan != kingdom.RulingClan)
                .Select(c => c.Hero).FirstOrDefault();
        }

        private static string RecognitionKey(Kingdom kingdom, Clan clan)
        {
            return kingdom == null || clan == null ? string.Empty : kingdom.StringId + ":" + clan.StringId;
        }

        private static int CurrentDay { get { return (int)Math.Floor(CampaignTime.Now.ToDays); } }

        private static Clan FindClan(string id)
        {
            return string.IsNullOrEmpty(id) ? null : Clan.All.FirstOrDefault(c => c != null && c.StringId == id);
        }

        private static Hero FindHero(string id)
        {
            return string.IsNullOrEmpty(id) ? null : Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h.StringId == id)
                ?? Hero.DeadOrDisabledHeroes.FirstOrDefault(h => h != null && h.StringId == id);
        }

        private static string Get(IDictionary<string, string> values, string key)
        {
            string value;
            return key != null && values.TryGetValue(key, out value) ? value : string.Empty;
        }

        private static void RunSafely(string operation, Action action)
        {
            if (action == null) return;
            try
            {
                action();
            }
            catch (Exception exception)
            {
                SuccessionDiagnostics.Error(operation + " failed; the remaining succession systems will continue.", exception);
            }
        }

        private static string LawName(SuccessionLaw law)
        {
            switch (law)
            {
                case SuccessionLaw.MalePreferencePrimogeniture: return "male-preference primogeniture";
                case SuccessionLaw.AgnaticPrimogeniture: return "agnatic primogeniture";
                case SuccessionLaw.HouseSeniority: return "house seniority";
                case SuccessionLaw.NomadicHouseSeniority: return "nomadic house seniority";
                default: return "absolute primogeniture";
            }
        }
    }
}
