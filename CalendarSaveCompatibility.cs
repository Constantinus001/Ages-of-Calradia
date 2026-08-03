using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// A persisted module-owned type. Once it is written to a save, Bannerlord
    /// requires this module's save definition to deserialize that save. This
    /// prevents loading a Twelve Month Calendar campaign with the module
    /// removed and silently changing its calendar interpretation.
    /// </summary>
    public sealed class CalendarSaveCompatibilityMarker
    {
        [SaveableField(1)]
        public string ModuleId = "_TwelveMonthCalendar";

        [SaveableField(2)]
        public int SchemaVersion = 1;
    }

    public sealed class TwelveMonthCalendarSaveDefiner : SaveableTypeDefiner
    {
        public TwelveMonthCalendarSaveDefiner()
            : base(485013)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(CalendarSaveCompatibilityMarker), 1);
        }
    }

    internal sealed class CalendarSaveCompatibilityBehavior : CampaignBehaviorBase
    {
        private CalendarSaveCompatibilityMarker _marker = new CalendarSaveCompatibilityMarker();

        public override void RegisterEvents()
        {
        }

        public override void SyncData(IDataStore dataStore)
        {
            bool markerWasPresent = dataStore.SyncData("TwelveMonthCalendar.SaveCompatibilityMarker", ref _marker);
            if (dataStore.IsLoading && !markerWasPresent)
            {
                // Saves made before v1.3 do not yet contain the marker. Allow
                // a one-time migration; the next save will contain it and will
                // then require Twelve Month Calendar to load.
                _marker = new CalendarSaveCompatibilityMarker();
                Diagnostics.Info("Loaded a pre-v1.3 calendar save; it will become module-locked after the next save.");
            }

            if (_marker == null)
            {
                _marker = new CalendarSaveCompatibilityMarker();
            }
        }
    }
}
