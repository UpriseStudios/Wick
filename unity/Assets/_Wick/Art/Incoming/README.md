# Art/Incoming

Finished assets from the Asset Production pack land here, one folder per asset:

```
Incoming/<category>/<asset_id>/<asset_id>.fbx
Incoming/<category>/<asset_id>/<asset_id>_albedo.png   (_mask, _emissive)
Incoming/<category>/<asset_id>/<asset_id>@<clip>.fbx   (optional: one clip per file)
Incoming/sfx/<asset_id>/<asset_id>.wav
```

Categories: char, trap, guard, tile, prop, vfx, ui, sfx, music. Asset ids follow
`<category>_<name>_<variant>` in lower case, for example `char_wick_default` or `trap_drip_pipe`.
Working files go in a `source/` or `previews/` subfolder, which the checks skip.

Every import is checked against docs/asset-production.md: triangle and texture budgets, one material
(plus the flame), pivot at the base centre, scale (Wick 0.6 m, one tile 1 m), the shared Wick rig and
face blend shapes, clip names, mono SFX. Violations are reported, never fixed: see
Wick > Art > Check Incoming Assets, or Logs/art-import-report.md.
