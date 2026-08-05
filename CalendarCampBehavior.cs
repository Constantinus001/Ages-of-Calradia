using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Campaign camp menu. It owns resting and delegates the
    /// persistent Viking Conquest-style refuge record to
    /// <see cref="CalendarRefugeBehavior"/>. Refuge visits use an isolated
    /// mission and never create a campaign settlement.
    /// </summary>
    internal sealed class CalendarCampBehavior : CampaignBehaviorBase
    {
        internal const string CampMenuId = "realistic_calendar_camp";
        internal const string RefugeStatusMenuId = "realistic_calendar_refuge_status";
        private const string RefugeUpgradesMenuId = "realistic_calendar_refuge_upgrades";
        private const string CampRestMenuId = "realistic_calendar_camp_rest";
        private const float RestHours = 8f;
        private const float DawnHour = 7f;
        private static float _activeRestHours = RestHours;
        private const string CampTentPrefabId = "map_icon_siege_camp_tent";

        // A compact native camp marker beside the resting party.
        private static readonly Vec2[] CampTentOffsets =
        {
            new Vec2(-0.45f, -0.28f),
            new Vec2(0.42f, -0.20f),
            new Vec2(-0.52f, 0.32f),
            new Vec2(0.00f, 0.50f),
            new Vec2(0.55f, 0.22f)
        };

        private static readonly List<GameEntity> CampTentEntities = new List<GameEntity>();
        private static Scene _campTentScene;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // The temporary camp itself has no save data. The separate
            // CalendarRefugeBehavior owns the persistent refuge record.
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddGameMenu(
                CampMenuId,
                "{=RCT_CampMenuTitle}You set up camp. What do you want to do?",
                OnCampMenuInit,
                GameMenu.MenuOverlayType.None);

            starter.AddGameMenu(
                RefugeStatusMenuId,
                "{=RCT_RefugeStatusTitle}Refuge construction progress",
                OnRefugeStatusMenuInit,
                GameMenu.MenuOverlayType.None);

            starter.AddGameMenu(
                RefugeUpgradesMenuId,
                "{=RCT_RefugeUpgradesTitle}The sergeant-at-arms reviews the refuge plans.",
                OnRefugeUpgradesMenuInit,
                GameMenu.MenuOverlayType.None);

            starter.AddGameMenuOption(
                RefugeStatusMenuId,
                "realistic_calendar_refuge_status_progress",
                "{=RCT_RefugeStatusProgress}Construction progress",
                RefugeStatusProgressCondition,
                null,
                isLeave: false,
                index: -1);

            starter.AddGameMenuOption(
                RefugeStatusMenuId,
                "realistic_calendar_refuge_status_enter",
                "{=RCT_RefugeStatusEnter}Enter the refuge",
                CanEnterRefuge,
                EnterRefuge,
                isLeave: false,
                index: -1);

            starter.AddGameMenuOption(
                RefugeStatusMenuId,
                "realistic_calendar_refuge_status_sergeant",
                "{=RCT_RefugeSergeant}Speak with the sergeant-at-arms",
                CanManageRefuge,
                OpenRefugeUpgrades,
                isLeave: false,
                index: -1);

            starter.AddGameMenuOption(
                RefugeStatusMenuId,
                "realistic_calendar_refuge_status_leave",
                "{=RCT_RefugeStatusLeave}Return to the campaign map",
                CanLeaveRefugeStatus,
                LeaveRefugeStatus,
                isLeave: true,
                index: -1);

            AddRefugeUpgradeOption(starter, "barracks", "Build barracks (1,200 denars)", RefugeUpgrade.Barracks, 1200);
            AddRefugeUpgradeOption(starter, "tavern", "Build a tavern and cookhouse (900 denars)", RefugeUpgrade.Tavern, 900);
            AddRefugeUpgradeOption(starter, "staff_tents", "Build staff tents (650 denars)", RefugeUpgrade.StaffTents, 650);
            AddRefugeUpgradeOption(starter, "sleeping_quarters", "Build sleeping quarters (700 denars)", RefugeUpgrade.SleepingQuarters, 700);
            AddRefugeUpgradeOption(starter, "blacksmith", "Build a blacksmith (1,100 denars)", RefugeUpgrade.Blacksmith, 1100);
            AddRefugeUpgradeOption(starter, "stash", "Build a protected stash inside the palisade (350 denars)", RefugeUpgrade.Stash, 350);
            AddRefugeUpgradeOption(starter, "guard_towers", "Complete the four guard towers (1,500 denars)", RefugeUpgrade.GuardTowers, 1500);

            starter.AddGameMenuOption(
                RefugeUpgradesMenuId,
                "realistic_calendar_refuge_upgrade_return",
                "{=RCT_RefugeUpgradeReturn}Return to refuge status",
                CanReturnFromRefugeUpgrades,
                ReturnFromRefugeUpgrades,
                isLeave: true,
                index: -1);

            starter.AddGameMenuOption(
                CampMenuId,
                "realistic_calendar_camp_rest",
                "{=RCT_CampRest}Wait here (up to eight hours)",
                CanUseCamp,
                OpenRestMenu,
                isLeave: false,
                index: -1);

            starter.AddGameMenuOption(
                CampMenuId,
                "realistic_calendar_camp_rest_until_dawn",
                "{=RCT_CampRestUntilDawn}Rest until dawn",
                CanUseCamp,
                OpenRestUntilDawn,
                isLeave: false,
                index: -1);

            starter.AddGameMenuOption(
                CampMenuId,
                "realistic_calendar_camp_order_land_refuge",
                "{=RCT_OrderLandRefuge}Order construction of a land refuge",
                CanOrderLandRefugeConstruction,
                OrderLandRefugeConstruction,
                isLeave: false,
                index: -1);

            starter.AddGameMenuOption(
                CampMenuId,
                "realistic_calendar_camp_order_river_refuge",
                "{=RCT_OrderRiverRefuge}Order construction of a river refuge",
                CanOrderRiverRefugeConstruction,
                OrderRiverRefugeConstruction,
                isLeave: false,
                index: -1);

            starter.AddGameMenuOption(
                CampMenuId,
                "realistic_calendar_camp_order_coast_refuge",
                "{=RCT_OrderCoastRefuge}Order construction of a coastal refuge",
                CanOrderCoastRefugeConstruction,
                OrderCoastRefugeConstruction,
                isLeave: false,
                index: -1);

            starter.AddGameMenuOption(
                CampMenuId,
                "realistic_calendar_camp_refuge_under_construction",
                "{=RCT_RefugeUnderConstruction}Your refuge is under construction",
                RefugeUnderConstructionCondition,
                null,
                isLeave: false,
                index: -1);

            starter.AddGameMenuOption(
                CampMenuId,
                "realistic_calendar_camp_refuge_complete",
                "{=RCT_RefugeComplete}Your refuge is complete",
                RefugeCompleteCondition,
                null,
                isLeave: false,
                index: -1);

            starter.AddGameMenuOption(
                CampMenuId,
                "realistic_calendar_camp_leave",
                "{=RCT_CampLeave}Leave camp",
                CanUseCamp,
                LeaveCamp,
                isLeave: true,
                index: -1);

            starter.AddWaitGameMenu(
                CampRestMenuId,
                "{=RCT_CampRestMenu}Your party rests at camp.",
                OnRestMenuInit,
                CanUseCamp,
                FinishRest,
                OnRestTick,
                GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption,
                GameMenu.MenuOverlayType.None,
                RestHours);

            starter.AddGameMenuOption(
                CampRestMenuId,
                "realistic_calendar_camp_stop_resting",
                "{=RCT_CampStopResting}Stop resting",
                CanStopResting,
                StopResting,
                isLeave: true,
                index: -1);

            Diagnostics.Info("Realistic Calendar camp menu registered.");
        }

        private static void OnCampMenuInit(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            if (refuge != null
                && refuge.ConstructionState == RefugeConstructionState.UnderConstruction)
            {
                TextObject title = new TextObject(
                    "{=RCT_CampMenuConstructionTitle}You set up camp. Refuge construction: {HOURS} hour(s) remaining.");
                title.SetTextVariable("HOURS", refuge.GetConstructionHoursRemaining());
                args.MenuTitle = title;
                return;
            }

            args.MenuTitle = new TextObject("{=RCT_CampMenuTitle}You set up camp. What do you want to do?");
        }

        private static void OnRefugeStatusMenuInit(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            if (refuge != null && refuge.ConstructionState == RefugeConstructionState.UnderConstruction)
            {
                TextObject title = new TextObject(
                    "{=RCT_RefugeStatusBuilding}Refuge construction: {HOURS} hour(s) remaining.");
                title.SetTextVariable("HOURS", refuge.GetConstructionHoursRemaining());
                args.MenuTitle = title;
                return;
            }

            args.MenuTitle = new TextObject("{=RCT_RefugeStatusComplete}Your refuge is complete.");
        }

        private static void OnRefugeUpgradesMenuInit(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            int completed = refuge == null ? 0 : refuge.GetUpgradeCount();
            TextObject title = new TextObject(
                "{=RCT_RefugeUpgradesTitle}The sergeant-at-arms reviews the refuge plans. Completed structures: {COUNT}/7.");
            title.SetTextVariable("COUNT", completed);
            args.MenuTitle = title;
        }

        private static void AddRefugeUpgradeOption(
            CampaignGameStarter starter,
            string id,
            string text,
            RefugeUpgrade upgrade,
            int cost)
        {
            starter.AddGameMenuOption(
                RefugeUpgradesMenuId,
                "realistic_calendar_refuge_upgrade_" + id,
                text,
                delegate(MenuCallbackArgs args) { return CanPurchaseRefugeUpgrade(args, upgrade, cost); },
                delegate(MenuCallbackArgs args) { PurchaseRefugeUpgrade(args, upgrade, cost); },
                isLeave: false,
                index: -1);
        }

        private static bool CanManageRefuge(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            bool isComplete = refuge != null
                && refuge.ConstructionState == RefugeConstructionState.Complete;
            args.IsEnabled = isComplete;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            if (!isComplete)
            {
                args.Tooltip = new TextObject("{=RCT_RefugeSergeantUnavailable}The sergeant-at-arms arrives when the refuge is complete.");
            }

            return refuge != null && refuge.HasRefuge;
        }

        private static void OpenRefugeUpgrades(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu(RefugeUpgradesMenuId);
        }

        private static bool CanPurchaseRefugeUpgrade(
            MenuCallbackArgs args,
            RefugeUpgrade upgrade,
            int cost)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            if (refuge == null || refuge.ConstructionState != RefugeConstructionState.Complete)
            {
                return false;
            }

            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            if (refuge.HasUpgrade(upgrade))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=RCT_RefugeUpgradeBuilt}This structure has already been built.");
                return true;
            }

            Hero hero = Hero.MainHero;
            args.IsEnabled = hero != null && hero.Gold >= cost;
            args.Tooltip = args.IsEnabled
                ? new TextObject("{=RCT_RefugeUpgradeBuy}The sergeant will begin this permanent refuge improvement immediately.")
                : new TextObject("{=RCT_RefugeUpgradeNeedGold}You do not have enough denars for this construction order.");
            return true;
        }

        private static void PurchaseRefugeUpgrade(
            MenuCallbackArgs args,
            RefugeUpgrade upgrade,
            int cost)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            string failure = string.Empty;
            if (refuge == null || !refuge.TryPurchaseUpgrade(upgrade, cost, out failure))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    string.IsNullOrEmpty(failure) ? "The refuge upgrade could not be ordered." : failure));
                return;
            }

            InformationManager.DisplayMessage(new InformationMessage(
                "The sergeant-at-arms has added " + GetUpgradeName(upgrade) + " to the refuge."));
            GameMenu.SwitchToMenu(RefugeUpgradesMenuId);
        }

        private static bool CanReturnFromRefugeUpgrades(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private static void ReturnFromRefugeUpgrades(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu(RefugeStatusMenuId);
        }

        private static string GetUpgradeName(RefugeUpgrade upgrade)
        {
            switch (upgrade)
            {
                case RefugeUpgrade.Barracks: return "the barracks";
                case RefugeUpgrade.Tavern: return "the tavern and cookhouse";
                case RefugeUpgrade.StaffTents: return "the staff tents";
                case RefugeUpgrade.SleepingQuarters: return "the sleeping quarters";
                case RefugeUpgrade.Blacksmith: return "the blacksmith";
                case RefugeUpgrade.Stash: return "the protected stash";
                case RefugeUpgrade.GuardTowers: return "the guard towers";
                default: return "the improvement";
            }
        }

        private static bool RefugeStatusProgressCondition(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            if (refuge == null || !refuge.HasRefuge)
            {
                return false;
            }

            args.IsEnabled = false;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            if (refuge.ConstructionState == RefugeConstructionState.UnderConstruction)
            {
                TextObject text = new TextObject(
                    "{=RCT_RefugeStatusProgressBuilding}Five construction tents are marked here ({HOURS} hour(s) remaining).");
                text.SetTextVariable("HOURS", refuge.GetConstructionHoursRemaining());
                args.Text = text;
            }
            else
            {
                args.Text = new TextObject("{=RCT_RefugeStatusProgressComplete}A palisaded camp marks your completed refuge.");
            }

            return true;
        }

        private static bool CanLeaveRefugeStatus(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private static bool CanEnterRefuge(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            bool canEnter = refuge != null
                && refuge.ConstructionState == RefugeConstructionState.Complete
                && refuge.IsMainPartyWithinInteractionRange
                && Campaign.Current != null
                && Mission.Current == null;

            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            args.IsEnabled = canEnter;
            if (!canEnter)
            {
                args.Tooltip = new TextObject(
                    refuge != null
                        && refuge.ConstructionState == RefugeConstructionState.Complete
                        && !refuge.IsMainPartyWithinInteractionRange
                        ? "{=RCT_RefugeEnterTooFar}Move your party closer to the refuge before entering."
                        : "{=RCT_RefugeEnterUnavailable}The refuge must be complete and no other mission may be active.");
            }

            return refuge != null && refuge.HasRefuge;
        }

        private static void EnterRefuge(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            string failure = string.Empty;
            if (refuge == null || !refuge.TryEnterCompletedRefuge(out failure))
            {
                InformationManager.DisplayMessage(
                    new InformationMessage(
                        string.IsNullOrWhiteSpace(failure)
                            ? "The refuge scene could not be opened."
                            : failure));
            }
        }

        private static void LeaveRefugeStatus(MenuCallbackArgs args)
        {
            GameMenu.ExitToLast();
        }

        private static bool CanOrderLandRefugeConstruction(MenuCallbackArgs args)
        {
            return CanOrderRefugeConstruction(
                args,
                RefugeWaterAccessType.Land,
                "Requires 30 party members, more than 150 denars in camp funds, and 1,000 denars for construction. A land refuge has no ship berth. Construction takes one campaign hour.");
        }

        private static bool CanOrderRiverRefugeConstruction(MenuCallbackArgs args)
        {
            return CanOrderRefugeConstruction(
                args,
                RefugeWaterAccessType.River,
                "Requires 30 party members, more than 150 denars in camp funds, 1,000 denars for construction, and a verified navigable river. River refuges can later store ships. Construction takes one campaign hour.");
        }

        private static bool CanOrderCoastRefugeConstruction(MenuCallbackArgs args)
        {
            return CanOrderRefugeConstruction(
                args,
                RefugeWaterAccessType.Coast,
                "Requires 30 party members, more than 150 denars in camp funds, 1,000 denars for construction, and a verified coastline. Coastal refuges can later store ships. Construction takes one campaign hour.");
        }

        private static bool CanOrderRefugeConstruction(
            MenuCallbackArgs args,
            RefugeWaterAccessType requestedAccess,
            string enabledTooltip)
        {
            args.IsEnabled = false;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            if (refuge == null)
            {
                args.Tooltip = new TextObject("{=RCT_RefugeUnavailable}Refuge management is not available in this campaign session.");
                return true;
            }

            if (refuge.HasRefuge)
            {
                return false;
            }

            string reason;
            args.IsEnabled = refuge.CanStartConstruction(requestedAccess, out reason);
            args.Tooltip = args.IsEnabled
                ? new TextObject(enabledTooltip)
                : new TextObject(reason);
            return true;
        }

        private static void OrderLandRefugeConstruction(MenuCallbackArgs args)
        {
            OrderRefugeConstruction(args, RefugeWaterAccessType.Land);
        }

        private static void OrderRiverRefugeConstruction(MenuCallbackArgs args)
        {
            OrderRefugeConstruction(args, RefugeWaterAccessType.River);
        }

        private static void OrderCoastRefugeConstruction(MenuCallbackArgs args)
        {
            OrderRefugeConstruction(args, RefugeWaterAccessType.Coast);
        }

        private static void OrderRefugeConstruction(MenuCallbackArgs args, RefugeWaterAccessType requestedAccess)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            string failure = string.Empty;
            if (refuge == null || !refuge.TryStartConstruction(requestedAccess, out failure))
            {
                InformationManager.DisplayMessage(
                    new InformationMessage(string.IsNullOrEmpty(failure)
                        ? "Refuge construction could not be started."
                        : failure));
                return;
            }

            InformationManager.DisplayMessage(
                new InformationMessage("Refuge construction has begun: 1 campaign hour remaining."));
            GameMenu.SwitchToMenu(CampMenuId);
        }

        private static bool RefugeUnderConstructionCondition(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            if (refuge == null || refuge.ConstructionState != RefugeConstructionState.UnderConstruction)
            {
                return false;
            }

            int hoursRemaining = refuge.GetConstructionHoursRemaining();
            args.IsEnabled = false;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            TextObject status = new TextObject("{=RCT_RefugeUnderConstruction}Your refuge is under construction ({HOURS} hour(s) remaining)");
            status.SetTextVariable("HOURS", hoursRemaining);
            args.Text = status;
            args.Tooltip = new TextObject("{=RCT_RefugeConstructionTooltip}Construction continues while time passes on the campaign map.");
            return true;
        }

        private static bool RefugeCompleteCondition(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            if (refuge == null || refuge.ConstructionState != RefugeConstructionState.Complete)
            {
                return false;
            }

            args.IsEnabled = false;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            args.Tooltip = refuge.HasShipAccess
                ? new TextObject("{=RCT_RefugeCompleteWaterTooltip}This refuge can be visited and has water access for the future ship-storage system.")
                : new TextObject("{=RCT_RefugeCompleteLandTooltip}This refuge can be visited but has no ship berth.");
            return true;
        }

        private static bool CanUseCamp(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Wait;
            bool canUse = IsCampaignMapAvailable();
            if (!canUse)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=RCT_CampUnavailable}Camp is unavailable during an encounter, settlement visit, or mission.");
            }

            return canUse;
        }

        private static bool CanStopResting(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private static void OpenRestMenu(MenuCallbackArgs args)
        {
            _activeRestHours = RestHours;
            GameMenu.SwitchToMenu(CampRestMenuId);
        }

        private static void OpenRestUntilDawn(MenuCallbackArgs args)
        {
            float hoursUntilDawn = DawnHour - CampaignTime.Now.CurrentHourInDay;
            if (hoursUntilDawn <= 0.05f)
            {
                hoursUntilDawn += 24f;
            }

            // Do not create a zero-length wait if the player camps exactly
            // at dawn. A one-hour minimum preserves the native wait menu's
            // start/finish behavior and still lets campaign healing tick.
            _activeRestHours = Math.Max(1f, hoursUntilDawn);
            GameMenu.SwitchToMenu(CampRestMenuId);
        }

        private static void LeaveCamp(MenuCallbackArgs args)
        {
            GameMenu.ExitToLast();
        }

        private static void OnRestMenuInit(MenuCallbackArgs args)
        {
            args.MenuTitle = new TextObject("{=RCT_CampRestMenu}Your party rests at camp.");
            args.MenuContext.GameMenu.SetTargetedWaitingTimeAndInitialProgress(_activeRestHours, 0f);
            ShowTemporaryCampVisual();
        }

        private static void OnRestTick(MenuCallbackArgs args, CampaignTime dt)
        {
            // The menu's native wait clock owns the actual elapsed time. The
            // target wait duration is selected by the camp menu, so advance
            // its progress from the campaign-time delta supplied by
            // Bannerlord rather than manually altering campaign time.
            float progress = args.MenuContext.GameMenu.Progress
                + (float)(dt.ToHours / Math.Max(1f, _activeRestHours));
            args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(Math.Min(1f, progress));
        }

        private static void FinishRest(MenuCallbackArgs args)
        {
            HideTemporaryCampVisual();
            _activeRestHours = RestHours;
            GameMenu.SwitchToMenu(CampMenuId);
        }

        private static void StopResting(MenuCallbackArgs args)
        {
            args.MenuContext.GameMenu.EndWait();
            HideTemporaryCampVisual();
            _activeRestHours = RestHours;
            GameMenu.SwitchToMenu(CampMenuId);
        }

        /// <summary>
        /// Creates five native map tent props beside the party only while the
        /// camp wait menu is active. The entities are held by reference and
        /// removed again on either normal completion or "Stop resting".
        /// </summary>
        private static void ShowTemporaryCampVisual()
        {
            HideTemporaryCampVisual();

            MobileParty party = MobileParty.MainParty;
            Scene scene = TryGetCampaignScene();
            if (party == null || scene == null || Campaign.Current == null
                || Campaign.Current.MapSceneWrapper == null)
            {
                return;
            }

            try
            {
                CampaignVec2 partyPosition = party.Position;
                float terrainHeight = 0f;
                if (!Campaign.Current.MapSceneWrapper.GetHeightAtPoint(partyPosition, ref terrainHeight))
                {
                    return;
                }

                // Camp is a temporary stop, so it uses a single tent. The
                // refuge construction marker is the larger five-tent site.
                for (int index = 0; index < 1; index++)
                {
                    Vec2 offset = CampTentOffsets[index];
                    MatrixFrame frame = MatrixFrame.Identity;
                    frame.origin = new Vec3(
                        partyPosition.X + offset.x,
                        partyPosition.Y + offset.y,
                        terrainHeight);
                    GameEntity tent = GameEntity.Instantiate(scene, CampTentPrefabId, frame, false);
                    if (tent != null)
                    {
                        CampTentEntities.Add(tent);
                    }
                }

                _campTentScene = scene;
            }
            catch (Exception exception)
            {
                // The resting system itself must remain usable if a game
                // version or another map module rejects a visual prefab.
                HideTemporaryCampVisual();
                Diagnostics.Error("Temporary camp tent visual could not be placed safely.", exception);
            }
        }

        private static void HideTemporaryCampVisual()
        {
            Scene scene = _campTentScene;
            if (scene != null)
            {
                for (int index = 0; index < CampTentEntities.Count; index++)
                {
                    try
                    {
                        scene.RemoveEntity(CampTentEntities[index], 0);
                    }
                    catch (Exception exception)
                    {
                        Diagnostics.Error("A temporary camp tent could not be removed safely.", exception);
                    }
                }
            }

            CampTentEntities.Clear();
            _campTentScene = null;
        }

        private static Scene TryGetCampaignScene()
        {
            if (Campaign.Current == null || Campaign.Current.MapSceneWrapper == null)
            {
                return null;
            }

            try
            {
                PropertyInfo sceneProperty = Campaign.Current.MapSceneWrapper.GetType().GetProperty(
                    "Scene",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return sceneProperty == null
                    ? null
                    : sceneProperty.GetValue(Campaign.Current.MapSceneWrapper, null) as Scene;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Campaign map scene could not be accessed safely.", exception);
                return null;
            }
        }

        private static bool IsCampaignMapAvailable()
        {
            if (Campaign.Current == null || Game.Current == null || MobileParty.MainParty == null)
            {
                return false;
            }

            if (!(Game.Current.GameStateManager.ActiveState is MapState))
            {
                return false;
            }

            if (Settlement.CurrentSettlement != null
                || PlayerEncounter.Current != null
                || MapEvent.PlayerMapEvent != null
                || MobileParty.MainParty.IsCurrentlyAtSea)
            {
                return false;
            }

            return MobileParty.MainParty.IsActive;
        }
    }
}
