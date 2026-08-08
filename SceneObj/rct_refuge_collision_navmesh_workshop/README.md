# Refuge Collision and Navmesh Workshop

This is an editor-only copy of `rct_refuge_fort`, created so collision and
navigation work do not modify the original authored layout.

- Open `rct_refuge_collision_navmesh_workshop` in the Bannerlord Scene Editor.
- Keep the palisade platform sides facing into the compound.
- Add or correct collision first, then bake a fresh navmesh.
- Do not copy a `navmesh.bin` from another scene; save this scene in the editor
  after baking so it writes its own `navmesh.bin`.
- The copied ground planes and water are test-workspace content, not a final
  runtime terrain. Replace them with real terrain before using this scene in
  game.
