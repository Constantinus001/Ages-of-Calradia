using System;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Fixed construction sockets in the refuge footprint.  These are flags
    /// rather than separate save objects so the feature remains resilient to
    /// save/load and never needs to serialize scene entities.
    /// </summary>
    [Flags]
    internal enum RefugeUpgrade
    {
        None = 0,
        Barracks = 1 << 0,
        Tavern = 1 << 1,
        StaffTents = 1 << 2,
        SleepingQuarters = 1 << 3,
        Blacksmith = 1 << 4,
        Stash = 1 << 5,
        GuardTowers = 1 << 6
    }
}
