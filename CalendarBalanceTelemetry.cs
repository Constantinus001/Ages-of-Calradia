using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Emits one compact, read-only campaign snapshot per calendar month. It
    /// gives balance reports useful baseline data without modifying campaign
    /// state or adding saved data to existing campaigns.
    /// </summary>
    internal static class CalendarBalanceTelemetry
    {
        private static int _lastSnapshotMonth = int.MinValue;

        internal static void TryRecordMonthlySnapshot()
        {
            if (!CalendarSettingsState.AnnualBalanceDiagnosticsEnabled)
            {
                return;
            }

            try
            {
                CampaignTime now = CampaignTime.Now;
                int calendarMonth = CalendarTimeMath.GetYear(now) * 12 + CalendarTimeMath.GetMonth(now);
                if (calendarMonth == _lastSnapshotMonth)
                {
                    return;
                }

                _lastSnapshotMonth = calendarMonth;

                int townCount = 0;
                double foodStocks = 0d;
                double prosperity = 0d;
                double loyalty = 0d;
                double security = 0d;
                double militia = 0d;
                long townGold = 0L;
                foreach (Town town in Town.AllTowns)
                {
                    if (town == null)
                    {
                        continue;
                    }

                    townCount++;
                    foodStocks += town.FoodStocks;
                    prosperity += town.Prosperity;
                    loyalty += town.Loyalty;
                    security += town.Security;
                    militia += town.Militia;
                    townGold += town.Gold;
                }

                long clanGold = 0L;
                int clanCount = 0;
                foreach (Clan clan in Clan.All)
                {
                    if (clan == null)
                    {
                        continue;
                    }

                    clanCount++;
                    clanGold += clan.Gold;
                }

                Diagnostics.Info(string.Format(
                    "Monthly balance snapshot. Date={0}; Towns={1}; AvgFood={2:F1}; AvgProsperity={3:F1}; AvgLoyalty={4:F1}; AvgSecurity={5:F1}; AvgMilitia={6:F1}; TownGold={7}; Clans={8}; ClanGold={9}; Parties={10}; Caravans={11}; Patrols={12}; Bandits={13}.",
                    now,
                    townCount,
                    Average(foodStocks, townCount),
                    Average(prosperity, townCount),
                    Average(loyalty, townCount),
                    Average(security, townCount),
                    Average(militia, townCount),
                    townGold,
                    clanCount,
                    clanGold,
                    MobileParty.All.Count,
                    MobileParty.AllCaravanParties.Count,
                    MobileParty.AllPatrolParties.Count,
                    MobileParty.AllBanditParties.Count));
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Monthly balance telemetry failed; no campaign state was changed.", exception);
            }
        }

        private static double Average(double total, int count)
        {
            return count == 0 ? 0d : total / count;
        }
    }
}
