using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace AgesOfCalradiaSuccession
{
    /// <summary>
    /// Sidecar notification for the existing Ages of Calradia campaign-map
    /// political border behavior. This does not replace or patch its renderer.
    /// </summary>
    internal static class SuccessionCampaignMapBorderBridge
    {
        private const string BorderBehaviorTypeName = "TwelveMonthCalendar.CampaignKingdomBorderBehavior";

        internal static bool RequestRefresh(out string detail)
        {
            detail = string.Empty;
            if (Campaign.Current == null)
            {
                detail = "no active campaign";
                return false;
            }

            try
            {
                Type behaviorType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(BorderBehaviorTypeName, false))
                    .FirstOrDefault(type => type != null);
                if (behaviorType == null)
                {
                    detail = "campaign political border behavior is unavailable";
                    return false;
                }

                MethodInfo getBehavior = typeof(Campaign).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(method => method.Name == "GetCampaignBehavior" && method.IsGenericMethodDefinition
                        && method.GetParameters().Length == 0);
                MethodInfo markDirty = behaviorType.GetMethod("MarkDirty", BindingFlags.Instance | BindingFlags.NonPublic);
                if (getBehavior == null || markDirty == null)
                {
                    detail = "campaign political border refresh entry point is unavailable";
                    return false;
                }

                object behavior = getBehavior.MakeGenericMethod(behaviorType).Invoke(Campaign.Current, null);
                if (behavior == null)
                {
                    detail = "campaign political border behavior is not registered";
                    return false;
                }

                markDirty.Invoke(behavior, null);
                detail = "campaign political border rebuild requested";
                return true;
            }
            catch (Exception exception)
            {
                Exception actual = exception is TargetInvocationException && exception.InnerException != null
                    ? exception.InnerException
                    : exception;
                SuccessionDiagnostics.Error("Campaign political border refresh request failed.", actual);
                detail = "campaign political border refresh failed; its daily ownership audit will retry";
                return false;
            }
        }
    }
}
