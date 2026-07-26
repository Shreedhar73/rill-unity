# RILL — Feature Checklist

Every feature named in `RILL-game-design.md`, with its real state.

**Legend**
- **Done** — built, type-checked, and exercised by the headless smoke test or directly observed.
- **Built** — code complete and compiling, but never observed working. Assume bugs.
- **Partial** — some of it works, named gap.
- **Not started** — no code.

---

## Core mechanic

| Feature | State | Notes |
|---|---|---|
| Rule 1 — water flows downhill | **Done** | Gravity along the bilinear gradient, `sin θ` form. Measured: 100–250 m runs. |
| Rule 2 — flowing water carves; carved paths attract water | **Done** | Erosion/deposition brush + polish field lowering drag. Measured: ~90 m³ moved/run, −5 m cumulative cut. |
| Rule 3 — nothing ever resets | **Done** | Save round-trip verified byte-exact. |
| One-thumb lateral steering | **Done** | Both touch and mouse paths compiled and checked. Feel never validated by hand. |
| Steering costs momentum (restraint as skill ceiling) | **Done** | `SteerSpeedCost` bleeds speed proportional to lean. Balance unvalidated. |
| Momentum economics (fresh vs polished) | **Done** | ~9 m/s fresh, ~24 m/s polished. |
| Speed gates / crest ridges at speed | **Done** | Falls out of physics; momentum gates also exist as pickups. |
| Emergent hydrology (oxbows, deltas, capture) | **Partial** | Physics supports it; never observed over enough runs to confirm it emerges. |
| Waterfall / plunge-pool detection | **Built** | Fires splash events and deepens the pool. Never seen. |

## Progression

| Feature | State | Notes |
|---|---|---|
| Depth track (channels deepen) | **Done** | Measured run over run. |
| Volume track (basins fill, overflow) | **Partial** | Basins fill and report percentages (`South basin 1% → 14%`). Overflow → cascade code path exists but has never fired in a measured run. |
| Life track (moss → village) | **Built** | Tier logic and instanced props exist. Never observed past the first tier. |
| Revelation track (buried secrets) | **Partial** | Works mechanically; **0 of 60 found in 24 runs**, so the track is invisible in practice. |
| No XP / levels / currencies | **Done** | None exist anywhere in the codebase. |
| Progression screen *is* the world | **Done** | Camera pull-back; no stats screen was ever built. |
| Biomes — Sandstone | **Done** | The only one ever run. |
| Biomes — Granite / Glacier / Volcanic | **Built** | Palettes, hardness, freeze/melt, land creation, obsidian. Never balanced or seen. |

## Session & world

| Feature | State | Notes |
|---|---|---|
| 20-second run, ends with carve report | **Done** | Runs currently 12–40 s. |
| Carve report proves the run mattered | **Done** | Diffed from the field; never empty. |
| Carve overlay glowing on the mountain | **Built** | Vertex-alpha glow with decay. Not seen since the shader rewrite. |
| Zero-input session (watch rain, close) | **Partial** | Idle state exists; no rain visuals. |
| Rain gathers while away | **Done** | Bonus volume on next run, capped, never expiring. |
| Weather calendar (storm/drought/snowmelt/meteor) | **Built** | Deterministic from UTC date. Never observed. |
| Sediment healing between runs | **Done** | Unused channels silt closed; wetness and polish decay. |
| No energy system, no timers | **Done** | None exist. |

## Meta

| Feature | State | Notes |
|---|---|---|
| Projects (surfaced, never assigned) | **Built** | Read off basins, near-surface secrets, ecosystem rungs; shown on the idle line. |
| The Almanac | **Built** | Auto journal, run history, day streak, milestones. |
| Time-lapse | **Built** | Keyframes written every 3 runs; playback path never watched. |
| Daily Rill (same seed worldwide, 7 runs) | **Built** | Separate world, own save file, run limit. |
| Share glyph | **Partial** | Renders and copies to clipboard, but comes out nearly empty — needs to read as an image at a glance. |
| Postcards | **Built** | Share button also writes a screenshot next to the save. |
| Almanac subscription / cloud archive | **Not started** | Monetisation surface. |

## Social

| Feature | State | Notes |
|---|---|---|
| Confluence delta queue | **Built** | Sparse per-run deltas queued locally, capped at 8 MB. |
| Confluence backend / merge | **Not started** | Deliberately out of scope — the game is offline-first. |
| Visits, paper boats | **Not started** | |
| Seed browser / creator seeds | **Not started** | |
| Marketplace | **Not started** | Year-2 item in the design. |

## Presentation

| Feature | State | Notes |
|---|---|---|
| Strata legible as colour | **Partial** | Per-pixel bands + seams + concavity occlusion written; **not visually confirmed since the rewrite**. |
| Terrain silhouette (ridges, valleys, cliffs) | **Done** | Droplet erosion pre-pass + terracing. Summit 146 m over a 512 m base. |
| Water ribbon as hero element | **Partial** | Widened and lifted; still not confirmed to read as the brightest thing in frame. |
| Lakes | **Partial** | Render as flat discs with hard shorelines. No depth gradient, no foam. |
| The sea | **Partial** | A single blue plane. No shoreline treatment. |
| Ecosystem props | **Partial** | Procedural cones/discs/blades, density scaled by life. Silhouettes are placeholder. |
| Splash particles | **Built** | Code-built system + `Droplet.shader`. Never seen. |
| Camera (follow, report framing, idle pan) | **Done** | Retuned to 62 m back / 46 m up. |
| Sense of speed | **Not started** | No FOV kick, no spray, no shake. |
| Procedural water audio | **Built** | Synthesised from run state, no audio files. **Never heard.** |
| Haptics | **Built** | Event-only, throttled, platform-guarded. |
| No-HUD-by-default framing | **Partial** | HUD ghosts in, but is placeholder quality. |
| Onboarding | **Not started** | First 30 seconds explain nothing. |

## Technical

| Feature | State | Notes |
|---|---|---|
| Offline, no accounts | **Done** | No network code anywhere. |
| Save format, cheap and durable | **Done** | Gzipped binary v4, atomic-ish replace, verified round-trip. |
| Runtime-built scene (no prefabs) | **Done** | One GameObject; everything else constructed in code. |
| Deterministic generation | **Done** | Own PRNG and noise; never `UnityEngine.Random` for content. |
| 60 fps on 2021 mid-range Android | **Not started** | Never run on a device. No profiling of any kind. |
| GPU compute erosion | **Not started** | CPU only, confined to a brush along the stream head. |
| Region streaming | **Not started** | One 256² field, 512 m across. |
| Automated tests / CI | **Partial** | Headless smoke test + stub type-check script. No CI, no unit tests. |
