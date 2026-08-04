using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Uses Bannerlord's public pregnancy-model contract instead of modifying
    /// PregnancyCampaignBehavior's private saved pregnancy records. The native
    /// behavior reads this duration when conception occurs, so calendar-month
    /// pregnancies remain exact while avoiding version-sensitive reflection.
    /// </summary>
    internal sealed class CalendarPregnancyModel : PregnancyModel
    {
        private readonly PregnancyModel _native;

        internal CalendarPregnancyModel(PregnancyModel native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
        }

        public override float PregnancyDurationInDays
        {
            get
            {
                if (!CalendarSettingsState.ExtendedCalendarEnabled)
                {
                    return _native.PregnancyDurationInDays;
                }

                CampaignTime conception = CampaignTime.Now;
                CampaignTime dueDate = CalendarTimeMath.GetPregnancyDueDate(conception);
                return Math.Max(0.1f, (float)(dueDate.ToDays - conception.ToDays));
            }
        }

        public override float GetDailyChanceOfPregnancyForHero(Hero hero)
        {
            return _native.GetDailyChanceOfPregnancyForHero(hero);
        }

        public override float MaternalMortalityProbabilityInLabor
        {
            get { return _native.MaternalMortalityProbabilityInLabor; }
        }

        public override float StillbirthProbability
        {
            get { return _native.StillbirthProbability; }
        }

        public override float DeliveringFemaleOffspringProbability
        {
            get { return _native.DeliveringFemaleOffspringProbability; }
        }

        public override float DeliveringTwinsProbability
        {
            get { return _native.DeliveringTwinsProbability; }
        }
    }
}
