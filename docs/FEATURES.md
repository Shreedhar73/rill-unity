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
| Rule 1 — water flows downhill | **Done** | Gravity along the bilinear gradient, `sin θ` form. Measured: avg **253 m/run** over 150 runs, using 127 m of the mountain's 141 m of descent. Steering can no longer push water uphill: the uphill component of a lean is discarded. |
| Rule 2 — flowing water carves; carved paths attract water | **Done** | Erosion/deposition brush + polish field lowering drag. Measured: **88 m³ moved/run**, −9.4 m cumulative cut, +6.0 m of deposit in 11 silt bars. |
| Rule 3 — nothing ever resets | **Done** | Save round-trip verified byte-exact. |
| One-thumb lateral steering | **Done** | Both touch and mouse paths compiled and checked. Authority scales with speed and cannot act up the fall line; `SteerAccel` set from a 6-arm × 150-run sweep. Closest approach to an aimed basin **53 m**, against 98 m before. Feel never validated by hand. |
| Steering costs momentum (restraint as skill ceiling) | **Done** | `SteerSpeedCost` bleeds speed proportional to lean, and slow water is barely steerable at all, so speed buys reach *and* control. Balance unvalidated by hand. |
| Momentum economics (fresh vs polished) | **Done** | ~9 m/s fresh, ~24 m/s polished. |
| Speed gates / crest ridges at speed | **Done** | Falls out of physics; momentum gates also exist as pickups. |
| Emergent hydrology (oxbows, deltas, capture) | **Partial** | Physics supports it; never observed over enough runs to confirm it emerges. |
| Waterfall / plunge-pool detection | **Built** | Fires splash events and deepens the pool. Never seen. |

## Progression

| Feature | State | Notes |
|---|---|---|
| Depth track (channels deepen) | **Done** | Measured run over run. |
| Volume track (basins fill, overflow) | **Partial** | Basins fill and report percentages; overflow now fires in measured runs (`North basin broke its banks`). But only **2 of 5 basins ever receive water** across 150 runs — the other three sit at 0%. See L-027. |
| Basin crossing (full lake acts as river) | **Done** | 150 runs: **69 runs crossed a full lake, 54 of those reached the sea**, avg 107 m travelled after crossing. A basin with room now also drinks from a stream passing over it — 2,388 m³ per 150 runs left in lakes in passing. |
| Life track (moss → village) | **Built** | Tier logic and instanced props exist. Submerged cells now drown (>0.25 m of standing water) and shore cells beside a lake count as fully moist, so a filled basin grows a visible waterline ring instead of sinking the forest out of sight (L-056, confirmed by render at 24 runs). Never observed past the first tier in play. |
| Revelation track (buried secrets) | **Done** | `3 of 60` revealed in 24 runs, `12 of 60` in 150. Placement is half on a summit-traced corridor, half on the wider drainage network; revelation tests erosion (`Virgin - Height`) within 4 m, so a find always means water was routed there. |
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
| Share glyph | **Partial** | Renders and copies to clipboard, but comes out nearly empty — needs to read as an image at a glance. A Daily is three runs, so the real case is sparser than anything the smoke test prints. |
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
| Strata legible as colour | **Done** | Per-pixel bands + seams + concavity occlusion. Confirmed working in the editor by the project owner (L-011); no screenshot archived. Occlusion now normalises over 1.6 m rather than 4 m — measured, only 191 cells in 150 runs are cut deeper than 1.5 m, so the old range darkened a real channel by ~7%. |
| Terrain silhouette (ridges, valleys, cliffs) | **Done** | Droplet erosion pre-pass + terracing. Summit 146 m over a 512 m base. Aerial perspective hazes distance from 90 m out. |
| Water ribbon as hero element | **Partial** | Widened and lifted; still not confirmed to read as the brightest thing in frame. |
| Lakes | **Done** | Level surface (ring vertices take the lake's surface level, not terrain height), depth gradient over 2.5 m, soft shore. Confirmed by screenshot. |
| The sea | **Done** | Subdivided 96², each vertex carrying real depth. Coastline grades deep blue → shallows → pale beach band, and deep water is now genuinely opaque — it was capped near 72% everywhere, which showed the seabed through the ocean and drew the heightfield's square boundary across it (L-045). Confirmed by render. |
| Ecosystem props | **Done** | Conifers with trunks and tiered crowns, moss cushions, reed clumps, huts with pitched roofs on flat ground. Vertex-baked vertical shading gives each internal form while staying one instanced material. Confirmed by render (L-016); canopies exist and are unused. |
| Splash particles | **Built** | Code-built system + `Droplet.shader`. Now also driven continuously by speed, not only by plunges. Never seen — needs play mode; the capture tool cannot show per-run state. |
| Camera (follow, report framing, idle pan) | **Done** | Retuned to 62 m back / 46 m up. |
| Sense of speed | **Built** | FOV kick (13° at 24 m/s), camera closing 22% toward the bed, ribbon widening and brightening, and spray above 12 m/s — roughly terminal speed on fresh rock, so spray means "faster than un-carved ground allows". Never seen. Plunge impact still not started. |
| Procedural water audio | **Built** | Synthesised from run state, no audio files. **Never heard.** |
| Haptics | **Built** | Event-only, throttled, platform-guarded. |
| No-HUD-by-default framing | **Partial** | HUD ghosts in, but is placeholder quality. |
| Onboarding | **Built** | Gated on whether the player has ever steered, not on run number — the first attempt keyed off `RunNumber < 6` and so could never appear on an existing mountain. Never watched. |

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
