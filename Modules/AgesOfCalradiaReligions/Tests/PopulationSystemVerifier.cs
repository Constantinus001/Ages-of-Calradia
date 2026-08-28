using System;
using System.Collections.Generic;
using System.Linq;

namespace AgesOfCalradiaReligions
{
    internal static class PopulationSystemVerifier
    {
        private static int Main()
        {
            try
            {
                VerifyBaselinePools();
                VerifyHighMedievalVitalRates();
                VerifyMobilizationCeilings();
                VerifyTownRecruitmentAdvantage();
                VerifyMonthlyInvariants();
                VerifyPersistenceRoundTrip();
                VerifyReligionPersistenceRoundTrip();
                VerifyReligionSimulationMath();
                VerifyCharacterReligionMath();
                Console.WriteLine("Population domain verification passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        }

        private static void VerifyCharacterReligionMath()
        {
            Require(ReligionCatalog.DefaultFaithForCulture("aserai") == "mazirism", "Aserai characters must start as Mazirists.");
            Require(ReligionCatalog.DefaultFaithForCulture("empire") == "valeronism", "Empire characters must start as Valeronists.");
            Require(ReligionCatalog.DefaultFaithForCulture("vlandia") == "valeronism", "Vlandian characters must start as Valeronists.");
            Require(ReligionCatalog.DefaultFaithForCulture("sturgia") == "veyrhold", "Sturgian characters must start with Veyrhold.");
            Require(ReligionCatalog.DefaultFaithForCulture("nord") == "veyrhold", "Nord characters must start with Veyrhold.");
            Require(ReligionCatalog.DefaultFaithForCulture("battania") == "caerwydd", "Battanian characters must start with Caerwydd.");
            Require(ReligionCatalog.DefaultFaithForCulture("khuzait") == "kok_orun_way", "Khuzait characters must start with the Kok-Orun Way.");
            Require(CharacterReligionMath.GetInheritedFaith("asharim", "asharim", "valeronism", 1) == "asharim", "Two same-faith parents must pass their faith to a child.");
            Require(CharacterReligionMath.GetInheritedFaith("asharim", "valeronism", "caerwydd", 0) == "asharim", "Even inheritance seed must select the mother's valid faith.");
            Require(CharacterReligionMath.GetInheritedFaith("asharim", "valeronism", "caerwydd", 1) == "valeronism", "Odd inheritance seed must select the father's valid faith.");
            double tolerant = CharacterReligionMath.GetMonthlyConversionChance(CrownReligiousPolicy.TraditionalTolerance, 50f, 50f, true, false, false);
            double suppressed = CharacterReligionMath.GetMonthlyConversionChance(CrownReligiousPolicy.Suppression, 50f, 50f, true, false, false);
            double ruler = CharacterReligionMath.GetMonthlyConversionChance(CrownReligiousPolicy.Suppression, 50f, 50f, true, true, false);
            Require(suppressed > tolerant, "Suppression must create more character conversion pressure than tolerance.");
            Require(ruler < suppressed, "Rulers must resist automatic conversion more strongly than ordinary nobles.");
            float legitimate = CharacterReligionMath.GetReligiousLegitimacy(50f, 70f, 80f, true, true, true, 0);
            float disputed = CharacterReligionMath.GetReligiousLegitimacy(10f, 20f, 30f, false, false, false, 3);
            Require(legitimate > disputed && legitimate <= 100f && disputed >= 0f, "Religious legitimacy calculation is not ordered or bounded correctly.");
        }

        private static void VerifyReligionSimulationMath()
        {
            long tolerantRelated = ReligionSimulationMath.GetMonthlyConversionCount(1000000L, 100000L, "asharim", "valeronism", CrownReligiousPolicy.TraditionalTolerance, 50f);
            long tolerantUnrelated = ReligionSimulationMath.GetMonthlyConversionCount(1000000L, 100000L, "caerwydd", "valeronism", CrownReligiousPolicy.TraditionalTolerance, 50f);
            long suppression = ReligionSimulationMath.GetMonthlyConversionCount(1000000L, 100000L, "asharim", "valeronism", CrownReligiousPolicy.Suppression, 50f);
            Require(tolerantRelated > tolerantUnrelated, "Related Aserac conversion should be easier than unrelated conversion.");
            Require(suppression > tolerantRelated, "Suppression must convert faster than tolerance while increasing tension.");
            Require(ReligionSimulationMath.GetMonthlyConversionCount(100000000L, 3L, "asharim", "valeronism", CrownReligiousPolicy.Suppression, 100f) == 3L, "Conversion exceeded the source cohort.");
            Require(ReligionSimulationMath.GetTensionTarget(50f, CrownReligiousPolicy.Suppression, 20f, true)
                > ReligionSimulationMath.GetTensionTarget(50f, CrownReligiousPolicy.UniversalProtection, 0f, false), "Suppression and blocked holy access must raise tension.");
        }

        private static void VerifyReligionPersistenceRoundTrip()
        {
            HeroReligionState original = new HeroReligionState("hero_test", "valeronism", 63.25f, 100)
            {
                Piety = 28.5f,
                LastPilgrimageDay = 240,
                BirthFaithId = "asharim",
                ConversionCount = 2,
                ReligiousLegitimacy = 77.75f
            };
            string payload = ReligionPersistence.SerializeHeroes(new[] { original });
            Dictionary<string, HeroReligionState> loaded;
            Require(ReligionPersistence.TryDeserializeHeroes(payload, out loaded), "Hero religion persistence did not round-trip.");
            HeroReligionState copy = loaded["hero_test"];
            Require(copy.FaithId == original.FaithId && Math.Abs(copy.Zeal - original.Zeal) < 0.001f, "Round-trip changed hero faith or zeal.");
            Require(Math.Abs(copy.Piety - original.Piety) < 0.001f && copy.LastPilgrimageDay == original.LastPilgrimageDay, "Round-trip changed piety or pilgrimage history.");
            Require(copy.BirthFaithId == "asharim" && copy.ConversionCount == 2 && Math.Abs(copy.ReligiousLegitimacy - 77.75f) < 0.001f, "Round-trip changed birth faith, conversion history, or legitimacy.");

            string legacy = "AOCHEROFAITH1\nhero_old|asharim|50000|-1";
            Require(ReligionPersistence.TryDeserializeHeroes(legacy, out loaded), "Legacy hero religion save did not migrate.");
            Require(loaded["hero_old"].Piety == 0f && loaded["hero_old"].LastPilgrimageDay == -1, "Legacy pilgrimage defaults are invalid.");
            string versionTwo = "AOCHEROFAITH2\nhero_v2|valeronism|50000|-1|12000|300";
            Require(ReligionPersistence.TryDeserializeHeroes(versionTwo, out loaded), "AOCHEROFAITH2 save did not migrate.");
            Require(loaded["hero_v2"].BirthFaithId == "valeronism" && loaded["hero_v2"].ConversionCount == 0 && loaded["hero_v2"].ReligiousLegitimacy == 50f, "AOCHEROFAITH2 character defaults are invalid.");

            RealmReligionState realm = new RealmReligionState("kingdom_test", "valeronism")
            {
                Policy = CrownReligiousPolicy.OfficialSupremacy,
                ClergyGovernance = ClergyGovernancePolicy.CrownSupervision,
                ClergyRelations = 41.5f,
                ReligiousUnity = 72.25f
            };
            string realmPayload = ReligionPersistence.SerializeRealms(new[] { realm });
            Dictionary<string, RealmReligionState> realms;
            Require(ReligionPersistence.TryDeserializeRealms(realmPayload, out realms), "Realm clergy governance did not round-trip.");
            Require(realms["kingdom_test"].ClergyGovernance == ClergyGovernancePolicy.CrownSupervision, "Round-trip changed clergy governance.");

            ClergyOfficeState office = new ClergyOfficeState("town_test", "valeronism", "hero_priest") { Treasury = 4321L, LastClergyTaxDay = 220 };
            string officePayload = ReligionPersistence.SerializeClergyOffices(new[] { office });
            Dictionary<string, ClergyOfficeState> offices;
            Require(ReligionPersistence.TryDeserializeClergyOffices(officePayload, out offices), "Clergy office persistence did not round-trip.");
            Require(offices["town_test"].HolderHeroId == "hero_priest" && offices["town_test"].Treasury == 4321L && offices["town_test"].LastClergyTaxDay == 220, "Round-trip changed clergy office state.");
        }

        private static void VerifyHighMedievalVitalRates()
        {
            Require(Math.Abs(PopulationMath.AnnualCrudeBirthRate - 0.045d) < 0.000001d, "Annual births must be 45 per 1,000 at baseline.");
            Require(Math.Abs(PopulationMath.AnnualCrudeDeathRate - 0.042d) < 0.000001d, "Annual deaths must be 42 per 1,000 at baseline.");
            double naturalGrowth = PopulationMath.AnnualCrudeBirthRate - PopulationMath.AnnualCrudeDeathRate;
            Require(naturalGrowth >= 0.001d && naturalGrowth <= 0.005d, "Baseline natural growth must remain within the designed medieval range.");
        }

        private static void VerifyTownRecruitmentAdvantage()
        {
            float village = PopulationMath.GetRecruitmentLocationFactor(false, true, 0d);
            float ordinaryTown = PopulationMath.GetRecruitmentLocationFactor(true, false, 0.20d);
            float majorCity = PopulationMath.GetRecruitmentLocationFactor(true, false, 0.75d);
            Require(ordinaryTown > village, "Towns must replenish recruits faster than villages.");
            Require(majorCity > ordinaryTown, "Major urban regions must replenish recruits faster than ordinary towns.");
            Require(majorCity <= 2.25f, "The town recruitment bonus exceeded its designed ceiling.");
        }

        private static void VerifyBaselinePools()
        {
            long total = PopulationMath.MajorUrbanPopulation + PopulationMath.OtherUrbanPopulation + PopulationMath.RuralPopulation + PopulationMath.PastoralPopulation + PopulationMath.InstitutionalPopulation;
            Require(total == 61000000L, "The population pools must reconcile to exactly 61,000,000.");
            Require(PopulationMath.MajorUrbanPopulation == total / 4L, "The 20 major urban regions must hold exactly 25% of the baseline.");
            Require(Math.Abs(PopulationMath.GarrisonMobilizationShare + PopulationMath.FieldArmyMobilizationShare - 1d) < 0.000001d, "Garrison and field allocations must reconcile to 100%.");
            Require(PopulationMath.GarrisonMobilizationShare > 0.5d, "Garrisons must receive most mobilized soldiers.");
        }

        private static void VerifyMobilizationCeilings()
        {
            ProvincePopulationState state = CreateState(1000000L);
            long[] expected = { 2500L, 10000L, 25000L, 50000L, 100000L };
            for (int index = 0; index < expected.Length; index++)
            {
                state.ConscriptionPolicy = (ConscriptionPolicy)index;
                Require(PopulationMath.GetMobilizationCeiling(state) == expected[index], "Unexpected conscription ceiling for policy " + index + ".");
            }

            Require(PopulationMath.GetMobilizationShare(ConscriptionPolicy.EmergencyMobilization) == 0.10d, "Emergency mobilization must be capped at exactly 10%.");
            state.AvailableManpower = expected[1];
            state.ConscriptionPolicy = ConscriptionPolicy.LimitedLevy;
            int limitedGarrison = PopulationMath.GetGarrisonCapacityGameTroops(state);
            state.AvailableManpower = expected[4];
            state.ConscriptionPolicy = ConscriptionPolicy.EmergencyMobilization;
            int emergencyGarrison = PopulationMath.GetGarrisonCapacityGameTroops(state);
            Require(limitedGarrison > 0 && emergencyGarrison > limitedGarrison, "Garrison capacity must grow with literal available manpower.");
            Require(emergencyGarrison < expected[4], "Logistics must prevent the entire manpower pool from occupying one garrison.");
        }

        private static void VerifyMonthlyInvariants()
        {
            ProvincePopulationState state = CreateState(1000000L);
            state.AvailableManpower = PopulationMath.GetMobilizationCeiling(state);
            PopulationMath.AdvanceMonth(state, false, false);
            Require(state.TotalPopulation > 0L, "Monthly simulation produced a non-positive population.");
            Require(state.Happiness >= 0f && state.Happiness <= 100f, "Happiness escaped its 0-100 bounds.");
            Require(state.AvailableManpower <= PopulationMath.GetMobilizationCeiling(state), "Manpower exceeded the conscription ceiling.");
            Require(FaithTotal(state) == state.TotalPopulation, "Faith cohorts no longer reconcile with total population.");

            PopulationMath.AdvanceMonth(state, true, true);
            Require(state.UrbanPopulation >= 0L && state.RuralPopulation >= 0L && state.PastoralPopulation >= 0L && state.InstitutionalPopulation >= 0L, "A crisis produced a negative cohort.");
            Require(FaithTotal(state) == state.TotalPopulation, "Faith cohorts failed to reconcile after a crisis month.");
        }

        private static void VerifyPersistenceRoundTrip()
        {
            ProvincePopulationState original = CreateState(1000000L);
            original.Happiness = 43.275f;
            original.TaxPolicy = TaxPolicy.Heavy;
            original.ConscriptionPolicy = ConscriptionPolicy.WartimeLevy;
            original.AvailableManpower = 12345L;
            original.TownRecruitReserve = 678L;
            original.LastMonthlyBirths = 3750L;
            original.LastMonthlyDeaths = 3500L;
            original.LastMonthlyMigrationNet = -125L;
            original.ReligiousTension = 37.5f;
            original.LastMonthlyConverts = 42L;
            original.LastReligiousIncident = ReligiousIncidentType.ClericalDispute;
            original.FaithInstitutionStrengths[ReligionCatalog.IndexOf("valeronism")] = 76.25f;
            original.FaithInstitutionTiers[ReligionCatalog.IndexOf("valeronism")] = ReligiousInstitutionTier.Temple;
            string serialized = PopulationPersistence.Serialize(new[] { original });
            Dictionary<string, ProvincePopulationState> loaded;
            Require(PopulationPersistence.TryDeserialize(serialized, out loaded), "Population persistence did not round-trip.");
            ProvincePopulationState copy = loaded[original.SettlementId];
            Require(copy.TotalPopulation == original.TotalPopulation, "Round-trip changed total population.");
            Require(copy.AvailableManpower == original.AvailableManpower, "Round-trip changed manpower.");
            Require(copy.TownRecruitReserve == original.TownRecruitReserve, "Round-trip changed the town recruit reserve.");
            Require(copy.LastMonthlyBirths == original.LastMonthlyBirths && copy.LastMonthlyDeaths == original.LastMonthlyDeaths, "Round-trip changed monthly vital statistics.");
            Require(copy.LastMonthlyMigrationNet == original.LastMonthlyMigrationNet, "Round-trip changed monthly migration.");
            Require(Math.Abs(copy.ReligiousTension - original.ReligiousTension) < 0.001f, "Round-trip changed religious tension.");
            Require(copy.LastMonthlyConverts == original.LastMonthlyConverts, "Round-trip changed monthly converts.");
            Require(copy.LastReligiousIncident == original.LastReligiousIncident, "Round-trip changed the religious incident.");
            Require(Math.Abs(copy.GetInstitutionStrength("valeronism") - 76.25f) < 0.001f, "Round-trip changed clergy institution strength.");
            Require(copy.GetInstitutionTier("valeronism") == ReligiousInstitutionTier.Temple, "Round-trip changed the religious institution tier.");
            Require(copy.TaxPolicy == original.TaxPolicy && copy.ConscriptionPolicy == original.ConscriptionPolicy, "Round-trip changed policy.");
            Require(FaithTotal(copy) == copy.TotalPopulation, "Round-trip changed faith totals.");

            string[] lines = serialized.Split('\n');
            string[] fields = lines[1].Split('|');
            string versionFour = "AOCPOP4\n" + string.Join("|", fields.Take(fields.Length - ReligionCatalog.FaithIds.Count));
            Require(PopulationPersistence.TryDeserialize(versionFour, out loaded), "AOCPOP4 save did not migrate to institution tiers.");
            Require(loaded[original.SettlementId].GetInstitutionTier("valeronism") == ReligiousInstitutionTier.Temple, "AOCPOP4 institution tier inference was incorrect.");
        }

        private static ProvincePopulationState CreateState(long total)
        {
            ProvincePopulationState state = new ProvincePopulationState("town_test")
            {
                UrbanPopulation = total / 4L,
                RuralPopulation = total / 2L,
                PastoralPopulation = total / 8L,
                InstitutionalPopulation = total - total / 4L - total / 2L - total / 8L,
                CarryingCapacity = total * 2L,
                Happiness = 60f
            };
            state.FaithPopulations[ReligionCatalog.IndexOf("valeronism")] = total * 8L / 10L;
            state.FaithPopulations[ReligionCatalog.IndexOf("calradic_old_faith")] = total - total * 8L / 10L;
            return state;
        }

        private static long FaithTotal(ProvincePopulationState state)
        {
            long total = 0L;
            foreach (long population in state.FaithPopulations) total += population;
            return total;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
