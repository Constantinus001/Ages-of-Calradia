using System;
using AgesOfCalradiaReligions;
using TaleWorlds.CampaignSystem;

namespace AgesOfCalradiaSuccession
{
    /// <summary>Read-only boundary between succession and religion ownership.</summary>
    internal static class SuccessionReligionBridge
    {
        internal static float GetReligiousLegitimacy(Hero hero)
        {
            if (hero == null) return 50f;
            try
            {
                HeroReligionState state = ReligionService.GetHeroReligion(hero);
                return state == null ? 50f : state.ReligiousLegitimacy;
            }
            catch (Exception exception)
            {
                SuccessionDiagnostics.Error("Religion legitimacy lookup failed; using neutral legitimacy.", exception);
                return 50f;
            }
        }

        internal static string GetPersonalFaith(Hero hero)
        {
            if (hero == null) return string.Empty;
            try
            {
                HeroReligionState state = ReligionService.GetHeroReligion(hero);
                return state == null ? string.Empty : state.FaithId;
            }
            catch (Exception exception)
            {
                SuccessionDiagnostics.Error("Personal-faith lookup failed; using no faith modifier.", exception);
                return string.Empty;
            }
        }

        internal static string GetOfficialFaith(Kingdom kingdom)
        {
            if (kingdom == null) return string.Empty;
            try
            {
                RealmReligionState state = ReligionService.GetRealmReligion(kingdom);
                return state == null ? string.Empty : state.OfficialFaithId;
            }
            catch (Exception exception)
            {
                SuccessionDiagnostics.Error("Official-faith lookup failed; using no faith modifier.", exception);
                return string.Empty;
            }
        }
    }
}
