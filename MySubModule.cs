using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI;
using TaleWorlds.MountAndBlade;

namespace TwelveMonthCalendar
{
    public class MySubModule : MBSubModuleBase
    {
        private const string HarmonyId = "com.realisticcalendartweaks";
        private static readonly HashSet<string> CorePatchTypeNames = new HashSet<string>(
            StringComparer.Ordinal)
        {
            nameof(CampaignTimeCalendarPatches),
            nameof(CampaignTimeToStringPatch),
            nameof(MapTimeTrackerPatch)
        };
        private Harmony _harmony;
        private bool _runtimePatchesApplied;
        private readonly List<string> _disabledOptionalPatchGroups = new List<string>();

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            Diagnostics.Initialize();
#if STRATEGIC_PROVINCE_DIAGNOSTICS
            StrategicProvinceDiagnosticsLog.Initialize();
            StrategicProvinceDiagnosticsLog.Info("Strategic province diagnostics enabled in the v1.5.5 Test build.");
#endif
            Diagnostics.Info("Submodule load started.");
            CrashFlightRecorder.Record("Module", "OnSubModuleLoad entered.");
            CrashDiagnostics.RegisterUnhandledExceptionHandler();
            CalendarSettingsState.Load();
            CalendarSettingsState.SettingsChanged += OnCalendarSettingsChanged;

            try
            {
                _harmony = new Harmony(HarmonyId);
                CalendarPatchSafetyAudit.BeginStartupAudit();
                ApplyPatchGroups();
                CalendarPatchSafetyAudit.WriteHarmonyPatchAudit(HarmonyId);
                _runtimePatchesApplied = true;
                CrashFlightRecorder.Record("Harmony", "All runtime patches registered successfully.");
                Diagnostics.Info("Harmony patches applied successfully.");
                Diagnostics.Info(
                    string.Format(
                        "Campaign time multiplier: {0:F6}; NormalPace=fixed; FastForwardSpeed={1:F0}; configured common year={2} days; average year={3:F4} days.",
                        CalendarTimeMath.CampaignTimeMultiplier,
                        CalendarSettingsState.FastForwardTimeMultiplier,
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
                    "Harmony patch registration failed. Ages of Calradia runtime patches were disabled to protect the game session.",
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
                CalendarSettingsState.BeginCampaignSession();
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

                MobilePartyFoodConsumptionModel nativeFoodConsumption = campaignStarter.GetModel<MobilePartyFoodConsumptionModel>();
                if (nativeFoodConsumption == null)
                {
                    Diagnostics.Info("Calendar party-food model was not installed because Bannerlord did not provide a MobilePartyFoodConsumptionModel.");
                }
                else if (nativeFoodConsumption is CalendarMobilePartyFoodConsumptionModel)
                {
                    Diagnostics.Info("Calendar party-food model was already installed.");
                }
                else
                {
                    campaignStarter.AddModel(new CalendarMobilePartyFoodConsumptionModel(nativeFoodConsumption));
                    Diagnostics.Info("Calendar party-food model installed; daily rations and AI reserve durations preserve their native fraction of a year.");
                }

                PartyFoodBuyingModel nativeFoodBuying = campaignStarter.GetModel<PartyFoodBuyingModel>();
                if (nativeFoodBuying == null)
                {
                    Diagnostics.Info("Calendar party-food buying model was not installed because Bannerlord did not provide a PartyFoodBuyingModel.");
                }
                else if (nativeFoodBuying is CalendarPartyFoodBuyingModel)
                {
                    Diagnostics.Info("Calendar party-food buying model was already installed.");
                }
                else
                {
                    campaignStarter.AddModel(new CalendarPartyFoodBuyingModel(nativeFoodBuying));
                    Diagnostics.Info("Calendar party-food buying model installed; town and village reserve targets match the Gregorian ration cadence.");
                }

                SettlementFoodModel nativeSettlementFood = campaignStarter.GetModel<SettlementFoodModel>();
                if (nativeSettlementFood == null)
                {
                    Diagnostics.Info("Calendar settlement-food model was not installed because Bannerlord did not provide a SettlementFoodModel.");
                }
                else if (nativeSettlementFood is CalendarSettlementFoodModel)
                {
                    Diagnostics.Info("Calendar settlement-food model was already installed.");
                }
                else
                {
                    campaignStarter.AddModel(new CalendarSettlementFoodModel(nativeSettlementFood));
                    Diagnostics.Info("Calendar settlement-food model installed; direct town food balance, village food, food workshops, party rations, and AI reserves use the matched Gregorian cadence.");
                }

                InstallAnnualBalanceModels(campaignStarter);

                campaignStarter.AddBehavior(new CalendarDiagnosticsBehavior());
#if STRATEGIC_PROVINCE_DIAGNOSTICS
                campaignStarter.AddBehavior(new StrategicProvinceDiagnosticsBehavior());
#endif
                campaignStarter.AddBehavior(new CalendarCampaignProfileBehavior());
                campaignStarter.AddBehavior(new CalendarTreatyMigrationBehavior());
                campaignStarter.AddBehavior(new CalendarWorldLedgerBehavior());
                campaignStarter.AddBehavior(new CampaignKingdomBorderBehavior());
#if STRATEGIC_PROVINCE_DIAGNOSTICS
                campaignStarter.AddBehavior(new CalendarRefugeBehavior());
                campaignStarter.AddBehavior(new CalendarCampBehavior());
#endif
                Diagnostics.Info("Calendar diagnostics behavior registered for campaign.");
#if STRATEGIC_PROVINCE_DIAGNOSTICS
                Diagnostics.Info("Strategic province diagnostics behavior registered; full province snapshots use StrategicProvinceDiagnostics.tsv.");
                Diagnostics.Info("Test-only refuge and camp behaviors registered.");
#endif
                Diagnostics.Info("Calendar soft profile behavior registered; new saves write no calendar module-lock marker.");
                Diagnostics.Info("Calendar treaty migration behavior registered for existing tribute agreements.");
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            try
            {
                // The Strategic Map uses a public custom TextureProvider. Ask
                // Gauntlet to rescan loaded module assemblies after the normal
                // UI stack is available, before the World Calendar movie opens.
                TextureProviderFactory.RefreshProviderTypes();
                Diagnostics.Info("Strategic map texture provider registered with Gauntlet.");
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Gauntlet could not register the Strategic Map texture provider.", exception);
            }

            try
            {
                TaleWorlds.TwoDimension.SpriteCategory legendCategory =
                    TaleWorlds.Engine.GauntletUI.UIResourceManager.LoadSpriteCategory("rct_legend_markers_v2");
                bool townExists = TaleWorlds.Engine.GauntletUI.UIResourceManager.SpriteData
                    .SpriteExists("rct_legend_town_v2");
                bool castleExists = TaleWorlds.Engine.GauntletUI.UIResourceManager.SpriteData
                    .SpriteExists("rct_legend_castle_v2");
                Diagnostics.Info("Strategic legend sprite diagnostic: categoryLoaded="
                    + (legendCategory != null && legendCategory.IsLoaded)
                    + "; townExists=" + townExists + "; castleExists=" + castleExists + ".");
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Gauntlet could not load the Strategic Map legend sprite category.", exception);
            }
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
                + "; NormalPace=fixed"
                + "; FastForwardSpeed=" + CalendarSettingsState.FastForwardTimeMultiplier.ToString("F0")
                + "; LeapYears=" + CalendarSettingsState.UseLeapYears
                + "; DateFormat=" + CalendarSettingsState.DateFormat);
        }

        /// <summary>
        /// Core calendar patches are all-or-nothing. UI, diagnostics, and
        /// balance patches are applied independently so an API change in one
        /// optional feature cannot disable the calendar or crash startup.
        /// </summary>
        private void ApplyPatchGroups()
        {
            Type[] patchTypes = GetPatchTypes();
            foreach (Type patchType in patchTypes.Where(
                type => CorePatchTypeNames.Contains(type.Name)))
            {
                try
                {
                    _harmony.CreateClassProcessor(patchType).Patch();
                    Diagnostics.Info("Core Harmony patch registered: " + patchType.Name + ".");
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        "Required calendar patch could not be registered: " + patchType.FullName,
                        exception);
                }
            }

            // The three core calendar groups must all resolve against the
            // vetted Bannerlord methods. Do not continue with a partially
            // converted calendar if a game update changed one of them.
            CalendarPatchSafetyAudit.EnsureCoreTargetsValidated();

            foreach (Type patchType in patchTypes.Where(
                type => !CorePatchTypeNames.Contains(type.Name)))
            {
                try
                {
                    _harmony.CreateClassProcessor(patchType).Patch();
                    Diagnostics.Info("Optional Harmony patch registered: " + patchType.Name + ".");
                }
                catch (Exception exception)
                {
                    _disabledOptionalPatchGroups.Add(patchType.Name);
                    Diagnostics.Error(
                        "Optional Harmony patch was disabled because its Bannerlord target is incompatible: "
                        + patchType.FullName,
                        exception);
                }
            }

            Diagnostics.Info(
                "Feature health: CoreCalendar=ready; OptionalPatchesDisabled="
                + _disabledOptionalPatchGroups.Count
                + (_disabledOptionalPatchGroups.Count == 0
                    ? "; Disabled=<none>."
                    : "; Disabled=" + string.Join(",", _disabledOptionalPatchGroups) + "."));
        }

        private static Type[] GetPatchTypes()
        {
            try
            {
                return typeof(MySubModule).Assembly
                    .GetTypes()
                    .Where(type => type.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
                    .OrderBy(type => type.FullName, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (ReflectionTypeLoadException exception)
            {
                string failures = string.Join(
                    "; ",
                    exception.LoaderExceptions
                        .Where(loaderException => loaderException != null)
                        .Select(loaderException => loaderException.GetType().Name));
                throw new InvalidOperationException(
                    "Calendar patch types could not be discovered. " + failures,
                    exception);
            }
        }

        private static void InstallAnnualBalanceModels(CampaignGameStarter campaignStarter)
        {
            PregnancyModel nativePregnancy = campaignStarter.GetModel<PregnancyModel>();
            if (nativePregnancy == null)
            {
                Diagnostics.Info("Calendar pregnancy duration was not installed because Bannerlord did not provide a PregnancyModel.");
            }
            else if (nativePregnancy is CalendarPregnancyModel)
            {
                Diagnostics.Info("Calendar pregnancy model was already installed.");
            }
            else
            {
                campaignStarter.AddModel(new CalendarPregnancyModel(nativePregnancy));
                Diagnostics.Info("Calendar pregnancy wrapper installed; due dates use the configured calendar-month duration without private save-data edits.");
            }

            HeroDeathProbabilityCalculationModel nativeHeroDeath = campaignStarter.GetModel<HeroDeathProbabilityCalculationModel>();
            if (nativeHeroDeath == null)
            {
                Diagnostics.Info("Lord old-age mortality scaling was not installed because Bannerlord did not provide a HeroDeathProbabilityCalculationModel.");
            }
            else if (nativeHeroDeath is CalendarHeroDeathProbabilityModel)
            {
                Diagnostics.Info("Calendar lord old-age mortality model was already installed.");
            }
            else
            {
                campaignStarter.AddModel(new CalendarHeroDeathProbabilityModel(nativeHeroDeath));
                Diagnostics.Info(
                    "Calendar lord old-age mortality wrapper installed; eligible noble lords retain "
                    + CalendarSettingsState.LordDeathRateMultiplier.ToString("F2")
                    + " of their native annual death chance.");
            }

            PartyHealingModel nativePartyHealing = campaignStarter.GetModel<PartyHealingModel>();
            if (nativePartyHealing == null)
            {
                Diagnostics.Info("Lord battle-mortality scaling was not installed because Bannerlord did not provide a PartyHealingModel.");
            }
            else if (nativePartyHealing is CalendarLordBattleSurvivalModel)
            {
                Diagnostics.Info("Calendar lord battle-survival model was already installed.");
            }
            else
            {
                campaignStarter.AddModel(new CalendarLordBattleSurvivalModel(nativePartyHealing));
                Diagnostics.Info(
                    "Calendar lord battle-survival wrapper installed; eligible noble lord death chance retains "
                    + CalendarSettingsState.LordDeathRateMultiplier.ToString("F2")
                    + " of native after Bannerlord medicine, armor, age, and damage rules.");
            }

            TournamentModel nativeTournament = campaignStarter.GetModel<TournamentModel>();
            if (nativeTournament == null)
            {
                Diagnostics.Info("Annual tournament balance was not installed because Bannerlord did not provide a TournamentModel.");
            }
            else if (nativeTournament is CalendarTournamentModel)
            {
                Diagnostics.Info("Calendar tournament model was already installed.");
            }
            else
            {
                campaignStarter.AddModel(new CalendarTournamentModel(nativeTournament));
                Diagnostics.Info("Calendar tournament wrapper installed; daily start/end chances are annualized for all towns.");
            }

            SettlementPatrolModel nativePatrol = campaignStarter.GetModel<SettlementPatrolModel>();
            if (nativePatrol == null)
            {
                Diagnostics.Info("Annual patrol balance was not installed because Bannerlord did not provide a SettlementPatrolModel.");
            }
            else if (nativePatrol is CalendarSettlementPatrolModel)
            {
                Diagnostics.Info("Calendar settlement-patrol model was already installed.");
            }
            else
            {
                campaignStarter.AddModel(new CalendarSettlementPatrolModel(nativePatrol));
                Diagnostics.Info("Calendar settlement-patrol wrapper installed; daily patrol spawn durations preserve native annual cadence.");
            }

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
