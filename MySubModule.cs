using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TwelveMonthCalendar
{
    public sealed class MySubModule : MBSubModuleBase
    {
        private const string HarmonyId = "com.codex.twelvemonthcalendar";
        private Harmony _harmony;
        private bool _runtimePatchesApplied;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            Diagnostics.Initialize();
            Diagnostics.Info("Submodule load started.");
            CrashFlightRecorder.Record("Module", "OnSubModuleLoad entered.");
            CrashDiagnostics.RegisterUnhandledExceptionHandler();
            CalendarSettingsState.Load();
            CalendarSettingsState.SettingsChanged += OnCalendarSettingsChanged;

            try
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(typeof(MySubModule).Assembly);
                _runtimePatchesApplied = true;
                CrashFlightRecorder.Record("Harmony", "All runtime patches registered successfully.");
                Diagnostics.Info("Harmony patches applied successfully.");
                Diagnostics.Info(
                    string.Format(
                        "Campaign time multiplier: {0:F6}; configured common year={1} days; average year={2:F4} days.",
                        CalendarTimeMath.CampaignTimeMultiplier,
                        CalendarTimeMath.DaysInYear,
                        CalendarTimeMath.AverageDaysInYear));
                Diagnostics.Info(
                    string.Format(
                        "Balance factors: DailyRate={0:F6}; PartyBaseSpeed={1:F2}; Pregnancy={2} months / {3:F2} fixed days.",
                        CalendarTimeMath.NativeDaysInYear / CalendarTimeMath.AverageDaysInYear,
                        4f,
                        CalendarSettingsState.PregnancyDurationMonths,
                        CalendarTimeMath.PregnancyDurationInDays));
                Diagnostics.Info(
                    "Native diplomacy balance support registered. Active="
                    + CalendarSettingsState.ExtendedCalendarEnabled
                    + "; it scales annual proposal cadence, war/peace cooldowns, treaty durations, alliance timing, and hourly influence rewards while the Gregorian calendar is active.");
            }
            catch (Exception exception)
            {
                // A Bannerlord update can change or remove a private target.
                // Remove every partial patch and leave the game running with
                // this mod's runtime behavior disabled, rather than rethrowing
                // into Bannerlord's startup sequence.
                Diagnostics.Error(
                    "Harmony patch registration failed. Twelve Month Calendar runtime patches were disabled to protect the game session.",
                    exception);
                try
                {
                    _harmony?.UnpatchAll(HarmonyId);
                }
                catch (Exception cleanupException)
                {
                    Diagnostics.Error("Harmony patch cleanup after a failed registration also failed.", cleanupException);
                }

                _harmony = null;
                _runtimePatchesApplied = false;
                CrashFlightRecorder.Record("Harmony", "Runtime patch registration failed and partial patches were removed.");
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            CrashFlightRecorder.Record(
                "Module",
                "OnGameStart. GameType=" + (game.GameType == null ? "<null>" : game.GameType.GetType().FullName));

            if (_runtimePatchesApplied
                && game.GameType is Campaign
                && gameStarterObject is CampaignGameStarter campaignStarter)
            {
                ClanFinanceModel nativeFinance = campaignStarter.GetModel<ClanFinanceModel>();
                if (nativeFinance == null)
                {
                    Diagnostics.Info("Clan-finance scaling was not installed because Bannerlord did not provide a ClanFinanceModel.");
                }
                else if (nativeFinance is CalendarClanFinanceModel)
                {
                    Diagnostics.Info("Calendar clan-finance model was already installed.");
                }
                else
                {
                    campaignStarter.AddModel(new CalendarClanFinanceModel(nativeFinance));
                    Diagnostics.Info("Calendar clan-finance wrapper installed without patching DefaultClanFinanceModel.");
                }

                PartySpeedModel nativePartySpeed = campaignStarter.GetModel<PartySpeedModel>();
                if (nativePartySpeed == null)
                {
                    Diagnostics.Info("Calendar party-speed model was not installed because Bannerlord did not provide a PartySpeedModel.");
                }
                else if (nativePartySpeed is CalendarPartySpeedModel)
                {
                    Diagnostics.Info("Calendar party-speed model was already installed.");
                }
                else
                {
                    campaignStarter.AddModel(new CalendarPartySpeedModel(nativePartySpeed));
                    Diagnostics.Info(
                        "Calendar party-speed wrapper installed. Common base speed is 4.00; native movement modifiers remain active.");
                    CrashFlightRecorder.Record("Movement", "Common base-speed party/army wrapper installed; base=4.00.");
                }

                InstallAnnualBalanceModels(campaignStarter);

                campaignStarter.AddBehavior(new CalendarDiagnosticsBehavior());
                campaignStarter.AddBehavior(new CalendarSaveCompatibilityBehavior());
                Diagnostics.Info("Calendar diagnostics behavior registered for campaign.");
                Diagnostics.Info("Calendar save-compatibility marker registered; saves written by v1.3 require Twelve Month Calendar to load.");
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
            CrashFlightRecorder.Record("Module", "OnSubModuleUnloaded entered.");
            CalendarSettingsState.SettingsChanged -= OnCalendarSettingsChanged;
            _harmony?.UnpatchAll(HarmonyId);
            _harmony = null;
            _runtimePatchesApplied = false;

            base.OnSubModuleUnloaded();
        }

        private void OnCalendarSettingsChanged()
        {
            CrashFlightRecorder.Record(
                "Settings",
                "SettingsChanged; TimeScale=" + CalendarSettingsState.CampaignTimeScale.ToString("F6")
                + "; LeapYears=" + CalendarSettingsState.UseLeapYears
                + "; DateFormat=" + CalendarSettingsState.DateFormat);
        }

        private static void InstallAnnualBalanceModels(CampaignGameStarter campaignStarter)
        {
            PartyImpairmentModel nativeImpairment = campaignStarter.GetModel<PartyImpairmentModel>();
            if (nativeImpairment == null)
            {
                Diagnostics.Info("Annual impairment balance was not installed because Bannerlord did not provide a PartyImpairmentModel.");
            }
            else if (nativeImpairment is CalendarPartyImpairmentModel)
            {
                Diagnostics.Info("Calendar party-impairment model was already installed.");
            }
            else
            {
                campaignStarter.AddModel(new CalendarPartyImpairmentModel(nativeImpairment));
                Diagnostics.Info("Calendar party-impairment wrapper installed; recovery durations preserve their native fraction of a campaign year.");
            }

            PrisonerRecruitmentCalculationModel nativePrisonerRecruitment = campaignStarter.GetModel<PrisonerRecruitmentCalculationModel>();
            if (nativePrisonerRecruitment == null)
            {
                Diagnostics.Info("Annual prisoner-recruitment balance was not installed because Bannerlord did not provide a PrisonerRecruitmentCalculationModel.");
            }
            else if (nativePrisonerRecruitment is CalendarPrisonerRecruitmentModel)
            {
                Diagnostics.Info("Calendar prisoner-recruitment model was already installed.");
            }
            else
            {
                campaignStarter.AddModel(new CalendarPrisonerRecruitmentModel(nativePrisonerRecruitment));
                Diagnostics.Info("Calendar prisoner-recruitment wrapper installed; conformity gain per campaign hour is annualized for player and AI parties.");
            }

            MarriageModel nativeMarriage = campaignStarter.GetModel<MarriageModel>();
            if (nativeMarriage == null)
            {
                Diagnostics.Info("Annual NPC-marriage balance was not installed because Bannerlord did not provide a MarriageModel.");
            }
            else if (nativeMarriage is CalendarMarriageModel)
            {
                Diagnostics.Info("Calendar marriage model was already installed.");
            }
            else
            {
                campaignStarter.AddModel(new CalendarMarriageModel(nativeMarriage));
                Diagnostics.Info("Calendar marriage wrapper installed; NPC marriage chance is annualized while player marriage rules remain native.");
            }

            MapTrackModel nativeMapTracks = campaignStarter.GetModel<MapTrackModel>();
            if (nativeMapTracks == null)
            {
                Diagnostics.Info("Annual map-track balance was not installed because Bannerlord did not provide a MapTrackModel.");
            }
            else if (nativeMapTracks is CalendarMapTrackModel)
            {
                Diagnostics.Info("Calendar map-track model was already installed.");
            }
            else
            {
                campaignStarter.AddModel(new CalendarMapTrackModel(nativeMapTracks));
                Diagnostics.Info("Calendar map-track wrapper installed; track lifetimes preserve their native fraction of a campaign year.");
            }
        }
    }
}
