using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TwelveMonthCalendar
{
    public sealed class MySubModule : MBSubModuleBase
    {
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            Diagnostics.Initialize();
            Diagnostics.Info("Submodule load started.");
            CrashDiagnostics.RegisterUnhandledExceptionHandler();
            CalendarSettingsState.Load();

            try
            {
                _harmony = new Harmony("com.codex.twelvemonthcalendar");
                _harmony.PatchAll(typeof(MySubModule).Assembly);
                Diagnostics.Info("Harmony patches applied successfully.");
                Diagnostics.Info(
                    string.Format(
                        "Campaign time multiplier: {0:F6}; configured common year={1} days; average year={2:F4} days.",
                        CalendarTimeMath.CampaignTimeMultiplier,
                        CalendarTimeMath.DaysInYear,
                        CalendarTimeMath.AverageDaysInYear));
                Diagnostics.Info(
                    string.Format(
                        "Balance factors: DailyRate={0:F6}; PartySpeedCompensation={1:F6}; Pregnancy={2} months / {3:F2} fixed days.",
                        CalendarTimeMath.NativeDaysInYear / CalendarTimeMath.AverageDaysInYear,
                        1f,
                        CalendarSettingsState.PregnancyDurationMonths,
                        CalendarTimeMath.PregnancyDurationInDays));
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Harmony patch registration failed.", exception);
                throw;
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            DefaultClanFinanceInitialization.InitializeAfterCampaignGameStart(game);

            if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter campaignStarter)
            {
                campaignStarter.AddBehavior(new CalendarDiagnosticsBehavior());
                Diagnostics.Info("Calendar diagnostics behavior registered for campaign.");
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            OptionalMcmIntegration.TryInitialize();
        }

        protected override void OnSubModuleUnloaded()
        {
            Diagnostics.Info("Submodule unload started.");
            _harmony?.UnpatchAll("com.codex.twelvemonthcalendar");
            _harmony = null;

            base.OnSubModuleUnloaded();
        }
    }
}
