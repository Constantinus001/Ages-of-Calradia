using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace AgesOfCalradiaReligions
{
    public static class PopulationService
    {
        internal static PopulationCampaignBehavior ActiveBehavior { get; set; }

        public static PopulationSnapshot GetProvince(string settlementId)
        {
            ProvincePopulationState state;
            return ActiveBehavior != null && ActiveBehavior.TryGetState(settlementId, out state) ? new PopulationSnapshot(state) : null;
        }

        public static long GetCalradiaPopulation()
        {
            return ActiveBehavior == null ? 0L : ActiveBehavior.GetTotalPopulation();
        }

        public static int StrategicMapRevision
        {
            get { return ActiveBehavior == null ? 0 : ActiveBehavior.MapRevision; }
        }

        public static string GetStrategicMapSnapshotPayload()
        {
            return ActiveBehavior == null ? string.Empty : ActiveBehavior.GetStrategicMapSnapshotPayload();
        }

        public static string GetCensusSnapshotPayload()
        {
            return ActiveBehavior == null ? string.Empty : ActiveBehavior.GetCensusSnapshotPayload();
        }

        public static bool SetTaxPolicy(string settlementId, TaxPolicy policy)
        {
            return ActiveBehavior != null && ActiveBehavior.SetTaxPolicy(settlementId, policy);
        }

        public static bool SetConscriptionPolicy(string settlementId, ConscriptionPolicy policy)
        {
            return ActiveBehavior != null && ActiveBehavior.SetConscriptionPolicy(settlementId, policy);
        }

        internal static bool TryGetState(Settlement settlement, out ProvincePopulationState state)
        {
            state = null;
            return settlement != null && ActiveBehavior != null && ActiveBehavior.TryGetStateForSettlement(settlement, out state);
        }

        internal static double GetTaxMultiplier(string settlementId)
        {
            ProvincePopulationState state;
            return ActiveBehavior != null && ActiveBehavior.TryGetState(settlementId, out state) ? PopulationMath.GetTaxMultiplier(state.TaxPolicy) : 1d;
        }

        internal static float GetVolunteerFactor(Settlement settlement)
        {
            ProvincePopulationState state;
            if (settlement == null || ActiveBehavior == null || !ActiveBehavior.TryGetStateForSettlement(settlement, out state)) return 1f;
            long ceiling = PopulationMath.GetMobilizationCeiling(state);
            double reserve = ceiling <= 0 ? 0d : state.AvailableManpower / (double)ceiling;
            double policy = Math.Sqrt(PopulationMath.GetMobilizationShare(state.ConscriptionPolicy) / 0.01d);
            double happiness = 0.55d + state.Happiness / 180d;
            double urbanShare = state.TotalPopulation <= 0 ? 0d : state.UrbanPopulation / (double)state.TotalPopulation;
            float location = PopulationMath.GetRecruitmentLocationFactor(settlement.IsTown, settlement.IsVillage, urbanShare);
            return (float)Math.Max(0.10d, Math.Min(3d, reserve * policy * happiness * location));
        }

        internal static float GetArmySupportFactor(IFaction faction)
        {
            return ActiveBehavior == null ? 1f : ActiveBehavior.GetArmySupportFactor(faction);
        }

        internal static int GetGarrisonCapacity(Settlement settlement)
        {
            ProvincePopulationState state;
            return settlement != null && ActiveBehavior != null && ActiveBehavior.TryGetStateForSettlement(settlement, out state)
                ? PopulationMath.GetGarrisonCapacityGameTroops(state)
                : int.MaxValue;
        }
    }
}
