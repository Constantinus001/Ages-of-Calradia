using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace AgesOfCalradiaReligions
{
    /// <summary>Standalone entry point for the Ages of Calradia religion system.</summary>
    public sealed class ReligionSubModule : MBSubModuleBase
    {
        private const string HarmonyId = "com.agesofcalradia.religions";
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ReligionDiagnostics.Initialize();
            try
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                StrategicMapModeIntegration.Install(_harmony);
                MapOverlayTextColorIntegration.Install(_harmony);
                ReligionDiagnostics.Info("Population tax, recruitment, and army-size integrations registered.");
            }
            catch (Exception exception)
            {
                ReligionDiagnostics.Error("Population integrations could not be registered; campaign behaviors will continue without model patches.", exception);
                _harmony = null;
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            var campaignStarter = gameStarterObject as CampaignGameStarter;
            if (campaignStarter != null)
            {
                campaignStarter.AddBehavior(new ReligionCampaignBehavior());
                campaignStarter.AddBehavior(new PopulationCampaignBehavior());
                campaignStarter.AddBehavior(new OpeningPeaceBehavior());
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            if (_harmony != null)
            {
                MapOverlayTextColorIntegration.Reset();
                StrategicMapModeIntegration.Reset();
                _harmony.UnpatchAll(HarmonyId);
                _harmony = null;
            }

            base.OnSubModuleUnloaded();
        }
    }
}
