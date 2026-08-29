using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace AgesOfCalradiaReligions
{
    // Native target: DefaultSettlementTaxModel.CalculateTownTax in Bannerlord 1.4.8.
    // Purpose: make the province's selected tax policy alter native tax income.
    // Risk: another mod may replace the model; in that case this postfix is skipped safely.
    [HarmonyPatch(typeof(DefaultSettlementTaxModel), "CalculateTownTax")]
    internal static class PopulationTownTaxPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Town __0, ref ExplainedNumber __result)
        {
            Town town = __0;
            if (town == null || town.Settlement == null) return;
            double multiplier = PopulationService.GetTaxMultiplier(town.Settlement.StringId);
            __result.AddFactor((float)(multiplier - 1d), new TextObject("Population tax policy"));
        }
    }

    // Native target: DefaultSettlementTaxModel.CalculateVillageTaxFromIncome.
    // Villages inherit the policy of their bound town or castle province.
    [HarmonyPatch(typeof(DefaultSettlementTaxModel), "CalculateVillageTaxFromIncome")]
    internal static class PopulationVillageTaxPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Village __0, ref int __result)
        {
            Village village = __0;
            if (village == null || village.Bound == null) return;
            __result = (int)System.Math.Round(__result * PopulationService.GetTaxMultiplier(village.Bound.StringId));
        }
    }

    // Native target: DefaultVolunteerModel.GetDailyVolunteerProductionProbability.
    // Recruitment slows when a province exhausts its demographic manpower pool.
    [HarmonyPatch(typeof(DefaultVolunteerModel), "GetDailyVolunteerProductionProbability")]
    internal static class PopulationVolunteerPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Settlement __2, ref float __result)
        {
            __result *= PopulationService.GetVolunteerFactor(__2);
        }
    }

    // Native target: DefaultPartySizeLimitModel.GetPartyMemberSizeLimit.
    // Lord-party capacity scales mildly with realm population and available manpower;
    // the postfix preserves every native leadership, clan-tier, and perk modifier.
    [HarmonyPatch(typeof(DefaultPartySizeLimitModel), "GetPartyMemberSizeLimit")]
    internal static class PopulationPartySizePatch
    {
        [HarmonyPostfix]
        private static void Postfix(PartyBase __0, ref ExplainedNumber __result)
        {
            PartyBase party = __0;
            if (party == null || party.LeaderHero == null || party.MapFaction == null) return;
            float factor = PopulationService.GetArmySupportFactor(party.MapFaction);
            __result.AddFactor(factor - 1f, new TextObject("Population and available manpower"));
        }
    }

    // Native target: DefaultPartySizeLimitModel.CalculateGarrisonPartySizeLimit.
    // The local province reserves 65% of mobilized manpower for its garrison.
    [HarmonyPatch(typeof(DefaultPartySizeLimitModel), "CalculateGarrisonPartySizeLimit")]
    internal static class PopulationGarrisonSizePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Settlement __0, ref ExplainedNumber __result)
        {
            Settlement settlement = __0;
            int capacity = PopulationService.GetGarrisonCapacity(settlement);
            if (capacity != int.MaxValue)
            {
                __result.LimitMax(capacity, new TextObject("Local population and garrison manpower"));
            }
        }
    }
}
