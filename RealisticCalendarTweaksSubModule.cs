namespace RealisticCalendarTweaks
{
    /// <summary>
    /// New public entry point for the renamed module. The implementation stays
    /// in the legacy namespace to keep the runtime data layout stable while the
    /// module's public identity uses the new name.
    /// </summary>
    public sealed class MySubModule : TwelveMonthCalendar.MySubModule
    {
    }
}
