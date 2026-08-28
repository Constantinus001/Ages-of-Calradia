using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AgesOfCalradiaReligions
{
    internal static class PopulationPersistence
    {
        private const string Header = "AOCPOP5";
        private const string VersionFourHeader = "AOCPOP4";
        private const string VersionThreeHeader = "AOCPOP3";
        private const string VersionTwoHeader = "AOCPOP2";
        private const string LegacyHeader = "AOCPOP1";

        internal static string Serialize(IEnumerable<ProvincePopulationState> states)
        {
            StringBuilder builder = new StringBuilder(Header);
            foreach (ProvincePopulationState state in states)
            {
                builder.Append('\n').Append(Uri.EscapeDataString(state.SettlementId));
                Append(builder, state.UrbanPopulation);
                Append(builder, state.RuralPopulation);
                Append(builder, state.PastoralPopulation);
                Append(builder, state.InstitutionalPopulation);
                Append(builder, state.CarryingCapacity);
                Append(builder, state.AvailableManpower);
                Append(builder, (long)Math.Round(state.Happiness * 1000f));
                Append(builder, (long)state.TaxPolicy);
                Append(builder, (long)state.ConscriptionPolicy);
                Append(builder, state.TownRecruitReserve);
                Append(builder, state.LastMonthlyBirths);
                Append(builder, state.LastMonthlyDeaths);
                Append(builder, state.LastMonthlyMigrationNet);
                for (int index = 0; index < state.FaithPopulations.Length; index++)
                {
                    Append(builder, state.FaithPopulations[index]);
                }
                Append(builder, (long)Math.Round(state.ReligiousTension * 1000f));
                Append(builder, state.LastMonthlyConverts);
                Append(builder, (long)state.LastReligiousIncident);
                for (int index = 0; index < state.FaithInstitutionStrengths.Length; index++)
                    Append(builder, (long)Math.Round(state.FaithInstitutionStrengths[index] * 1000f));
                for (int index = 0; index < state.FaithInstitutionTiers.Length; index++)
                    Append(builder, (long)state.FaithInstitutionTiers[index]);
            }

            return builder.ToString();
        }

        internal static bool TryDeserialize(string serialized, out Dictionary<string, ProvincePopulationState> states)
        {
            states = new Dictionary<string, ProvincePopulationState>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(serialized)) return false;
            string[] lines = serialized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return false;
            bool legacy = string.Equals(lines[0].Trim(), LegacyHeader, StringComparison.Ordinal);
            bool versionTwo = string.Equals(lines[0].Trim(), VersionTwoHeader, StringComparison.Ordinal);
            bool versionThree = string.Equals(lines[0].Trim(), VersionThreeHeader, StringComparison.Ordinal);
            bool versionFour = string.Equals(lines[0].Trim(), VersionFourHeader, StringComparison.Ordinal);
            if (!legacy && !versionTwo && !versionThree && !versionFour && !string.Equals(lines[0].Trim(), Header, StringComparison.Ordinal)) return false;

            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                string[] fields = lines[lineIndex].Split('|');
                int expectedFields = legacy ? 10 + ReligionCatalog.FaithIds.Count
                    : versionTwo ? 14 + ReligionCatalog.FaithIds.Count
                    : versionThree ? 16 + ReligionCatalog.FaithIds.Count * 2
                    : versionFour ? 17 + ReligionCatalog.FaithIds.Count * 2
                    : 17 + ReligionCatalog.FaithIds.Count * 3;
                if (fields.Length != expectedFields) return false;

                string settlementId = Uri.UnescapeDataString(fields[0]);
                long[] values = new long[expectedFields - 1];
                for (int fieldIndex = 1; fieldIndex < fields.Length; fieldIndex++)
                {
                    if (!long.TryParse(fields[fieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[fieldIndex - 1])) return false;
                }

                if (string.IsNullOrWhiteSpace(settlementId) || states.ContainsKey(settlementId)) return false;
                ProvincePopulationState state = new ProvincePopulationState(settlementId)
                {
                    UrbanPopulation = Math.Max(0L, values[0]),
                    RuralPopulation = Math.Max(0L, values[1]),
                    PastoralPopulation = Math.Max(0L, values[2]),
                    InstitutionalPopulation = Math.Max(0L, values[3]),
                    CarryingCapacity = Math.Max(1L, values[4]),
                    AvailableManpower = Math.Max(0L, values[5]),
                    Happiness = Math.Max(0f, Math.Min(100f, values[6] / 1000f)),
                    TaxPolicy = (TaxPolicy)Math.Max(0L, Math.Min(3L, values[7])),
                    ConscriptionPolicy = (ConscriptionPolicy)Math.Max(0L, Math.Min(4L, values[8]))
                };

                int faithOffset;
                if (legacy)
                {
                    faithOffset = 9;
                }
                else
                {
                    state.TownRecruitReserve = Math.Max(0L, values[9]);
                    state.LastMonthlyBirths = Math.Max(0L, values[10]);
                    state.LastMonthlyDeaths = Math.Max(0L, values[11]);
                    state.LastMonthlyMigrationNet = values[12];
                    faithOffset = 13;
                }

                for (int faithIndex = 0; faithIndex < ReligionCatalog.FaithIds.Count; faithIndex++)
                {
                    state.FaithPopulations[faithIndex] = Math.Max(0L, values[faithOffset + faithIndex]);
                }

                int religionOffset = faithOffset + ReligionCatalog.FaithIds.Count;
                if (!legacy && !versionTwo)
                {
                    state.ReligiousTension = Math.Max(0f, Math.Min(100f, values[religionOffset] / 1000f));
                    state.LastMonthlyConverts = Math.Max(0L, values[religionOffset + 1]);
                    int institutionOffset = religionOffset + 2;
                    if (!versionThree)
                    {
                        state.LastReligiousIncident = (ReligiousIncidentType)Math.Max(0L, Math.Min(5L, values[religionOffset + 2]));
                        institutionOffset++;
                    }
                    for (int faithIndex = 0; faithIndex < ReligionCatalog.FaithIds.Count; faithIndex++)
                        state.FaithInstitutionStrengths[faithIndex] = Math.Max(0f, Math.Min(100f, values[institutionOffset + faithIndex] / 1000f));
                    int tierOffset = institutionOffset + ReligionCatalog.FaithIds.Count;
                    if (!versionThree && !versionFour)
                    {
                        for (int faithIndex = 0; faithIndex < ReligionCatalog.FaithIds.Count; faithIndex++)
                            state.FaithInstitutionTiers[faithIndex] = (ReligiousInstitutionTier)Math.Max(0L, Math.Min(3L, values[tierOffset + faithIndex]));
                    }
                    else InitializeLegacyInstitutionTiers(state);
                }
                else
                {
                    InitializeLegacyReligionFields(state);
                    InitializeLegacyInstitutionTiers(state);
                }

                states.Add(settlementId, state);
            }

            return states.Count > 0;
        }

        private static void Append(StringBuilder builder, long value)
        {
            builder.Append('|').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void InitializeLegacyReligionFields(ProvincePopulationState state)
        {
            state.ReligiousTension = 10f;
            long total = Math.Max(1L, state.TotalPopulation);
            for (int index = 0; index < state.FaithPopulations.Length; index++)
                state.FaithInstitutionStrengths[index] = Math.Max(5f, Math.Min(90f, 15f + state.FaithPopulations[index] * 70f / total));
        }

        private static void InitializeLegacyInstitutionTiers(ProvincePopulationState state)
        {
            for (int index = 0; index < state.FaithInstitutionStrengths.Length; index++)
            {
                float strength = state.FaithInstitutionStrengths[index];
                state.FaithInstitutionTiers[index] = strength >= 90f ? ReligiousInstitutionTier.GreatSanctuary
                    : strength >= 60f ? ReligiousInstitutionTier.Temple
                    : strength >= 25f ? ReligiousInstitutionTier.Shrine : ReligiousInstitutionTier.None;
            }
        }
    }
}
