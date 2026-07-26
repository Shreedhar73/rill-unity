# RILL — Unity implementation

*"Water flows downhill. Water remembers."*

A working implementation of the RILL design document: one-thumb steering, persistent hydraulic
erosion, basins that fill across runs, an ecosystem that arrives where water persists, buried
secrets you can only uncover by routing water over them, an automatic Almanac, time-lapse, and the
Daily Rill with its shareable glyph.

Everything is offline. There is no account, no server, no energy system, and nothing purchasable
touches the terrain.

**It is a prototype, not a finished game.** The core loop is built and measured; it has never been
playtested, and the presentation is placeholder. See [docs/STATUS.md](docs/STATUS.md).

---

## Opening the project

1. Unity Hub → **Add** → **Add project from disk** → select `rill-unity/`.
2. Editor version: **6000.5.5f1** (Unity 6.5). Built-in Render Pipeline, no packages beyond uGUI.
3. Open `Assets/Scenes/Rill.unity` and press Play.
   If the scene fails to open for any reason, use the menu **RILL → Build Scene** — the scene
   contains exactly one GameObject with `GameBootstrap` on it, and that menu item recreates it.

Everything else — camera, sun, terrain meshes, water, UI, audio — is constructed at runtime by
`GameBootstrap`. There are no prefabs and no imported assets of any kind.

## Playing

| Input | Action |
|---|---|
| Tap (idle) | Release the water |
| Hold and lean (during a run) | Lateral pull on the stream head. Close to the water = fine control, far = hard lean |
| Release | Let the water choose |
| Drag (idle) | Pan the mountain |
| Almanac / Time-lapse / Daily Rill / Share | Bottom buttons, idle only |

Steering costs momentum. Riding your own polished channels is free speed. Expert play is knowing
when *not* to touch.

## Where things live

```
Assets/Scripts/
  Core/     HeightField (the save file), Noise, MountainGenerator, HydraulicErosion,
            BasinSystem, StrataPalette, SecretSite, MinHeap
  Flow/     FlowSimulation (the three rules), RunController (the loop), CarveReport
  Input/    ThumbInput — the entire control scheme
  Render/   TerrainMeshBuilder (strata + occlusion), WaterRibbon, PooledWaterMesh, RillCamera, SplashFX
  World/    EcosystemSystem, RevelationSystem, WeatherSystem, BiomeRules, Collectibles, PropMeshes
  Meta/     SaveSystem, Almanac, TimeLapse, DailyRill, GlyphGenerator, ProjectSystem, ConfluenceQueue
  UI/       HudController, UIFactory (UGUI built in code)
  Audio/    FlowAudio — water synthesised from the run's own state; no audio files
  App/      GameBootstrap, RillWorld, GameConfig (every tuning number), Haptics
Assets/Resources/Shaders/   Strata, WaterRibbon, PooledWater, Prop, Droplet (Built-in RP, hand-written HLSL)
Assets/Editor/              RillEditorTools (menus), RillSmokeTest (headless 24-run check)
tools/unity-stub/           Unity API stubs + typecheck.sh — type-checks everything in ~2 s
```

## Documentation

**[`OPEN-LOOPS.md`](OPEN-LOOPS.md) drives implementation** — current work in priority order, what
was closed recently, and the evidence that closed it. Read it before starting anything.
[`CLAUDE.md`](CLAUDE.md) holds the working protocol.

Reference docs live in [`docs/`](docs/README.md):

| Document | Read it when |
|---|---|
| [docs/STATUS.md](docs/STATUS.md) | You want project state and what to do next. |
| [docs/FEATURES.md](docs/FEATURES.md) | Per-feature checklist: done / built / partial / not started. |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | You are about to change code. |
| [docs/TUNING.md](docs/TUNING.md) | Runs feel wrong, or you are balancing anything. |
| [docs/VERIFICATION.md](docs/VERIFICATION.md) | Before you claim something works. |

## How the design maps to the code

| Design document | Implementation |
|---|---|
| Rule 1 — water flows downhill | `FlowSimulation.Step` — gravity along the bilinear gradient of `HeightField.Height` |
| Rule 2 — flowing water carves, carved paths attract water | `HeightField.AddBrush` on `Height` (erosion/deposition, sediment-capacity model) and on `Polish`, which lowers drag |
| Rule 3 — nothing ever resets | `SaveSystem` persists the arrays; nothing anywhere clears them |
| Momentum economics | `GameConfig.DragFresh` → `DragPolished`, plus `SteerSpeedCost` on every steering input |
| Basins fill, then overflow | `BasinSystem` — priority-flood depression analysis, real capacity, real spill point; overflow raises a cascade run the player watches |
| Emergence (oxbows, deltas, waterfalls, capture) | Not scripted. Falls out of sediment capacity + deposition + the persistent field |
| Carve report | `RillWorld.EndRun` diffs the field against its state at run start; `TerrainMeshBuilder.ShowCarveOverlay` glows the change on the mountain itself |
| Revelation | `SecretSite.RevealElevation`; you cannot dig, only lower the ground |
| Life arrives where water persists | `EcosystemSystem` — moisture-driven tiers, instanced procedural props |
| Seeds, dye-flowers, momentum gates | `World/Collectibles.cs` — placed deterministically per run; gates prefer your own fast channels, so a deep river is where the game starts putting things worth catching |
| Dye is permanent | `HeightField.Dye` — splashed colour is saved with the terrain and blended into the strata |
| Projects (surfaced, never assigned) | `Meta/ProjectSystem.cs` — read off basin fills, near-surface secrets, ecosystem rungs; shown as the idle line |
| Biome verbs | `World/BiomeRules.cs` — Glacier freeze/melt (ice makes rock ~3.5× harder until a thaw hands it back), Volcanic vents that *create* land and make obsidian where water quenches lava, Granite that barely heals |
| Haptics on real moments | `App/Haptics.cs` — events only, throttled; the game is complete with it off |
| Sediment healing | `RillWorld.ApplyBetweenRunDrift` — unused channels silt closed slowly, so topology can't ossify |
| Weather as invitation | `WeatherSystem` — deterministic from the UTC date, so a storm is the same event for everyone |
| Rain gathered while away | `GameBootstrap.RainGatheredWhileAway` — bonus volume, never required, never expiring |
| The Almanac | `Almanac` — automatic journal with dates and run numbers |
| Time-lapse | `TimeLapseArchive` — 128² 16-bit keyframes every few runs, ~32 KB each |
| Daily Rill + share glyph | `DailyRill` + `GlyphGenerator` — same seed worldwide, emoji-block signature to the clipboard |
| Postcards | Share button also writes a screenshot next to the save |
| The Confluence | `ConfluenceQueue` — sparse per-run deltas queued locally; upload endpoint deliberately absent (offline-first) |

## Save data

`Application.persistentDataPath/rill/`

- `world_0.rill` — gzipped terrain arrays, secrets, lifetime record (a mature mountain is a few MB)
- `almanac_0.json`, `timelapse_0.bin`, `daily.json`, `confluence_queue_0.bin`, `postcard_run*.png`

**RILL → Open Save Folder** reveals it. **RILL → Delete Saved Mountain** wipes slot 0 (with a
confirmation, because that world only exists because someone played it).

## Tuning

Select the `RILL` object in the scene; every constant is on `GameConfig` in the inspector, grouped
in the order the design argues for them. The three that decide whether the game is a game:

- `DragFresh` vs `DragPolished` — the size of the reward for having carved
- `SteerSpeedCost` — the price of fighting gravity, i.e. the skill ceiling
- `CarveRate` — how fast a mountain becomes yours

`ResetWorldOnPlay` wipes the save on entering Play mode. Development only.

## Status in one line

The core loop works and is measured; the game has never been playtested, and several progression
tracks are alive but too faint to feel. See [docs/STATUS.md](docs/STATUS.md) for the full picture
and [docs/FEATURES.md](docs/FEATURES.md) for the per-feature breakdown.
