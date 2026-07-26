# RILL — Open Loops

**This file drives implementation.** Read it first, work the top open loop, close it with evidence,
then update this file. It is the only place that says what happens next.

Last updated: **2026-07-26** · Open loops: **11** · Closed this cycle: **8**

---

## How this works

A **loop** is one thing that is not finished. It is open until there is *evidence* it works — not
until the code is written. "It compiles" closes nothing.

Every loop has:

- **ID** — `L-###`, never reused, never renumbered.
- **Why** — what breaks or stays broken if this is skipped.
- **Done when** — a condition someone else could check without asking you.
- **Evidence** — what was actually observed. Filled in at close time. No evidence, no close.

### The cycle

1. Pick the top loop under **Now**. If it is blocked, say so in the loop and take the next one.
2. Do the work.
3. Verify — `./tools/unity-stub/typecheck.sh`, then whatever the loop's *Done when* demands.
4. Move it to **Recently closed** with its evidence and the date.
5. Promote something from **Next** into **Now**. Add any new loops the work exposed.
6. When **Recently closed** exceeds ~10 entries, move the oldest into
   [`docs/loops/`](docs/loops/) as a dated archive file. See
   [`docs/loops/README.md`](docs/loops/README.md).

### Rules

- **Evidence beats intention.** A measured number, a log line, a screenshot, or a person who played
  it. Not "should work now".
- **A loop that silently does nothing looks exactly like a loop that works.** If a loop creates
  things, its evidence must include a count of what it created.
- **Never close two loops with one unverified change.** Split them.
- **Blocked is a status, not a failure.** Write what unblocks it.
- New work discovered mid-loop becomes a new loop, not a bigger current one.

---

## Now

### L-009 · Confirm the basin retune
**Why** — Basin strength has swung twice: once too weak (`water held 0 m³`, no retention loop at
all), once too greedy (`24/24 pooled`, nothing reached the sea, run distance halved). The current
values — 5 tarns, through-flow at 50% fill — have never been measured. Every downstream tuning
decision depends on this number being known.
**Done when** — A smoke test shows a *mixture* of endings, several basins at different fill levels,
and water reaching the sea at least sometimes.
**Evidence needed** — Smoke test output pasted into the close entry.
**Blocked by** — Nothing now. Requires the Unity editor to be **closed** (it holds the project lock;
a batch run against an open project exits silently after ~28 log lines).

### L-010 · Make secrets findable
**Why** — `secrets revealed 0 of 60` after 24 runs. Revelation is one of the four progression tracks
in the design document and it is currently invisible over any realistic session. A run polishes
~1.6% of the field, so 60 sites yields roughly one hit per 24 runs at best.
**Done when** — A 24-run smoke test reveals 2–5 secrets, and none of them is reachable without
routing water over the spot.
**Evidence needed** — `secrets revealed N of M` from the smoke test, N ≥ 2.
**Approach** — In preference order: lock placement to the drainage network rather than biasing
toward it; reveal on proximity to sufficient erosion rather than the exact cell; shallower burial
for common kinds; more sites.

### L-011 · See the strata pass
**Why** — Per-pixel strata bands, seam darkening and concavity occlusion were written to fix a
mountain that rendered as a smooth orange bedsheet. Shaders compile. **Nobody has looked at it.**
The design's central visual promise — "every metre of depth is legible as colour" — is unconfirmed.
**Done when** — A screenshot shows distinct sediment bands, and a carved channel is visibly a
channel from the idle camera.
**Evidence needed** — Screenshot.

---

## Next

### L-012 · Hand playtest against the kill criterion
**Why** — The design document sets an explicit M3 kill criterion: if playtesting does not produce
unprompted "one more run" behaviour, redesign *before* art exists. Nothing in this repo speaks to
it. Every metric so far is a proxy for fun, and proxies have been wrong before.
**Done when** — At least one person has played 20+ runs unprompted, and the reaction is recorded
honestly — including if it is boring.
**Evidence needed** — Written notes. Negative results are the valuable ones here.

### L-013 · Water rendering
**Why** — Lakes render as flat discs with hard shorelines; the sea is a plain blue plane. Water is
the subject of the entire game and currently looks like placeholder geometry.
**Done when** — Lakes have a depth gradient and a soft shoreline; the sea has a shoreline
treatment; the ribbon reads as the brightest thing in frame.

### L-014 · Sense of speed
**Why** — The momentum economy is the game's skill ceiling, and at 24 m/s it currently looks the
same as 9 m/s. The player cannot feel the thing they are optimising.
**Done when** — Speed is legible without the HUD meter: FOV kick, spray, and impact on plunges.

### L-015 · Persistent wet-channel darkening
**Why** — A carved channel is invisible when dry, so the player cannot see their own river system
between runs — which is most of the time they spend looking at the mountain.
**Done when** — Old channels read as channels from the idle camera with no water in them.

---

## Later

| ID | Loop | Why it waits |
|---|---|---|
| L-016 | Prop silhouettes worth looking at | Cones and discs. Cosmetic until the loop is proven fun. |
| L-017 | UI pass — legacy `Text`, hand-placed rects | Placeholder is survivable; the loop is not. |
| L-018 | Onboarding — first 30 seconds explain nothing | Needs the loop settled first, or it teaches the wrong thing. |
| L-019 | Cascade / dam-break spectacle | Code path exists, has never fired in a measured run. Depends on L-009. |
| L-020 | Daily glyph legibility — currently near-empty | Viral spine, but pointless before retention exists. |
| L-021 | Biome balance — Glacier / Volcanic / Granite | Implemented, never run. Sandstone must be right first. |
| L-022 | Device performance pass | Never run on a phone. No profiling of any kind, ever. |
| L-023 | Region streaming beyond one 512 m field | Scope question, not a bug. |
| L-024 | Confluence backend, visits, paper boats | Deliberately out of scope while offline-first. |
| L-025 | Monetisation surfaces | Nothing built. Premature until retention is real. |

---

## Recently closed

### L-008 · Documentation set — closed 2026-07-26
Wrote `docs/` (STATUS, FEATURES, ARCHITECTURE, TUNING, VERIFICATION), vendored the Unity stub into
`tools/unity-stub/` so the documented type-check workflow survives a temp-dir wipe.
**Evidence** — `./tools/unity-stub/typecheck.sh` runs clean from the repo copy.

### L-007 · Basins hold water — closed 2026-07-26
Tarns were carved as a subtracted bump with an open downhill lip, so they drained continuously and
could never fill. Rewrote to excavate to an absolute floor with a rim closing all the way round and
one low spill point.
**Evidence** — `water held 1,264 m³ across 16 basins · fullest basin 100.0%`, and the carve report
reading `South basin now 1% full` → `14% full` over 24 runs.

### L-006 · Water is never silently destroyed — closed 2026-07-26
Two separate bugs deleted the player's water without a trace: `AddWater` discarded the run's entire
remaining volume whenever the stream stopped outside a depression (i.e. usually), and
`GatherExistingWater` deleted water in cells that were no longer inside a labelled basin, silently
emptying lakes between runs. Both now route the water downhill to low ground.
**Evidence** — Basin volumes persist across runs and accumulate; see L-007.

### L-005 · Basins actually exist — closed 2026-07-26
`CarveBasins` was placing **zero** basins on every seed — rejection sampling on surface slope can
never pass on terraced ground. Replaced with deterministic best-site scoring, which cannot fail to
place N. The tell was a capacity number that did not change across a code edit.
**Evidence** — `[RILL] Carved 7 basins into the eroded mountain.` and basin count 9 → 16.

### L-004 · Mountain silhouette — closed 2026-07-26
Noise alone produced lumps. Added a 60k-droplet hydraulic erosion pre-pass (dendritic valleys, sharp
ridgelines), domain-warped ridge noise, strata-keyed terracing so hard bands become cliffs, and
summit normalisation.
**Evidence** — Summit 74 m → **146 m** over a 512 m base; run distance 60 m → **225 m**.

### L-003 · Runs no longer stall at the spawn — closed 2026-07-26
Three compounding faults: a summit "dish" that trapped every run within 3 m of spawn, talus not
scaled to cell size (capping slopes at 17°, a pillow), and a drag/gravity ratio giving 2.5 m/s
terminal speed against a 0.75 m/s pool threshold.
**Evidence** — Run duration 0.8 s → 12–40 s; distance 3 m → 100–250 m; sediment ~0 → 80–100 m³/run.

### L-002 · Verification harness — closed 2026-07-26
Built signature-only Unity API stubs plus `typecheck.sh` (whole codebase, three build configs, ~2 s,
no editor), and `RillSmokeTest` — 24 unattended headless runs reporting endings, sediment, basin
lattice, secrets, save round-trip and the Daily glyph.
**Evidence** — Every real bug found since (L-003 … L-007) was found by this harness. None were
visible by reading the code.

### L-001 · Project compiles and runs in Unity — closed 2026-07-26
Scaffolded the project, aligned it to Unity 6000.5.5f1, fixed the one real compile error
(`CompressionLevel` is ambiguous between `UnityEngine` and `System.IO.Compression`).
**Evidence** — Batch compile clean, zero errors and zero warnings at `-warn:4`; Game view renders.

---

*Archive of older cycles: [`docs/loops/`](docs/loops/)*
