using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
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
                double directFoodChange = 0d;
                double marketFoodChange = 0d;
                double prosperity = 0d;
                double loyalty = 0d;
                double security = 0d;
                double militia = 0d;
                long townGold = 0L;
                int criticalTownReports = 0;
                int foodStockCapCount = 0;
                int emptyFoodStockCount = 0;
                SettlementFoodModel foodModel = Campaign.Current.Models.SettlementFoodModel;
                foreach (Town town in Town.AllTowns)
                {
                    if (town == null)
                    {
                        continue;
                    }

                    townCount++;
                    foodStocks += town.FoodStocks;
                    ExplainedNumber directFood = foodModel.CalculateTownFoodStocksChange(
                        town,
                        includeMarketStocks: false,
                        includeDescriptions: false);
                    ExplainedNumber totalFood = foodModel.CalculateTownFoodStocksChange(
                        town,
                        includeMarketStocks: true,
                        includeDescriptions: false);
                    directFoodChange += directFood.ResultNumber;
                    marketFoodChange += totalFood.ResultNumber - directFood.ResultNumber;
                    if (town.FoodStocks <= 0f)
                    {
                        emptyFoodStockCount++;
                    }
                    else if (town.FoodStocks >= town.FoodStocksUpperLimit())
                    {
                        foodStockCapCount++;
                    }
                    prosperity += town.Prosperity;
                    loyalty += town.Loyalty;
                    security += town.Security;
                    militia += town.Militia;
                    townGold += town.Gold;

                    if (criticalTownReports < 12
                        && (town.Settlement.IsStarving || town.FoodStocks <= 30f || town.Loyalty <= 40f))
                    {
                        ExplainedNumber loyaltyChange = Campaign.Current.Models.SettlementLoyaltyModel
                            .CalculateLoyaltyChange(town, includeDescriptions: true);
                        Diagnostics.Info(string.Format(
                            "Settlement watch. Town={0}; Food={1:F1}; FoodChange={2:F2}; Starving={3}; StarvingDays={4:F1}; Loyalty={5:F1}; LoyaltyChange={6:F2}; Security={7:F1}; OwnerCultureMatch={8}; LoyaltyBreakdown={9}",
                            town.Name,
                            town.FoodStocks,
                            town.FoodChange,
                            town.Settlement.IsStarving,
                            town.Settlement.Party.DaysStarving,
                            town.Loyalty,
                            loyaltyChange.ResultNumber,
                            town.Security,
                            town.Settlement.OwnerClan != null && town.Settlement.OwnerClan.Culture == town.Settlement.Culture,
                            loyaltyChange.GetExplanations().Replace(Environment.NewLine, " | ")));
                        criticalTownReports++;
                    }
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
                    "Monthly balance snapshot. Date={0}; Towns={1}; AvgFood={2:F1}; AvgDirectFoodChange={3:F2}; AvgMarketFoodChange={4:F2}; EmptyFoodTowns={5}; CappedFoodTowns={6}; AvgProsperity={7:F1}; AvgLoyalty={8:F1}; AvgSecurity={9:F1}; AvgMilitia={10:F1}; TownGold={11}; Clans={12}; ClanGold={13}; Parties={14}; Caravans={15}; Patrols={16}; Bandits={17}.",
                    now,
                    townCount,
                    Average(foodStocks, townCount),
                    Average(directFoodChange, townCount),
                    Average(marketFoodChange, townCount),
                    emptyFoodStockCount,
                    foodStockCapCount,
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
                CalendarFinanceTelemetry.ReportMonthlyHealth();
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
