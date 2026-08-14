using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Map-safe World Calendar host. A pushed ScreenBase replaces the map
    /// renderer in this Bannerlord build, so this uses ScreenManager's global
    /// layer stack to present the same Gauntlet movie over the active map.
    /// </summary>
    internal sealed class WorldCalendarScreen : GlobalLayer
    {
        private static WorldCalendarScreen _active;
        private readonly GauntletLayer _layer;
        private readonly CalendarWorldLedgerVM _dataSource;
        private int _lastOwnershipRevision = -1;
        private float _friendlyArmyRefreshElapsed;

        private WorldCalendarScreen()
        {
            // This follows Bannerlord's own map-bar and full-screen-notice
            // global-layer setup. The focus flag belongs to the ScreenLayer,
            // not the GauntletLayer constructor.
            _layer = new GauntletLayer("WorldCalendar", 240, false);
            _dataSource = new CalendarWorldLedgerVM(Close);
            _layer.LoadMovie("WorldCalendar", _dataSource);
            Layer = _layer;
            Layer.IsFocusLayer = true;
            Layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
        }

        internal static void Show()
        {
            if (_active != null) return;

            try
            {
                WorldCalendarScreen screen = new WorldCalendarScreen();
                _active = screen;
                ScreenManager.AddGlobalLayer(screen, true);
                Diagnostics.Info("World Events opened with refreshed campaign data.");
            }
            catch (Exception exception)
            {
                _active = null;
                Diagnostics.Error("World Calendar overlay could not be initialized.", exception);
            }
        }

        internal static void Toggle()
        {
            if (_active != null)
            {
                _active.Close();
                return;
            }

            Show();
        }

        protected override void OnTick(float dt)
        {
            base.OnTick(dt);

            // The view model accepts this wheel input only while the pointer
            // is over the map viewport. The Kingdom Summary and saved-history
            // panels retain their normal independent scrolling.
            if (_dataSource.IsStrategicMap && Input.IsMouseScrollChanged)
            {
                _dataSource.AdjustStrategicMapZoomFromMouseWheel(Input.DeltaMouseScroll);
            }

            if (_dataSource.IsStrategicMap)
            {
                _friendlyArmyRefreshElapsed += dt;
                if (_friendlyArmyRefreshElapsed >= 1f)
                {
                    _friendlyArmyRefreshElapsed = 0f;
                    _dataSource.RefreshStrategicFriendlyArmies();
                }
            }
            else
            {
                _friendlyArmyRefreshElapsed = 0f;
            }

            int ownershipRevision = CalendarWorldLedgerBehavior.OwnershipRevision;
            if (ownershipRevision == _lastOwnershipRevision) return;

            _lastOwnershipRevision = ownershipRevision;
            _dataSource.RefreshWorldState();
        }

        private void Close()
        {
            if (_active != this) return;

            try
            {
                // Capture the latest ownership, tracking, calendar, and event
                // state before the overlay is discarded. A newly opened
                // overlay creates a fresh VM and refreshes it again.
                _dataSource.RefreshWorldState();
                Diagnostics.Info("World Events refreshed and closed.");
            }
            catch (Exception exception)
            {
                Diagnostics.Error("World Events close-time refresh failed; the overlay will still close.", exception);
            }
            finally
            {
                ScreenManager.RemoveGlobalLayer(this);
                _layer.InputRestrictions.ResetInputRestrictions();
                _dataSource.OnFinalize();
                _active = null;
            }
        }
    }
}
