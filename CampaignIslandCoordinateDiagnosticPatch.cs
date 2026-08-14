using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace TwelveMonthCalendar
{
    [HarmonyPatch]
    internal static class CampaignIslandCoordinateDiagnosticPatch
    {
        private static MethodBase TargetMethod()
        {
            System.Type mapScreenType = AccessTools.TypeByName("SandBox.View.Map.MapScreen");
            MethodBase method = mapScreenType == null ? null : AccessTools.Method(mapScreenType, "HandleLeftMouseButtonClick");
            Diagnostics.Info("Campaign island diagnostic handler=" + (method == null ? "missing" : method.ToString()) + ".");
            return method;
        }

        private static void Postfix(object[] __args)
        {
            string[] values = new string[__args == null ? 0 : __args.Length];
            for (int index = 0; index < values.Length; index++)
            {
                object value = __args[index];
                values[index] = value == null ? "null" : value.GetType().FullName + "=" + value;
            }
            Diagnostics.Info("Campaign island diagnostic click arguments: " + string.Join(" | ", values) + ".");
        }
    }
}
