# Blender toolkit

Scripts that build Wick's game models headless in Blender 4.5 LTS, to the Asset Production pack's specs.

```
blender -b --factory-startup --python tools/blender/build_skins.py -- [asset_id ...] [--no-previews]
```

With no ids, every skin is built: `char_wick_default`, `char_drippy`, `char_cinder`, `char_pip`, `char_twist`, `char_ember`, and the
`char_twist_gilded` repaint. Each lands in `unity/Assets/_Wick/Art/Incoming/char/<id>/` (FBX, albedo atlas, mask, SOURCES.md). Then run
`Wick > Rebuild Placeholder Content` in Unity, and the import checks and skin tests take it from there. Previews (front, three-quarter, side,
64 px silhouette) go to `tools/blender/previews/<id>/`, which git ignores.

What the scripts guarantee, so Unity's checks pass:
- 1 unit = 1 m, built facing -Y and turned half a turn on export, so Unity sees +Z forward with the left eye on -X.
- The shared rig: `root, body, head, arm.L/R, foot.L/R, flame_base/mid/tip, eye.L/R` under an object named `Armature`, rigid per-part weights.
- One body mesh (`body_mesh`) with the face shape keys `fear, greed, smug, wince, blink`; the flame is its own mesh (`flame`, UV v base to tip).
- One material: every part is UV-mapped into a swatch of a painted 1024 px palette atlas (top-down gradient painted in).
- Clips (`char_wick_default` only): the twelve from the handoff, rotations and scales, with only the body bone moving.
- Every model stays within 5,000 triangles; the build prints each count.

These are blockouts from the locked references: shapes, proportions, colours and faces, not the hero renders' glossy detail.
An artist (or image-to-3D plus cleanup) can replace any of them; keep the names above and the checks stay green.
