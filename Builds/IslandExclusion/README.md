# 13:52 base with exact rendered-island exclusion

This sidecar is paired only with `Deploy-20260810-135246-separate-islands-filled-lakes`.
It leaves that August 10 13:52 assembly unchanged and post-processes only its
exact political-land result. Decorative terrain islands have no land navigation
faces, so this version samples the renderer's own classifier at 0.75 campaign-map
units and selects every small, complete disconnected component in the target
southwestern sea and the circled northern island-chain region.

The two requested enclosed lakes are excluded from political fill. Their exact
shorelines come from the preserved August 9 6:59 PM
`campaign_political_land_mask.png`, applied only after tight,
projection-derived geographic bounds prevent nearby mainland from being cut.

Runtime diagnostics report method availability, the first native-water match in
each lake, and periodic totals for lake-region probes, water matches, forced fill
changes, already-filled faces, probe failures, and reflection exceptions.

Components touching the sampling boundary or exceeding the island-size cap are
rejected as mainland. No external Kingdom Frontiers code is used. The preserved
August 9 mask affects only the two lake windows; territory fill and
kingdom-frontier construction both consume the patched exact classifier.
