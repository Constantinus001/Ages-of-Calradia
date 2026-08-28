using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AgesOfCalradiaReligions
{
    internal static class ReligionPersistence
    {
        internal static string SerializeHeroes(IEnumerable<HeroReligionState> states)
        {
            StringBuilder result = new StringBuilder("AOCHEROFAITH3");
            foreach (HeroReligionState state in states)
            {
                result.Append('\n').Append(Uri.EscapeDataString(state.HeroId)).Append('|')
                    .Append(Uri.EscapeDataString(state.FaithId)).Append('|')
                    .Append(Math.Round(state.Zeal * 1000f).ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(state.LastConversionDay.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(Math.Round(state.Piety * 1000f).ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(state.LastPilgrimageDay.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(Uri.EscapeDataString(state.BirthFaithId)).Append('|')
                    .Append(state.ConversionCount.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(Math.Round(state.ReligiousLegitimacy * 1000f).ToString(CultureInfo.InvariantCulture));
            }
            return result.ToString();
        }

        internal static bool TryDeserializeHeroes(string value, out Dictionary<string, HeroReligionState> states)
        {
            states = new Dictionary<string, HeroReligionState>(StringComparer.Ordinal);
            string[] lines = Split(value, "AOCHEROFAITH3");
            int version = 3;
            if (lines == null)
            {
                lines = Split(value, "AOCHEROFAITH2");
                version = 2;
            }
            if (lines == null)
            {
                lines = Split(value, "AOCHEROFAITH1");
                version = 1;
            }
            if (lines == null) return false;
            for (int index = 1; index < lines.Length; index++)
            {
                string[] fields = lines[index].Split('|');
                int zeal;
                int day;
                int piety = 0;
                int pilgrimageDay = -1;
                int conversionCount = 0;
                int legitimacy = 50000;
                if (fields.Length != (version == 1 ? 4 : version == 2 ? 6 : 9) || !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out zeal)
                    || !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out day)) return false;
                if (version >= 2 && (!int.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out piety)
                    || !int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out pilgrimageDay))) return false;
                if (version >= 3 && (!int.TryParse(fields[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out conversionCount)
                    || !int.TryParse(fields[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out legitimacy))) return false;
                string id = Uri.UnescapeDataString(fields[0]);
                string faith = Uri.UnescapeDataString(fields[1]);
                string birthFaith = version >= 3 ? Uri.UnescapeDataString(fields[6]) : faith;
                if (string.IsNullOrEmpty(id) || ReligionCatalog.IndexOf(faith) < 0 || ReligionCatalog.IndexOf(birthFaith) < 0) return false;
                states[id] = new HeroReligionState(id, faith, zeal / 1000f, day)
                {
                    Piety = Math.Max(0f, Math.Min(100f, piety / 1000f)),
                    LastPilgrimageDay = pilgrimageDay,
                    BirthFaithId = birthFaith,
                    ConversionCount = Math.Max(0, conversionCount),
                    ReligiousLegitimacy = Math.Max(0f, Math.Min(100f, legitimacy / 1000f))
                };
            }
            return true;
        }

        internal static string SerializeRealms(IEnumerable<RealmReligionState> states)
        {
            StringBuilder result = new StringBuilder("AOCREALMFAITH2");
            foreach (RealmReligionState state in states)
            {
                result.Append('\n').Append(Uri.EscapeDataString(state.KingdomId)).Append('|')
                    .Append(Uri.EscapeDataString(state.OfficialFaithId)).Append('|')
                    .Append(((int)state.Policy).ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(Math.Round(state.ClergyRelations * 1000f).ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(Math.Round(state.ReligiousUnity * 1000f).ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(((int)state.ClergyGovernance).ToString(CultureInfo.InvariantCulture));
            }
            return result.ToString();
        }

        internal static bool TryDeserializeRealms(string value, out Dictionary<string, RealmReligionState> states)
        {
            states = new Dictionary<string, RealmReligionState>(StringComparer.Ordinal);
            string[] lines = Split(value, "AOCREALMFAITH2");
            bool legacy = false;
            if (lines == null)
            {
                lines = Split(value, "AOCREALMFAITH1");
                legacy = lines != null;
            }
            if (lines == null) return false;
            for (int index = 1; index < lines.Length; index++)
            {
                string[] fields = lines[index].Split('|');
                int policy;
                int clergy;
                int unity;
                int governance = (int)ClergyGovernancePolicy.CrownConcordat;
                if (fields.Length != (legacy ? 5 : 6) || !int.TryParse(fields[2], out policy) || !int.TryParse(fields[3], out clergy) || !int.TryParse(fields[4], out unity)) return false;
                if (!legacy && !int.TryParse(fields[5], out governance)) return false;
                string id = Uri.UnescapeDataString(fields[0]);
                string faith = Uri.UnescapeDataString(fields[1]);
                if (string.IsNullOrEmpty(id) || ReligionCatalog.IndexOf(faith) < 0) return false;
                RealmReligionState state = new RealmReligionState(id, faith)
                {
                    Policy = (CrownReligiousPolicy)Math.Max(0, Math.Min(3, policy)),
                    ClergyRelations = Math.Max(0f, Math.Min(100f, clergy / 1000f)),
                    ReligiousUnity = Math.Max(0f, Math.Min(100f, unity / 1000f)),
                    ClergyGovernance = (ClergyGovernancePolicy)Math.Max(0, Math.Min(2, governance))
                };
                states[id] = state;
            }
            return true;
        }

        internal static string SerializeClergyOffices(IEnumerable<ClergyOfficeState> states)
        {
            StringBuilder result = new StringBuilder("AOCCLERGYOFFICES1");
            foreach (ClergyOfficeState state in states)
            {
                result.Append('\n').Append(Uri.EscapeDataString(state.SettlementId)).Append('|')
                    .Append(Uri.EscapeDataString(state.FaithId)).Append('|')
                    .Append(Uri.EscapeDataString(state.HolderHeroId)).Append('|')
                    .Append(state.Treasury.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(state.LastClergyTaxDay.ToString(CultureInfo.InvariantCulture));
            }
            return result.ToString();
        }

        internal static bool TryDeserializeClergyOffices(string value, out Dictionary<string, ClergyOfficeState> states)
        {
            states = new Dictionary<string, ClergyOfficeState>(StringComparer.Ordinal);
            string[] lines = Split(value, "AOCCLERGYOFFICES1");
            if (lines == null) return false;
            for (int index = 1; index < lines.Length; index++)
            {
                string[] fields = lines[index].Split('|');
                long treasury;
                int taxDay;
                if (fields.Length != 5 || !long.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out treasury)
                    || !int.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out taxDay)) return false;
                string settlementId = Uri.UnescapeDataString(fields[0]);
                string faithId = Uri.UnescapeDataString(fields[1]);
                string holder = Uri.UnescapeDataString(fields[2]);
                if (string.IsNullOrEmpty(settlementId) || ReligionCatalog.IndexOf(faithId) < 0 || states.ContainsKey(settlementId)) return false;
                states.Add(settlementId, new ClergyOfficeState(settlementId, faithId, holder)
                {
                    Treasury = Math.Max(0L, treasury),
                    LastClergyTaxDay = taxDay
                });
            }
            return true;
        }

        internal static string SerializeHolySites(IEnumerable<HolySiteState> states)
        {
            StringBuilder result = new StringBuilder("AOCHOLYSITES1");
            foreach (HolySiteState state in states)
            {
                result.Append('\n').Append(Uri.EscapeDataString(state.SiteId));
                for (int index = 0; index < state.AccessByFaith.Length; index++) result.Append('|').Append((int)state.AccessByFaith[index]);
            }
            return result.ToString();
        }

        internal static bool TryDeserializeHolySites(string value, out Dictionary<string, HolySiteState> states)
        {
            states = new Dictionary<string, HolySiteState>(StringComparer.Ordinal);
            string[] lines = Split(value, "AOCHOLYSITES1");
            if (lines == null) return false;
            for (int line = 1; line < lines.Length; line++)
            {
                string[] fields = lines[line].Split('|');
                if (fields.Length != 1 + ReligionCatalog.FaithIds.Count) return false;
                string id = Uri.UnescapeDataString(fields[0]);
                HolySiteState state = new HolySiteState(id);
                for (int index = 0; index < state.AccessByFaith.Length; index++)
                {
                    int access;
                    if (!int.TryParse(fields[index + 1], out access)) return false;
                    state.AccessByFaith[index] = (HolySiteAccess)Math.Max(0, Math.Min(2, access));
                }
                states[id] = state;
            }
            return true;
        }

        private static string[] Split(string value, string header)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string[] lines = value.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 0 && string.Equals(lines[0].Trim(), header, StringComparison.Ordinal) ? lines : null;
        }
    }
}
