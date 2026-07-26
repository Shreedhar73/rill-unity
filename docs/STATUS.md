# RILL — Status

**Last updated:** 2026-07-26
**Engine:** Unity 6000.5.5f1, Built-in Render Pipeline
**Verified:** C# type-checks clean (all three build configs). Headless simulation runs 24 and 150
unattended runs and produces sane numbers, plus dedicated harnesses for steering authority, basin
soak, a sustained basin campaign, and glacier freeze/thaw. **Never playtested by a human beyond one
look at the Game view.**

Read this with [TUNING.md](TUNING.md) (the numbers and the measurements behind them) and
[VERIFICATION.md](VERIFICATION.md) (how anything here was actually checked).

---

## The honest one-paragraph summary

Every system in the design document exists in code and compiles, and as of 2026-07-26 the core
loop is not merely alive but strong. Water runs an average of **253 m** down a mountain that offers
141 m of descent and uses **127 m** of it; **82 runs in 150 reach the sea**; four of five basins
fill to 93–100%; and no run in 150 fails to end. Those numbers are 1.5× to 2× what they were the
same morning, and the changes behind them were physics the simulation was missing rather than
constants nudged by taste — hollows with no fill-and-spill, a thumb that could push water uphill,
basins that did not absorb a stream passing over them, and four fifths of the basin lattice sitting
off the spring's drainage entirely.

What has *not* happened is the thing the design document calls the kill criterion: **nobody has
played it and wanted one more run.** Every number here is a proxy, and this project's own history is
that proxies mislead — five separate "the simulation is broken" conclusions turned out to be flaws
in the test harness. The visual presentation has moved from placeholder toward the "geology as
papercraft" the document describes, but almost none of it has been looked at by a person.

---

## Done

### Core simulation — working, measured

| System | File | State |
|---|---|---|
| Persistent heightfield (the save file) | `Core/HeightField.cs` | Height, polish, water, wetness, hardness, virgin, dye, ice. All persist. |
| Flow simulation (the three rules) | `Flow/FlowSimulation.cs` | Gravity along bilinear gradient, momentum, sediment capacity, erosion/deposition, infiltration. Fixed sub-steps so runs are reproducible. Fill-and-spill for hollows too small for the basin lattice to name, so a run can no longer stall without ending. |
| One-thumb steering | `Input/ThumbInput.cs` | Lateral pull scaled by thumb distance, paid for in speed. Authority is bought with momentum, and whatever part of a lean points up the fall line is discarded — the thumb steers across the mountain, never up it. Touch and mouse paths both compiled and checked. |
| Momentum economy | `App/GameConfig.cs` | Drag lerps fresh → polished. Terminal speed ~9 m/s on fresh rock, ~24 m/s in a carved channel. The 2.6× gap is the game. |
| Basin analysis | `Core/BasinSystem.cs` | Priority-flood depression detection, real capacity, real spill point, overflow event. |
| Terrain generation | `Core/MountainGenerator.cs` | Domain-warped ridged noise, 60k-droplet erosion pre-pass, strata-keyed terracing, deliberate tarns cut only on ground the spring's water can reach, spawn notch. |
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
  endings, sediment, distances, basin lattice, secrets, save round-trip and the Daily glyph. It also
  reports *why* runs ended (slope, standing water and polish at the stop point, with the terminal
  speed the flow constants imply for that ground), how many hollows were filled, how many dam breaks
  fired, the deposit footprint, whether the sea is reachable downhill from the spring, and — for
  runs that time out — a once-a-second trace of the head. This is the single most valuable thing in
  the repo: every real bug so far was found by it.
- **`RILL → Run Headless Steering Sweep` / `Basin Soak Sweep` / `Basin Campaign` / `Campaign Sweep`**
  — A/B harnesses that play 150 runs per arm on the same seed with one constant varied, so tuning
  numbers come from a table rather than from taste. `Basin Campaign` tests the claim the whole
  progression track rests on: that a player who commits to one off-channel basin can fill it.
- **`RILL → Preview Mountain Seed`** — writes a top-down PNG of a random seed.
- **`RILL → Build Scene`**, **`Open Save Folder`**, **`Delete Saved Mountain`**.

---

## Known problems

Ranked by how much they threaten the design.

### 1. Nobody has played it — the kill criterion is untested
The design document sets an explicit M3 kill criterion: if a 20-person playtest does not produce
unprompted "one more run" behaviour, redesign before art exists. Nothing in this repo speaks to
that. Every number below is a proxy.

### 2. Filling a basin is a campaign, not a run (L-027, L-030, L-040 closed 2026-07-26)
This has been re-measured from the ground up and the earlier readings should not be trusted.

Four of the five basins on the default seed **could not be reached downhill from the spring at
all** — they were scored on concavity anywhere on the mountain, which describes where a lake could
sit rather than where the player can put water. They read as `0%` forever, indistinguishable in
every report from a basin that simply had not been filled yet. Basins are now cut only on ground the
spring's water can reach: `reach (climb 0 m)` went **1 of 5 → 5 of 5**.

With that fixed, a sustained campaign takes an off-channel basin **0% → 100%**, the lattice after
150 runs reads `100% · 93% · 0% · 100% · 100%`, and an aimed run delivers into its target **11 times
in 21** where the target had room to receive anything (31% of all aimed runs, the rest being aimed
at basins that were already full — a real state of the world now that basins fill). Closest approach
fell from 98 m to 53 m.

### 3. Secrets are findable (L-010 closed 2026-07-26)
`secrets revealed 3 of 60` after 24 runs, `12 of 60` after 150. The blocker was not burial depth but
placement: flow accumulation describes drainage across the whole mountain, while every run starts at
one summit spring and converges into a single corridor, so **45 of 51 sites had received no erosion
at all after 150 runs**. Sites are now split half on a summit-traced corridor and half on the wider
network. Revelation tests erosion within 4 m rather than the exact cell — but it must compare
`Virgin - Height`, not elevation: an elevation comparison is satisfied by slope alone and revealed
37 of 51 before anyone played.

### 4. Visual presentation — much is built, almost none of it has been seen
The document promises legible sediment bands where "every metre of depth is legible as colour".
Per-pixel strata, concavity occlusion, lake depth gradients, soft shorelines, a subdivided sea,
aerial perspective and real conifer silhouettes are all in, and the water rendering was confirmed by
screenshot (L-013). Added 2026-07-26 and **unobserved**: spray above 12 m/s, and terrain that draws
the *cut* rather than only the polish — polish decays between runs and the rock does not, so the
oldest work on the mountain used to be the least visible. The occlusion normaliser was also
recalibrated from 4 m to 1.6 m, which is the depth channels here actually reach; against the old
value a real channel darkened by about 7%.

Still placeholder: UI (legacy `Text`, hand-placed rects) and the impact of a plunge.

### 5. Progression tracks are alive but faint
Ecosystem, revelation and volume all technically advance, none at a rate that would register
across a real session. Life tiers in particular have never been observed reaching birds or deer.

### 6. The Daily glyph is nearly empty
Rendered from 24 runs it produced a handful of marks on a 7×7 grid. The viral share unit needs to
look like something at a glance. A Daily is three runs, so the real case is sparser still than
anything the smoke test prints.

### 7. A first session never sees a dam break
Counted for the first time 2026-07-26: **4 overflows and 207 m³ over the lip per 150 runs, and zero
per 24.** The cascade works — this is the first evidence it fires at all outside a headline string —
but the spectacle it exists for is invisible in a first session, and nobody has ever watched one.

---

## Remaining work

### Immediate — decides whether this is a game
1. **Playtest by hand** (L-012). 20 runs, watching for whether the carve → speed → reach loop
   compels. Everything else is secondary to this, and it is now the *only* thing left in
   `OPEN-LOOPS.md` that a terminal cannot advance. Protocol is written at
   [PLAYTEST.md](PLAYTEST.md); run it on a fresh save slot.
2. **Look at the things that were built and never seen** — the title screen, the settling beat
   before the report card, onboarding, spray, the drawn channel cut, a dam break, a time-lapse.
   Seven separate loops are waiting on eyes rather than on work.

### Presentation
3. Prop silhouettes beyond conifers and canopies.
4. UI pass — legacy `Text` and hand-placed rects throughout.
5. Impact on plunges: `Splash` fires on drops over 1.1 m and always has, but nothing was added for
   the *feel* of one.
6. The Daily glyph at three runs.

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
