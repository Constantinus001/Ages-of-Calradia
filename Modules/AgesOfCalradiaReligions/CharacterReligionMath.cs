using System;

namespace AgesOfCalradiaReligions
{
    internal static class CharacterReligionMath
    {
        internal static string GetInheritedFaith(string motherFaith, string fatherFaith, string cultureFaith, int stableSeed)
        {
            bool motherValid = ReligionCatalog.IndexOf(motherFaith) >= 0;
            bool fatherValid = ReligionCatalog.IndexOf(fatherFaith) >= 0;
            if (motherValid && fatherValid)
            {
                if (motherFaith == fatherFaith) return motherFaith;
                return (stableSeed & 1) == 0 ? motherFaith : fatherFaith;
            }
            if (motherValid) return motherFaith;
            if (fatherValid) return fatherFaith;
            return ReligionCatalog.IndexOf(cultureFaith) >= 0 ? cultureFaith : "calradic_old_faith";
        }

        internal static double GetMonthlyConversionChance(CrownReligiousPolicy policy, float clergyRelations, float zeal,
            bool relatedFaith, bool isRuler, bool spouseAlreadyConverted)
        {
            double baseChance = policy == CrownReligiousPolicy.UniversalProtection ? 0.00025d
                : policy == CrownReligiousPolicy.TraditionalTolerance ? 0.0006d
                : policy == CrownReligiousPolicy.OfficialSupremacy ? 0.002d : 0.006d;
            double clergy = 0.5d + Math.Max(0f, Math.Min(100f, clergyRelations)) / 100d;
            double resistance = Math.Max(0.25d, 1d - Math.Max(0f, Math.Min(100f, zeal)) * 0.0075d);
            double chance = baseChance * clergy * resistance * (relatedFaith ? 1.4d : 0.8d)
                * (isRuler ? 0.3d : 1d) * (spouseAlreadyConverted ? 1.35d : 1d);
            return Math.Max(0.00002d, Math.Min(0.02d, chance));
        }

        internal static float GetReligiousLegitimacy(float piety, float clergyRelations, float realmUnity,
            bool officialFaith, bool relatedFaith, bool cultureFaith, int conversionCount)
        {
            float value = 12f + piety * 0.22f + clergyRelations * 0.24f + realmUnity * 0.20f
                + (officialFaith ? 24f : relatedFaith ? 9f : -8f) + (cultureFaith ? 5f : 0f) - Math.Max(0, conversionCount) * 3f;
            return Math.Max(0f, Math.Min(100f, value));
        }
    }
}
