using System;
using System.Collections.Generic;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Single source of truth for refuge construction.  The present enum is a
    /// compact save representation; gameplay UI must obtain building data
    /// from this catalog rather than repeat prices and durations in menus.
    /// </summary>
    internal sealed class RefugeBuildingDefinition
    {
        internal readonly RefugeUpgrade Id;
        internal readonly string Name;
        internal readonly int Cost;
        internal readonly int ConstructionHours;
        internal readonly string Effect;

        internal RefugeBuildingDefinition(
            RefugeUpgrade id,
            string name,
            int cost,
            int constructionHours,
            string effect)
        {
            Id = id;
            Name = name;
            Cost = cost;
            ConstructionHours = constructionHours;
            Effect = effect;
        }
    }

    internal static class RefugeBuildingCatalog
    {
        // Temporary testing pace. Restore individual durations here once the
        // refuge layout and upgrade visuals are approved.
        private const int TestConstructionHours = 1;

        private static readonly RefugeBuildingDefinition[] BuildingDefinitions =
        {
            new RefugeBuildingDefinition(RefugeUpgrade.Barracks, "Barracks", 1200, TestConstructionHours,
                "Adds room for a larger garrison and strengthens the refuge."),
            new RefugeBuildingDefinition(RefugeUpgrade.Tavern, "Tavern and cookhouse", 900, TestConstructionHours,
                "Improves morale when the party rests at the refuge."),
            new RefugeBuildingDefinition(RefugeUpgrade.StaffTents, "Staff tents", 650, TestConstructionHours,
                "Adds administrative capacity for refuge staff and garrison."),
            new RefugeBuildingDefinition(RefugeUpgrade.SleepingQuarters, "Sleeping quarters", 700, TestConstructionHours,
                "Adds capacity and reduces future garrison upkeep."),
            new RefugeBuildingDefinition(RefugeUpgrade.Blacksmith, "Blacksmith", 1100, TestConstructionHours,
                "Reserves a future equipment repair and supply service."),
            new RefugeBuildingDefinition(RefugeUpgrade.Stash, "Protected stash", 350, TestConstructionHours,
                "Unlocks secure item storage inside the palisade."),
            new RefugeBuildingDefinition(RefugeUpgrade.GuardTowers, "Guard towers", 1500, TestConstructionHours,
                "Substantially raises refuge defense against future raids."),
            new RefugeBuildingDefinition(RefugeUpgrade.Infirmary, "Infirmary", 800, TestConstructionHours,
                "Creates a treatment tent for recovery and medical services."),
            new RefugeBuildingDefinition(RefugeUpgrade.TrainingYard, "Training yard", 1000, TestConstructionHours,
                "Gives low-tier refuge garrison troops training experience during rest.")
        };

        internal static IEnumerable<RefugeBuildingDefinition> All
        {
            get { return BuildingDefinitions; }
        }

        internal static RefugeBuildingDefinition Get(RefugeUpgrade id)
        {
            for (int index = 0; index < BuildingDefinitions.Length; index++)
            {
                if (BuildingDefinitions[index].Id == id)
                {
                    return BuildingDefinitions[index];
                }
            }

            throw new ArgumentOutOfRangeException("id", "Unknown refuge building.");
        }

        internal static int GetConstructionHours(RefugeUpgrade id)
        {
            return id == RefugeUpgrade.None ? 0 : Get(id).ConstructionHours;
        }
    }
}
