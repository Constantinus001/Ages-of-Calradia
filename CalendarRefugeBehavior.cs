using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Xml;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Owns the campaign-side record for the player's single refuge.
    ///
    /// The record is intentionally serialized as one primitive XML string,
    /// rather than as a module-owned save type. That keeps this feature
    /// independent of Bannerlord's settlement save graph and lets later
    /// scene/layout work reconstruct runtime objects from simple data.
    /// </summary>
    internal sealed class CalendarRefugeBehavior : CampaignBehaviorBase
    {
        internal const int MinimumPartySize = 30;
        internal const int MinimumCampFunds = 151;
        internal const int ConstructionCost = 1000;
        internal const int ConstructionDurationHours = 1;
        internal const double ConstructionDurationDays = ConstructionDurationHours / 24d;
        internal const int GuardTowerConstructionHours = 24;
        internal const double GuardTowerConstructionDays = GuardTowerConstructionHours / 24d;
        internal const float InteractionRadius = 2.0f;

        // V2 intentionally starts from a clean refuge record. It does not
        // import experimental V1 sites, menu state, or inventory payloads.
        private const string SerializedStateKey = "RealisticCalendarTweaks.RefugeV2";
        private const string SerializedStashKey = "RealisticCalendarTweaks.RefugeStashV2";
        private const string SerializedStewardKey = "RealisticCalendarTweaks.RefugeStewardV2";
        private const string SerializedCookKey = "RealisticCalendarTweaks.RefugeCookV2";
        private const string SerializedGuardCaptainKey = "RealisticCalendarTweaks.RefugeGuardCaptainV2";
        private const string SerializedHealerKey = "RealisticCalendarTweaks.RefugeHealerV2";
        private const string SerializedGarrisonKey = "RealisticCalendarTweaks.RefugeGarrisonV2";
        private const string VisitedTerrainProfilesKey = "RealisticCalendarTweaks.RefugeVisitedTerrainsV1";
        private const int StateSchemaVersion = 5;
        private const int MaximumSerializedStateLength = 1024;
        private const string AuthoredRefugeSceneId = "rct_refuge_fort";

        // Curated native-scene profiles. The temperate land foundation is a
        // static dry scene; the generated biome profile previously exposed
        // only its water/base layer in this isolated mission workflow.
        // The open-plains terrain used for the refuge layout work.  It has a
        // fixed, tested anchor so the entire compound remains visible.
        private const string TemperateLandSceneId = "battle_terrain_biome_130";
        private const string TemperateRiverSceneId = "river_bt_empirewest_01_4x4km";
        private const string TemperateCoastSceneId = "battle_terrain_coastal_02";
        private const string DesertLandSceneId = "battle_terrain_009";
        private const string DesertRiverSceneId = "river_bt_aserai_01_4x4km";
        private const string DesertCoastSceneId = "battle_terrain_coastal_01";
        private const string SnowLandSceneId = "battle_terrain_006";
        private const string SnowRiverSceneId = "river_bt_nord_01_4x4km";
        private const string SnowCoastSceneId = "coastal_terrain_north_of_the_north_sea_01";
        // Read-only migration aliases for refuges created by earlier builds.
        // They are never selected for a new mission.
        private const string LegacyTemperateBiomeSceneId = "battle_terrain_biome_130";
        private const string LegacyTemperateLandSceneId = "forest_hideout_003";
        private const string LegacyTemperateCoastSceneId = "sea_bandit_b";
        private const string LegacyDesertLandSceneId = "desert_hideout_002";
        private const string LegacyTemperateRiverNavalSceneId = "river_bt_empirewest_01_4x4km";
        private const string LegacyTemperateCoastNavalSceneId = "battle_terrain_coastal_02";
        private const string LegacyDesertRiverNavalSceneId = "river_bt_aserai_01_4x4km";
        private const string LegacyDesertCoastNavalSceneId = "battle_terrain_coastal_01";
        private const string LegacySnowLandSceneId = "nord_battle_terrain_a";
        private const string LegacySnowRiverNavalSceneId = "river_bt_nord_01_4x4km";
        private const string LegacySnowCoastNavalSceneId = "coastal_terrain_north_of_the_north_sea_01";
        private const string RefugeMapTentPrefabId = "map_icon_siege_camp_tent";
        // Keep the campaign marker on a Native map prefab.  Custom module
        // prefab XML is parsed by the Scene Editor at startup and older
        // experimental versions made that editor unstable.
        private const string CompletedRefugeMapPrefabId = RefugeMapTentPrefabId;

        // This is deliberately our own compact camp layout rather than a
        // copied native siege-camp arrangement. The positions are campaign
        // map units relative to the saved refuge site.
        private static readonly Vec2[] RefugeTentOffsets =
        {
            new Vec2(-0.40f, -0.25f),
            new Vec2(0.38f, -0.20f),
            new Vec2(-0.48f, 0.34f),
            new Vec2(0.05f, 0.45f),
            new Vec2(0.52f, 0.20f)
        };

        private static readonly float[] SurveyDistances = { 0.75f, 1.5f, 2.5f, 4f, 6f };
        private const float WaterSurveyReuseDistanceSquared = 0.04f;
        private static CalendarRefugeBehavior _active;

        private string _serializedState = string.Empty;
        private string _visitedTerrainProfiles = string.Empty;
        private ItemRoster _stash = new ItemRoster();
        private TroopRoster _garrison = TroopRoster.CreateDummyTroopRoster();
        private Hero _stewardHero;
        private Hero _cookHero;
        private Hero _guardCaptainHero;
        private Hero _healerHero;
        private RefugeState _state = RefugeState.Empty;
        private bool _hasCachedWaterSurvey;
        private float _cachedWaterSurveyX;
        private float _cachedWaterSurveyY;
        private RefugeWaterAccessType _cachedWaterAccess;
        private bool _refugeMapMarkerPlaced;
        private readonly List<GameEntity> _refugeMapMarkerEntities = new List<GameEntity>();
        private Scene _refugeMapMarkerScene;

        internal static CalendarRefugeBehavior Active
        {
            get { return _active; }
        }

        internal Hero StewardHero
        {
            get { return _stewardHero; }
        }

        internal Hero CookHero
        {
            get { return _cookHero; }
        }

        internal Hero GuardCaptainHero
        {
            get { return _guardCaptainHero; }
        }

        internal Hero HealerHero
        {
            get { return _healerHero; }
        }

        internal RefugeConstructionState ConstructionState
        {
            get
            {
                CompleteConstructionIfDue(false);
                return _state.State;
            }
        }

        internal bool HasRefuge
        {
            get { return ConstructionState == RefugeConstructionState.UnderConstruction || ConstructionState == RefugeConstructionState.Complete; }
        }

        internal bool HasCamp
        {
            get { return ConstructionState != RefugeConstructionState.None; }
        }

        internal bool IsCampOnly
        {
            get { return ConstructionState == RefugeConstructionState.Camp; }
        }

        internal RefugeWaterAccessType WaterAccess
        {
            get { return _state.WaterAccess; }
        }

        /// <summary>
        /// A berth is a property of the founded site, never of the generic
        /// refuge. This lets the future ship system stay unavailable for a
        /// land refuge instead of creating unreachable stored ships.
        /// </summary>
        internal bool HasShipAccess
        {
            get
            {
                RefugeWaterAccessType access = _state.WaterAccess;
                return access == RefugeWaterAccessType.River
                    || access == RefugeWaterAccessType.Coast;
            }
        }

        internal string SelectedFortPrefabId
        {
            get { return _state.FortPrefabId; }
        }

        internal string SelectedFortDisplayName
        {
            get
            {
                RefugeFortPrefabDefinition fort;
                return RefugeFortPrefabCatalog.TryGet(_state.FortPrefabId, out fort)
                    ? fort.DisplayName
                    : "Unknown fort";
            }
        }

        internal RefugeUpgrade Upgrades
        {
            get
            {
                CompleteUpgradeIfDue(false);
                return _state.Upgrades;
            }
        }

        internal RefugeUpgrade ActiveUpgrade
        {
            get
            {
                CompleteUpgradeIfDue(false);
                return _state.ActiveUpgrade;
            }
        }

        internal float GetActiveUpgradeProgress()
        {
            CompleteUpgradeIfDue(false);
            if (_state.ActiveUpgrade == RefugeUpgrade.None) return 0f;
            double duration = _state.UpgradeCompletionDay - _state.UpgradeStartedDay;
            if (duration <= 0d) return 1f;
            double progress = (CampaignTime.Now.ToDays - _state.UpgradeStartedDay) / duration;
            return (float)Math.Max(0d, Math.Min(1d, progress));
        }

        internal int GetActiveUpgradeHoursRemaining()
        {
            CompleteUpgradeIfDue(false);
            if (_state.ActiveUpgrade == RefugeUpgrade.None) return 0;
            return Math.Max(0, (int)Math.Ceiling((_state.UpgradeCompletionDay - CampaignTime.Now.ToDays) * 24d));
        }

        internal bool HasUpgrade(RefugeUpgrade upgrade)
        {
            CompleteUpgradeIfDue(false);
            return upgrade != RefugeUpgrade.None
                && (_state.Upgrades & upgrade) == upgrade;
        }

        internal int GetUpgradeCount()
        {
            int count = 0;
            foreach (RefugeUpgrade upgrade in new[]
            {
                RefugeUpgrade.Barracks,
                RefugeUpgrade.Tavern,
                RefugeUpgrade.StaffTents,
                RefugeUpgrade.SleepingQuarters,
                RefugeUpgrade.Blacksmith,
                RefugeUpgrade.Stash,
                RefugeUpgrade.GuardTowers,
                RefugeUpgrade.Infirmary,
                RefugeUpgrade.TrainingYard
            })
            {
                if (HasUpgrade(upgrade))
                {
                    count++;
                }
            }

            return count;
        }

        internal int GarrisonCapacity
        {
            get
            {
                int capacity = 20;
                if (HasUpgrade(RefugeUpgrade.Barracks)) capacity += 40;
                if (HasUpgrade(RefugeUpgrade.SleepingQuarters)) capacity += 30;
                if (HasUpgrade(RefugeUpgrade.StaffTents)) capacity += 10;
                return capacity;
            }
        }

        internal int DefenceRating
        {
            get
            {
                int defence = 10;
                if (HasUpgrade(RefugeUpgrade.Barracks)) defence += 5;
                if (HasUpgrade(RefugeUpgrade.GuardTowers)) defence += 40;
                return defence;
            }
        }

        internal float GarrisonUpkeepMultiplier
        {
            get { return HasUpgrade(RefugeUpgrade.SleepingQuarters) ? 0.75f : 1f; }
        }

        internal string GetManagementSummary()
        {
            CompleteUpgradeIfDue(false);
            return "Fort: " + SelectedFortDisplayName
                + " | Defence: " + DefenceRating
                + " | Garrison: " + GarrisonCount + "/" + GarrisonCapacity
                + " | Garrison upkeep: " + (int)(GarrisonUpkeepMultiplier * 100f) + "%"
                + " | Stash: " + (HasUpgrade(RefugeUpgrade.Stash) ? "available" : "locked")
                + " | Ship berth: " + (HasShipAccess ? "available" : "none");
        }

        internal bool TryOpenStash(out string failure)
        {
            CompleteUpgradeIfDue(false);
            if (_state.State != RefugeConstructionState.Complete)
            {
                failure = "The refuge must be complete before its stash can be used.";
                return false;
            }

            if (!HasUpgrade(RefugeUpgrade.Stash))
            {
                failure = "Build the protected stash before storing goods here.";
                return false;
            }

            if (!IsMainPartyWithinInteractionRange)
            {
                failure = "Move your party closer to the refuge before using its stash.";
                return false;
            }

            if (_stash == null)
            {
                _stash = new ItemRoster();
            }

            InventoryScreenHelper.OpenScreenAsStash(_stash);
            failure = string.Empty;
            return true;
        }

        internal int GarrisonCount
        {
            get { return _garrison == null ? 0 : _garrison.TotalManCount; }
        }

        internal bool TryOpenGarrison(out string failure)
        {
            CompleteUpgradeIfDue(false);
            if (_state.State != RefugeConstructionState.Complete)
            {
                failure = "The refuge must be complete before it can house a garrison.";
                return false;
            }
            if (!IsMainPartyWithinInteractionRange)
            {
                failure = "Move your party closer to the refuge before managing its garrison.";
                return false;
            }
            if (_garrison == null)
            {
                _garrison = TroopRoster.CreateDummyTroopRoster();
            }

            // The left side is always a clone. Bannerlord may clear the
            // temporary roster on Cancel; keeping the saved garrison out of
            // that lifecycle prevents lost troops or a damaged main party.
            TroopRoster workingGarrison = _garrison.CloneRosterData();
            PartyScreenHelper.OpenScreenWithCondition(
                PartyScreenHelper.ClanManageTroopTransferableDelegate,
                CanConfirmGarrisonTransfer,
                ConfirmGarrisonTransfer,
                null,
                PartyScreenLogic.TransferState.Transferable,
                PartyScreenLogic.TransferState.NotTransferable,
                new TextObject("{=RCT_RefugeGarrison}Refuge Garrison"),
                GarrisonCapacity,
                true,
                false,
                PartyScreenHelper.PartyScreenMode.TroopsManage,
                workingGarrison,
                TroopRoster.CreateDummyTroopRoster());
            failure = string.Empty;
            return true;
        }

        private Tuple<bool, TextObject> CanConfirmGarrisonTransfer(
            TroopRoster leftMembers,
            TroopRoster leftPrisoners,
            TroopRoster rightMembers,
            TroopRoster rightPrisoners,
            int leftLimit,
            int rightLimit)
        {
            if (leftMembers == null || leftMembers.TotalManCount <= GarrisonCapacity)
            {
                return new Tuple<bool, TextObject>(true, new TextObject(string.Empty));
            }

            TextObject message = new TextObject("{=RCT_RefugeGarrisonLimit}The refuge can house at most {COUNT} troops.");
            message.SetTextVariable("COUNT", GarrisonCapacity);
            return new Tuple<bool, TextObject>(false, message);
        }

        private bool ConfirmGarrisonTransfer(
            TroopRoster leftMembers,
            TroopRoster leftPrisoners,
            TroopRoster rightMembers,
            TroopRoster rightPrisoners,
            FlattenedTroopRoster takenPrisoners,
            FlattenedTroopRoster releasedPrisoners,
            bool isForced,
            PartyBase leftParty = null,
            PartyBase rightParty = null)
        {
            if (leftMembers == null || leftMembers.TotalManCount > GarrisonCapacity)
            {
                return false;
            }

            _garrison = leftMembers.CloneRosterData();
            Diagnostics.Info("Refuge garrison confirmed. Troops=" + _garrison.TotalManCount + ".");
            return true;
        }

        internal int ApplyRestBenefitIfAtRefuge()
        {
            if (!HasUpgrade(RefugeUpgrade.Tavern) || !IsMainPartyWithinInteractionRange)
            {
                return 0;
            }

            MobileParty party = MobileParty.MainParty;
            if (party == null)
            {
                return 0;
            }

            const int moraleBonus = 3;
            party.RecentEventsMorale += moraleBonus;
            return moraleBonus;
        }

        internal int ApplyHealerRestBenefitIfAtRefuge()
        {
            if (!HasUpgrade(RefugeUpgrade.Infirmary) || !IsMainPartyWithinInteractionRange)
            {
                return 0;
            }

            MobileParty party = MobileParty.MainParty;
            if (party == null || party.MemberRoster == null)
            {
                return 0;
            }

            const int maximumRecovered = 3;
            int recovered = 0;
            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster())
            {
                if (recovered >= maximumRecovered || element.Character == null || element.Character.IsHero
                    || element.WoundedNumber <= 0)
                {
                    continue;
                }

                int amount = Math.Min(maximumRecovered - recovered, element.WoundedNumber);
                party.MemberRoster.AddToCounts(element.Character, 0, false, -amount);
                recovered += amount;
            }
            return recovered;
        }

        internal int ApplyGarrisonTrainingIfAtRefuge()
        {
            if (!HasUpgrade(RefugeUpgrade.TrainingYard) || !IsMainPartyWithinInteractionRange || _garrison == null)
            {
                return 0;
            }

            int trained = 0;
            foreach (TroopRosterElement element in _garrison.GetTroopRoster())
            {
                if (element.Character == null || element.Character.IsHero || element.Character.Tier > 2)
                {
                    continue;
                }
                _garrison.AddXpToTroop(element.Character, element.Number * 15);
                trained += element.Number;
            }
            return trained;
        }

        internal bool TryPurchaseUpgrade(RefugeUpgrade upgrade, int cost, out string failure)
        {
            CompleteConstructionIfDue(false);
            CompleteUpgradeIfDue(false);
            if (_state.State != RefugeConstructionState.Complete)
            {
                failure = "The refuge must be complete before it can be improved.";
                return false;
            }

            if (upgrade == RefugeUpgrade.None || cost < 0)
            {
                failure = "That refuge upgrade is not valid.";
                return false;
            }

            if (HasUpgrade(upgrade))
            {
                failure = "That structure has already been built.";
                return false;
            }

            if (_state.ActiveUpgrade != RefugeUpgrade.None)
            {
                failure = "Construction is already in progress: " + _state.ActiveUpgrade + ".";
                return false;
            }

            Hero mainHero = Hero.MainHero;
            if (mainHero == null || mainHero.Gold < cost)
            {
                failure = "You do not have enough denars for that construction order.";
                return false;
            }

            GiveGoldAction.ApplyBetweenCharacters(mainHero, null, cost, disableNotification: true);
            double startedOnDay = CampaignTime.Now.ToDays;
            _state = _state.WithUpgradeConstruction(
                upgrade,
                startedOnDay,
                startedOnDay + GetUpgradeConstructionDays(upgrade));
            _serializedState = Serialize(_state);
            Diagnostics.Info("Refuge upgrade purchased. Upgrade=" + upgrade + "; Cost=" + cost + ".");
            failure = string.Empty;
            return true;
        }

        internal bool TryEnterCompletedRefuge(out string failure)
        {
            CompleteConstructionIfDue(false);
            CompleteUpgradeIfDue(false);
            if (_state.State != RefugeConstructionState.Complete && _state.State != RefugeConstructionState.Camp)
            {
                failure = "The camp must be established before it can be entered.";
                return false;
            }

            if (!IsMainPartyWithinInteractionRange)
            {
                failure = "Move your party closer to the refuge before entering.";
                return false;
            }

            string sceneId = _state.SceneId;
            RefugeSceneClimate climate;
            if (_state.State == RefugeConstructionState.Camp)
            {
                climate = GetSceneClimateForSite(MobileParty.MainParty);
            }
            else if (!TryGetSceneClimate(sceneId, out climate))
            {
                failure = "The refuge's fixed scene profile is invalid.";
                return false;
            }

            // A new coast camp binds its campaign-patch scene when founded.
            // Only migrate the old hard-coded NavalDLC coast records; never
            // re-resolve an already bound scene on every visit.
            if (ShouldRebindLegacyCoastScene(sceneId, _state.WaterAccess))
            {
                sceneId = GetSceneId(climate, _state.WaterAccess, _state.FortPrefabId);
                if (!string.IsNullOrEmpty(sceneId)
                    && !string.Equals(sceneId, _state.SceneId, StringComparison.Ordinal))
                {
                    _state = _state.WithSceneId(sceneId);
                    _serializedState = Serialize(_state);
                    Diagnostics.Info("PortableCampDiagnostic LegacyCoastSceneRebound; Scene=" + sceneId + ".");
                }
            }

            if (string.IsNullOrEmpty(sceneId))
            {
                failure = "No safe refuge scene profile is available for this site.";
                return false;
            }

            bool isWinter = CalendarTimeMath.GetSeason(CampaignTime.Now)
                == (int)CampaignTime.Seasons.Winter;
            RecordTerrainVisit(climate, _state.WaterAccess, sceneId);
            InformationManager.DisplayMessage(new InformationMessage(GetTerrainVisitChecklist()));
            EnsureRefugeStaffHeroes();
            Vec3 portableAnchor;
            bool hasPortableAnchor = TryGetPortableSceneAnchor(sceneId, out portableAnchor);
            return CalendarRefugeMission.TryOpen(
                sceneId,
                _state.FortPrefabId,
                _state.State == RefugeConstructionState.Camp,
                climate,
                _state.WaterAccess,
                isWinter,
                _stewardHero,
                _cookHero,
                _guardCaptainHero,
                _healerHero,
                _state.Upgrades,
                _state.ActiveUpgrade,
                GetActiveUpgradeProgress(),
                hasPortableAnchor,
                portableAnchor,
                out failure);
        }

        internal void SavePortableSceneAnchor(string sceneId, Vec3 position)
        {
            PortableCampAnchorStore.Save(sceneId, position);
            Diagnostics.Info("PortableCampDiagnostic PlayerAnchorSaved; Scene=" + sceneId
                + "; Position=" + position.x.ToString("F2", CultureInfo.InvariantCulture)
                + "," + position.y.ToString("F2", CultureInfo.InvariantCulture)
                + "," + position.z.ToString("F2", CultureInfo.InvariantCulture) + ".");
        }

        private bool TryGetPortableSceneAnchor(string sceneId, out Vec3 position)
        {
            return PortableCampAnchorStore.TryGet(sceneId, out position);
        }

        private void RecordTerrainVisit(RefugeSceneClimate climate, RefugeWaterAccessType access, string sceneId)
        {
            string entry = climate + "/" + access + "/" + sceneId;
            string[] previous = (_visitedTerrainProfiles ?? string.Empty).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < previous.Length; index++)
            {
                if (string.Equals(previous[index], entry, StringComparison.Ordinal))
                {
                    Diagnostics.Info("PortableCampDiagnostic TerrainRevisited; Profile=" + entry + "; Visited=" + _visitedTerrainProfiles + ".");
                    return;
                }
            }
            _visitedTerrainProfiles = string.IsNullOrEmpty(_visitedTerrainProfiles) ? entry : _visitedTerrainProfiles + "|" + entry;
            Diagnostics.Info("PortableCampDiagnostic TerrainVisited; Profile=" + entry + "; Visited=" + _visitedTerrainProfiles + ".");
        }

        private string GetTerrainVisitChecklist()
        {
            string[] profiles =
            {
                "Temperate Plain/battle_terrain_001", "Temperate River/river_bt_empirewest_01_4x4km", "Temperate Coast/battle_terrain_coastal_02",
                "Sturgian Plain/battle_terrain_006", "Sturgian River/river_bt_nord_01_4x4km", "Sturgian Coast/coastal_terrain_north_of_the_north_sea_01",
                "Desert Plain/battle_terrain_009", "Desert River/river_bt_aserai_01_4x4km", "Desert Coast/battle_terrain_coastal_01"
            };
            string visited = _visitedTerrainProfiles ?? string.Empty;
            System.Text.StringBuilder checklist = new System.Text.StringBuilder("Camp terrain checks: ");
            for (int index = 0; index < profiles.Length; index++)
            {
                string[] parts = profiles[index].Split('/');
                if (index > 0) checklist.Append(" | ");
                checklist.Append(visited.IndexOf("/" + parts[1], StringComparison.Ordinal) >= 0 ? "[x] " : "[ ] ");
                checklist.Append(parts[0]);
            }
            return checklist.ToString();
        }

        internal bool CanRemoveRefuge(out string reason)
        {
            CompleteConstructionIfDue(false);
            if (_state.State == RefugeConstructionState.None)
            {
                reason = "There is no refuge to dismantle.";
                return false;
            }

            if (!IsMainPartyWithinInteractionRange)
            {
                reason = "Move your party closer to the refuge before dismantling it.";
                return false;
            }

            if (_stash != null && _stash.Count > 0)
            {
                reason = "Empty the refuge stash before dismantling it.";
                return false;
            }

            if (_garrison != null && _garrison.TotalManCount > 0)
            {
                reason = "Move all troops out of the refuge garrison before dismantling it.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Removes the campaign-owned refuge record without attempting to
        /// mutate unsupported native map-scene entities. Any existing map
        /// marker becomes non-interactive immediately and is absent after the
        /// next map-scene load. Stash and garrison must be empty first, so no
        /// player inventory or troop data can be lost.
        /// </summary>
        internal bool TryRemoveRefuge(out string failure)
        {
            if (!CanRemoveRefuge(out failure))
            {
                return false;
            }

            string removedScene = _state.SceneId;
            string removedFort = _state.FortPrefabId;
            _state = RefugeState.Empty;
            _stash = new ItemRoster();
            _garrison = TroopRoster.CreateDummyTroopRoster();
            _serializedState = Serialize(_state);
            ClearRefugeMapMarkerVisuals();
            _hasCachedWaterSurvey = false;

            Diagnostics.Info("Refuge dismantled. Scene=" + removedScene
                + "; Fort=" + removedFort
                + "; Campaign marker visual removed immediately.");
            failure = string.Empty;
            return true;
        }

        internal bool CanChangeFortStyle(string fortPrefabId, out string reason)
        {
            CompleteConstructionIfDue(false);
            if (_state.State != RefugeConstructionState.Complete)
            {
                reason = "The camp must be upgraded before its refuge style can be changed.";
                return false;
            }
            if (!IsMainPartyWithinInteractionRange)
            {
                reason = "Speak with the Steward at the refuge to change its style.";
                return false;
            }
            if (_state.ActiveUpgrade != RefugeUpgrade.None)
            {
                reason = "Finish the current construction project before changing refuge style.";
                return false;
            }

            RefugeFortPrefabDefinition fort;
            if (!RefugeFortPrefabCatalog.TryGet(fortPrefabId, out fort))
            {
                reason = "That refuge style is not registered.";
                return false;
            }
            if (!RefugeFortPrefabCatalog.IsAssetReady(fortPrefabId, out reason))
            {
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal bool TryChangeFortStyle(string fortPrefabId, out string failure)
        {
            if (!CanChangeFortStyle(fortPrefabId, out failure))
            {
                return false;
            }

            if (string.Equals(_state.FortPrefabId, fortPrefabId, StringComparison.Ordinal))
            {
                failure = "That refuge style is already active.";
                return false;
            }

            _state = new RefugeState(
                RefugeConstructionState.Complete,
                _state.MapX,
                _state.MapY,
                _state.WaterAccess,
                _state.SceneId,
                _state.StartedOnDay,
                _state.CompletionDay,
                _state.Upgrades,
                _state.ActiveUpgrade,
                _state.UpgradeStartedDay,
                _state.UpgradeCompletionDay,
                fortPrefabId);
            _serializedState = Serialize(_state);
            Diagnostics.Info("Refuge style changed by Steward. Fort=" + fortPrefabId
                + "; Scene=" + _state.SceneId + ".");
            failure = string.Empty;
            return true;
        }

        internal bool IsMainPartyWithinInteractionRange
        {
            get
            {
                MobileParty mainParty = MobileParty.MainParty;
                if (mainParty == null || _state.State == RefugeConstructionState.None)
                {
                    return false;
                }

                float deltaX = mainParty.Position.X - _state.MapX;
                float deltaY = mainParty.Position.Y - _state.MapY;
                return (deltaX * deltaX) + (deltaY * deltaY)
                    <= InteractionRadius * InteractionRadius;
            }
        }

        public override void RegisterEvents()
        {
            _active = this;
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(VisitedTerrainProfilesKey, ref _visitedTerrainProfiles);
            dataStore.SyncData(SerializedStashKey, ref _stash);
            dataStore.SyncData(SerializedGarrisonKey, ref _garrison);
            dataStore.SyncData(SerializedStewardKey, ref _stewardHero);
            dataStore.SyncData(SerializedCookKey, ref _cookHero);
            dataStore.SyncData(SerializedGuardCaptainKey, ref _guardCaptainHero);
            dataStore.SyncData(SerializedHealerKey, ref _healerHero);
            if (_stash == null)
            {
                _stash = new ItemRoster();
            }
            if (_garrison == null)
            {
                _garrison = TroopRoster.CreateDummyTroopRoster();
            }

            if (dataStore.IsLoading)
            {
                string loadedState = string.Empty;
                bool hasSavedState = dataStore.SyncData(SerializedStateKey, ref loadedState);
                RefugeState restoredState;
                string failure = string.Empty;
                if (hasSavedState && TryDeserialize(loadedState, out restoredState, out failure))
                {
                    _state = NormalizeUpgradeConstructionDuration(
                        NormalizeConstructionDuration(restoredState));
                    _serializedState = Serialize(_state);
                }
                else
                {
                    _state = RefugeState.Empty;
                    _serializedState = string.Empty;
                    if (hasSavedState)
                    {
                        Diagnostics.Info(
                            "Saved refuge state was ignored because it was invalid: "
                            + (string.IsNullOrEmpty(failure) ? "unknown validation failure" : failure)
                            + ". The campaign remains playable and no refuge will be restored.");
                    }
                }

                return;
            }

            _serializedState = Serialize(_state);
            dataStore.SyncData(SerializedStateKey, ref _serializedState);
        }

        /// <summary>
        /// Checks every construction prerequisite without changing campaign
        /// state. This is used both for the camp-menu tooltip and immediately
        /// before payment, so a stale UI state cannot charge the player.
        /// </summary>
        internal bool CanStartConstruction(RefugeWaterAccessType requestedAccess, out string reason)
        {
            CompleteConstructionIfDue(false);

            if (!IsSupportedSiteAccess(requestedAccess))
            {
                reason = "Choose a land, river, or coastal refuge site.";
                return false;
            }

            if (_state.State != RefugeConstructionState.None && _state.State != RefugeConstructionState.Camp)
            {
                reason = "Only one refuge may be founded in a campaign.";
                return false;
            }

            MobileParty mainParty = MobileParty.MainParty;
            Hero mainHero = Hero.MainHero;
            if (Campaign.Current == null || mainParty == null || mainHero == null)
            {
                reason = "A campaign party is required to found a refuge.";
                return false;
            }

            if (mainParty.MemberRoster == null || mainParty.MemberRoster.TotalManCount < MinimumPartySize)
            {
                reason = "A refuge requires at least " + MinimumPartySize + " men in your party.";
                return false;
            }

            // Viking Conquest's camp-funds threshold is retained as a
            // requirement, but it is not charged separately. The 1,000-denar
            // construction payment already exceeds it.
            if (mainHero.Gold < MinimumCampFunds)
            {
                reason = "You need more than 150 denars available to establish camp.";
                return false;
            }

            try
            {
                if (!NavigationHelper.IsPositionValidForNavigationType(
                        mainParty.Position,
                        MobileParty.NavigationType.Default))
                {
                    reason = "The refuge must be founded from a valid land position.";
                    return false;
                }

                if (requestedAccess == RefugeWaterAccessType.Land)
                {
                    reason = string.Empty;
                    return true;
                }

                RefugeWaterAccessType verifiedWaterAccess;
                if (!TryGetVerifiedWaterAccess(mainParty.Position, out verifiedWaterAccess))
                {
                    reason = "Build beside a verified navigable river or coastline to use that refuge type.";
                    return false;
                }

                if (verifiedWaterAccess != requestedAccess)
                {
                    reason = requestedAccess == RefugeWaterAccessType.River
                        ? "This site is not beside a verified navigable river."
                        : "This site is not beside a verified coastline.";
                    return false;
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Refuge location validation failed safely.", exception);
                reason = "This location could not be surveyed safely. Move a short distance and try again.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Extends the ordinary site survey with the selected fort's asset and
        /// authored-scene contract. New refuges never fall back to the old
        /// freeform layout importer.
        /// </summary>
        internal bool CanStartConstruction(
            RefugeWaterAccessType requestedAccess,
            string fortPrefabId,
            out string reason)
        {
            if (!CanStartConstruction(requestedAccess, out reason))
            {
                return false;
            }

            if (!RefugeFortPrefabCatalog.IsAssetReady(fortPrefabId, out reason))
            {
                reason = "This fort style is not installed correctly: " + reason + ".";
                return false;
            }

            RefugeSceneClimate climate = GetSceneClimateForSite(MobileParty.MainParty);
            string sceneId;
            if (!RefugeSceneProfileCatalog.TryGetReadySceneId(
                    climate,
                    requestedAccess,
                    fortPrefabId,
                    out sceneId))
            {
                if (RefugeFortPrefabCatalog.AllowsNativeTestFallback(fortPrefabId))
                {
                    reason = string.Empty;
                    return true;
                }
                reason = "This fort style does not yet have a finished "
                    + climate.ToString().ToLowerInvariant() + " "
                    + requestedAccess.ToString().ToLowerInvariant() + " scene.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Surveys the party's current position and chooses the most capable
        /// refuge profile supported by that site. Verified navigable river or
        /// coastal access takes precedence; every other valid position becomes
        /// a land refuge. No campaign state or gold is changed by this survey.
        /// </summary>
        internal bool TrySurveyCurrentSite(
            out RefugeWaterAccessType recommendedAccess,
            out string reason)
        {
            recommendedAccess = RefugeWaterAccessType.None;
            MobileParty mainParty = MobileParty.MainParty;
            if (Campaign.Current == null || mainParty == null)
            {
                reason = "A campaign party is required to survey a refuge site.";
                return false;
            }

            try
            {
                RefugeWaterAccessType verifiedWaterAccess;
                recommendedAccess = TryGetVerifiedWaterAccess(
                    mainParty.Position,
                    out verifiedWaterAccess)
                    ? verifiedWaterAccess
                    : RefugeWaterAccessType.Land;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Automatic refuge-site survey failed safely.", exception);
                reason = "This location could not be surveyed safely. Move a short distance and try again.";
                return false;
            }

            return CanStartConstruction(recommendedAccess, out reason);
        }

        /// <summary>
        /// Performs the atomic campaign-side portion of construction. It
        /// records the map site only after every prerequisite still passes and
        /// after the native gold action succeeds.
        /// </summary>
        internal bool TryStartConstruction(
            RefugeWaterAccessType requestedAccess,
            string fortPrefabId,
            out string failure)
        {
            if (!CanStartConstruction(requestedAccess, fortPrefabId, out failure))
            {
                return false;
            }

            MobileParty mainParty = MobileParty.MainParty;
            Hero mainHero = Hero.MainHero;
            if (mainParty == null || mainHero == null)
            {
                failure = "The party is no longer ready to found a refuge.";
                return false;
            }

            RefugeWaterAccessType siteAccess = requestedAccess;
            if (requestedAccess != RefugeWaterAccessType.Land)
            {
                RefugeWaterAccessType verifiedWaterAccess;
                if (!TryGetVerifiedWaterAccess(mainParty.Position, out verifiedWaterAccess)
                    || verifiedWaterAccess != requestedAccess)
                {
                    failure = "The water access changed before construction could begin. Survey the site again.";
                    return false;
                }

                siteAccess = verifiedWaterAccess;
            }

            RefugeSceneClimate climate = GetSceneClimateForSite(mainParty);
            string sceneId;
            if (!RefugeSceneProfileCatalog.TryGetReadySceneId(
                    climate,
                    siteAccess,
                    fortPrefabId,
                    out sceneId))
            {
                sceneId = GetSceneId(climate, siteAccess, fortPrefabId);
                if (string.IsNullOrEmpty(sceneId))
                {
                    failure = "The selected fort style has no finished scene for this refuge site.";
                    return false;
                }
                Diagnostics.Info("Refuge fort style is using the temporary native test fallback. Fort="
                    + fortPrefabId + "; Scene=" + sceneId + ".");
            }


            if (mainHero.Gold < ConstructionCost)
            {
                failure = "You no longer have enough denars to order construction.";
                return false;
            }

            GiveGoldAction.ApplyBetweenCharacters(mainHero, null, ConstructionCost, disableNotification: true);
            double startedOnDay = CampaignTime.Now.ToDays;
            _state = new RefugeState(
                RefugeConstructionState.UnderConstruction,
                mainParty.Position.X,
                mainParty.Position.Y,
                siteAccess,
                sceneId,
                startedOnDay,
                startedOnDay + ConstructionDurationDays,
                RefugeUpgrade.None,
                fortPrefabId: fortPrefabId);

            Diagnostics.Info(
                "Refuge construction ordered. Access=" + siteAccess
                + "; Climate=" + climate
                + "; MapX=" + _state.MapX.ToString("F3", CultureInfo.InvariantCulture)
                + "; MapY=" + _state.MapY.ToString("F3", CultureInfo.InvariantCulture)
                + "; Scene=" + sceneId
                + "; Fort=" + fortPrefabId
                + "; CompletionDay=" + _state.CompletionDay.ToString("F3", CultureInfo.InvariantCulture)
                + ".");
            EnsureRefugeMapMarker();
            failure = string.Empty;
            return true;
        }

        internal bool TryFoundCamp(RefugeWaterAccessType requestedAccess, out string failure)
        {
            if (!CanStartConstruction(requestedAccess, out failure)) return false;
            MobileParty party = MobileParty.MainParty;
            if (party == null) { failure = "A campaign party is required to establish camp."; return false; }
            RefugeSceneClimate climate = GetSceneClimateForSite(party);
            string sceneId = GetSceneId(climate, requestedAccess, RefugeFortPrefabCatalog.DefaultFortPrefabId);
            if (string.IsNullOrEmpty(sceneId)) { failure = "No portable camp terrain is available for this site."; return false; }
            _state = new RefugeState(RefugeConstructionState.Camp, party.Position.X, party.Position.Y, requestedAccess, sceneId, CampaignTime.Now.ToDays, CampaignTime.Now.ToDays, RefugeUpgrade.None);
            _serializedState = Serialize(_state);
            EnsureRefugeMapMarker();
            Diagnostics.Info("Portable camp founded. Scene=" + sceneId + "; Access=" + requestedAccess + ".");
            InformationManager.DisplayMessage(new InformationMessage(
                "Camp terrain debug: " + climate + " / " + requestedAccess + " / " + sceneId));
            failure = string.Empty;
            return true;
        }

        internal int GetConstructionHoursRemaining()
        {
            CompleteConstructionIfDue(false);
            if (_state.State != RefugeConstructionState.UnderConstruction)
            {
                return 0;
            }

            return Math.Max(
                1,
                (int)Math.Ceiling((_state.CompletionDay - CampaignTime.Now.ToDays) * 24d));
        }

        /// <summary>
        /// Version-specific campaign-map click entry point. It accepts only
        /// clicks whose terrain intersection falls inside the refuge's small
        /// marker radius; all other native map clicks continue unchanged.
        /// </summary>
        internal bool TryOpenProgressFromMapClick(CampaignVec2 clickedPosition)
        {
            CompleteConstructionIfDue(false);
            if (_state.State == RefugeConstructionState.None)
            {
                return false;
            }

            float deltaX = clickedPosition.X - _state.MapX;
            float deltaY = clickedPosition.Y - _state.MapY;
            const float MarkerClickRadius = 1.35f;
            if ((deltaX * deltaX) + (deltaY * deltaY) > MarkerClickRadius * MarkerClickRadius)
            {
                return false;
            }

            Diagnostics.Info(
                "Refuge campaign marker clicked. State=" + _state.State
                + "; Scene=" + _state.SceneId + ".");
            GameMenu.ActivateGameMenu(CalendarCampBehavior.RefugeStatusMenuId);
            return true;
        }

        private void OnDailyTick()
        {
            CompleteConstructionIfDue(true);
            CompleteUpgradeIfDue(true);
            ChargeGarrisonUpkeep();
        }

        private void ChargeGarrisonUpkeep()
        {
            if (_state.State != RefugeConstructionState.Complete || _garrison == null
                || _garrison.TotalManCount <= 0 || Campaign.Current == null || Hero.MainHero == null
                || MobileParty.MainParty == null || Campaign.Current.Models == null
                || Campaign.Current.Models.PartyWageModel == null)
            {
                return;
            }

            try
            {
                int normalWage = Math.Max(0, Campaign.Current.Models.PartyWageModel
                    .GetTotalWage(MobileParty.MainParty, _garrison).RoundedResultNumber);
                int upkeep = (int)Math.Ceiling(normalWage * GarrisonUpkeepMultiplier);
                if (upkeep <= 0 || Hero.MainHero.Gold < upkeep)
                {
                    return;
                }

                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, upkeep, disableNotification: true);
                Diagnostics.Info("Refuge garrison upkeep paid. Cost=" + upkeep + "; Troops="
                    + _garrison.TotalManCount + ".");
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Refuge garrison upkeep could not be processed safely.", exception);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Added entities are runtime map-scene visuals, not saved
            // campaign objects. Recreate this one visual cluster after a
            // save/load or a fresh campaign-map session.
            ForgetRefugeMapMarkerVisuals();
            EnsureRefugeMapMarker();
        }

        /// <summary>
        /// Places a non-interactive five-tent native marker at the refuge
        /// site, including while construction is in progress.
        /// This never creates a settlement, party, quest, or navigation node.
        /// </summary>
        private void EnsureRefugeMapMarker()
        {
            if (_state.State == RefugeConstructionState.None)
            {
                return;
            }

            if (Campaign.Current == null || Campaign.Current.MapSceneWrapper == null)
            {
                return;
            }

            try
            {
                Scene mapScene = TryGetCampaignMapScene();
                if (mapScene == null)
                {
                    Diagnostics.Info("Refuge map marker will be retried after the campaign map scene is available.");
                    return;
                }

                if (_refugeMapMarkerPlaced && ReferenceEquals(mapScene, _refugeMapMarkerScene))
                {
                    return;
                }

                // A campaign-map scene can be recreated after load or a map
                // refresh. Its old entities have already been discarded by
                // the engine, so drop the stale references before rebuilding
                // this visual cluster in the new scene.
                if (_refugeMapMarkerPlaced)
                {
                    ForgetRefugeMapMarkerVisuals();
                    Diagnostics.Info("Campaign map scene changed; rebuilding refuge marker visuals.");
                }

                // Construction replaces the temporary marker with the
                // completed marker. Tracking our own entities lets removal
                // happen immediately without recreating Bannerlord's entire
                // campaign map scene.
                ClearRefugeMapMarkerVisuals();
                if (_state.State == RefugeConstructionState.Complete)
                {
                    CampaignVec2 completedRefugePosition = new CampaignVec2(
                        new Vec2(_state.MapX, _state.MapY),
                        isOnLand: true);
                    AddRefugeMapMarkerEntity(mapScene, CompletedRefugeMapPrefabId, completedRefugePosition);
                    _refugeMapMarkerPlaced = true;
                    Diagnostics.Info("Placed completed refuge map marker at the saved refuge site.");
                    return;
                }

                for (int index = 0; index < RefugeTentOffsets.Length; index++)
                {
                    Vec2 offset = RefugeTentOffsets[index];
                    CampaignVec2 tentPosition = new CampaignVec2(
                        new Vec2(_state.MapX + offset.x, _state.MapY + offset.y),
                        isOnLand: true);
                    AddRefugeMapMarkerEntity(mapScene, RefugeMapTentPrefabId, tentPosition);
                }

                _refugeMapMarkerPlaced = true;
                Diagnostics.Info("Placed five-tent refuge map marker at the saved refuge site.");
            }
            catch (Exception exception)
            {
                // The saved refuge remains valid even if a visual prefab is
                // unavailable in a changed game version or another module's
                // map scene. It will be retried on the next map session.
                Diagnostics.Error("Refuge map marker could not be placed safely.", exception);
            }
        }

        private void AddRefugeMapMarkerEntity(Scene scene, string prefabId, CampaignVec2 position)
        {
            GameEntity entity = GameEntity.Instantiate(scene, prefabId, callScriptCallbacks: true);
            if (entity == null)
            {
                throw new InvalidOperationException("Map marker prefab could not be instantiated: " + prefabId);
            }
            entity.SetLocalPosition(position.AsVec3());
            _refugeMapMarkerEntities.Add(entity);
            _refugeMapMarkerScene = scene;
        }

        private void ClearRefugeMapMarkerVisuals()
        {
            Scene scene = _refugeMapMarkerScene;
            if (scene != null)
            {
                for (int index = 0; index < _refugeMapMarkerEntities.Count; index++)
                {
                    try
                    {
                        scene.RemoveEntity(_refugeMapMarkerEntities[index], 0);
                    }
                    catch (Exception exception)
                    {
                        Diagnostics.Error("A refuge map marker visual could not be removed safely.", exception);
                    }
                }
            }
            _refugeMapMarkerEntities.Clear();
            _refugeMapMarkerScene = null;
            _refugeMapMarkerPlaced = false;
        }

        /// <summary>
        /// Drops managed references when Bannerlord has already recreated the
        /// whole campaign-map scene. Unlike ClearRefugeMapMarkerVisuals this
        /// deliberately does not call RemoveEntity on stale native handles.
        /// </summary>
        private void ForgetRefugeMapMarkerVisuals()
        {
            _refugeMapMarkerEntities.Clear();
            _refugeMapMarkerScene = null;
            _refugeMapMarkerPlaced = false;
        }

        private static Scene TryGetCampaignMapScene()
        {
            try
            {
                object mapScene = Campaign.Current == null ? null : Campaign.Current.MapSceneWrapper;
                PropertyInfo sceneProperty = mapScene == null ? null : mapScene.GetType().GetProperty(
                    "Scene",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return sceneProperty == null ? null : sceneProperty.GetValue(mapScene, null) as Scene;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Campaign map scene could not be accessed for refuge marker refresh.", exception);
                return null;
            }
        }

        private void CompleteConstructionIfDue(bool notifyPlayer)
        {
            if (_state.State != RefugeConstructionState.UnderConstruction
                || Campaign.Current == null
                || CampaignTime.Now.ToDays < _state.CompletionDay)
            {
                return;
            }

            _state = new RefugeState(
                RefugeConstructionState.Complete,
                _state.MapX,
                _state.MapY,
                _state.WaterAccess,
                _state.SceneId,
                _state.StartedOnDay,
                _state.CompletionDay,
                _state.Upgrades,
                _state.ActiveUpgrade,
                _state.UpgradeStartedDay,
                _state.UpgradeCompletionDay,
                _state.FortPrefabId);

            EnsureRefugeStaffHeroes();

            ClearRefugeMapMarkerVisuals();
            EnsureRefugeMapMarker();
            Diagnostics.Info("Refuge construction completed. Scene=" + _state.SceneId + ".");
            if (notifyPlayer)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage("Your refuge is complete. You can return to camp to manage it."));
            }
        }

        private void EnsureRefugeStaffHeroes()
        {
            CharacterObject template = Hero.MainHero != null && Hero.MainHero.Culture != null
                ? Hero.MainHero.Culture.BasicTroop
                : null;
            if (template == null)
            {
                Diagnostics.Info("Refuge staff could not be created because no cultured native template is available.");
                return;
            }

            _stewardHero = EnsureStaffHero(
                _stewardHero,
                template,
                32,
                "{=RCT_RefugeStewardName}Refuge Steward",
                "{=RCT_RefugeStewardFirstName}Steward");
            _cookHero = EnsureStaffHero(
                _cookHero,
                template,
                29,
                "{=RCT_RefugeCookName}Refuge Cook",
                "{=RCT_RefugeCookFirstName}Cook");
            _guardCaptainHero = EnsureStaffHero(
                _guardCaptainHero,
                template,
                35,
                "{=RCT_RefugeGuardCaptainName}Refuge Guard Captain",
                "{=RCT_RefugeGuardCaptainFirstName}Captain");
            CharacterObject healerTemplate = GetFemaleHealerTemplate();
            if (healerTemplate != null)
            {
                _healerHero = EnsureStaffHero(
                    _healerHero,
                    healerTemplate,
                    31,
                    "{=RCT_RefugeHealerName}Refuge Healer",
                    "{=RCT_RefugeHealerFirstName}Healer");
            }
        }

        private static CharacterObject GetFemaleHealerTemplate()
        {
            try
            {
                string cultureId = Hero.MainHero != null && Hero.MainHero.Culture != null
                    ? Hero.MainHero.Culture.StringId
                    : string.Empty;
                CharacterObject template = string.IsNullOrEmpty(cultureId)
                    ? null
                    : MBObjectManager.Instance.GetObject<CharacterObject>("townswoman_" + cultureId);
                return template ?? MBObjectManager.Instance.GetObject<CharacterObject>("townswoman_empire");
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Refuge Healer template lookup failed safely.", exception);
                return null;
            }
        }

        private static Hero EnsureStaffHero(
            Hero existingHero,
            CharacterObject template,
            int age,
            string fullName,
            string firstName)
        {
            if (existingHero != null && existingHero.IsAlive)
            {
                return existingHero;
            }

            try
            {
                Hero staffMember = HeroCreator.CreateSpecialHero(template, null, null, null, age);
                staffMember.SetName(new TextObject(fullName), new TextObject(firstName));
                staffMember.HiddenInEncyclopedia = true;
                staffMember.ChangeState(Hero.CharacterStates.Active);
                Diagnostics.Info("Created persistent refuge staff hero: " + staffMember.Name);
                return staffMember;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Refuge staff hero creation failed safely.", exception);
                return existingHero;
            }
        }

        private void CompleteUpgradeIfDue(bool notifyPlayer)
        {
            if (_state.ActiveUpgrade == RefugeUpgrade.None
                || Campaign.Current == null
                || CampaignTime.Now.ToDays < _state.UpgradeCompletionDay)
            {
                return;
            }

            RefugeUpgrade completedUpgrade = _state.ActiveUpgrade;
            _state = _state.WithUpgrades(_state.Upgrades | completedUpgrade);
            _serializedState = Serialize(_state);
            Diagnostics.Info("Refuge upgrade construction completed. Upgrade=" + completedUpgrade + ".");
            if (notifyPlayer)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage("Construction is complete: " + completedUpgrade + "."));
            }
        }

        private static double GetUpgradeConstructionDays(RefugeUpgrade upgrade)
        {
            return GetUpgradeConstructionHours(upgrade) / 24d;
        }

        internal static int GetUpgradeConstructionHours(RefugeUpgrade upgrade)
        {
            return RefugeBuildingCatalog.GetConstructionHours(upgrade);
        }

        /// <summary>
        /// Finds a nearby water face that Bannerlord's own navigation model
        /// accepts for naval movement. Deliberately accepts only the map's
        /// explicit River, CoastalSea, and OpenSea terrain classes; generic
        /// water and lakes are rejected until a future profile proves them
        /// suitable. This avoids creating an unreachable refuge on a pond.
        /// </summary>
        private static bool TryFindVerifiedWaterAccess(
            CampaignVec2 origin,
            out RefugeWaterAccessType waterAccess)
        {
            waterAccess = RefugeWaterAccessType.None;
            if (Campaign.Current == null || Campaign.Current.MapSceneWrapper == null)
            {
                return false;
            }

            bool foundCoast = false;
            for (int distanceIndex = 0; distanceIndex < SurveyDistances.Length; distanceIndex++)
            {
                float distance = SurveyDistances[distanceIndex];
                for (int directionIndex = 0; directionIndex < 16; directionIndex++)
                {
                    double angle = directionIndex * Math.PI * 2d / 16d;
                    CampaignVec2 candidate = new CampaignVec2(
                        new Vec2(
                            origin.X + (float)(Math.Cos(angle) * distance),
                            origin.Y + (float)(Math.Sin(angle) * distance)),
                        isOnLand: false);

                    if (!candidate.IsValid() || !candidate.Face.IsValid())
                    {
                        continue;
                    }

                    TerrainType terrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(candidate.Face);
                    if (terrain == TerrainType.River)
                    {
                        // Temporary camp-terrain tests require only a real
                        // riverbank, not a ship-navigable river lane.
                        waterAccess = RefugeWaterAccessType.River;
                        return true;
                    }

                    if (!NavigationHelper.IsPositionValidForNavigationType(
                            candidate,
                            MobileParty.NavigationType.Naval))
                    {
                        continue;
                    }

                    if (terrain == TerrainType.CoastalSea || terrain == TerrainType.OpenSea)
                    {
                        foundCoast = true;
                    }
                }
            }

            if (foundCoast)
            {
                waterAccess = RefugeWaterAccessType.Coast;
                return true;
            }

            return false;
        }

        private bool TryGetVerifiedWaterAccess(
            CampaignVec2 origin,
            out RefugeWaterAccessType waterAccess)
        {
            float deltaX = origin.X - _cachedWaterSurveyX;
            float deltaY = origin.Y - _cachedWaterSurveyY;
            if (_hasCachedWaterSurvey
                && (deltaX * deltaX) + (deltaY * deltaY) <= WaterSurveyReuseDistanceSquared)
            {
                waterAccess = _cachedWaterAccess;
                return waterAccess != RefugeWaterAccessType.None;
            }

            bool foundWaterAccess = TryFindVerifiedWaterAccess(origin, out waterAccess);
            _hasCachedWaterSurvey = true;
            _cachedWaterSurveyX = origin.X;
            _cachedWaterSurveyY = origin.Y;
            _cachedWaterAccess = foundWaterAccess ? waterAccess : RefugeWaterAccessType.None;
            return foundWaterAccess;
        }

        private static RefugeSceneClimate GetSceneClimateForSite(MobileParty party)
        {
            if (party == null || Campaign.Current == null)
            {
                return RefugeSceneClimate.Temperate;
            }

            try
            {
                TerrainType terrain = Campaign.Current.MapSceneWrapper == null
                    ? TerrainType.Plain
                    : Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace);
                if (terrain == TerrainType.Snow)
                {
                    return RefugeSceneClimate.Snow;
                }

                Settlement nearest = null;
                float nearestDistanceSquared = float.MaxValue;
                foreach (Settlement settlement in Settlement.All)
                {
                    if (settlement == null || !settlement.IsActive)
                    {
                        continue;
                    }

                    float deltaX = settlement.Position.X - party.Position.X;
                    float deltaY = settlement.Position.Y - party.Position.Y;
                    float distanceSquared = deltaX * deltaX + deltaY * deltaY;
                    if (distanceSquared < nearestDistanceSquared)
                    {
                        nearest = settlement;
                        nearestDistanceSquared = distanceSquared;
                    }
                }

                string cultureId = nearest != null && nearest.Culture != null
                    ? nearest.Culture.StringId
                    : string.Empty;
                if (string.Equals(cultureId, "sturgia", StringComparison.OrdinalIgnoreCase))
                {
                    return RefugeSceneClimate.Snow;
                }

                if (terrain == TerrainType.Desert
                    || terrain == TerrainType.Dune
                    || string.Equals(cultureId, "aserai", StringComparison.OrdinalIgnoreCase))
                {
                    return RefugeSceneClimate.Desert;
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Refuge climate survey failed; using the temperate profile safely.", exception);
            }

            return RefugeSceneClimate.Temperate;
        }

        private static string GetSceneId(
            RefugeSceneClimate climate,
            RefugeWaterAccessType waterAccess,
            string fortPrefabId)
        {
            // A finished profile owns its terrain, linked fort, collision, and
            // navmesh. It is the only route that preserves the circular
            // authored layout without reconstructing its children at runtime.
            string authoredSceneId;
            if (RefugeSceneProfileCatalog.TryGetReadySceneId(
                    climate,
                    waterAccess,
                    fortPrefabId,
                    out authoredSceneId))
            {
                return authoredSceneId;
            }

            // Existing saves made before fort-style selection retain their
            // known native fallback. New construction is blocked unless its
            // selected fort has a ready authored profile.
            if (!RefugeFortPrefabCatalog.AllowsNativeTestFallback(fortPrefabId))
            {
                return string.Empty;
            }

            // Coast camps must be tied to the actual campaign-map patch,
            // not a climate-wide NavalDLC scene. Those generic naval scenes
            // expose ship/water navigation and can put a pedestrian refuge
            // below the map. Binding the resolved scene here makes it part of
            // the saved refuge state, so later visits never re-roll it.
            if (waterAccess == RefugeWaterAccessType.Coast)
            {
                string campaignCoastScene = TryResolveCampaignPatchScene();
                if (!string.IsNullOrEmpty(campaignCoastScene))
                {
                    Diagnostics.Info("PortableCampDiagnostic CampaignCoastSceneBound"
                        + "; Climate=" + climate
                        + "; Scene=" + campaignCoastScene + ".");
                    return campaignCoastScene;
                }
            }

            // Portable-camp test route: use the same climate/access matrix
            // documented in RefugeSceneProfiles.xml so every map type can be
            // verified before its module-owned scene is authored.
            string portableSceneId;
            if (climate == RefugeSceneClimate.Desert)
            {
                portableSceneId = waterAccess == RefugeWaterAccessType.River ? "river_bt_aserai_01_4x4km"
                    : waterAccess == RefugeWaterAccessType.Coast ? "battle_terrain_coastal_01"
                    : "battle_terrain_009";
            }
            else if (climate == RefugeSceneClimate.Snow)
            {
                portableSceneId = waterAccess == RefugeWaterAccessType.River ? "river_bt_nord_01_4x4km"
                    : waterAccess == RefugeWaterAccessType.Coast ? "coastal_terrain_north_of_the_north_sea_01"
                    : "battle_terrain_006";
            }
            else
            {
                portableSceneId = waterAccess == RefugeWaterAccessType.River ? "river_bt_empirewest_01_4x4km"
                    : waterAccess == RefugeWaterAccessType.Coast ? "battle_terrain_coastal_02"
                    : "battle_terrain_001";
            }
            Diagnostics.Info("PortableCampDiagnostic ProfileSelected"
                + "; Climate=" + climate
                + "; Access=" + waterAccess
                + "; Fort=" + fortPrefabId
                + "; Scene=" + portableSceneId + ".");
            return portableSceneId;
        }

        private static string TryResolveCampaignPatchScene()
        {
            try
            {
                if (Campaign.Current == null
                    || Campaign.Current.Models == null
                    || Campaign.Current.Models.SceneModel == null
                    || Campaign.Current.MapSceneWrapper == null
                    || MobileParty.MainParty == null)
                {
                    return string.Empty;
                }

                string sceneId = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(
                    Campaign.Current.MapSceneWrapper.GetMapPatchAtPosition(MobileParty.MainParty.Position),
                    false);
                return string.IsNullOrWhiteSpace(sceneId) ? string.Empty : sceneId;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Campaign coast scene binding failed; using the legacy test scene.", exception);
                return string.Empty;
            }
        }

        private static bool ShouldRebindLegacyCoastScene(string sceneId, RefugeWaterAccessType waterAccess)
        {
            if (waterAccess != RefugeWaterAccessType.Coast || string.IsNullOrWhiteSpace(sceneId))
            {
                return false;
            }

            return string.Equals(sceneId, TemperateCoastSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, DesertCoastSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, SnowCoastSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyTemperateCoastSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyTemperateCoastNavalSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyDesertCoastNavalSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacySnowCoastNavalSceneId, StringComparison.Ordinal);
        }

        private static bool TryGetSceneClimate(string sceneId, out RefugeSceneClimate climate)
        {
            RefugeSceneProfile profile;
            if (RefugeSceneProfileCatalog.TryGetProfile(sceneId, out profile))
            {
                climate = profile.Climate;
                return true;
            }

            if (string.Equals(sceneId, AuthoredRefugeSceneId, StringComparison.Ordinal))
            {
                climate = RefugeSceneClimate.Temperate;
                return true;
            }

            if (string.Equals(sceneId, DesertLandSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, DesertRiverSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, DesertCoastSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyDesertLandSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyDesertRiverNavalSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyDesertCoastNavalSceneId, StringComparison.Ordinal))
            {
                climate = RefugeSceneClimate.Desert;
                return true;
            }

            if (string.Equals(sceneId, SnowLandSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, SnowRiverSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, SnowCoastSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacySnowLandSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacySnowRiverNavalSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacySnowCoastNavalSceneId, StringComparison.Ordinal))
            {
                climate = RefugeSceneClimate.Snow;
                return true;
            }

            if (string.Equals(sceneId, TemperateLandSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, TemperateRiverSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, TemperateCoastSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyTemperateBiomeSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyTemperateLandSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyTemperateCoastSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyTemperateRiverNavalSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyTemperateCoastNavalSceneId, StringComparison.Ordinal))
            {
                climate = RefugeSceneClimate.Temperate;
                return true;
            }

            climate = RefugeSceneClimate.Temperate;
            return false;
        }

        private static bool IsSceneCompatibleWithWater(string sceneId, RefugeWaterAccessType waterAccess)
        {
            RefugeSceneProfile profile;
            if (RefugeSceneProfileCatalog.TryGetProfile(sceneId, out profile))
            {
                return profile.WaterAccess == waterAccess;
            }

            if (string.Equals(sceneId, AuthoredRefugeSceneId, StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(sceneId, LegacyTemperateBiomeSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyTemperateLandSceneId, StringComparison.Ordinal))
            {
                return waterAccess == RefugeWaterAccessType.Land;
            }

            if (string.Equals(sceneId, LegacyTemperateCoastSceneId, StringComparison.Ordinal))
            {
                return waterAccess == RefugeWaterAccessType.Coast;
            }

            if (string.Equals(sceneId, LegacyDesertLandSceneId, StringComparison.Ordinal))
            {
                return waterAccess == RefugeWaterAccessType.Land;
            }

            if (string.Equals(sceneId, LegacyTemperateRiverNavalSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyDesertRiverNavalSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacySnowRiverNavalSceneId, StringComparison.Ordinal))
            {
                return waterAccess == RefugeWaterAccessType.River;
            }

            if (string.Equals(sceneId, LegacyTemperateCoastNavalSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacyDesertCoastNavalSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, LegacySnowCoastNavalSceneId, StringComparison.Ordinal))
            {
                return waterAccess == RefugeWaterAccessType.Coast;
            }

            if (string.Equals(sceneId, LegacySnowLandSceneId, StringComparison.Ordinal))
            {
                return waterAccess == RefugeWaterAccessType.Land;
            }

            return string.Equals(sceneId, GetSceneId(RefugeSceneClimate.Temperate, waterAccess, RefugeFortPrefabCatalog.DefaultFortPrefabId), StringComparison.Ordinal)
                || string.Equals(sceneId, GetSceneId(RefugeSceneClimate.Desert, waterAccess, RefugeFortPrefabCatalog.DefaultFortPrefabId), StringComparison.Ordinal)
                || string.Equals(sceneId, GetSceneId(RefugeSceneClimate.Snow, waterAccess, RefugeFortPrefabCatalog.DefaultFortPrefabId), StringComparison.Ordinal);
        }

        // Existing saves may contain construction that was started when the
        // earlier multi-day prototypes were active. Preserve the site and all
        // other data, but apply the current one-hour construction rule.
        private static RefugeState NormalizeConstructionDuration(RefugeState state)
        {
            if (state.State != RefugeConstructionState.UnderConstruction)
            {
                return state;
            }

            double currentDurationEnd = state.StartedOnDay + ConstructionDurationDays;
            if (state.CompletionDay <= currentDurationEnd)
            {
                return state;
            }

            return new RefugeState(
                state.State,
                state.MapX,
                state.MapY,
                state.WaterAccess,
                state.SceneId,
                state.StartedOnDay,
                currentDurationEnd,
                state.Upgrades,
                state.ActiveUpgrade,
                state.UpgradeStartedDay,
                state.UpgradeCompletionDay,
                state.FortPrefabId);
        }

        private static RefugeState NormalizeUpgradeConstructionDuration(RefugeState state)
        {
            if (state.ActiveUpgrade == RefugeUpgrade.None)
            {
                return state;
            }

            // Test builds use one campaign hour for every upgrade. Apply that
            // rule to a project already under way as well as new projects.
            double completionDay = state.UpgradeStartedDay
                + GetUpgradeConstructionDays(state.ActiveUpgrade);
            if (Math.Abs(state.UpgradeCompletionDay - completionDay) < 0.000001d)
            {
                return state;
            }

            return new RefugeState(
                state.State,
                state.MapX,
                state.MapY,
                state.WaterAccess,
                state.SceneId,
                state.StartedOnDay,
                state.CompletionDay,
                state.Upgrades,
                state.ActiveUpgrade,
                state.UpgradeStartedDay,
                completionDay,
                state.FortPrefabId);
        }

        private static string Serialize(RefugeState state)
        {
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = false,
                OmitXmlDeclaration = true,
                NewLineHandling = NewLineHandling.None
            };

            using (System.IO.StringWriter output = new System.IO.StringWriter(CultureInfo.InvariantCulture))
            using (XmlWriter writer = XmlWriter.Create(output, settings))
            {
                writer.WriteStartElement("refuge");
                writer.WriteAttributeString("v", StateSchemaVersion.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("state", ((int)state.State).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("x", state.MapX.ToString("R", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("y", state.MapY.ToString("R", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("water", ((int)state.WaterAccess).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("scene", state.SceneId ?? string.Empty);
                writer.WriteAttributeString("fort", state.FortPrefabId ?? RefugeFortPrefabCatalog.DefaultFortPrefabId);
                writer.WriteAttributeString("started", state.StartedOnDay.ToString("R", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("complete", state.CompletionDay.ToString("R", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("upgrades", ((int)state.Upgrades).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("activeUpgrade", ((int)state.ActiveUpgrade).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("upgradeStarted", state.UpgradeStartedDay.ToString("R", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("upgradeComplete", state.UpgradeCompletionDay.ToString("R", CultureInfo.InvariantCulture));
                writer.WriteEndElement();
                writer.Flush();
                return output.ToString();
            }
        }

        private static bool TryDeserialize(string serialized, out RefugeState state, out string failure)
        {
            state = RefugeState.Empty;
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return true;
            }

            if (serialized.Length > MaximumSerializedStateLength)
            {
                failure = "payload exceeded the maximum safe length";
                return false;
            }

            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                XmlResolver = null
            };

            try
            {
                using (System.IO.StringReader input = new System.IO.StringReader(serialized))
                using (XmlReader reader = XmlReader.Create(input, settings))
                {
                    reader.MoveToContent();
                    if (reader.NodeType != XmlNodeType.Element || reader.Name != "refuge")
                    {
                        failure = "root element was not refuge";
                        return false;
                    }

                    int version;
                    int constructionState;
                    int waterAccess;
                    int upgradeMask = 0;
                    int activeUpgradeMask = 0;
                    float mapX;
                    float mapY;
                    double startedOnDay;
                    double completionDay;
                    double upgradeStartedDay = 0d;
                    double upgradeCompletionDay = 0d;
                    string sceneId = reader.GetAttribute("scene") ?? string.Empty;
                    if (!TryReadInt(reader.GetAttribute("v"), out version)
                        || !TryReadInt(reader.GetAttribute("state"), out constructionState)
                        || !TryReadInt(reader.GetAttribute("water"), out waterAccess)
                        || !TryReadFloat(reader.GetAttribute("x"), out mapX)
                        || !TryReadFloat(reader.GetAttribute("y"), out mapY)
                        || !TryReadDouble(reader.GetAttribute("started"), out startedOnDay)
                        || !TryReadDouble(reader.GetAttribute("complete"), out completionDay))
                    {
                        failure = "required attributes were missing or malformed";
                        return false;
                    }

                    string fortPrefabId = version >= 5
                        ? reader.GetAttribute("fort") ?? string.Empty
                        : RefugeFortPrefabCatalog.DefaultFortPrefabId;

                    if (version != 1 && version != 2 && version != 3 && version != 4 && version != StateSchemaVersion)
                    {
                        failure = "unsupported schema version " + version;
                        return false;
                    }

                    if (version >= 2
                        && !TryReadInt(reader.GetAttribute("upgrades"), out upgradeMask))
                    {
                        failure = "upgrade state was missing or malformed";
                        return false;
                    }

                    if (version >= 3
                        && (!TryReadInt(reader.GetAttribute("activeUpgrade"), out activeUpgradeMask)
                            || !TryReadDouble(reader.GetAttribute("upgradeStarted"), out upgradeStartedDay)
                            || !TryReadDouble(reader.GetAttribute("upgradeComplete"), out upgradeCompletionDay)))
                    {
                        failure = "active upgrade state was missing or malformed";
                        return false;
                    }

                    if (constructionState < (int)RefugeConstructionState.None
                        || constructionState > (int)RefugeConstructionState.Complete
                        || waterAccess < (int)RefugeWaterAccessType.None
                        || waterAccess > (int)RefugeWaterAccessType.Land)
                    {
                        failure = "state contained an unknown enum value";
                        return false;
                    }

                    const int KnownUpgradeMask = (int)(RefugeUpgrade.Barracks
                        | RefugeUpgrade.Tavern
                        | RefugeUpgrade.StaffTents
                        | RefugeUpgrade.SleepingQuarters
                        | RefugeUpgrade.Blacksmith
                        | RefugeUpgrade.Stash
                        | RefugeUpgrade.GuardTowers
                        | RefugeUpgrade.Infirmary
                        | RefugeUpgrade.TrainingYard);
                    if (upgradeMask < 0 || (upgradeMask & ~KnownUpgradeMask) != 0)
                    {
                        failure = "state contained an unknown refuge upgrade";
                        return false;
                    }
                    if (activeUpgradeMask != 0
                        && ((activeUpgradeMask & ~KnownUpgradeMask) != 0
                            || (activeUpgradeMask & (activeUpgradeMask - 1)) != 0))
                    {
                        failure = "state contained an invalid active refuge upgrade";
                        return false;
                    }

                    RefugeConstructionState parsedConstructionState = (RefugeConstructionState)constructionState;
                    RefugeWaterAccessType parsedWaterAccess = (RefugeWaterAccessType)waterAccess;
                    RefugeFortPrefabDefinition registeredFort;
                    if (!RefugeFortPrefabCatalog.TryGet(fortPrefabId, out registeredFort))
                    {
                        // A retired optional style must not make a campaign
                        // save unopenable. It falls back to the original fort.
                        fortPrefabId = RefugeFortPrefabCatalog.DefaultFortPrefabId;
                    }
                    if (parsedConstructionState == RefugeConstructionState.None)
                    {
                        state = RefugeState.Empty;
                        return true;
                    }

                    if (!IsFinite(mapX)
                        || !IsFinite(mapY)
                        || double.IsNaN(startedOnDay)
                        || double.IsInfinity(startedOnDay)
                        || double.IsNaN(completionDay)
                        || double.IsInfinity(completionDay)
                        || completionDay < startedOnDay
                        || double.IsNaN(upgradeStartedDay)
                        || double.IsInfinity(upgradeStartedDay)
                        || double.IsNaN(upgradeCompletionDay)
                        || double.IsInfinity(upgradeCompletionDay)
                        || upgradeCompletionDay < upgradeStartedDay)
                    {
                        failure = "coordinates or construction dates were invalid";
                        return false;
                    }

                    if (parsedWaterAccess == RefugeWaterAccessType.None
                        || !IsSceneCompatibleWithWater(sceneId, parsedWaterAccess))
                    {
                        failure = "scene profile did not match the saved water access";
                        return false;
                    }

                    state = new RefugeState(
                        parsedConstructionState,
                        mapX,
                        mapY,
                        parsedWaterAccess,
                        sceneId,
                        startedOnDay,
                        completionDay,
                        (RefugeUpgrade)upgradeMask,
                        (RefugeUpgrade)activeUpgradeMask,
                        upgradeStartedDay,
                        upgradeCompletionDay,
                        fortPrefabId);
                    return true;
                }
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name;
                return false;
            }
        }

        private static bool TryReadInt(string value, out int result)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryReadFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryReadDouble(string value, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsSupportedSiteAccess(RefugeWaterAccessType access)
        {
            return access == RefugeWaterAccessType.Land
                || access == RefugeWaterAccessType.River
                || access == RefugeWaterAccessType.Coast;
        }

        private sealed class RefugeState
        {
            internal static readonly RefugeState Empty = new RefugeState(
                RefugeConstructionState.None,
                0f,
                0f,
                RefugeWaterAccessType.None,
                string.Empty,
                0d,
                0d,
                RefugeUpgrade.None);

            internal readonly RefugeConstructionState State;
            internal readonly float MapX;
            internal readonly float MapY;
            internal readonly RefugeWaterAccessType WaterAccess;
            internal readonly string SceneId;
            internal readonly double StartedOnDay;
            internal readonly double CompletionDay;
            internal readonly RefugeUpgrade Upgrades;
            internal readonly RefugeUpgrade ActiveUpgrade;
            internal readonly double UpgradeStartedDay;
            internal readonly double UpgradeCompletionDay;
            internal readonly string FortPrefabId;

            internal RefugeState(
                RefugeConstructionState state,
                float mapX,
                float mapY,
                RefugeWaterAccessType waterAccess,
                string sceneId,
                double startedOnDay,
                double completionDay,
                RefugeUpgrade upgrades,
                RefugeUpgrade activeUpgrade = RefugeUpgrade.None,
                double upgradeStartedDay = 0d,
                double upgradeCompletionDay = 0d,
                string fortPrefabId = RefugeFortPrefabCatalog.DefaultFortPrefabId)
            {
                State = state;
                MapX = mapX;
                MapY = mapY;
                WaterAccess = waterAccess;
                SceneId = sceneId ?? string.Empty;
                StartedOnDay = startedOnDay;
                CompletionDay = completionDay;
                Upgrades = upgrades;
                ActiveUpgrade = activeUpgrade;
                UpgradeStartedDay = upgradeStartedDay;
                UpgradeCompletionDay = upgradeCompletionDay;
                FortPrefabId = string.IsNullOrWhiteSpace(fortPrefabId)
                    ? RefugeFortPrefabCatalog.DefaultFortPrefabId
                    : fortPrefabId;
            }

            internal RefugeState WithUpgrades(RefugeUpgrade upgrades)
            {
                return new RefugeState(
                    State,
                    MapX,
                    MapY,
                    WaterAccess,
                    SceneId,
                    StartedOnDay,
                    CompletionDay,
                    upgrades,
                    fortPrefabId: FortPrefabId);
            }

            internal RefugeState WithUpgradeConstruction(
                RefugeUpgrade upgrade,
                double startedOnDay,
                double completionDay)
            {
                return new RefugeState(
                    State,
                    MapX,
                    MapY,
                    WaterAccess,
                    SceneId,
                    StartedOnDay,
                    CompletionDay,
                    Upgrades,
                    upgrade,
                    startedOnDay,
                    completionDay,
                    FortPrefabId);
            }

            internal RefugeState WithSceneId(string sceneId)
            {
                return new RefugeState(
                    State,
                    MapX,
                    MapY,
                    WaterAccess,
                    sceneId,
                    StartedOnDay,
                    CompletionDay,
                    Upgrades,
                    ActiveUpgrade,
                    UpgradeStartedDay,
                    UpgradeCompletionDay,
                    FortPrefabId);
            }
        }
    }

    internal enum RefugeConstructionState
    {
        None = 0,
        UnderConstruction = 1,
        Complete = 2,
        Camp = 3
    }

    internal enum RefugeWaterAccessType
    {
        None = 0,
        River = 1,
        Coast = 2,
        Land = 3
    }

    internal enum RefugeSceneClimate
    {
        Temperate = 0,
        Desert = 1,
        Snow = 2
    }
}
