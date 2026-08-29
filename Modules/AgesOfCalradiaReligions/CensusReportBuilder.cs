using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;

namespace AgesOfCalradiaReligions
{
    internal static class CensusReportBuilder
    {
        internal static CensusDisplayData Build()
        {
            string payload = PopulationService.GetCensusSnapshotPayload();
            if (string.IsNullOrWhiteSpace(payload)) return CensusDisplayData.Unavailable();

            string[] lines = payload.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string[] header = lines.Length == 0 ? new string[0] : lines[0].Split('|');
            if (header.Length != 2 || !string.Equals(header[0], "AOCCENSUS1", StringComparison.Ordinal))
            {
                return CensusDisplayData.Unavailable();
            }

            string[] faithIds = header[1].Split(',');
            CensusAggregate calradia = new CensusAggregate(faithIds);
            CensusAggregate current = new CensusAggregate(faithIds);
            Clan playerClan = Clan.PlayerClan;
            Kingdom playerKingdom = playerClan == null ? null : playerClan.Kingdom;
            string realmName = playerKingdom != null && playerKingdom.Name != null
                ? playerKingdom.Name.ToString()
                : playerClan != null && playerClan.Name != null ? playerClan.Name.ToString() : "No Current Realm";

            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                string[] fields = lines[lineIndex].Split('|');
                long population;
                float happiness;
                if (fields.Length != 9
                    || !long.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out population)
                    || !float.TryParse(fields[7], NumberStyles.Float, CultureInfo.InvariantCulture, out happiness))
                {
                    continue;
                }

                long[] faithPopulations = ParseFaithPopulations(fields[8], faithIds.Length);
                string cultureId = Uri.UnescapeDataString(fields[2]);
                string kingdomId = Uri.UnescapeDataString(fields[3]);
                string clanId = Uri.UnescapeDataString(fields[5]);
                calradia.Add(population, happiness, cultureId, faithPopulations);
                bool belongsToCurrentRealm = playerKingdom != null
                    ? string.Equals(kingdomId, playerKingdom.StringId, StringComparison.Ordinal)
                    : playerClan != null && string.Equals(clanId, playerClan.StringId, StringComparison.Ordinal);
                if (belongsToCurrentRealm) current.Add(population, happiness, cultureId, faithPopulations);
            }

            return new CensusDisplayData(
                realmName,
                FormatPopulation(current.Population),
                FormatShare(current.Population, calradia.Population),
                current.Provinces.ToString("N0", CultureInfo.InvariantCulture),
                FormatHappiness(current),
                current.FormatCultures(),
                current.FormatReligions(),
                FormatPopulation(calradia.Population),
                calradia.Provinces.ToString("N0", CultureInfo.InvariantCulture),
                FormatHappiness(calradia),
                calradia.FormatCultures(),
                calradia.FormatReligions());
        }

        private static long[] ParseFaithPopulations(string field, int expected)
        {
            long[] populations = new long[expected];
            string[] values = (field ?? string.Empty).Split(',');
            for (int index = 0; index < populations.Length && index < values.Length; index++)
            {
                long.TryParse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out populations[index]);
            }
            return populations;
        }

        private static string FormatPopulation(long population)
        {
            return population.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string FormatShare(long part, long whole)
        {
            return whole <= 0L ? "0.0% of Calradia" : (part * 100d / whole).ToString("0.0", CultureInfo.InvariantCulture) + "% of Calradia";
        }

        private static string FormatHappiness(CensusAggregate aggregate)
        {
            return aggregate.Population <= 0L
                ? "0.0"
                : (aggregate.WeightedHappiness / aggregate.Population).ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static string DisplayCulture(string id)
        {
            string value = (id ?? string.Empty).ToLowerInvariant();
            if (value.Contains("empire")) return "Empire";
            if (value.Contains("vlandia")) return "Vlandian";
            if (value.Contains("khuzait")) return "Khuzait";
            if (value.Contains("battania")) return "Battanian";
            if (value.Contains("sturgia")) return "Sturgian";
            if (value.Contains("nord")) return "Nord";
            if (value.Contains("aserai")) return "Aserai";
            return string.IsNullOrEmpty(id) ? "Unrecorded" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' '));
        }

        private static string DisplayFaith(string id)
        {
            if (id == "isharan_way") return "Isharan Way";
            if (id == "kok_orun_way") return "Kok-Orun Way";
            if (id == "calradic_old_faith") return "Calradic Old Faith";
            return string.IsNullOrEmpty(id) ? "Unrecorded" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' '));
        }

        private sealed class CensusAggregate
        {
            private readonly string[] _faithIds;
            private readonly long[] _faithPopulations;
            private readonly Dictionary<string, long> _culturePopulations = new Dictionary<string, long>(StringComparer.Ordinal);

            internal CensusAggregate(string[] faithIds)
            {
                _faithIds = faithIds ?? new string[0];
                _faithPopulations = new long[_faithIds.Length];
            }

            internal long Population { get; private set; }
            internal int Provinces { get; private set; }
            internal double WeightedHappiness { get; private set; }

            internal void Add(long population, float happiness, string cultureId, long[] faithPopulations)
            {
                population = Math.Max(0L, population);
                Population += population;
                Provinces++;
                WeightedHappiness += population * happiness;
                long culturePopulation;
                _culturePopulations.TryGetValue(cultureId ?? string.Empty, out culturePopulation);
                _culturePopulations[cultureId ?? string.Empty] = culturePopulation + population;
                for (int index = 0; index < _faithPopulations.Length && index < faithPopulations.Length; index++)
                {
                    _faithPopulations[index] += Math.Max(0L, faithPopulations[index]);
                }
            }

            internal string FormatCultures()
            {
                List<KeyValuePair<string, long>> rows = new List<KeyValuePair<string, long>>(_culturePopulations);
                rows.Sort(delegate(KeyValuePair<string, long> left, KeyValuePair<string, long> right) { return right.Value.CompareTo(left.Value); });
                return FormatRows(rows, DisplayCulture);
            }

            internal string FormatReligions()
            {
                List<KeyValuePair<string, long>> rows = new List<KeyValuePair<string, long>>();
                for (int index = 0; index < _faithIds.Length; index++) rows.Add(new KeyValuePair<string, long>(_faithIds[index], _faithPopulations[index]));
                rows.Sort(delegate(KeyValuePair<string, long> left, KeyValuePair<string, long> right) { return right.Value.CompareTo(left.Value); });
                return FormatRows(rows, DisplayFaith);
            }

            private string FormatRows(List<KeyValuePair<string, long>> rows, Func<string, string> display)
            {
                if (Population <= 0L) return "No population recorded";
                string result = string.Empty;
                foreach (KeyValuePair<string, long> row in rows)
                {
                    if (row.Value <= 0L) continue;
                    if (result.Length > 0) result += "\n";
                    result += display(row.Key) + "  " + (row.Value * 100d / Population).ToString("0.0", CultureInfo.InvariantCulture) + "%";
                }
                return result.Length == 0 ? "No population recorded" : result;
            }
        }
    }

    internal sealed class CensusDisplayData
    {
        internal CensusDisplayData(string realmName, string realmPopulation, string realmShare, string realmProvinces,
            string realmHappiness, string realmCultures, string realmReligions, string calradiaPopulation,
            string calradiaProvinces, string calradiaHappiness, string calradiaCultures, string calradiaReligions)
        {
            RealmName = realmName; RealmPopulation = realmPopulation; RealmShare = realmShare; RealmProvinces = realmProvinces;
            RealmHappiness = realmHappiness; RealmCultures = realmCultures; RealmReligions = realmReligions;
            CalradiaPopulation = calradiaPopulation; CalradiaProvinces = calradiaProvinces; CalradiaHappiness = calradiaHappiness;
            CalradiaCultures = calradiaCultures; CalradiaReligions = calradiaReligions;
        }

        internal string RealmName { get; private set; }
        internal string RealmPopulation { get; private set; }
        internal string RealmShare { get; private set; }
        internal string RealmProvinces { get; private set; }
        internal string RealmHappiness { get; private set; }
        internal string RealmCultures { get; private set; }
        internal string RealmReligions { get; private set; }
        internal string CalradiaPopulation { get; private set; }
        internal string CalradiaProvinces { get; private set; }
        internal string CalradiaHappiness { get; private set; }
        internal string CalradiaCultures { get; private set; }
        internal string CalradiaReligions { get; private set; }

        internal static CensusDisplayData Unavailable()
        {
            return new CensusDisplayData("No Current Realm", "0", "0.0% of Calradia", "0", "0.0", "No population recorded",
                "No population recorded", "0", "0", "0.0", "No population recorded", "No population recorded");
        }
    }
}
