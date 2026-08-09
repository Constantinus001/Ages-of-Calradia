# Strategy-map improvement research

The existing strategy-map work already uses one composed atlas, deterministic
settlement marker spacing, and a gated mouse-wheel zoom. The remaining audit
found four display problems and they are now implemented with original code.

## Applied fixes

- [Uniform aspect-ratio scaling](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/Ages Of Calradia/CalendarWorldLedgerVM.cs:254>) fits the 1730x1720 source map to the viewport using one scale on both axes. The old independent X/Y scales stretched the map horizontally.
- [Selection feedback](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/Ages Of Calradia/GUI/Prefabs/WorldCalendar/WorldCalendar.xml:439>) now draws a layered gold glow around the clicked marker. The selection state drives both the glow layer and the crisp inner border.
- [Campaign borders](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/Ages Of Calradia/CampaignKingdomBorderBehavior.cs:1>) use live settlement Voronoi cells, padded campaign-map bounds, terrain-height sampling, and `vertex_color_mat` runtime meshes. This follows the publicly observable technique used by Artem's Better UI Visuals mod ([reference page](https://www.patreon.com/ArtemOfficial/posts/155991034)) without depending on its DLL or copying its code.
- The campaign renderer is fully self-contained. Artem's DLL is used only as a reference for the technique; this module does not depend on it or delegate rendering to it. Disable Artem's campaign-border feature while testing this implementation to avoid duplicate meshes.
- [Collision-aware town labels](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarStrategicMapTextureProvider.cs:797>) prioritizes shorter names and tries four deterministic positions. Labels that would overlap another label or leave the map are skipped instead of producing unreadable text piles.

The composed texture remains the source of territory, settlement symbols, and
siege badges. The marker buttons remain hit targets only; they do not alter
settlement positions. The static province border artwork is enabled above the
composed strategic-map texture, while the selected marker glow is drawn by
the live Gauntlet marker layer.

## Deferred intentionally

The province ownership index and static owner bindings need a separate asset
audit before changing. The current renderer is stable and changing its source
index would risk miscoloring the 133 audited regions. The campaign-border
behavior is deliberately independent of that strategic-map index.
