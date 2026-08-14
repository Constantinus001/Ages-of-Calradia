using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;

namespace AgesOfCalradiaLogistics
{
    /// <summary>Entry point for the standalone Ages of Calradia Logistics add-on.</summary>
    public sealed class LogisticsSubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            LogisticsDiagnostics.Info("Module loaded. Version=v0.1.0");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            CampaignGameStarter campaignStarter = gameStarterObject as CampaignGameStarter;
            if (campaignStarter != null)
            {
                campaignStarter.AddBehavior(new LogisticsReserveBehavior());
                LogisticsDiagnostics.Info("Campaign reserve behaviour registered.");
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            if (mission != null)
            {
                // MissionTeamAIType is assigned after this initialization hook.
                // Add lightweight behaviours now; each one verifies field-battle
                // state in AfterStart before it can spawn or modify anything.
                mission.AddMissionBehavior(new BaggageTrainMissionBehavior());
                mission.AddMissionBehavior(new BaggageResupplyMissionBehavior());
                mission.AddMissionBehavior(new BaggageGuardMissionBehavior());
                LogisticsDiagnostics.Info("Baggage and resupply behaviours registered for mission initialization.");
            }
        }
    }
}
