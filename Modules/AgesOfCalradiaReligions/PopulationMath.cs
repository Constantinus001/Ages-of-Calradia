using System;

namespace AgesOfCalradiaReligions
{
    internal static class PopulationMath
    {
        internal const long CalradiaBaselinePopulation = 61000000L;
        internal const long MajorUrbanPopulation = 15250000L;
        internal const long OtherUrbanPopulation = 3750000L;
        internal const long RuralPopulation = 36500000L;
        internal const long PastoralPopulation = 4200000L;
        internal const long InstitutionalPopulation = 1300000L;
        internal const double GarrisonMobilizationShare = 0.65d;
        internal const double FieldArmyMobilizationShare = 0.35d;
        internal const double AnnualCrudeBirthRate = 0.045d;
        internal const double AnnualCrudeDeathRate = 0.042d;

        internal static double GetTaxMultiplier(TaxPolicy policy)
        {
            switch (policy)
            {
                case TaxPolicy.Relief: return 0.75d;
                case TaxPolicy.Heavy: return 1.25d;
                case TaxPolicy.Extreme: return 1.5d;
                default: return 1d;
            }
        }

        internal static double GetMobilizationShare(ConscriptionPolicy policy)
        {
            switch (policy)
            {
                case ConscriptionPolicy.Volunteers: return 0.0025d;
                case ConscriptionPolicy.LimitedLevy: return 0.01d;
                case ConscriptionPolicy.WartimeLevy: return 0.025d;
                case ConscriptionPolicy.HeavyLevy: return 0.05d;
                case ConscriptionPolicy.EmergencyMobilization: return 0.10d;
                default: return 0.01d;
            }
        }

        internal static long GetMobilizationCeiling(ProvincePopulationState state)
        {
            return Math.Max(0L, (long)Math.Floor(state.TotalPopulation * GetMobilizationShare(state.ConscriptionPolicy)));
        }

        internal static PopulationMonthResult AdvanceMonth(ProvincePopulationState state, bool foodCrisis, bool warDamage)
        {
            double policyPressure = GetTaxHappinessPressure(state.TaxPolicy) + GetConscriptionHappinessPressure(state.ConscriptionPolicy);
            double targetHappiness = 64d + policyPressure - (foodCrisis ? 24d : 0d) - (warDamage ? 18d : 0d);
            state.Happiness = Clamp((float)(state.Happiness + (targetHappiness - state.Happiness) * 0.18d), 0f, 100f);

            long total = state.TotalPopulation;
            double happinessFactor = 0.7d + state.Happiness / 225d;
            long births = (long)Math.Floor(total * (AnnualCrudeBirthRate / 12d) * happinessFactor);
            double deathRate = AnnualCrudeDeathRate / 12d + (foodCrisis ? 0.0035d : 0d) + (warDamage ? 0.0015d : 0d);
            if (state.CarryingCapacity > 0 && total > state.CarryingCapacity)
            {
                deathRate += Math.Min(0.003d, (total - state.CarryingCapacity) / (double)state.CarryingCapacity * 0.002d);
            }

            long deaths = (long)Math.Floor(total * deathRate);
            state.AddPopulation(births - deaths);
            ReconcileFaithTotals(state, births - deaths);

            long ceiling = GetMobilizationCeiling(state);
            long monthlyRecovery = Math.Max(1L, (long)Math.Floor(state.TotalPopulation * 0.00045d));
            state.AvailableManpower = Math.Min(ceiling, Math.Max(0L, state.AvailableManpower + monthlyRecovery));
            return new PopulationMonthResult(births, deaths);
        }

        internal static long GetTownRecruitReserveCapacity(ProvincePopulationState state)
        {
            if (state == null || state.UrbanPopulation <= 0) return 0L;
            double happinessFactor = 0.55d + state.Happiness / 150d;
            double policyFactor = Math.Sqrt(GetMobilizationShare(state.ConscriptionPolicy) / 0.01d);
            return Math.Max(25L, (long)Math.Floor(Math.Sqrt(state.UrbanPopulation) * 0.55d * happinessFactor * policyFactor));
        }

        internal static long GetMonthlyTownRecruitRecovery(ProvincePopulationState state)
        {
            long capacity = GetTownRecruitReserveCapacity(state);
            return capacity <= 0 ? 0L : Math.Max(2L, capacity / 12L);
        }

        internal static float GetArmySupportFactor(long kingdomPopulation, long availableManpower, long mobilizationCeiling, int activeLordParties)
        {
            if (kingdomPopulation <= 0 || mobilizationCeiling <= 0)
            {
                return 0.55f;
            }

            // Manpower is literal people. Operational army size grows by the
            // square root because officers, supply, transport, and cohesion—not
            // an artificial people-per-model ratio—limit how many can campaign.
            double operationalTroops = Math.Sqrt(Math.Min(availableManpower, mobilizationCeiling)) * 8d;
            double troopsPerParty = operationalTroops / Math.Max(1, activeLordParties);
            double allocationFactor = troopsPerParty / 120d;
            double populationFactor = Math.Pow(kingdomPopulation / 6800000d, 0.12d);
            double reserveFactor = 0.55d + 0.45d * Math.Min(1d, availableManpower / (double)mobilizationCeiling);
            return Clamp((float)(allocationFactor * populationFactor * reserveFactor), 0.55f, 1.75f);
        }

        internal static int GetGarrisonCapacityGameTroops(ProvincePopulationState state)
        {
            long demographicCeiling = (long)Math.Floor(GetMobilizationCeiling(state) * GarrisonMobilizationShare);
            long supportedPeople = Math.Min(demographicCeiling, state.AvailableManpower);
            double happinessFactor = 0.6d + state.Happiness / 250d;
            return Math.Max(20, (int)Math.Floor(Math.Sqrt(supportedPeople) * 3d * happinessFactor));
        }

        internal static float GetRecruitmentLocationFactor(bool isTown, bool isVillage, double urbanPopulationShare)
        {
            if (isTown)
            {
                // Dense towns contain more households, tradespeople, migrants,
                // and recruiting notables. Major urban regions therefore refill
                // their volunteer rosters faster than smaller towns.
                return Clamp((float)(1.5d + 0.75d * Math.Max(0d, Math.Min(1d, urbanPopulationShare))), 1.5f, 2.25f);
            }

            // Villages retain most workers for agriculture. Castles use the
            // neutral factor because their soldiers are governed primarily by
            // the separate garrison allocation.
            return isVillage ? 0.75f : 1f;
        }

        internal static double GetTaxHappinessPressure(TaxPolicy policy)
        {
            switch (policy)
            {
                case TaxPolicy.Relief: return 8d;
                case TaxPolicy.Heavy: return -9d;
                case TaxPolicy.Extreme: return -19d;
                default: return 0d;
            }
        }

        internal static double GetConscriptionHappinessPressure(ConscriptionPolicy policy)
        {
            switch (policy)
            {
                case ConscriptionPolicy.Volunteers: return 5d;
                case ConscriptionPolicy.WartimeLevy: return -8d;
                case ConscriptionPolicy.HeavyLevy: return -18d;
                case ConscriptionPolicy.EmergencyMobilization: return -35d;
                default: return 0d;
            }
        }

        private static void ReconcileFaithTotals(ProvincePopulationState state, long populationDelta)
        {
            long faithTotal = 0L;
            for (int index = 0; index < state.FaithPopulations.Length; index++) faithTotal += state.FaithPopulations[index];
            if (faithTotal <= 0)
            {
                state.FaithPopulations[ReligionCatalog.IndexOf("calradic_old_faith")] = state.TotalPopulation;
                return;
            }

            long assigned = 0L;
            for (int index = 0; index < state.FaithPopulations.Length - 1; index++)
            {
                long share = populationDelta * state.FaithPopulations[index] / faithTotal;
                state.FaithPopulations[index] = Math.Max(0L, state.FaithPopulations[index] + share);
                assigned += share;
            }

            int last = state.FaithPopulations.Length - 1;
            state.FaithPopulations[last] = Math.Max(0L, state.FaithPopulations[last] + populationDelta - assigned);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    internal sealed class PopulationMonthResult
    {
        internal PopulationMonthResult(long births, long deaths)
        {
            Births = births;
            Deaths = deaths;
        }

        internal long Births { get; private set; }
        internal long Deaths { get; private set; }
    }
}
