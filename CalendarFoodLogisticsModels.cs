using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Converts party rations to the Gregorian daily cadence. Settlement food
    /// uses its own coordinated town, production, and market conversion.
    /// </summary>
    internal sealed class CalendarMobilePartyFoodConsumptionModel : MobilePartyFoodConsumptionModel
    {
        private readonly MobilePartyFoodConsumptionModel _native;

        internal CalendarMobilePartyFoodConsumptionModel(MobilePartyFoodConsumptionModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override int NumberOfMenOnMapToEatOneFood
        {
            get { return _native.NumberOfMenOnMapToEatOneFood; }
        }

        public override ExplainedNumber CalculateDailyBaseFoodConsumptionf(
            MobileParty party,
            bool includeDescription = false)
        {
            ExplainedNumber result = _native.CalculateDailyBaseFoodConsumptionf(party, includeDescription);
            if (CalendarSettingsState.ExtendedCalendarEnabled)
            {
                SettlementBalanceMath.Scale(ref result);
            }

            return result;
        }

        public override ExplainedNumber CalculateDailyFoodConsumptionf(
            MobileParty party,
            ExplainedNumber baseConsumption)
        {
            // The base consumption above is already annualized. Native perk
            // modifiers must apply to that adjusted base once, not be scaled
            // a second time after their factors have been evaluated.
            return _native.CalculateDailyFoodConsumptionf(party, baseConsumption);
        }

        public override bool DoesPartyConsumeFood(MobileParty mobileParty)
        {
            return _native.DoesPartyConsumeFood(mobileParty);
        }
    }

    /// <summary>
    /// Keeps AI reserve purchases proportional to the slower ration use.
    /// Vanilla's 30-day town reserve therefore becomes about 130 Gregorian
    /// days, holding the same amount of food and the same fraction of a year.
    /// </summary>
    internal sealed class CalendarPartyFoodBuyingModel : PartyFoodBuyingModel
    {
        private readonly PartyFoodBuyingModel _native;

        internal CalendarPartyFoodBuyingModel(PartyFoodBuyingModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override float MinimumDaysFoodToLastWhileBuyingFoodFromTown
        {
            get { return ScaleReserveDays(_native.MinimumDaysFoodToLastWhileBuyingFoodFromTown); }
        }

        public override float MinimumDaysFoodToLastWhileBuyingFoodFromVillage
        {
            get { return ScaleReserveDays(_native.MinimumDaysFoodToLastWhileBuyingFoodFromVillage); }
        }

        public override float LowCostFoodPriceAverage
        {
            get { return _native.LowCostFoodPriceAverage; }
        }

        public override void FindItemToBuy(
            MobileParty mobileParty,
            Settlement settlement,
            out ItemRosterElement itemRosterElement,
            out float itemElementsPrice)
        {
            _native.FindItemToBuy(mobileParty, settlement, out itemRosterElement, out itemElementsPrice);
        }

        private static float ScaleReserveDays(float nativeDays)
        {
            return CalendarSettingsState.ExtendedCalendarEnabled
                ? nativeDays / SettlementBalanceMath.DailyRateFactor
                : nativeDays;
        }
    }

    /// <summary>
    /// Annualizes town food safely. The native model can calculate its direct
    /// village/building/consumption balance without market sales. That direct
    /// balance is scaled once; market food is then added unscaled because its
    /// village and workshop production has already been annualized upstream.
    /// This prevents food-market output from being converted twice.
    /// </summary>
    internal sealed class CalendarSettlementFoodModel : SettlementFoodModel
    {
        private readonly SettlementFoodModel _native;

        internal CalendarSettlementFoodModel(SettlementFoodModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override int FoodStocksUpperLimit
        {
            get { return _native.FoodStocksUpperLimit; }
        }

        public override int NumberOfProsperityToEatOneFood
        {
            get { return _native.NumberOfProsperityToEatOneFood; }
        }

        public override int NumberOfMenOnGarrisonToEatOneFood
        {
            get { return _native.NumberOfMenOnGarrisonToEatOneFood; }
        }

        public override int CastleFoodStockUpperLimitBonus
        {
            get { return _native.CastleFoodStockUpperLimitBonus; }
        }

        public override ExplainedNumber CalculateTownFoodStocksChange(
            Town town,
            bool includeMarketStocks = true,
            bool includeDescriptions = false)
        {
            ExplainedNumber directBalance = _native.CalculateTownFoodStocksChange(
                town,
                includeMarketStocks: false,
                includeDescriptions);
            if (!CalendarSettingsState.ExtendedCalendarEnabled)
            {
                return includeMarketStocks
                    ? _native.CalculateTownFoodStocksChange(town, includeMarketStocks: true, includeDescriptions)
                    : directBalance;
            }

            float nativeDirectResult = directBalance.ResultNumber;
            SettlementBalanceMath.Scale(ref directBalance);
            if (!includeMarketStocks)
            {
                return directBalance;
            }

            float nativeMarketResult = _native.CalculateTownFoodStocksChange(
                town,
                includeMarketStocks: true,
                includeDescriptions: false).ResultNumber - nativeDirectResult;
            directBalance.Add(nativeMarketResult);
            return directBalance;
        }
    }
}
