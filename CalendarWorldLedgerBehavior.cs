using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Save-safe foundation for the World Calendar. Records are primitive
    /// strings so saves remain loadable without a module-owned save type.
    /// UI and event-specific producers are added independently.
    /// </summary>
    internal sealed class CalendarWorldLedgerBehavior : CampaignBehaviorBase
    {
        private const string LedgerKey = "RealisticCalendarTweaks.WorldLedgerV1";
        private const string SettlementOwnershipKey = "RealisticCalendarTweaks.SettlementOwnershipV1";
        private const int MaximumEntries = 500;
        private List<string> _entries = new List<string>();
        // Primitive, ID-keyed snapshot: settlementId, settlementName, factionId, factionName.
        // Keeping this as strings avoids a custom save type and remains compatible with saves.
        private List<string> _settlementOwners = new List<string>();
        private static CalendarWorldLedgerBehavior _active;
        private static int _ownershipRevision;

        internal static int OwnershipRevision { get { return _ownershipRevision; } }

        public override void RegisterEvents()
        {
            _active = this;
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnPeaceMade);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, OnGivenBirth);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
        }


        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(LedgerKey, ref _entries);
            dataStore.SyncData(SettlementOwnershipKey, ref _settlementOwners);
            _entries = _entries ?? new List<string>();
            _settlementOwners = _settlementOwners ?? new List<string>();
            if (_entries.Count > MaximumEntries)
            {
                _entries.RemoveRange(0, _entries.Count - MaximumEntries);
            }
        }

        internal void Record(string category, string message)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(message)) return;
            string entry = CampaignTime.Now.ToDays.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + "\t" + category.Trim() + "\t" + message.Trim();
            _entries.Add(entry);
            if (_entries.Count > MaximumEntries) _entries.RemoveAt(0);
        }

        internal static string GetRecentEntriesText(string filter)
        {
            CalendarWorldLedgerBehavior active = _active;
            if (active == null || active._entries == null || active._entries.Count == 0)
            {
                return "No world events have been recorded yet.";
            }

            StringBuilder text = new StringBuilder();
            int first = Math.Max(0, active._entries.Count - 40);
            bool groupByDay = string.Equals(filter, "ByDay", StringComparison.Ordinal);
            int previousDay = int.MinValue;
            for (int index = active._entries.Count - 1; index >= first; index--)
            {
                string[] fields = active._entries[index].Split(new[] { '\t' }, 3);
                if (fields.Length == 3)
                {
                    if (!groupByDay && !MatchesFilter(fields[1], filter)) continue;
                    if (groupByDay)
                    {
                        double recordedDays;
                        if (!double.TryParse(fields[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out recordedDays)) continue;
                        int recordedDay = (int)Math.Floor(recordedDays);
                        if (recordedDay != previousDay)
                        {
                            if (text.Length > 0) text.AppendLine();
                            text.Append(CalendarFormatter.Format(CampaignTime.Days(recordedDay))).AppendLine();
                            previousDay = recordedDay;
                        }
                    }
                    text.Append('[').Append(fields[1]).Append("] ").Append(fields[2]);
                }
                else
                {
                    text.Append(active._entries[index]);
                }

                if (index > first) text.AppendLine().AppendLine();
            }

            return text.Length == 0 ? "No matching world events have been recorded yet." : text.ToString();
        }

        internal static string GetTrackedSettlementOwnersText()
        {
            CalendarWorldLedgerBehavior active = _active;
            if (active == null) return "Settlement ownership tracker is not active yet.";

            active.CaptureSettlementOwnership(false);
            List<string> entries = new List<string>(active._settlementOwners);
            entries.Sort(StringComparer.OrdinalIgnoreCase);

            StringBuilder text = new StringBuilder("TRACKED SETTLEMENT OWNERS (" + entries.Count + ")\n\n");
            // This panel has a fixed height in the Gauntlet screen. Keep the
            // summary readable instead of stacking every settlement into one
            // unscrollable TextWidget. The full ID-keyed snapshot remains
            // saved and is still used by the strategic-map renderer.
            const int visibleEntryLimit = 24;
            int visibleEntries = 0;
            foreach (string entry in entries)
            {
                if (visibleEntries >= visibleEntryLimit) break;
                string[] fields = entry.Split(new[] { '\t' }, 4);
                if (fields.Length < 4) continue;
                visibleEntries++;
                text.Append(fields[1]).Append(" — ").Append(fields[3]).AppendLine();
            }
            int hiddenEntries = entries.Count - visibleEntries;
            if (hiddenEntries > 0)
            {
                text.AppendLine().Append("+ ").Append(hiddenEntries).Append(" more settlements tracked.");
            }
            return text.ToString();
        }

        internal static string GetTrackedOwnerFactionId(string settlementId)
        {
            CalendarWorldLedgerBehavior active = _active;
            if (active == null || string.IsNullOrEmpty(settlementId)) return string.Empty;

            active.CaptureSettlementOwnership(false);
            foreach (string entry in active._settlementOwners)
            {
                string[] fields = entry.Split(new[] { '\t' }, 4);
                if (fields.Length < 3 || !string.Equals(fields[0], settlementId, StringComparison.Ordinal)) continue;
                return fields[2] ?? string.Empty;
            }
            return string.Empty;
        }

        private static bool MatchesFilter(string category, string filter)
        {
            if (string.IsNullOrEmpty(filter) || string.Equals(filter, "All", StringComparison.Ordinal)) return true;
            if (string.Equals(filter, "ByDay", StringComparison.Ordinal)) return true;
            if (string.Equals(filter, "Diplomacy", StringComparison.Ordinal))
                return string.Equals(category, "War", StringComparison.Ordinal) || string.Equals(category, "Peace", StringComparison.Ordinal);
            if (string.Equals(filter, "Settlements", StringComparison.Ordinal))
                return string.Equals(category, "Settlement", StringComparison.Ordinal);
            if (string.Equals(filter, "People", StringComparison.Ordinal))
                return string.Equals(category, "Birth", StringComparison.Ordinal) || string.Equals(category, "Death", StringComparison.Ordinal);
            return false;
        }

        internal static string GetDaySummary(long absoluteDay, string filter)
        {
            CalendarWorldLedgerBehavior active = _active;
            if (active == null || active._entries == null) return string.Empty;

            StringBuilder summary = new StringBuilder();
            for (int index = active._entries.Count - 1; index >= 0 && summary.Length < 34; index--)
            {
                string[] fields = active._entries[index].Split(new[] { '\t' }, 3);
                double recordedDays;
                if (fields.Length != 3 || !double.TryParse(fields[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out recordedDays) || (long)Math.Floor(recordedDays) != absoluteDay || !MatchesFilter(fields[1], filter)) continue;
                if (summary.Length > 0) summary.AppendLine();
                summary.Append('•').Append(' ').Append(fields[1]);
            }

            return summary.ToString();
        }

        private void OnDailyTick()
        {
            // The event listener handles normal captures; this pass also catches
            // ownership changes performed by other mods without raising the event.
            CaptureSettlementOwnership(true);

            // A siege can begin or end without a settlement ownership change.
            // The Strategic Map reads SiegeEvent live, so signal its open
            // screen once per campaign day even when the ownership snapshot is
            // unchanged. This keeps contested territory in sync automatically
            // without re-building the map every frame.
            _ownershipRevision++;
        }

        private void OnWarDeclared(IFaction first, IFaction second, DeclareWarAction.DeclareWarDetail detail)
        {
            Record("War", NameOf(first) + " declared war on " + NameOf(second) + ".");
        }

        private void OnPeaceMade(IFaction first, IFaction second, MakePeaceAction.MakePeaceDetail detail)
        {
            Record("Peace", NameOf(first) + " made peace with " + NameOf(second) + ".");
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero claimant, Hero oldOwner, Hero newOwner, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            UpdateSettlementOwner(settlement, false);
            Record("Settlement", NameOf(settlement) + " changed owner to " + NameOf(newOwner) + ".");
        }

        private void CaptureSettlementOwnership(bool recordChanges)
        {
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement == null || settlement.Town == null) continue;
                UpdateSettlementOwner(settlement, recordChanges);
            }
        }

        // Town.MapFaction is the campaign-map authority for a town/castle's
        // current owner. It is updated by ownership changes directly, whereas
        // reading only OwnerClan can transiently retain an old clan/faction
        // during capture and kingdom-transfer processing. Settlement.MapFaction
        // is retained as a compatibility fallback for nonstandard components.
        internal static IFaction GetLiveSettlementFaction(Settlement settlement)
        {
            if (settlement == null) return null;
            IFaction faction = settlement.Town == null ? null : settlement.Town.MapFaction;
            if (faction != null) return faction;

            faction = settlement.MapFaction;
            if (faction != null) return faction;

            Clan ownerClan = settlement.OwnerClan;
            return ownerClan == null ? null : ownerClan.MapFaction ?? ownerClan;
        }

        private void UpdateSettlementOwner(Settlement settlement, bool recordChanges)
        {
            IFaction faction = GetLiveSettlementFaction(settlement);
            if (settlement == null || faction == null) return;
            string settlementId = settlement.StringId ?? string.Empty;
            string settlementName = NameOf(settlement);
            string factionId = faction.StringId ?? string.Empty;
            string factionName = NameOf(faction);
            string replacement = settlementId + "\t" + settlementName + "\t" + factionId + "\t" + factionName;

            int existingIndex = -1;
            string previousFactionId = null;
            for (int index = 0; index < _settlementOwners.Count; index++)
            {
                string[] fields = _settlementOwners[index].Split(new[] { '\t' }, 4);
                if (fields.Length == 0 || !string.Equals(fields[0], settlementId, StringComparison.Ordinal)) continue;
                existingIndex = index;
                previousFactionId = fields.Length > 2 ? fields[2] : string.Empty;
                break;
            }

            if (existingIndex < 0)
            {
                _settlementOwners.Add(replacement);
                _ownershipRevision++;
                return;
            }

            if (string.Equals(previousFactionId, factionId, StringComparison.Ordinal))
            {
                _settlementOwners[existingIndex] = replacement;
                return;
            }

            _settlementOwners[existingIndex] = replacement;
            _ownershipRevision++;
            if (recordChanges)
            {
                Record("Settlement", settlementName + " is now controlled by " + factionName + ".");
            }
        }

        private void OnGivenBirth(Hero mother, List<Hero> children, int numberOfChildren)
        {
            Record("Birth", NameOf(mother) + " gave birth to " + Math.Max(1, numberOfChildren) + " child" + (numberOfChildren == 1 ? "." : "ren."));
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            Record("Death", NameOf(victim) + " died." );
        }

        private static string NameOf(object value)
        {
            if (value == null) return "Unknown";
            var property = value.GetType().GetProperty("Name");
            object name = property == null ? null : property.GetValue(value, null);
            return name == null ? value.ToString() : name.ToString();
        }
    }
}
