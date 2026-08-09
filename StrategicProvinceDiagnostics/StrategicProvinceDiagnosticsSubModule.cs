using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TwelveMonthCalendar
{
    public sealed class StrategicProvinceDiagnosticsSubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            StrategicProvinceDiagnosticsLog.Initialize();
            StrategicProvinceDiagnosticsLog.Info("Ages of Calradia Diagnostics loaded. This module does not patch or reference the refuge system.");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (game == null || !(game.GameType is Campaign) || !(gameStarterObject is CampaignGameStarter))
            {
                return;
            }

            ((CampaignGameStarter)gameStarterObject).AddBehavior(new StrategicProvinceDiagnosticsBehavior());
            StrategicProvinceDiagnosticsLog.Info("Campaign diagnostics behavior registered.");
        }

        protected override void OnSubModuleUnloaded()
        {
            StrategicProvinceDiagnosticsLog.Info("Ages of Calradia Diagnostics unloaded.");
            base.OnSubModuleUnloaded();
        }
    }
}
