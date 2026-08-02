using System;
using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    internal sealed class CalendarDiagnosticsBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this,
                OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter campaignStarter)
        {
            try
            {
                CalendarSettingsState.MarkCampaignSessionStarted();
                CampaignTime now = CampaignTime.Now;
                Diagnostics.Info(
                    string.Format(
                        "Campaign session launched. NowDays={0:F3}; Date={1}; Year={2}; DayOfYear={3}; Season={4}; DayOfSeason={5}; LogPath={6}",
                        now.ToDays,
                        now,
                        now.GetYear,
                        now.GetDayOfYear,
                        now.GetSeasonOfYear,
                        now.GetDayOfSeason,
                        Diagnostics.LogPath));
                int year = now.GetYear;
                Diagnostics.Info(
                    string.Format(
                        "Calendar structure. YearLength={0}; SeasonLengths={1},{2},{3},{4}; AutoTimeScale={5}; TimeScale={6:F6}",
                        CalendarTimeMath.GetYearLength(year),
                        CalendarTimeMath.GetSeasonLength(year, 0),
                        CalendarTimeMath.GetSeasonLength(year, 1),
                        CalendarTimeMath.GetSeasonLength(year, 2),
                        CalendarTimeMath.GetSeasonLength(year, 3),
                        CalendarSettingsState.AutoCampaignTimeScale,
                        CalendarSettingsState.CampaignTimeScale));

                if (Hero.MainHero != null)
                {
                    Diagnostics.Info(
                        string.Format(
                            "Main hero age check. Age={0:F3}; BirthDay={1}; BirthDayDays={2:F3}",
                            Hero.MainHero.Age,
                            Hero.MainHero.BirthDay,
                            Hero.MainHero.BirthDay.ToDays));
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Session diagnostics failed.", exception);
            }
        }
    }
}
