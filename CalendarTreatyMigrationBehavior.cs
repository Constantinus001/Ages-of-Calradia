using System;
using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Converts tribute agreements already stored in a save to the configured
    /// remaining treaty length exactly once. Future agreements are handled by
    /// the diplomacy model patch at negotiation time.
    /// </summary>
    internal sealed class CalendarTreatyMigrationBehavior : CampaignBehaviorBase
    {
        private const int GregorianTributeTreatyDays = 235;
        private const string MigrationKey = "RealisticCalendarTweaks.TributeTreatyMigrationV1";
        private const string LegacyMigrationKey = "TwelveMonthCalendar.TributeTreatyMigrationV1";
        private bool _hasMigratedExistingTributes;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsLoading)
            {
                bool currentValuePresent = dataStore.SyncData(
                    MigrationKey,
                    ref _hasMigratedExistingTributes);
                if (!currentValuePresent)
                {
                    dataStore.SyncData(LegacyMigrationKey, ref _hasMigratedExistingTributes);
                }

                return;
            }

            dataStore.SyncData(MigrationKey, ref _hasMigratedExistingTributes);
        }

        private void OnSessionLaunched(CampaignGameStarter campaignStarter)
        {
            if (_hasMigratedExistingTributes)
            {
                return;
            }

            int migrated = 0;
            try
            {
                for (int first = 0; first < Kingdom.All.Count; first++)
                {
                    Kingdom faction1 = Kingdom.All[first];
                    if (faction1 == null)
                    {
                        continue;
                    }

                    for (int second = first + 1; second < Kingdom.All.Count; second++)
                    {
                        Kingdom faction2 = Kingdom.All[second];
                        if (faction2 == null)
                        {
                            continue;
                        }

                        StanceLink stance = faction1.GetStanceWith(faction2);
                        int remaining = stance.GetRemainingTributePaymentCount();
                        if (remaining <= 0)
                        {
                            continue;
                        }

                        int completedInstallments = Math.Max(
                            0,
                            stance.DailyTributeInstallments - remaining);
                        stance.DailyTributeInstallments = completedInstallments + GregorianTributeTreatyDays;
                        migrated++;
                    }
                }

                _hasMigratedExistingTributes = true;
                Diagnostics.Info(string.Format(
                    "Tribute treaty migration completed. Existing agreements reset to {0} remaining calendar days; Agreements={1}.",
                    GregorianTributeTreatyDays,
                    migrated));
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Tribute treaty migration failed; it will retry next campaign session without changing incomplete agreements.", exception);
            }
        }
    }
}
