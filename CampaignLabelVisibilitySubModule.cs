using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AgesOfCalradia.CampaignLabelVisibility
{
    public sealed class CampaignLabelVisibilitySubModule : MBSubModuleBase
    {
        private const string HarmonyId = "AgesOfCalradia.CampaignLabelVisibility";
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(typeof(CampaignLabelVisibilitySubModule).Assembly);
        }

        protected override void OnSubModuleUnloaded()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchAll(HarmonyId);
                _harmony = null;
            }
            base.OnSubModuleUnloaded();
        }
    }

    /// <summary>
    /// Hides native settlement names once the campaign camera reaches the
    /// 580-altitude political-overview cutoff. The World Events UI strategic
    /// map owns its own permanent city labels independently.
    /// </summary>
    [HarmonyPatch]
    internal static class SettlementNameplateZoomPatch
    {
        private const float PoliticalOverviewStartAltitude = 580f;

        private static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName(
                "SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM");
            return type == null ? null : AccessTools.Method(type, "UpdateNameplateMT");
        }

        private static void Postfix(
            ref bool ____bindIsVisibleOnMap,
            Vec3 cameraPosition)
        {
            if (cameraPosition.z >= PoliticalOverviewStartAltitude)
                ____bindIsVisibleOnMap = false;
        }
    }
}
