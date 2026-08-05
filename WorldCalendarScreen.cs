using System;
using TaleWorlds.Engine.GauntletUI;
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
            }
            catch (Exception exception)
            {
                _active = null;
                Diagnostics.Error("World Calendar overlay could not be initialized.", exception);
            }
        }

        protected override void OnTick(float dt)
        {
            base.OnTick(dt);

            int ownershipRevision = CalendarWorldLedgerBehavior.OwnershipRevision;
            if (ownershipRevision == _lastOwnershipRevision) return;

            _lastOwnershipRevision = ownershipRevision;
            _dataSource.RefreshWorldState();
        }

        private void Close()
        {
            if (_active != this) return;

            ScreenManager.RemoveGlobalLayer(this);
            _layer.InputRestrictions.ResetInputRestrictions();
            _dataSource.OnFinalize();
            _active = null;
        }
    }
}
