using System;

namespace AgesOfCalradiaReligions
{
    internal static class ReligionSimulationMath
    {
        internal const double BaseMonthlyConversionRate = 0.000025d;

        internal static double GetPolicyConversionMultiplier(CrownReligiousPolicy policy)
        {
            return policy == CrownReligiousPolicy.UniversalProtection ? 0.35d
                : policy == CrownReligiousPolicy.TraditionalTolerance ? 1d
                : policy == CrownReligiousPolicy.OfficialSupremacy ? 2d : 4d;
        }

        internal static long GetMonthlyConversionCount(long totalPopulation, long sourcePopulation, string sourceFaith,
            string targetFaith, CrownReligiousPolicy policy, float targetInstitutionStrength)
        {
            if (totalPopulation <= 0L || sourcePopulation <= 0L || ReligionCatalog.IndexOf(targetFaith) < 0) return 0L;
            double institution = Math.Max(0.5d, Math.Min(2d, targetInstitutionStrength / 50d));
            double kinship = ReligionCatalog.AreRelated(sourceFaith, targetFaith) ? 1.25d : 0.75d;
            long requested = Math.Max(0L, (long)Math.Floor(totalPopulation * BaseMonthlyConversionRate
                * GetPolicyConversionMultiplier(policy) * institution * kinship));
            return Math.Min(sourcePopulation, requested);
        }

        internal static float GetTensionTarget(float minorityPercent, CrownReligiousPolicy policy, float holyAccessPressure, bool isDanustica)
        {
            float severity = policy == CrownReligiousPolicy.UniversalProtection ? -8f
                : policy == CrownReligiousPolicy.TraditionalTolerance ? -3f
                : policy == CrownReligiousPolicy.OfficialSupremacy ? 10f : 24f;
            return Math.Max(0f, Math.Min(100f, minorityPercent * 0.35f + severity + holyAccessPressure + (isDanustica ? 8f : 0f)));
        }
    }
}
