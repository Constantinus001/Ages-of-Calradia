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
        private const string CampRestMenuId = "realistic_calendar_camp_rest";
        private const float RestHours = 8f;
        private static float _activeRestHours = RestHours;
        private const string CampTentPrefabId = "map_icon_siege_camp_tent";

        // A single native siege tent marks the party's temporary camp from
        // the moment the camp menu opens until the player leaves it.
        private static readonly Vec2[] CampTentOffsets =
        {
            new Vec2(0f, 0f)
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

            starter.AddGameMenuOption(
                RefugeStatusMenuId,
                "realistic_calendar_refuge_status_pack_test_camp",
                "Pack up test camp",
                CanPackUpTestCamp,
                PackUpTestCamp,
                isLeave: false,
                index: -1);

            starter.AddGameMenuOption(
                RefugeStatusMenuId,
                "realistic_calendar_refuge_status_upgrade_camp",
                "Upgrade camp to refuge",
                CanUpgradeCamp,
                UpgradeCampToRefuge,
                isLeave: false,
                index: -1);

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
                "realistic_calendar_refuge_status_dismantle",
                "Dismantle this refuge",
                CanDismantleRefuge,
                ConfirmDismantleRefuge,
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

            starter.AddGameMenuOption(
                CampMenuId,
                "realistic_calendar_camp_rest",
                "{=RCT_CampRest}Wait here for some time (8 hours)",
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
                "realistic_calendar_camp_build_refuge",
                "{=RCT_BuildRefuge}Build a refuge",
                CanBuildSurveyedRefuge,
                ConfirmBuildSurveyedRefuge,
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
            ShowTemporaryCampVisual();

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

            if (refuge != null && refuge.IsCampOnly)
            {
                args.MenuTitle = new TextObject("Your camp is established.");
                return;
            }

            args.MenuTitle = new TextObject("{=RCT_RefugeStatusComplete}Your refuge is complete.");
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
                args.Text = new TextObject(
                    refuge.SelectedFortDisplayName + " marks your completed refuge. "
                    + refuge.GetManagementSummary());
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
                && (refuge.ConstructionState == RefugeConstructionState.Complete || refuge.ConstructionState == RefugeConstructionState.Camp)
                && refuge.IsMainPartyWithinInteractionRange
                && Campaign.Current != null
                && Mission.Current == null;

            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            args.IsEnabled = canEnter;
            if (!canEnter)
            {
                args.Tooltip = new TextObject(
                    refuge != null
                        && (refuge.ConstructionState == RefugeConstructionState.Complete || refuge.ConstructionState == RefugeConstructionState.Camp)
                        && !refuge.IsMainPartyWithinInteractionRange
                        ? "{=RCT_RefugeEnterTooFar}Move your party closer to the refuge before entering."
                        : "{=RCT_RefugeEnterUnavailable}The refuge must be complete and no other mission may be active.");
            }

            if (refuge != null && refuge.IsCampOnly)
            {
                args.Text = new TextObject("Enter camp");
            }
            return refuge != null && refuge.HasCamp;
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

        private static bool CanDismantleRefuge(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            string reason = string.Empty;
            bool canDismantle = refuge != null && refuge.CanRemoveRefuge(out reason);
            args.IsEnabled = canDismantle;
            if (!canDismantle)
            {
                args.Tooltip = new TextObject(refuge == null
                    ? "Refuge management is not available in this campaign session."
                    : reason);
            }
            return refuge != null && refuge.HasRefuge;
        }

        private static bool CanPackUpTestCamp(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            args.IsEnabled = refuge != null && refuge.IsCampOnly;
            return refuge != null && refuge.IsCampOnly;
        }

        private static void PackUpTestCamp(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            string failure = string.Empty;
            if (refuge != null && refuge.TryRemoveRefuge(out failure))
            {
                InformationManager.DisplayMessage(new InformationMessage("Test camp packed up. Your terrain checklist was kept; you may found the next camp."));
                GameMenu.SwitchToMenu(CampMenuId);
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(string.IsNullOrWhiteSpace(failure) ? "Test camp could not be packed up." : failure));
            }
        }

        private static void ConfirmDismantleRefuge(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            string failure = string.Empty;
            if (refuge == null || !refuge.CanRemoveRefuge(out failure))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    string.IsNullOrWhiteSpace(failure) ? "This refuge cannot be dismantled." : failure));
                return;
            }

            InformationManager.ShowInquiry(new InquiryData(
                "Dismantle " + refuge.SelectedFortDisplayName + "?",
                "This removes the refuge from this campaign. The stash and garrison must remain empty. "
                + "You may then found a new Palisade Ring refuge.",
                true,
                true,
                "Dismantle refuge",
                "Keep refuge",
                delegate
                {
                    string removeFailure;
                    if (refuge.TryRemoveRefuge(out removeFailure))
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "The refuge has been dismantled. You may build a new refuge."));
                        GameMenu.SwitchToMenu(CampMenuId);
                    }
                    else
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            string.IsNullOrWhiteSpace(removeFailure)
                                ? "The refuge could not be dismantled."
                                : removeFailure));
                    }
                },
                null), true);
        }

        private static bool CanBuildSurveyedRefuge(MenuCallbackArgs args)
        {
            args.IsEnabled = false;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            if (refuge == null)
            {
                args.Tooltip = new TextObject("{=RCT_RefugeUnavailable}Refuge management is not available in this campaign session.");
                return true;
            }

            if (refuge.HasCamp)
            {
                return false;
            }

            RefugeWaterAccessType recommendedAccess;
            string reason;
            args.IsEnabled = refuge.TrySurveyCurrentSite(out recommendedAccess, out reason);
            if (args.IsEnabled)
            {
                string kind = GetRefugeTypeName(recommendedAccess);
                TextObject optionText = new TextObject("{=RCT_BuildSurveyedRefuge}Build a {TYPE} refuge");
                optionText.SetTextVariable("TYPE", kind.ToLowerInvariant());
                args.Text = optionText;
                args.Tooltip = new TextObject(GetRefugeTypeDescription(recommendedAccess));
            }
            else
            {
                args.Tooltip = new TextObject(reason);
            }

            return true;
        }

        private static void ConfirmBuildSurveyedRefuge(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            RefugeWaterAccessType recommendedAccess;
            string failure = "Refuge management is not available in this campaign session.";
            if (refuge == null || !refuge.TrySurveyCurrentSite(out recommendedAccess, out failure))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    string.IsNullOrEmpty(failure) ? "This refuge site could not be surveyed." : failure));
                return;
            }

            if (refuge.TryFoundCamp(recommendedAccess, out failure))
            {
                InformationManager.DisplayMessage(new InformationMessage("Camp established. Move to its marker and enter camp to upgrade it into a refuge."));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(string.IsNullOrWhiteSpace(failure) ? "Camp could not be established." : failure));
            }
        }

        private static bool CanUpgradeCamp(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            args.IsEnabled = refuge != null && refuge.IsCampOnly && refuge.IsMainPartyWithinInteractionRange;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            if (!args.IsEnabled) args.Tooltip = new TextObject("Enter an established camp and stand near it to construct the Palisade Ring refuge.");
            return refuge != null && refuge.HasCamp;
        }

        private static void UpgradeCampToRefuge(MenuCallbackArgs args)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            if (refuge != null && refuge.IsCampOnly)
            {
                OrderRefugeConstruction(
                    refuge.WaterAccess,
                    RefugeFortPrefabCatalog.DefaultFortPrefabId);
            }
        }

        private static void OrderRefugeConstruction(
            RefugeWaterAccessType requestedAccess,
            string fortPrefabId)
        {
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            string failure = string.Empty;
            if (refuge == null || !refuge.TryStartConstruction(requestedAccess, fortPrefabId, out failure))
            {
                InformationManager.DisplayMessage(
                    new InformationMessage(string.IsNullOrEmpty(failure)
                        ? "Refuge construction could not be started."
                        : failure));
                return;
            }

            InformationManager.DisplayMessage(
                new InformationMessage("Refuge construction has begun: "
                    + refuge.SelectedFortDisplayName + ", 1 campaign hour remaining."));
            GameMenu.SwitchToMenu(CampMenuId);
        }

        private static string GetRefugeTypeName(RefugeWaterAccessType access)
        {
            switch (access)
            {
                case RefugeWaterAccessType.River: return "River";
                case RefugeWaterAccessType.Coast: return "Coastal";
                default: return "Land";
            }
        }

        private static string GetRefugeTypeDescription(RefugeWaterAccessType access)
        {
            switch (access)
            {
                case RefugeWaterAccessType.River:
                    return "This site has verified navigable-river access and can support ship storage.";
                case RefugeWaterAccessType.Coast:
                    return "This site has verified coastal access and can support ship storage.";
                default:
                    return "This inland site will create a land refuge without ship storage.";
            }
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
            float dawnHour = GetNativeSunriseHour();
            double hourInDay = CampaignTime.Now.ToHours % CampaignTime.HoursInDay;
            if (hourInDay < 0d)
            {
                hourInDay += CampaignTime.HoursInDay;
            }

            float hoursUntilDawn = dawnHour - (float)hourInDay;
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

        private static float GetNativeSunriseHour()
        {
            try
            {
                if (Campaign.Current != null
                    && Campaign.Current.Models != null
                    && Campaign.Current.Models.CampaignTimeModel != null)
                {
                    return Campaign.Current.Models.CampaignTimeModel.SunRise;
                }
            }
            catch
            {
                // Use the initialized native static value below.
            }

            if (CampaignTime.SunRise > 0 && CampaignTime.SunRise < CampaignTime.HoursInDay)
            {
                return CampaignTime.SunRise;
            }

            // Bannerlord's DefaultCampaignTimeModel sunrise is 02:00.
            return 2f;
        }

        private static void LeaveCamp(MenuCallbackArgs args)
        {
            HideTemporaryCampVisual();
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
            CalendarRefugeBehavior refuge = CalendarRefugeBehavior.Active;
            int moraleBonus = refuge == null ? 0 : refuge.ApplyRestBenefitIfAtRefuge();
            int recoveredTroops = refuge == null ? 0 : refuge.ApplyHealerRestBenefitIfAtRefuge();
            int trainedGarrison = refuge == null ? 0 : refuge.ApplyGarrisonTrainingIfAtRefuge();
            if (moraleBonus > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "The refuge tavern and cookhouse improve party morale by " + moraleBonus + "."));
            }
            if (recoveredTroops > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "The refuge healer treats " + recoveredTroops + " wounded troop"
                    + (recoveredTroops == 1 ? "." : "s.")));
            }
            if (trainedGarrison > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "The Guard Captain trains " + trainedGarrison + " low-tier garrison troop"
                    + (trainedGarrison == 1 ? "." : "s.")));
            }
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
        /// Creates one native map tent beside the party while the camp menu or
        /// its wait menu is active. The entity is held by reference and
        /// removed when the player leaves camp.
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
