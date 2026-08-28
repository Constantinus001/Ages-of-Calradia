using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace AgesOfCalradiaSuccession
{
    public static class SuccessionService
    {
        private static SuccessionCampaignBehavior _behavior;

        internal static void Attach(SuccessionCampaignBehavior behavior) { _behavior = behavior; }

        public static SuccessionLaw GetLaw(Kingdom kingdom)
        {
            return _behavior == null ? SuccessionResolver.DefaultLawFor(kingdom) : _behavior.GetLaw(kingdom);
        }

        public static IReadOnlyList<SuccessionClaim> GetClaimants(Kingdom kingdom)
        {
            return _behavior == null ? new List<SuccessionClaim>() : _behavior.GetClaimants(kingdom);
        }

        public static Hero GetUnderageHeir(Kingdom kingdom)
        {
            return _behavior == null ? null : _behavior.GetMinorHeir(kingdom);
        }

        public static Hero GetRegent(Kingdom kingdom)
        {
            return _behavior == null ? null : _behavior.GetRegent(kingdom);
        }

        public static float GetLegitimacy(Kingdom kingdom)
        {
            return _behavior == null ? 50f : _behavior.GetLegitimacy(kingdom);
        }

        public static bool IsCoronated(Kingdom kingdom)
        {
            return _behavior != null && _behavior.IsCoronated(kingdom);
        }

        public static Hero GetPretender(Kingdom kingdom)
        {
            return _behavior == null ? null : _behavior.GetPretender(kingdom);
        }

        public static ClanRecognition GetRecognition(Kingdom kingdom, Clan clan)
        {
            return _behavior == null ? ClanRecognition.Neutral : _behavior.GetRecognition(kingdom, clan);
        }
    }
}
