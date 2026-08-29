using System;
using System.Collections.Generic;

namespace AgesOfCalradiaReligions
{
    /// <summary>Canonical faith, family, clergy, and holy-place definitions.</summary>
    public static class ReligionCatalog
    {
        public const string AseracFamily = "aserac";

        private static readonly ReligionDefinition[] Definitions =
        {
            new ReligionDefinition("asharim", "Asharim", AseracFamily, "Keeper"),
            new ReligionDefinition("valeronism", "Valeronism", AseracFamily, "Priest"),
            new ReligionDefinition("mazirism", "Mazirism", AseracFamily, "Judge-Reciter"),
            new ReligionDefinition("isharan_way", "Isharan Way", "isharan", "Star-Reader"),
            new ReligionDefinition("kok_orun_way", "Kok-Orun Way", "steppe", "Shaman"),
            new ReligionDefinition("caerwydd", "Caerwydd", "western_pagan", "Druid"),
            new ReligionDefinition("veyrhold", "Veyrhold", "northern_pagan", "Godi"),
            new ReligionDefinition("calradic_old_faith", "Calradic Old Faith", "calradic_pagan", "Augur")
        };

        public static readonly IReadOnlyList<string> FaithIds = Array.ConvertAll(Definitions, value => value.Id);
        public static readonly IReadOnlyList<HolySiteDefinition> HolySites = new[]
        {
            new HolySiteDefinition("danustica_three_testaments", "town_ES1", "Danustica, City of the Three Testaments", "asharim", "valeronism", "mazirism"),
            new HolySiteDefinition("lycaron_valeronist_see", "town_ES4", "Lycaron Apostolic See", "valeronism"),
            new HolySiteDefinition("sanala_house_of_recitation", "town_A6", "Sanala House of Recitation", "mazirism"),
            new HolySiteDefinition("quyaz_star_well", "town_A1", "Quyaz Star-Well", "isharan_way"),
            new HolySiteDefinition("makeb_sky_shrine", "town_K3", "Makeb Sky Shrine", "kok_orun_way"),
            new HolySiteDefinition("baltakhand_ancestral_field", "town_K1", "Baltakhand Ancestral Field", "kok_orun_way"),
            new HolySiteDefinition("dunglanys_sacred_grove", "town_B2", "Dunglanys Sacred Grove", "caerwydd"),
            new HolySiteDefinition("marunath_oak_court", "town_B1", "Marunath Oak Court", "caerwydd"),
            new HolySiteDefinition("revyl_whale_stone", "town_S7", "Revyl Whale-Stone", "veyrhold"),
            new HolySiteDefinition("varcheg_hall_of_oaths", "town_S1", "Varcheg Hall of Oaths", "veyrhold")
        };

        public static int IndexOf(string faithId)
        {
            for (int index = 0; index < FaithIds.Count; index++)
                if (string.Equals(FaithIds[index], faithId, StringComparison.Ordinal)) return index;
            return -1;
        }

        public static ReligionDefinition Get(string faithId)
        {
            int index = IndexOf(faithId);
            return index < 0 ? Definitions[Definitions.Length - 1] : Definitions[index];
        }

        public static string GetName(string faithId) { return Get(faithId).Name; }

        public static bool AreRelated(string first, string second)
        {
            return string.Equals(Get(first).Family, Get(second).Family, StringComparison.Ordinal);
        }

        public static string DefaultFaithForCulture(string cultureId)
        {
            string value = (cultureId ?? string.Empty).ToLowerInvariant();
            if (value.Contains("aserai")) return "mazirism";
            if (value.Contains("khuzait")) return "kok_orun_way";
            if (value.Contains("battania")) return "caerwydd";
            if (value.Contains("sturgia") || value.Contains("nord")) return "veyrhold";
            if (value.Contains("empire") || value.Contains("vlandia")) return "valeronism";
            return "calradic_old_faith";
        }
    }
}
