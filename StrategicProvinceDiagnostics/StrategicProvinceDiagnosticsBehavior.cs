using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace TwelveMonthCalendar
{
    internal sealed class StrategicProvinceDiagnosticsBehavior : CampaignBehaviorBase
    {
        private readonly Dictionary<string, string> _previousStates = new Dictionary<string, string>(StringComparer.Ordinal);

        public override void RegisterEvents()
        {
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, OnAfterSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, OnSiegeEventStarted);
            CampaignEvents.OnSiegeEventEndedEvent.AddNonSerializedListener(this, OnSiegeEventEnded);
            Diagnostics.Info("Strategic province diagnostics event listeners registered: after-session, daily, siege-started, siege-ended.");
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnAfterSessionLaunched(CampaignGameStarter campaignStarter)
        {
            Capture("after-session", true);
        }

        private void OnDailyTick()
        {
            Capture("daily", false);
        }

        private void OnSiegeEventStarted(SiegeEvent siegeEvent)
        {
            string settlementId = siegeEvent == null || siegeEvent.BesiegedSettlement == null
                ? "<null>"
                : siegeEvent.BesiegedSettlement.StringId;
            StrategicProvinceDiagnosticsLog.Info("NATIVE_SIEGE_EVENT_STARTED settlement=" + settlementId);
            Capture("siege-started", true);
        }

        private void OnSiegeEventEnded(SiegeEvent siegeEvent)
        {
            string settlementId = siegeEvent == null || siegeEvent.BesiegedSettlement == null
                ? "<null>"
                : siegeEvent.BesiegedSettlement.StringId;
            StrategicProvinceDiagnosticsLog.Info("NATIVE_SIEGE_EVENT_ENDED settlement=" + settlementId);
            Capture("siege-ended", true);
        }

        private void Capture(string reason, bool forceFullReport)
        {
            try
            {
                StrategicProvinceDiagnosticsLog.SnapshotHeader();
                Dictionary<string, Settlement> settlements = IndexSettlements();
                Dictionary<string, int> mappingCounts = CountMappings();
                int stripeCount = 0;
                int mappedCount = 0;
                int missingCount = 0;
                int changedCount = 0;
                CampaignTime now = CampaignTime.Now;
                string timestamp = DateTime.Now.ToString("O");

                for (int index = 0; index < CalendarStrategicMapLayout.Provinces.Length; index++)
                {
                    CalendarStrategicProvinceDefinition province = CalendarStrategicMapLayout.Provinces[index];
                    string settlementId;
                    bool hasMapping = CalendarStrategicMapLayout.TryGetSettlementId(province.SpriteName, out settlementId);
                    Settlement settlement = null;
                    if (hasMapping && !string.IsNullOrEmpty(settlementId))
                    {
                        // Settlement.Find is the native object-manager lookup. The indexed
                        // collection remains a fallback for unusual initialization states.
                        settlement = Settlement.Find(settlementId);
                        if (settlement == null) settlements.TryGetValue(settlementId, out settlement);
                    }

                    ProvinceState state = ReadState(province, settlementId, settlement, hasMapping, mappingCounts);
                    if (state.SettlementFound) mappedCount++;
                    else missingCount++;
                    if (state.StripeEligible) stripeCount++;

                    string previous;
                    bool changed = !_previousStates.TryGetValue(province.SpriteName, out previous)
                        || !string.Equals(previous, state.ComparisonKey, StringComparison.Ordinal);
                    if (changed) changedCount++;
                    _previousStates[province.SpriteName] = state.ComparisonKey;

                    StrategicProvinceDiagnosticsLog.SnapshotRow(state.ToTsvRow(timestamp, now, reason));
                    if (forceFullReport || changed)
                    {
                        StrategicProvinceDiagnosticsLog.Info(state.ToHumanLine(reason));
                    }
                }

                StrategicProvinceDiagnosticsLog.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Capture complete. Reason={0}; CampaignDay={1:F3}; Provinces={2}; SettlementFound={3}; MissingSettlement={4}; StripeEligible={5}; Changed={6}; DuplicateMappings={7}.",
                        reason,
                        now.ToDays,
                        CalendarStrategicMapLayout.Provinces.Length,
                        mappedCount,
                        missingCount,
                        stripeCount,
                        changedCount,
                        CountDuplicateMappings(mappingCounts)));
            }
            catch (Exception exception)
            {
                StrategicProvinceDiagnosticsLog.Error("Strategic province capture failed.", exception);
                Diagnostics.Error("Strategic province capture failed; see StrategicProvinceDiagnostics.log for details.", exception);
            }
        }

        private static Dictionary<string, Settlement> IndexSettlements()
        {
            Dictionary<string, Settlement> result = new Dictionary<string, Settlement>(StringComparer.Ordinal);
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement == null || string.IsNullOrEmpty(settlement.StringId)) continue;
                if (!result.ContainsKey(settlement.StringId)) result.Add(settlement.StringId, settlement);
            }
            return result;
        }

        private static Dictionary<string, int> CountMappings()
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < CalendarStrategicMapLayout.Provinces.Length; index++)
            {
                string settlementId;
                if (!CalendarStrategicMapLayout.TryGetSettlementId(CalendarStrategicMapLayout.Provinces[index].SpriteName, out settlementId)) continue;
                int count;
                result.TryGetValue(settlementId, out count);
                result[settlementId] = count + 1;
            }
            return result;
        }

        private static int CountDuplicateMappings(Dictionary<string, int> mappingCounts)
        {
            int duplicateProvinceCount = 0;
            foreach (KeyValuePair<string, int> entry in mappingCounts)
            {
                if (entry.Value > 1) duplicateProvinceCount += entry.Value;
                if (entry.Value > 1)
                {
                    StrategicProvinceDiagnosticsLog.Info(
                        "DUPLICATE_MAPPING settlement=" + entry.Key + " province_count=" + entry.Value);
                }
            }
            return duplicateProvinceCount;
        }

        private static ProvinceState ReadState(
            CalendarStrategicProvinceDefinition province,
            string settlementId,
            Settlement settlement,
            bool hasMapping,
            Dictionary<string, int> mappingCounts)
        {
            IFaction ownerFaction = null;
            string ownerSource = "none";
            if (settlement != null && settlement.Town != null && settlement.Town.MapFaction != null)
            {
                ownerFaction = settlement.Town.MapFaction;
                ownerSource = "Town.MapFaction";
            }
            else if (settlement != null && settlement.MapFaction != null)
            {
                ownerFaction = settlement.MapFaction;
                ownerSource = "Settlement.MapFaction";
            }
            else if (settlement != null && settlement.OwnerClan != null)
            {
                ownerFaction = settlement.OwnerClan.MapFaction ?? settlement.OwnerClan;
                ownerSource = "OwnerClan.MapFaction";
            }

            Clan ownerClan = settlement == null ? null : settlement.OwnerClan;
            bool underSiege = settlement != null && settlement.IsUnderSiege;
            bool siegeEventPresent = settlement != null && settlement.SiegeEvent != null;
            bool besiegerCampPresent = siegeEventPresent && settlement.SiegeEvent.BesiegerCamp != null;
            IFaction besiegerFaction = besiegerCampPresent ? settlement.SiegeEvent.BesiegerCamp.MapFaction : null;
            bool stripeEligible = underSiege && besiegerFaction != null;

            StringBuilder issue = new StringBuilder();
            if (!hasMapping) issue.Append("NO_MANIFEST_MAPPING;");
            if (settlement == null) issue.Append("SETTLEMENT_NOT_FOUND;");
            int mappingCount;
            if (mappingCounts.TryGetValue(settlementId ?? string.Empty, out mappingCount) && mappingCount > 1)
            {
                issue.Append("DUPLICATE_MANIFEST_MAPPING;");
            }
            if (settlement != null && ownerFaction == null) issue.Append("NO_OWNER_FACTION;");
            if (underSiege && !siegeEventPresent) issue.Append("SIEGE_FLAG_WITHOUT_EVENT;");
            if (underSiege && !besiegerCampPresent) issue.Append("SIEGE_EVENT_WITHOUT_BESIEGER_CAMP;");
            if (underSiege && besiegerFaction == null) issue.Append("SIEGE_WITHOUT_BESIEGER_FACTION;");

            return new ProvinceState(
                province,
                settlementId,
                settlement,
                hasMapping,
                ownerSource,
                ownerFaction,
                ownerClan,
                underSiege,
                siegeEventPresent,
                besiegerCampPresent,
                besiegerFaction,
                stripeEligible,
                issue.ToString());
        }

        private sealed class ProvinceState
        {
            internal ProvinceState(
                CalendarStrategicProvinceDefinition province,
                string settlementId,
                Settlement settlement,
                bool hasMapping,
                string ownerSource,
                IFaction ownerFaction,
                Clan ownerClan,
                bool underSiege,
                bool siegeEventPresent,
                bool besiegerCampPresent,
                IFaction besiegerFaction,
                bool stripeEligible,
                string mappingIssue)
            {
                Province = province;
                SettlementId = settlementId ?? string.Empty;
                Settlement = settlement;
                HasMapping = hasMapping;
                OwnerSource = ownerSource;
                OwnerFaction = ownerFaction;
                OwnerClan = ownerClan;
                UnderSiege = underSiege;
                SiegeEventPresent = siegeEventPresent;
                BesiegerCampPresent = besiegerCampPresent;
                BesiegerFaction = besiegerFaction;
                StripeEligible = stripeEligible;
                MappingIssue = mappingIssue ?? string.Empty;
            }

            internal CalendarStrategicProvinceDefinition Province { get; private set; }
            internal string SettlementId { get; private set; }
            internal Settlement Settlement { get; private set; }
            internal bool HasMapping { get; private set; }
            internal string OwnerSource { get; private set; }
            internal IFaction OwnerFaction { get; private set; }
            internal Clan OwnerClan { get; private set; }
            internal bool UnderSiege { get; private set; }
            internal bool SiegeEventPresent { get; private set; }
            internal bool BesiegerCampPresent { get; private set; }
            internal IFaction BesiegerFaction { get; private set; }
            internal bool StripeEligible { get; private set; }
            internal string MappingIssue { get; private set; }

            internal bool SettlementFound { get { return Settlement != null; } }

            internal string ComparisonKey
            {
                get
                {
                    return string.Join("|", new[]
                    {
                        SettlementId,
                        OwnerFaction == null ? string.Empty : OwnerFaction.StringId,
                        OwnerSource,
                        UnderSiege.ToString(),
                        SiegeEventPresent.ToString(),
                        BesiegerCampPresent.ToString(),
                        BesiegerFaction == null ? string.Empty : BesiegerFaction.StringId,
                        MappingIssue
                    });
                }
            }

            internal string ToTsvRow(string timestamp, CampaignTime now, string reason)
            {
                return string.Join("\t", new[]
                {
                    StrategicProvinceDiagnosticsLog.Clean(timestamp),
                    now.ToDays.ToString("F3", CultureInfo.InvariantCulture),
                    StrategicProvinceDiagnosticsLog.Clean(reason),
                    (Array.IndexOf(CalendarStrategicMapLayout.Provinces, Province) + 1).ToString(CultureInfo.InvariantCulture),
                    StrategicProvinceDiagnosticsLog.Clean(Province.SpriteName),
                    StrategicProvinceDiagnosticsLog.Clean(SettlementId),
                    StrategicProvinceDiagnosticsLog.Clean(Settlement == null || Settlement.Name == null ? string.Empty : Settlement.Name.ToString()),
                    StrategicProvinceDiagnosticsLog.Clean(Settlement == null ? string.Empty : (Settlement.IsTown ? "town" : "castle_or_other")),
                    Province.X.ToString(CultureInfo.InvariantCulture),
                    Province.Y.ToString(CultureInfo.InvariantCulture),
                    Province.Width.ToString(CultureInfo.InvariantCulture),
                    Province.Height.ToString(CultureInfo.InvariantCulture),
                    Province.CenterX.ToString("F3", CultureInfo.InvariantCulture),
                    Province.CenterY.ToString("F3", CultureInfo.InvariantCulture),
                    SettlementFound.ToString(),
                    StrategicProvinceDiagnosticsLog.Clean(OwnerSource),
                    StrategicProvinceDiagnosticsLog.Clean(OwnerFaction == null ? string.Empty : OwnerFaction.StringId),
                    StrategicProvinceDiagnosticsLog.Clean(OwnerFaction == null || OwnerFaction.Name == null ? string.Empty : OwnerFaction.Name.ToString()),
                    StrategicProvinceDiagnosticsLog.Clean(OwnerClan == null ? string.Empty : OwnerClan.StringId),
                    StrategicProvinceDiagnosticsLog.Clean(OwnerClan == null || OwnerClan.Name == null ? string.Empty : OwnerClan.Name.ToString()),
                    ColorText(OwnerFaction),
                    UnderSiege.ToString(),
                    SiegeEventPresent.ToString(),
                    BesiegerCampPresent.ToString(),
                    StrategicProvinceDiagnosticsLog.Clean(BesiegerFaction == null ? string.Empty : BesiegerFaction.StringId),
                    StrategicProvinceDiagnosticsLog.Clean(BesiegerFaction == null || BesiegerFaction.Name == null ? string.Empty : BesiegerFaction.Name.ToString()),
                    ColorText(BesiegerFaction),
                    StripeEligible.ToString(),
                    StripeEligible ? "OWNER_AND_BESIEGER_STRIPES" : (OwnerFaction == null ? "TRANSPARENT_OR_UNMAPPED" : "OWNER_COLOR_ONLY"),
                    StrategicProvinceDiagnosticsLog.Clean(MappingIssue)
                });
            }

            internal string ToHumanLine(string reason)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Province {0} {1}: settlement={2}({3}); owner={4} [{5}]; underSiege={6}; siegeEvent={7}; besiegerCamp={8}; besieger={9}; STRIPE_ELIGIBLE={10}; fill={11}; issue={12}",
                    Province.SpriteName,
                    reason,
                    SettlementId,
                    Settlement == null || Settlement.Name == null ? "<missing>" : Settlement.Name.ToString(),
                    OwnerFaction == null ? "<none>" : OwnerFaction.StringId,
                    OwnerSource,
                    UnderSiege,
                    SiegeEventPresent,
                    BesiegerCampPresent,
                    BesiegerFaction == null ? "<none>" : BesiegerFaction.StringId,
                    StripeEligible,
                    StripeEligible ? "OWNER_AND_BESIEGER_STRIPES" : (OwnerFaction == null ? "TRANSPARENT_OR_UNMAPPED" : "OWNER_COLOR_ONLY"),
                    string.IsNullOrEmpty(MappingIssue) ? "<none>" : MappingIssue);
            }

            private static string ColorText(IFaction faction)
            {
                return faction == null ? string.Empty : "0x" + faction.Color.ToString("X8", CultureInfo.InvariantCulture);
            }
        }
    }
}
