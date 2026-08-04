using System.Collections.Generic;
using Bannerlord.UIExtenderEx;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TwelveMonthCalendar.BetterTimeUi
{
    /// <summary>
    /// Optional Better Time integration. This assembly is loaded only after
    /// Better Time and UIExtenderEx are confirmed present, so neither is a
    /// requirement for the calendar itself.
    /// </summary>
    public static class BetterTimeUiAdapter
    {
        public static void Initialize()
        {
            UIExtender extender = UIExtender.Create("TwelveMonthCalendar");
            extender.Register(typeof(BetterTimeUiAdapter).Assembly);
            extender.Enable();
        }
    }

    /// <summary>
    /// Reserve a gap before Better Time's sundial while keeping the longest
    /// Gregorian season/date string legible.
    /// </summary>
    [PrefabExtension("MapBar", "descendant::TimePanel/Children/TextWidget")]
    public sealed class BetterTimeCalendarDateFontPatch : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => new List<Attribute>
        {
            new Attribute("SuggestedWidth", "230"),
            new Attribute("PositionXOffset", "-30"),
            new Attribute("PositionYOffset", "-2"),
            new Attribute("Brush.FontSize", "17")
        };
    }

    /// <summary>Displays the season separately in Better Time's injected bar.</summary>
    [PrefabExtension("MapBar", "descendant::TimePanel/Children/TextWidget")]
    public sealed class BetterTimeCalendarSeasonPatch : PrefabExtensionInsertPatch
    {
        public override InsertType Type => (InsertType)0;

        [PrefabExtensionText(false)]
        public string Text => "<TextWidget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"105\" SuggestedHeight=\"22\" HorizontalAlignment=\"Left\" PositionXOffset=\"95\" VerticalAlignment=\"Bottom\" PositionYOffset=\"-4\" Brush=\"MapTextBrushGal\" Brush.FontSize=\"16\" Brush.FontColor=\"#FFF2D0FF\" Brush.TextHorizontalAlignment=\"Center\" Text=\"@Season\" />";
    }
}
