using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Supplies the current workshop to the public production-speed postfix.
    /// Bannerlord's WorkshopModel method has no output-category argument, so
    /// this narrow private-method context is the safest way to avoid slowing
    /// food-producing workshops along with long-term manufactured goods.
    /// </summary>
    internal static class WorkshopFoodContext
    {
        [ThreadStatic]
        private static Workshop _activeWorkshop;

        internal static Workshop ActiveWorkshop
        {
            get { return _activeWorkshop; }
        }

        internal static bool ProducesFood(Workshop workshop)
        {
            if (workshop == null || workshop.WorkshopType == null)
            {
                return false;
            }

            foreach (WorkshopType.Production production in workshop.WorkshopType.Productions)
            {
                foreach (var output in production.Outputs)
                {
                    ItemCategory category = output.Item1;
                    if (category != null
                        && category.Properties == ItemCategory.Property.BonusToFoodStores)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static Workshop Push(Workshop workshop)
        {
            Workshop previous = _activeWorkshop;
            _activeWorkshop = workshop;
            return previous;
        }

        internal static void Restore(Workshop previous)
        {
            _activeWorkshop = previous;
        }
    }

    [HarmonyPatch]
    internal static class WorkshopFoodContextPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(WorkshopsCampaignBehavior), "RunTownWorkshop");
        }

        [HarmonyPrefix]
        private static void Prefix(Workshop workshop, out Workshop __state)
        {
            __state = WorkshopFoodContext.Push(workshop);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, Workshop __state)
        {
            WorkshopFoodContext.Restore(__state);
            return __exception;
        }
    }
}
