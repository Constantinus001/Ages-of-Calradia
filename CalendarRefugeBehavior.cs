using System;
using System.Globalization;
using System.IO;
using System.Xml;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

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
        internal const float InteractionRadius = 2.0f;

        private const string SerializedStateKey = "RealisticCalendarTweaks.RefugeV1";
        private const int StateSchemaVersion = 2;
        private const int MaximumSerializedStateLength = 1024;

        // Nine native scene foundations provide distinct geography while our
        // mission controller supplies the original refuge layout and climate.
        // Keeping the heavy terrain assets in their owning game modules avoids
        // copying native files into this module.
        // A clear native battlefield foundation keeps the player-built
        // compound distinct from a pre-existing bandit camp. Our fixed
        // palisade layout supplies the refuge structures on this terrain.
        private const string TemperateLandSceneId = "battle_terrain_001";
        private const string LegacyTemperateLandSceneId = "bandit_forest";
        private const string TemperateRiverSceneId = "empire_village_e_navalraid";
        private const string TemperateCoastSceneId = "sea_bandit_b";
        private const string DesertLandSceneId = "desert_hideout_002";
        private const string DesertRiverSceneId = "aserai_village_c";
        private const string DesertCoastSceneId = "aserai_village_k_navalraid";
        private const string SnowLandSceneId = "sturgia_village_c";
        private const string SnowRiverSceneId = "sturgia_village_a";
        private const string SnowCoastSceneId = "sturgia_village_g_navalraid_v2";
        private const string RefugeMapTentPrefabId = "map_icon_siege_camp_tent";
        private const string CompletedRefugeMapPrefabId = "rct_refuge_complete_map";

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
        private RefugeState _state = RefugeState.Empty;
        private bool _hasCachedWaterSurvey;
        private float _cachedWaterSurveyX;
        private float _cachedWaterSurveyY;
        private RefugeWaterAccessType _cachedWaterAccess;
        private bool _refugeMapMarkerPlaced;

        internal static CalendarRefugeBehavior Active
        {
            get { return _active; }
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
            get { return ConstructionState != RefugeConstructionState.None; }
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

        internal RefugeUpgrade Upgrades
        {
            get { return _state.Upgrades; }
        }

        internal bool HasUpgrade(RefugeUpgrade upgrade)
        {
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
                RefugeUpgrade.GuardTowers
            })
            {
                if (HasUpgrade(upgrade))
                {
                    count++;
                }
            }

            return count;
        }

        internal bool TryPurchaseUpgrade(RefugeUpgrade upgrade, int cost, out string failure)
        {
            CompleteConstructionIfDue(false);
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

            Hero mainHero = Hero.MainHero;
            if (mainHero == null || mainHero.Gold < cost)
            {
                failure = "You do not have enough denars for that construction order.";
                return false;
            }

            GiveGoldAction.ApplyBetweenCharacters(mainHero, null, cost, disableNotification: true);
            _state = _state.WithUpgrades(_state.Upgrades | upgrade);
            _serializedState = Serialize(_state);
            Diagnostics.Info("Refuge upgrade purchased. Upgrade=" + upgrade + "; Cost=" + cost + ".");
            failure = string.Empty;
            return true;
        }

        internal bool TryEnterCompletedRefuge(out string failure)
        {
            CompleteConstructionIfDue(false);
            if (_state.State != RefugeConstructionState.Complete)
            {
                failure = "The refuge must be completed before it can be entered.";
                return false;
            }

            if (!IsMainPartyWithinInteractionRange)
            {
                failure = "Move your party closer to the refuge before entering.";
                return false;
            }

            MobileParty mainParty = MobileParty.MainParty;
            RefugeSceneClimate climate = GetSceneClimateForSite(mainParty);
            string sceneId = GetSceneId(climate, _state.WaterAccess);
            if (string.IsNullOrEmpty(sceneId))
            {
                failure = "No safe refuge scene profile is available for this site.";
                return false;
            }

            bool isWinter = CalendarTimeMath.GetSeason(CampaignTime.Now)
                == (int)CampaignTime.Seasons.Winter;
            return CalendarRefugeMission.TryOpen(sceneId, climate, isWinter, _state.Upgrades, out failure);
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
            if (dataStore.IsLoading)
            {
                string loadedState = string.Empty;
                bool hasSavedState = dataStore.SyncData(SerializedStateKey, ref loadedState);
                RefugeState restoredState;
                string failure = string.Empty;
                if (hasSavedState && TryDeserialize(loadedState, out restoredState, out failure))
                {
                    _state = NormalizeConstructionDuration(restoredState);
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

            if (_state.State != RefugeConstructionState.None)
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

            if (mainHero.Gold < ConstructionCost)
            {
                reason = "You need " + ConstructionCost + " denars to order construction.";
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
        /// Performs the atomic campaign-side portion of construction. It
        /// records the map site only after every prerequisite still passes and
        /// after the native gold action succeeds.
        /// </summary>
        internal bool TryStartConstruction(RefugeWaterAccessType requestedAccess, out string failure)
        {
            if (!CanStartConstruction(requestedAccess, out failure))
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
            string sceneId = GetSceneId(climate, siteAccess);
            if (string.IsNullOrEmpty(sceneId))
            {
                failure = "No safe native scene profile is available for this site.";
                return false;
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
                RefugeUpgrade.None);

            Diagnostics.Info(
                "Refuge construction ordered. Access=" + siteAccess
                + "; Climate=" + climate
                + "; MapX=" + _state.MapX.ToString("F3", CultureInfo.InvariantCulture)
                + "; MapY=" + _state.MapY.ToString("F3", CultureInfo.InvariantCulture)
                + "; Scene=" + sceneId
                + "; CompletionDay=" + _state.CompletionDay.ToString("F3", CultureInfo.InvariantCulture)
                + ".");
            EnsureRefugeMapMarker();
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
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Added entities are runtime map-scene visuals, not saved
            // campaign objects. Recreate this one visual cluster after a
            // save/load or a fresh campaign-map session.
            EnsureRefugeMapMarker();
        }

        /// <summary>
        /// Places a non-interactive five-tent native marker at the refuge
        /// site, including while construction is in progress.
        /// This never creates a settlement, party, quest, or navigation node.
        /// </summary>
        private void EnsureRefugeMapMarker()
        {
            if (_refugeMapMarkerPlaced || _state.State == RefugeConstructionState.None)
            {
                return;
            }

            if (Campaign.Current == null || Campaign.Current.MapSceneWrapper == null)
            {
                return;
            }

            try
            {
                if (_state.State == RefugeConstructionState.Complete)
                {
                    CampaignVec2 completedRefugePosition = new CampaignVec2(
                        new Vec2(_state.MapX, _state.MapY),
                        isOnLand: true);
                    Campaign.Current.MapSceneWrapper.AddNewEntityToMapScene(
                        CompletedRefugeMapPrefabId,
                        completedRefugePosition);
                    _refugeMapMarkerPlaced = true;
                    Diagnostics.Info("Placed completed palisade refuge map marker at the saved refuge site.");
                    return;
                }

                for (int index = 0; index < RefugeTentOffsets.Length; index++)
                {
                    Vec2 offset = RefugeTentOffsets[index];
                    CampaignVec2 tentPosition = new CampaignVec2(
                        new Vec2(_state.MapX + offset.x, _state.MapY + offset.y),
                        isOnLand: true);
                    Campaign.Current.MapSceneWrapper.AddNewEntityToMapScene(
                        RefugeMapTentPrefabId,
                        tentPosition);
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
                _state.Upgrades);

            // Runtime map entities cannot be removed through Bannerlord's
            // supported IMapScene API. Add the completed landmark now; after
            // the next map reload only this completed prefab is recreated.
            _refugeMapMarkerPlaced = false;
            EnsureRefugeMapMarker();
            Diagnostics.Info("Refuge construction completed. Scene=" + _state.SceneId + ".");
            if (notifyPlayer)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage("Your refuge is complete. You can return to camp to manage it."));
            }
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

                    if (!NavigationHelper.IsPositionValidForNavigationType(
                            candidate,
                            MobileParty.NavigationType.Naval))
                    {
                        continue;
                    }

                    TerrainType terrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(candidate.Face);
                    if (terrain == TerrainType.River)
                    {
                        waterAccess = RefugeWaterAccessType.River;
                        return true;
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

        private static string GetSceneId(RefugeSceneClimate climate, RefugeWaterAccessType waterAccess)
        {
            if (climate == RefugeSceneClimate.Desert)
            {
                switch (waterAccess)
                {
                    case RefugeWaterAccessType.Land: return DesertLandSceneId;
                    case RefugeWaterAccessType.River: return DesertRiverSceneId;
                    case RefugeWaterAccessType.Coast: return DesertCoastSceneId;
                    default: return string.Empty;
                }
            }

            if (climate == RefugeSceneClimate.Snow)
            {
                switch (waterAccess)
                {
                    case RefugeWaterAccessType.Land: return SnowLandSceneId;
                    case RefugeWaterAccessType.River: return SnowRiverSceneId;
                    case RefugeWaterAccessType.Coast: return SnowCoastSceneId;
                    default: return string.Empty;
                }
            }

            switch (waterAccess)
            {
                case RefugeWaterAccessType.Land: return TemperateLandSceneId;
                case RefugeWaterAccessType.River: return TemperateRiverSceneId;
                case RefugeWaterAccessType.Coast: return TemperateCoastSceneId;
                default: return string.Empty;
            }
        }

        private static bool IsSceneCompatibleWithWater(string sceneId, RefugeWaterAccessType waterAccess)
        {
            // The first river prototype used the scene now assigned to the
            // snowy coastal profile. Accept that one saved value and resolve
            // the current profile afresh when the player enters.
            bool isLegacyRiver = waterAccess == RefugeWaterAccessType.River
                && string.Equals(sceneId, SnowCoastSceneId, StringComparison.Ordinal);
            bool isLegacyTemperateLand = waterAccess == RefugeWaterAccessType.Land
                && string.Equals(sceneId, LegacyTemperateLandSceneId, StringComparison.Ordinal);
            return isLegacyRiver
                || isLegacyTemperateLand
                || string.Equals(sceneId, GetSceneId(RefugeSceneClimate.Temperate, waterAccess), StringComparison.Ordinal)
                || string.Equals(sceneId, GetSceneId(RefugeSceneClimate.Desert, waterAccess), StringComparison.Ordinal)
                || string.Equals(sceneId, GetSceneId(RefugeSceneClimate.Snow, waterAccess), StringComparison.Ordinal);
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
                state.Upgrades);
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
                writer.WriteAttributeString("started", state.StartedOnDay.ToString("R", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("complete", state.CompletionDay.ToString("R", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("upgrades", ((int)state.Upgrades).ToString(CultureInfo.InvariantCulture));
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
                    float mapX;
                    float mapY;
                    double startedOnDay;
                    double completionDay;
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

                    if (version != 1 && version != StateSchemaVersion)
                    {
                        failure = "unsupported schema version " + version;
                        return false;
                    }

                    if (version >= StateSchemaVersion
                        && !TryReadInt(reader.GetAttribute("upgrades"), out upgradeMask))
                    {
                        failure = "upgrade state was missing or malformed";
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
                        | RefugeUpgrade.GuardTowers);
                    if (upgradeMask < 0 || (upgradeMask & ~KnownUpgradeMask) != 0)
                    {
                        failure = "state contained an unknown refuge upgrade";
                        return false;
                    }

                    RefugeConstructionState parsedConstructionState = (RefugeConstructionState)constructionState;
                    RefugeWaterAccessType parsedWaterAccess = (RefugeWaterAccessType)waterAccess;
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
                        || completionDay < startedOnDay)
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
                        (RefugeUpgrade)upgradeMask);
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

            internal RefugeState(
                RefugeConstructionState state,
                float mapX,
                float mapY,
                RefugeWaterAccessType waterAccess,
                string sceneId,
                double startedOnDay,
                double completionDay,
                RefugeUpgrade upgrades)
            {
                State = state;
                MapX = mapX;
                MapY = mapY;
                WaterAccess = waterAccess;
                SceneId = sceneId ?? string.Empty;
                StartedOnDay = startedOnDay;
                CompletionDay = completionDay;
                Upgrades = upgrades;
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
                    upgrades);
            }
        }
    }

    internal enum RefugeConstructionState
    {
        None = 0,
        UnderConstruction = 1,
        Complete = 2
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
