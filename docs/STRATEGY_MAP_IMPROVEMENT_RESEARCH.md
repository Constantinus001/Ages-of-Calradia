# Strategy-map improvement research

The existing strategy-map work already uses one composed atlas, deterministic
settlement marker spacing, and a gated mouse-wheel zoom. The remaining audit
found three display problems and they are now implemented with original code.

## Applied fixes

- [Uniform aspect-ratio scaling](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarWorldLedgerVM.cs:254>) fits the 1730x1720 source map to the viewport using one scale on both axes. The old independent X/Y scales stretched the map horizontally.
- [Selection feedback](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/GUI/Prefabs/WorldCalendar/WorldCalendar.xml:439>) draws a thin bound border around the clicked marker. The selection state was already present in the view model but had no visible child in the active marker template.
- [Collision-aware town labels](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarStrategicMapTextureProvider.cs:797>) prioritizes shorter names and tries four deterministic positions. Labels that would overlap another label or leave the map are skipped instead of producing unreadable text piles.

The composed texture remains the source of territory, settlement symbols, and
siege badges. The marker buttons remain hit targets only; they do not repaint
the map or alter settlement positions.

## Deferred intentionally

The province ownership index and static owner bindings need a separate asset
audit before changing. The current renderer is stable and changing its source
index would risk miscoloring the 133 audited regions. No code or art was copied
from other mods or from decompiled projects.
