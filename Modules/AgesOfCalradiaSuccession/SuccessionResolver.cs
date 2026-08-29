using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace AgesOfCalradiaSuccession
{
    internal static class SuccessionResolver
    {
        internal const float AdultAge = 18f;

        internal static SuccessionLaw DefaultLawFor(Kingdom kingdom)
        {
            string culture = kingdom == null || kingdom.Culture == null
                ? string.Empty
                : (kingdom.Culture.StringId ?? string.Empty).ToLowerInvariant();

            if (culture.Contains("aserai")) return SuccessionLaw.AgnaticPrimogeniture;
            if (culture.Contains("vland")) return SuccessionLaw.MalePreferencePrimogeniture;
            if (culture.Contains("battan")) return SuccessionLaw.HouseSeniority;
            if (culture.Contains("sturg") || culture.Contains("nord")) return SuccessionLaw.HouseSeniority;
            if (culture.Contains("khuz") || culture.Contains("steppe")) return SuccessionLaw.NomadicHouseSeniority;
            return SuccessionLaw.AbsolutePrimogeniture;
        }

        internal static List<SuccessionClaim> Rank(Kingdom kingdom, Clan dynasty, Hero previousMonarch, SuccessionLaw law)
        {
            List<SuccessionClaim> claims = new List<SuccessionClaim>();
            if (kingdom == null) return claims;

            foreach (Clan clan in kingdom.Clans)
            {
                Hero hero = clan == null ? null : clan.Leader;
                if (!IsEligible(clan, hero, law)) continue;

                int kinship = KinshipScore(hero, previousMonarch);
                bool sameHouse = dynasty != null && clan == dynasty;
                int score = BaseLawScore(hero, law) + kinship + (sameHouse ? 100000 : 0);
                score += (int)Math.Round(SuccessionReligionBridge.GetReligiousLegitimacy(hero) * 10f);

                string reason = sameHouse
                    ? "lawful ruling house; " + RelationshipText(kinship)
                    : RelationshipText(kinship);
                reason += "; religious legitimacy " + SuccessionReligionBridge.GetReligiousLegitimacy(hero).ToString("0");
                claims.Add(new SuccessionClaim(hero, clan, score, reason));
            }

            return claims
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.Hero.StringId, StringComparer.Ordinal)
                .ToList();
        }

        internal static List<SuccessionClaim> RankEmergency(Kingdom kingdom, Clan dynasty)
        {
            List<SuccessionClaim> claims = new List<SuccessionClaim>();
            if (kingdom == null) return claims;
            foreach (Clan clan in kingdom.Clans)
            {
                Hero hero = clan == null ? null : clan.Leader;
                if (clan == null || hero == null || clan.IsEliminated || !hero.IsAlive || !hero.IsActive) continue;
                int score = (clan == dynasty ? 100000 : 0)
                    + (hero.Age >= AdultAge ? 10000 : 0)
                    + Math.Max(0, clan.Tier) * 500
                    + (int)Math.Round(Math.Max(0f, clan.Renown));
                claims.Add(new SuccessionClaim(hero, clan, score,
                    clan == dynasty ? "emergency continuation of the ruling house" : "deterministic emergency acclamation"));
            }
            return claims.OrderByDescending(c => c.Score).ThenBy(c => c.Hero.StringId, StringComparer.Ordinal).ToList();
        }

        internal static Hero FindLawfulDynasticHeir(Clan dynasty, Hero previousMonarch, SuccessionLaw law)
        {
            if (dynasty == null) return null;
            if (law == SuccessionLaw.HouseSeniority || law == SuccessionLaw.NomadicHouseSeniority)
            {
                IOrderedEnumerable<Hero> seniority;
                if (law == SuccessionLaw.NomadicHouseSeniority)
                    seniority = dynasty.Heroes.Where(h => IsLivingDynast(h) && h != previousMonarch).OrderBy(h => h.IsFemale).ThenByDescending(h => h.Age);
                else
                    seniority = dynasty.Heroes.Where(h => IsLivingDynast(h) && h != previousMonarch).OrderByDescending(h => h.Age);
                return seniority.ThenBy(h => h.StringId, StringComparer.Ordinal).FirstOrDefault();
            }

            if (previousMonarch != null)
            {
                HashSet<Hero> visited = new HashSet<Hero>();
                Hero descendant = FirstLivingInBranches(OrderedChildren(previousMonarch, law), law, visited);
                if (descendant != null) return descendant;

                IEnumerable<Hero> siblings = dynasty.Heroes
                    .Where(h => h != null && h != previousMonarch && ShareParent(h, previousMonarch));
                siblings = OrderByPreference(siblings, law);
                foreach (Hero sibling in siblings)
                {
                    if (GenderAllowed(sibling, law) && IsLivingDynast(sibling)) return sibling;
                    Hero branch = FirstLivingInBranches(OrderedChildren(sibling, law), law, visited);
                    if (branch != null) return branch;
                }
            }

            return OrderByPreference(dynasty.Heroes.Where(h => IsLivingDynast(h) && h != previousMonarch), law)
                .FirstOrDefault(h => GenderAllowed(h, law));
        }

        private static Hero FirstLivingInBranches(IEnumerable<Hero> branches, SuccessionLaw law, HashSet<Hero> visited)
        {
            foreach (Hero child in branches)
            {
                if (child == null || !visited.Add(child)) continue;
                if (GenderAllowed(child, law) && IsLivingDynast(child)) return child;
                if (law == SuccessionLaw.AgnaticPrimogeniture && child.IsFemale) continue;
                Hero descendant = FirstLivingInBranches(OrderedChildren(child, law), law, visited);
                if (descendant != null) return descendant;
            }
            return null;
        }

        private static IEnumerable<Hero> OrderedChildren(Hero parent, SuccessionLaw law)
        {
            return parent == null ? Enumerable.Empty<Hero>() : OrderByPreference(parent.Children, law);
        }

        private static IEnumerable<Hero> OrderByPreference(IEnumerable<Hero> heroes, SuccessionLaw law)
        {
            IEnumerable<Hero> filtered = heroes.Where(h => h != null);
            if (law == SuccessionLaw.MalePreferencePrimogeniture || law == SuccessionLaw.AgnaticPrimogeniture || law == SuccessionLaw.NomadicHouseSeniority)
                return filtered.OrderBy(h => h.IsFemale).ThenByDescending(h => h.Age).ThenBy(h => h.StringId, StringComparer.Ordinal);
            return filtered.OrderByDescending(h => h.Age).ThenBy(h => h.StringId, StringComparer.Ordinal);
        }

        private static bool GenderAllowed(Hero hero, SuccessionLaw law)
        {
            return hero != null && (law != SuccessionLaw.AgnaticPrimogeniture || !hero.IsFemale);
        }

        private static bool IsLivingDynast(Hero hero)
        {
            return hero != null && hero.IsAlive && hero.IsActive && hero.IsLord;
        }

        private static bool IsEligible(Clan clan, Hero hero, SuccessionLaw law)
        {
            if (clan == null || hero == null || clan.IsEliminated || clan.IsMinorFaction || clan.IsClanTypeMercenary) return false;
            if (!hero.IsAlive || !hero.IsActive || hero.Age < AdultAge) return false;
            if (law == SuccessionLaw.AgnaticPrimogeniture && hero.IsFemale) return false;
            return true;
        }

        private static int BaseLawScore(Hero hero, SuccessionLaw law)
        {
            int age = (int)Math.Min(100f, Math.Max(0f, hero.Age));
            switch (law)
            {
                case SuccessionLaw.MalePreferencePrimogeniture:
                    return (hero.IsFemale ? 0 : 3000) + (100 - age);
                case SuccessionLaw.AgnaticPrimogeniture:
                    return 3000 + (100 - age);
                case SuccessionLaw.HouseSeniority:
                    return age * 20;
                case SuccessionLaw.NomadicHouseSeniority:
                    return age * 15 + Math.Max(0, hero.Clan == null ? 0 : hero.Clan.Tier) * 100;
                default:
                    return 100 - age;
            }
        }

        private static int KinshipScore(Hero candidate, Hero monarch)
        {
            if (candidate == null || monarch == null) return 0;
            if (candidate.Father == monarch || candidate.Mother == monarch) return 12000;
            if (IsGrandchild(candidate, monarch)) return 10500;
            if (candidate == monarch.Spouse) return 7000;
            if (ShareParent(candidate, monarch)) return 8500;
            if (IsNieceOrNephew(candidate, monarch)) return 6500;
            return 0;
        }

        private static bool IsGrandchild(Hero candidate, Hero monarch)
        {
            return IsChildOf(candidate.Father, monarch) || IsChildOf(candidate.Mother, monarch);
        }

        private static bool IsNieceOrNephew(Hero candidate, Hero monarch)
        {
            return ShareParent(candidate.Father, monarch) || ShareParent(candidate.Mother, monarch);
        }

        private static bool IsChildOf(Hero child, Hero parent)
        {
            return child != null && parent != null && (child.Father == parent || child.Mother == parent);
        }

        private static bool ShareParent(Hero first, Hero second)
        {
            if (first == null || second == null) return false;
            return (first.Father != null && first.Father == second.Father)
                || (first.Mother != null && first.Mother == second.Mother);
        }

        private static string RelationshipText(int kinship)
        {
            if (kinship >= 12000) return "child of the previous monarch";
            if (kinship >= 10500) return "grandchild of the previous monarch";
            if (kinship >= 8500) return "sibling of the previous monarch";
            if (kinship >= 7000) return "spouse of the previous monarch";
            if (kinship >= 6500) return "niece or nephew of the previous monarch";
            return "no close dynastic relationship recorded";
        }
    }
}
