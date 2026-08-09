using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Current Bannerlord version hook for campaign-map left clicks. The
    /// SandBox view assembly is intentionally resolved by name so a future
    /// game update disables this optional patch cleanly instead of preventing
    /// the calendar module from loading.
    /// </summary>
    [HarmonyPatch]
    internal static class CalendarRefugeMapClickPatch
    {
        private static MethodBase TargetMethod()
        {
            System.Type mapScreenType = AccessTools.TypeByName("SandBox.View.Map.MapScreen");
            return mapScreenType == null
                ? null
                : AccessTools.Method(mapScreenType, "HandleLeftMouseButtonClick");
        }

        [HarmonyPrepare]
        private static bool Prepare()
        {
            return CalendarSettingsState.RefugeSystemEnabled && TargetMethod() != null;
        }

        [HarmonyPrefix]
        private static bool Prefix(CampaignVec2 __1)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            if (refuge == null)
            {
                return true;
            }

            try
            {
                // Returning false consumes only clicks on the refuge marker.
                return !refuge.TryOpenProgressFromMapClick(__1);
            }
            catch (System.Exception exception)
            {
                Diagnostics.Error("Refuge map-click patch failed safely; native click handling was preserved.", exception);
                return true;
            }
        }
    }
}
