using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Physical, scene-safe refuge staff interactions. These use Bannerlord's
    /// native inquiry controls after the player approaches a staff member, so
    /// management does not require a settlement encounter or campaign menu.
    /// </summary>
    internal static class CalendarRefugeStewardInteraction
    {
        private sealed class StaffChoice
        {
            internal readonly RefugeStaffRole Role;
            internal readonly string Id;

            internal StaffChoice(RefugeStaffRole role, string id)
            {
                Role = role;
                Id = id;
            }
        }

        internal static void Show(RefugeStaffRole role)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            bool isCampSteward = refuge != null
                && refuge.ConstructionState == RefugeConstructionState.Camp
                && role == RefugeStaffRole.Steward;
            if (refuge == null
                || (refuge.ConstructionState != RefugeConstructionState.Complete && !isCampSteward))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "The refuge staff are not available yet."));
                return;
            }

            List<InquiryElement> choices = new List<InquiryElement>
            {
                Choice(
                    role,
                    "status",
                    isCampSteward ? "Review camp plans" : "Review refuge status",
                    true,
                    isCampSteward
                        ? "The Steward can explain how the camp becomes a Palisade Ring refuge."
                        : refuge.GetManagementSummary())
            };

            if (isCampSteward)
            {
                MBInformationManager.ShowMultiSelectionInquiry(
                    new MultiSelectionInquiryData(
                        GetTitle(role),
                        "The Steward is preparing the camp. Upgrade it from the refuge progress menu to raise the default Palisade Ring.",
                        choices,
                        true,
                        1,
                        1,
                        "Speak",
                        "Leave",
                        HandleMainChoice,
                        null),
                    true);
                return;
            }

            if (HasProjects(role, refuge))
            {
                choices.Add(Choice(
                    role,
                    "build",
                    "Discuss construction projects",
                    refuge.ActiveUpgrade == RefugeUpgrade.None,
                    refuge.ActiveUpgrade == RefugeUpgrade.None
                        ? "Review this staff member's available improvements and order one project."
                        : "A construction project is already under way."));
            }

            if (role == RefugeStaffRole.Steward)
            {
                choices.Add(Choice(
                    role,
                    "style",
                    "Change refuge style",
                    refuge.ActiveUpgrade == RefugeUpgrade.None,
                    refuge.ActiveUpgrade == RefugeUpgrade.None
                        ? "Choose a different fort layout. The default Palisade Ring is built when the camp is upgraded."
                        : "Finish the current construction project before changing the refuge style."));
                choices.Add(Choice(
                    role,
                    "stash",
                    "Use the protected stash",
                    refuge.HasUpgrade(RefugeUpgrade.Stash),
                    refuge.HasUpgrade(RefugeUpgrade.Stash)
                        ? "The Steward can open the protected store."
                        : "Build the protected stash first."));
            }

            if (role == RefugeStaffRole.GuardCaptain)
            {
                choices.Add(Choice(
                    role,
                    "garrison",
                    "Manage refuge garrison",
                    true,
                    "Station troops here. Capacity: " + refuge.GarrisonCount + "/" + refuge.GarrisonCapacity + "."));
            }

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    GetTitle(role),
                    GetGreeting(role),
                    choices,
                    true,
                    1,
                    1,
                    "Speak",
                    "Leave",
                    HandleMainChoice,
                    null),
                true);
        }

        private static InquiryElement Choice(
            RefugeStaffRole role,
            string id,
            string title,
            bool enabled,
            string hint)
        {
            return new InquiryElement(new StaffChoice(role, id), title, null, enabled, hint);
        }

        private static void HandleMainChoice(List<InquiryElement> selected)
        {
            if (selected == null || selected.Count == 0) return;
            StaffChoice choice = selected[0].Identifier as StaffChoice;
            if (choice == null) return;

            if (string.Equals(choice.Id, "status", StringComparison.Ordinal))
            {
                ShowStatus(choice.Role);
                return;
            }

            if (string.Equals(choice.Id, "build", StringComparison.Ordinal))
            {
                ShowConstructionProjects(choice.Role);
                return;
            }

            if (string.Equals(choice.Id, "stash", StringComparison.Ordinal)
                && choice.Role == RefugeStaffRole.Steward)
            {
                OpenStash();
            }

            if (string.Equals(choice.Id, "style", StringComparison.Ordinal)
                && choice.Role == RefugeStaffRole.Steward)
            {
                CalendarCampBehavior.ShowFortStyleSelectionForSteward();
            }

            if (string.Equals(choice.Id, "garrison", StringComparison.Ordinal)
                && choice.Role == RefugeStaffRole.GuardCaptain)
            {
                OpenGarrison();
            }
        }

        private static void ShowStatus(RefugeStaffRole role)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            bool isCampSteward = refuge != null
                && refuge.ConstructionState == RefugeConstructionState.Camp
                && role == RefugeStaffRole.Steward;
            InformationManager.ShowInquiry(new InquiryData(
                GetTitle(role),
                refuge == null
                    ? "The refuge ledger is unavailable."
                    : (isCampSteward
                        ? "The camp is ready. Choose “Upgrade camp to refuge” to construct the default Palisade Ring. "
                            + "The Cook, Guard Captain, and Healer will arrive after the walls are complete."
                        : refuge.GetManagementSummary()),
                true,
                false,
                "Continue",
                string.Empty,
                delegate { Show(role); },
                null), true);
        }

        private static void ShowConstructionProjects(RefugeStaffRole role)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            if (refuge == null) return;

            List<InquiryElement> choices = new List<InquiryElement>();
            foreach (RefugeBuildingDefinition building in RefugeBuildingCatalog.All)
            {
                if (CanOffer(role, building.Id, refuge))
                {
                    AddProject(choices, refuge, building);
                }
            }

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    GetTitle(role),
                    "Choose one construction order. Only one project may be built at a time.",
                    choices,
                    true,
                    1,
                    1,
                    "Order project",
                    "Back",
                    OrderSelectedProject,
                    delegate(List<InquiryElement> ignored) { Show(role); }),
                true);
        }

        private static void AddProject(
            List<InquiryElement> choices,
            CalendarRefugeBehavior refuge,
            RefugeBuildingDefinition building)
        {
            bool alreadyBuilt = refuge.HasUpgrade(building.Id);
            bool enoughGold = Hero.MainHero != null && Hero.MainHero.Gold >= building.Cost;
            bool enabled = !alreadyBuilt && refuge.ActiveUpgrade == RefugeUpgrade.None && enoughGold;
            string hint = alreadyBuilt
                ? "This structure is already complete."
                : !enoughGold
                    ? "You do not have enough denars."
                    : building.Effect + " " + building.ConstructionHours + " campaign hours.";
            choices.Add(new InquiryElement(
                building.Id,
                building.Name + " - " + building.Cost + " denars",
                null,
                enabled,
                hint));
        }

        private static void OrderSelectedProject(List<InquiryElement> selected)
        {
            if (selected == null || selected.Count == 0) return;
            RefugeUpgrade upgrade = selected[0].Identifier is RefugeUpgrade
                ? (RefugeUpgrade)selected[0].Identifier
                : RefugeUpgrade.None;
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            string failure = string.Empty;
            int cost = upgrade == RefugeUpgrade.None ? 0 : RefugeBuildingCatalog.Get(upgrade).Cost;
            if (refuge == null || cost <= 0 || !refuge.TryPurchaseUpgrade(upgrade, cost, out failure))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    string.IsNullOrEmpty(failure) ? "The construction order could not be placed." : failure));
                return;
            }

            InformationManager.DisplayMessage(new InformationMessage(
                "The project has been ordered. Construction will take "
                + CalendarRefugeBehavior.GetUpgradeConstructionHours(upgrade) + " campaign hours."));
        }

        private static void OpenStash()
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            string failure = string.Empty;
            if (refuge == null || !refuge.TryOpenStash(out failure))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    string.IsNullOrEmpty(failure) ? "The protected stash is unavailable." : failure));
            }
        }

        private static void OpenGarrison()
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            string failure = string.Empty;
            if (refuge == null || !refuge.TryOpenGarrison(out failure))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    string.IsNullOrEmpty(failure) ? "The refuge garrison is unavailable." : failure));
            }
        }

        private static bool HasProjects(RefugeStaffRole role, CalendarRefugeBehavior refuge)
        {
            foreach (RefugeBuildingDefinition building in RefugeBuildingCatalog.All)
            {
                if (CanOffer(role, building.Id, refuge) && !refuge.HasUpgrade(building.Id))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool CanOffer(
            RefugeStaffRole role,
            RefugeUpgrade upgrade,
            CalendarRefugeBehavior refuge)
        {
            switch (role)
            {
                case RefugeStaffRole.Cook:
                    return refuge.HasUpgrade(RefugeUpgrade.Barracks)
                        && upgrade == RefugeUpgrade.Tavern;
                case RefugeStaffRole.GuardCaptain:
                    return refuge.HasUpgrade(RefugeUpgrade.GuardTowers)
                        && (upgrade == RefugeUpgrade.Barracks
                        || upgrade == RefugeUpgrade.SleepingQuarters
                        || upgrade == RefugeUpgrade.TrainingYard);
                case RefugeStaffRole.Healer:
                    return refuge.HasUpgrade(RefugeUpgrade.Tavern)
                        && upgrade == RefugeUpgrade.Infirmary;
                default:
                    return upgrade == RefugeUpgrade.StaffTents
                        || upgrade == RefugeUpgrade.Blacksmith
                        || upgrade == RefugeUpgrade.Stash
                        || upgrade == RefugeUpgrade.GuardTowers;
            }
        }

        private static string GetTitle(RefugeStaffRole role)
        {
            switch (role)
            {
                case RefugeStaffRole.Cook:
                    return "Refuge Cook";
                case RefugeStaffRole.GuardCaptain:
                    return "Refuge Guard Captain";
                case RefugeStaffRole.Healer:
                    return "Refuge Healer";
                default:
                    return "Refuge Steward";
            }
        }

        private static string GetGreeting(RefugeStaffRole role)
        {
            switch (role)
            {
                case RefugeStaffRole.Cook:
                    return "The Cook looks up from the hearth. What does the camp need?";
                case RefugeStaffRole.GuardCaptain:
                    return "The Guard Captain studies the refuge defenses. What are your orders?";
                case RefugeStaffRole.Healer:
                    return "The Healer checks her medicines and bandages. Who needs care?";
                default:
                    return "The Steward looks over the refuge ledger. What do you need?";
            }
        }
    }
}
