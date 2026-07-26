# RILL — Status

**Last updated:** 2026-07-26
**Engine:** Unity 6000.5.5f1, Built-in Render Pipeline
**Verified:** C# type-checks clean (all three build configs). Headless simulation runs and produces
sane numbers. **Never playtested by a human beyond one look at the Game view.**

Read this with [TUNING.md](TUNING.md) (the numbers and the measurements behind them) and
[VERIFICATION.md](VERIFICATION.md) (how anything here was actually checked).

---

## The honest one-paragraph summary

Every system in the design document exists in code and compiles. The core loop —
flow, carve, remember — demonstrably works: water runs 100–250 m down a mountain, cuts
channels that measurably deepen run over run, fills basins that report their fill percentage,
and the whole world round-trips to disk exactly. What has *not* happened is the thing the design
document itself calls the kill criterion: nobody has played it and wanted one more run. The
visual presentation is a prototype, not the "geology as papercraft" the document describes, and
several progression tracks are technically alive but tuned so weakly they do not yet register as
progression.

---

## Done

### Core simulation — working, measured

| System | File | State |
|---|---|---|
| Persistent heightfield (the save file) | `Core/HeightField.cs` | Height, polish, water, wetness, hardness, virgin, dye, ice. All persist. |
| Flow simulation (the three rules) | `Flow/FlowSimulation.cs` | Gravity along bilinear gradient, momentum, sediment capacity, erosion/deposition, infiltration. Fixed sub-steps so runs are reproducible. |
| One-thumb steering | `Input/ThumbInput.cs` | Lateral pull scaled by thumb distance, paid for in speed. Touch and mouse paths both compiled and checked. |
| Momentum economy | `App/GameConfig.cs` | Drag lerps fresh → polished. Terminal speed ~9 m/s on fresh rock, ~24 m/s in a carved channel. The 2.6× gap is the game. |
| Basin analysis | `Core/BasinSystem.cs` | Priority-flood depression detection, real capacity, real spill point, overflow event. |
| Terrain generation | `Core/MountainGenerator.cs` | Domain-warped ridged noise, 60k-droplet erosion pre-pass, strata-keyed terracing, deliberate tarns, spawn notch. |
| Hydraulic erosion pre-pass | `Core/HydraulicErosion.cs` | Standard droplet algorithm. This is what makes the terrain read as a mountain instead of noise. |
| Carve report | `App/RillWorld.cs` → `Flow/CarveReport.cs` | Diffs the field against run start. Never comes back empty-handed. |
| Save / load | `Meta/SaveSystem.cs` | Gzipped binary, format v4, atomic-ish replace. **Verified byte-exact round-trip in the smoke test.** |
| Between-run drift | `App/RillWorld.cs` | Unused channels silt closed, ground dries, polish dulls. |

### Systems built and compiling, but **not** proven in play

| System | File | Caveat |
|---|---|---|
| Ecosystem tiers | `World/EcosystemSystem.cs` | Moss → village, moisture-driven, instanced props. Growth curve never observed over a real play span. |
| Revelation / secrets | `World/RevelationSystem.cs` | Works, but see *Known problems* — nothing gets found. |
| Weather calendar | `World/WeatherSystem.cs` | Deterministic from UTC date. Never observed changing anything. |
| Biome verbs | `World/BiomeRules.cs` | Glacier freeze/melt, Volcanic land creation + obsidian, Granite low healing. Only Sandstone has ever been run. |
| Collectibles | `World/Collectibles.cs` | Seeds, dye flowers, momentum gates. Gate speed thresholds unvalidated. |
| Projects | `Meta/ProjectSystem.cs` | Surfaces goals from world state onto the idle line. |
| Almanac | `Meta/Almanac.cs` | Auto journal, run log, day streak. |
| Time-lapse | `Meta/TimeLapse.cs` | 128² 16-bit keyframes, ~32 KB each, own playback field. Playback never watched. |
| Daily Rill + glyph | `Meta/DailyRill.cs`, `Meta/GlyphGenerator.cs` | Same seed worldwide, emoji-block share string to clipboard. Glyph rendered in smoke test — currently nearly empty, see below. |
| Confluence queue | `Meta/ConfluenceQueue.cs` | Sparse per-run deltas queued locally. No backend by design. |
| Procedural audio | `Audio/FlowAudio.cs` | Water synthesised from run state, no audio files. **Never heard.** |
| Haptics | `App/Haptics.cs` | Event-only, throttled. |
| UI | `UI/HudController.cs`, `UI/UIFactory.cs` | Built in code, no prefabs. Placeholder quality. |

### Tooling

- **`tools/unity-stub/typecheck.sh`** — type-checks the entire codebase in ~2 s without Unity, across
  all three build configurations. Catches the majority of iteration errors without the editor.
- **`RILL → Run Headless Smoke Test`** — generates a mountain, plays 24 unattended runs, reports
  endings, sediment, distances, basin lattice, secrets, save round-trip and the Daily glyph.
  This is the single most valuable thing in the repo: every real bug so far was found by it.
- **`RILL → Preview Mountain Seed`** — writes a top-down PNG of a random seed.
- **`RILL → Build Scene`**, **`Open Save Folder`**, **`Delete Saved Mountain`**.

---

## Known problems

Ranked by how much they threaten the design.

### 1. Nobody has played it — the kill criterion is untested
The design document sets an explicit M3 kill criterion: if a 20-person playtest does not produce
unprompted "one more run" behaviour, redesign before art exists. Nothing in this repo speaks to
that. Every number below is a proxy.

### 2. The last balance change is unverified
Basins swung from too weak (held 0 m³, nothing ever pooled) to too greedy (24/24 runs pooled,
nothing reached the sea). The correction — 5 smaller tarns, through-flow at 50% fill instead of
75% — is **written but never measured**, because the editor held the project lock when the batch
run was attempted. First thing to do: run the smoke test.

### 3. Secrets are effectively unfindable
`secrets revealed 0 of 60` after 24 runs. A channel polishes ~1.6% of the field, so the chance any
of 60 buried sites sits under it is roughly one per 24 runs at best. The "Revelation" progression
track is therefore invisible over any realistic session. Needs either far more sites, shallower
burial, wider reveal tolerance, or placement locked to the drainage network rather than biased
toward it.

### 4. Visual presentation is not the design
The document promises legible sediment bands where "every metre of depth is legible as colour".
Per-pixel strata and concavity occlusion are now in, but unseen since. Still missing: lake
shorelines (flat discs with hard edges), sea treatment (a blue plane), any sense of speed, and
props that are literally cones and discs.

### 5. Progression tracks are alive but faint
Ecosystem, revelation and volume all technically advance, none at a rate that would register
across a real session. Life tiers in particular have never been observed reaching birds or deer.

### 6. The Daily glyph is nearly empty
Rendered from 24 runs it produced a handful of marks on a 7×7 grid. The viral share unit needs to
look like something at a glance.

---

## Remaining work

### Immediate — decides whether this is a game
1. **Run the smoke test** and confirm the basin retune. Target: a mix of endings, several basins at
   different fill levels, and water reaching the sea more often as channels deepen.
2. **Fix secret discovery rate** so the revelation track is felt within a session.
3. **Playtest by hand.** 20 runs, watching for whether the carve → speed → reach loop compels.
   Everything else is secondary to this.

### Presentation
4. Lake shorelines, depth gradient, foam; a real sea.
5. Speed readability — FOV kick, spray, screen shake on plunges.
6. Persistent wet-channel darkening so the river's history is visible when it is dry.
7. Prop silhouettes worth looking at.
8. UI pass — legacy `Text` and hand-placed rects throughout.
9. Onboarding: the first 30 seconds currently explain nothing.

### Systems not yet started
10. Cascade / dam-break spectacle — the code path exists, the moment does not.
11. Region streaming (currently one 256² field, 512 m across).
12. Confluence backend.
13. Visits and paper boats (asynchronous social).
14. Biome tuning for Glacier / Volcanic / Granite — implemented, never balanced.
15. GPU compute path for erosion — CPU only, confined to a brush along the stream head.
16. Monetisation surfaces — none built.

### Engineering debt
17. No automated test suite beyond the smoke test; no CI.
18. `PooledWaterMesh` rebuilds the full 256² grid on every change.
19. Ecosystem and revelation re-create materials on every world rebind (small leak per Daily toggle).
20. Time-lapse playback allocates a second terrain builder that is never released.
21. Terrain mesh rebuild budget is fixed at 3 chunks/frame and has never been profiled on a device.
22. Nothing has ever run on a phone.
