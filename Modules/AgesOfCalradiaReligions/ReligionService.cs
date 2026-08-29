using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace AgesOfCalradiaReligions
{
    public static class ReligionService
    {
        internal static ReligionCampaignBehavior ActiveBehavior { get; set; }

        public static HeroReligionState GetHeroReligion(Hero hero)
        {
            return ActiveBehavior == null ? null : ActiveBehavior.GetHeroState(hero);
        }

        public static RealmReligionState GetRealmReligion(Kingdom kingdom)
        {
            return ActiveBehavior == null ? null : ActiveBehavior.GetRealmState(kingdom);
        }

        public static HolySiteAccess GetHolySiteAccess(string siteId, string faithId)
        {
            return ActiveBehavior == null ? HolySiteAccess.Open : ActiveBehavior.GetHolySiteAccess(siteId, faithId);
        }

        internal static void ProcessMonthly(IEnumerable<ProvincePopulationState> states)
        {
            if (ActiveBehavior != null) ActiveBehavior.ProcessMonthly(states);
        }
    }
}
