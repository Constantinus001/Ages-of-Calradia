using System;

namespace AgesOfCalradiaReligions
{
    public enum TaxPolicy
    {
        Relief = 0,
        Standard = 1,
        Heavy = 2,
        Extreme = 3
    }

    public enum ConscriptionPolicy
    {
        Volunteers = 0,
        LimitedLevy = 1,
        WartimeLevy = 2,
        HeavyLevy = 3,
        EmergencyMobilization = 4
    }

    public sealed class ProvincePopulationState
    {
        internal ProvincePopulationState(string settlementId)
        {
            SettlementId = settlementId ?? string.Empty;
            FaithPopulations = new long[ReligionCatalog.FaithIds.Count];
            FaithInstitutionStrengths = new float[ReligionCatalog.FaithIds.Count];
            FaithInstitutionTiers = new ReligiousInstitutionTier[ReligionCatalog.FaithIds.Count];
            Happiness = 60f;
            TaxPolicy = TaxPolicy.Standard;
            ConscriptionPolicy = ConscriptionPolicy.LimitedLevy;
        }

        public string SettlementId { get; internal set; }
        public long UrbanPopulation { get; internal set; }
        public long RuralPopulation { get; internal set; }
        public long PastoralPopulation { get; internal set; }
        public long InstitutionalPopulation { get; internal set; }
        public long CarryingCapacity { get; internal set; }
        public long AvailableManpower { get; internal set; }
        public long TownRecruitReserve { get; internal set; }
        public long LastMonthlyBirths { get; internal set; }
        public long LastMonthlyDeaths { get; internal set; }
        public long LastMonthlyMigrationNet { get; internal set; }
        public float Happiness { get; internal set; }
        public TaxPolicy TaxPolicy { get; internal set; }
        public ConscriptionPolicy ConscriptionPolicy { get; internal set; }
        internal long[] FaithPopulations { get; set; }
        internal float[] FaithInstitutionStrengths { get; set; }
        internal ReligiousInstitutionTier[] FaithInstitutionTiers { get; set; }
        public float ReligiousTension { get; internal set; }
        public long LastMonthlyConverts { get; internal set; }
        public ReligiousIncidentType LastReligiousIncident { get; internal set; }

        public float GetInstitutionStrength(string faithId)
        {
            int index = ReligionCatalog.IndexOf(faithId);
            return index < 0 || index >= FaithInstitutionStrengths.Length ? 0f : FaithInstitutionStrengths[index];
        }

        public ReligiousInstitutionTier GetInstitutionTier(string faithId)
        {
            int index = ReligionCatalog.IndexOf(faithId);
            return index < 0 || index >= FaithInstitutionTiers.Length ? ReligiousInstitutionTier.None : FaithInstitutionTiers[index];
        }

        public long TotalPopulation
        {
            get { return UrbanPopulation + RuralPopulation + PastoralPopulation + InstitutionalPopulation; }
        }

        public long GetFaithPopulation(string faithId)
        {
            int index = ReligionCatalog.IndexOf(faithId);
            return index < 0 || index >= FaithPopulations.Length ? 0L : FaithPopulations[index];
        }

        internal void AddPopulation(long amount)
        {
            if (amount == 0 || TotalPopulation <= 0)
            {
                RuralPopulation = Math.Max(0L, RuralPopulation + amount);
                return;
            }

            long total = TotalPopulation;
            long urban = amount * UrbanPopulation / total;
            long pastoral = amount * PastoralPopulation / total;
            long institutional = amount * InstitutionalPopulation / total;
            long rural = amount - urban - pastoral - institutional;
            UrbanPopulation = Math.Max(0L, UrbanPopulation + urban);
            RuralPopulation = Math.Max(0L, RuralPopulation + rural);
            PastoralPopulation = Math.Max(0L, PastoralPopulation + pastoral);
            InstitutionalPopulation = Math.Max(0L, InstitutionalPopulation + institutional);
        }
    }

    public sealed class PopulationSnapshot
    {
        internal PopulationSnapshot(ProvincePopulationState state)
        {
            SettlementId = state.SettlementId;
            TotalPopulation = state.TotalPopulation;
            Happiness = state.Happiness;
            AvailableManpower = state.AvailableManpower;
            TownRecruitReserve = state.TownRecruitReserve;
            LastMonthlyPopulationChange = state.LastMonthlyBirths - state.LastMonthlyDeaths + state.LastMonthlyMigrationNet;
            TaxPolicy = state.TaxPolicy;
            ConscriptionPolicy = state.ConscriptionPolicy;
        }

        public string SettlementId { get; private set; }
        public long TotalPopulation { get; private set; }
        public float Happiness { get; private set; }
        public long AvailableManpower { get; private set; }
        public long TownRecruitReserve { get; private set; }
        public long LastMonthlyPopulationChange { get; private set; }
        public TaxPolicy TaxPolicy { get; private set; }
        public ConscriptionPolicy ConscriptionPolicy { get; private set; }
    }
}
