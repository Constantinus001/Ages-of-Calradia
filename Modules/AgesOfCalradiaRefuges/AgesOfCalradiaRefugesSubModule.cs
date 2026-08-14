using System;
using AgesOfCalradia;
using HarmonyLib;
using TwelveMonthCalendar;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace AgesOfCalradiaRefuges
{
    public sealed class AgesOfCalradiaRefugesSubModule : MBSubModuleBase
    {
        private const string HarmonyId = "com.agesofcalradia.refuges";
        private readonly Action _openCamp = OpenCamp;
        private Harmony _harmony;
        private bool _runtimeReady;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Diagnostics.Initialize();

            try
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(typeof(AgesOfCalradiaRefugesSubModule).Assembly);
                CalendarRefugeIntegration.RegisterCampOpener(_openCamp);
                _runtimeReady = true;
                Diagnostics.Info("Ages of Calradia Refuges loaded and registered with the optional calendar map-bar integration.");
            }
            catch (Exception exception)
            {
                _harmony?.UnpatchAll(HarmonyId);
                _harmony = null;
                _runtimeReady = false;
                Diagnostics.Error("Ages of Calradia Refuges could not start; the base module remains available.", exception);
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (!_runtimeReady
                || !(game.GameType is Campaign)
                || !(gameStarterObject is CampaignGameStarter campaignStarter))
            {
                return;
            }

            campaignStarter.AddBehavior(new CalendarRefugeBehavior());
            campaignStarter.AddBehavior(new CalendarCampBehavior());
            Diagnostics.Info("Standalone refuge and camp behaviors registered for campaign.");
        }

        protected override void OnSubModuleUnloaded()
        {
            CalendarRefugeIntegration.UnregisterCampOpener(_openCamp);
            _harmony?.UnpatchAll(HarmonyId);
            _harmony = null;
            _runtimeReady = false;
            base.OnSubModuleUnloaded();
        }

        private static void OpenCamp()
        {
            GameMenu.ActivateGameMenu(CalendarCampBehavior.CampMenuId);
        }
    }
}
