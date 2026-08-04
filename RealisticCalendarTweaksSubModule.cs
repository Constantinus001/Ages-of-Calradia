namespace RealisticCalendarTweaks
{
    /// <summary>
    /// New public entry point for the renamed module. The implementation stays
    /// in the legacy namespace so Bannerlord can still deserialize older
    /// calendar profile types during the one-time save migration.
    /// </summary>
    public sealed class MySubModule : TwelveMonthCalendar.MySubModule
    {
    }
}
