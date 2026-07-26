# RILL — Open Loops

**This file drives implementation.** Read it first, work the top open loop, close it with evidence,
then update this file. It is the only place that says what happens next.

Last updated: **2026-07-26** · Open loops: **15** · Closed this cycle: **20** (10 archived)

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

### L-031 · A cascade steals the player's next run
**Why** — Observed in play 2026-07-26: "on restart it starts on basin not on top of mountain."
`RunController.OnReportDismissed` dequeues a pending cascade and calls `StartCascade`, which runs
`Active.BeginRun()` (consuming a run number), spawns at the basin's spill cell instead of the summit,
and calls `SetSteer(false, ...)` so the player has no control. The tap that dismissed the report
looks exactly like the tap that starts a run, so the player believes their run began in a lake and
that the controls are dead.
This code is not new. It was **dormant** because basins never filled; the L-009/L-026/L-028 work made
overflow routine (`North basin broke its banks` now appears regularly), which turned a rare
set-piece into the common case. A latent design decision became a bug by being exercised.
**Done when** — After a basin overflows, the player's next tap starts *their* run from the summit
with full control, and the dam break is legible as a separate event rather than a stolen turn.
**Evidence needed** — Someone plays through an overflow and does not think the game broke.
**Fix chosen 2026-07-26 (by the project owner): play it before the report.** The dam break now runs
between the player's run ending and their report card appearing, so it reads as a *consequence* of
the run rather than as the next run. Dismissing the report always returns to idle at the summit;
nothing chains off that tap any more. The carve baseline is deliberately not reset while a report is
held, or the overlay would show only the cascade with the player's own carving subtracted out.
**Still open** — nobody has played through an overflow since the change. Also unresolved: a cascade
still calls `Active.BeginRun()`, so it consumes a run number and the report card can read "run 12"
while the world has moved to 13. Left deliberately, as changing run-number semantics is a separate
decision from the ordering one.

### L-032 · Basin crossing teleports the stream
**Why** — Observed in play 2026-07-26: "when water reaches the large basin, it kinda gets teleported
to next side." Accurate. `FlowSimulation.CrossBasin` sets `Head.Pos = outlet` in a single step — a
hard positional jump across the lake, with one `Path.Add` so the ribbon draws a straight line across
open water. The *simulation* is right (water is conserved, the lake fills to its spill and the
stream continues from the outlet, measured in L-029) but the *presentation* is a teleport.
**L-029 closed on measurement alone and its evidence stands** — 23 crossings, 5 sea arrivals, 42 m
travelled after crossing were all real. What that evidence could not show is what it looks like,
which is exactly the gap `docs/FEATURES.md` marks with Built vs Done. This loop says so rather than
editing a closed loop.
**Done when** — The stream visibly traverses the lake surface to the outlet instead of jumping, and
nobody watching calls it a teleport.
**Evidence needed** — Someone watches a crossing and does not flinch.
**Implemented 2026-07-26, unobserved.** The head now swims to the outlet: a `_crossing` state that
drifts across the *water surface* (bed height plus water depth), ignores terrain — the bed slopes
back toward the basin centre, which is what defeated every earlier "steer toward the spill" attempt
— and carves nothing on the way.
First attempt at it decayed speed by `0.98` per **sub-step** at 90 Hz, reaching `StartSpeed` within a
second, so a 40 m lake ate ~27 s of the 75 s run clock. Measured cost: crossings reaching the sea
5 → 2, distance after crossing 42 m → 25 m. Fixed by separating the drift speed (flat 6 m/s) from
the exit speed off the lip (banked at crossing time from arrival speed). After: `ReachedSea` 35,
`delivered to sea 1,808 m³` — both at or above the teleport baseline — with distance after crossing
at 33 m against 42 m before. That remaining ~20% is the honest price of the traverse taking real
time instead of being instantaneous, and is not a defect to chase.

### L-030 · An aimed run arrives about 40% of the time
**Why** — L-027 established that a player who commits a campaign to a basin can fill it. What it also
measured is that individual aimed runs are unreliable, and the ones that miss get within an average
of 56 m before ending 122 m away. Some of that is the design working — the incised channel *should*
fight you, and "restraint is the skill ceiling" — but it has never been examined, and
`TimedOut 12 per 150 runs` says some aimed runs crawl the full 75 s without getting anywhere.
**Done when** — Either the aimed-arrival rate is over 50%, or there is a written argument (backed by
the hand playtest in L-012) that ~40% is the intended difficulty, and the loop closes as by-design.
**Evidence needed** — `aimed delivered` and `aimed closest`, and for the by-design route, a person's
account of whether missing feels like their fault or the game's.
**Measured 2026-07-26** — Two metrics, 150 runs, campaigns in blocks of 50:
`aimed at a basin 36 runs, reached it 16 (44%)` and `aimed delivered 14 of 36 (39%)`.
The suspicion recorded when this loop was opened — that the stop-based "hit" *undercounts*, because
a run delivering water and flowing onward scores as a miss — **was wrong**. Delivery-based arrival
is slightly *lower*, since a run can stop in a basin that is already full and add nothing. Both
metrics agree on roughly 40%, so the number can now be trusted. The earlier 28% figure came from a
diagnostic that pointed every aimed run at one deliberately awkward basin and should not be compared
against these.
**Careful** — Still do not tune `SteerAccel` or `SteerSpeedCost` on this alone. 40% may be correct
for a game whose whole skill ceiling is restraint; that is a feel question and L-012 answers it.

---

## Next

### L-018 · Onboarding — the first 30 seconds explain nothing
**Why** — There is no button and nothing moves on its own to suggest steering exists, so a player
can complete several runs without discovering the only verb that matters. This is the single largest
false-negative risk for the L-012 playtest: a tester who never finds steering concludes the game is
boring for a reason that has nothing to do with the game.
**Done when** — A first-time player discovers both verbs (tap to release, hold-drag to lean) inside
their first two runs without being told by a person.
**Evidence needed** — Watch someone start cold. Their questions are the measurement.
**Implemented 2026-07-26, unobserved.** Runs 1–2 idle line reads "Tap to let the water go", runs 3–5
"Hold and drag while it runs to lean the water", and during the very first run a hint appears if two
seconds pass untouched. Deliberately says **nothing** about the mountain remembering, basins, or
goals — that discovery is the game, and naming it replaces it with a chore. It names only what the
thumb does.
**Risk to watch** — this now overrides the projects/idle line for the first five runs, so a player
never sees a project prompt early. That is probably right, but it is a real trade and not measured.

### L-012 · Hand playtest against the kill criterion
**Why** — The design document sets an explicit M3 kill criterion: if playtesting does not produce
unprompted "one more run" behaviour, redesign *before* art exists. Nothing in this repo speaks to
it. Every metric so far is a proxy for fun, and in this project proxies have been wrong repeatedly —
three separate "the simulation is broken" conclusions were flaws in the test harness.
**Done when** — At least one person has played 20+ runs unprompted, and the reaction is recorded
honestly — including if it is boring.
**Evidence needed** — Written notes, pasted verbatim into the close entry. Negative results are the
valuable ones here.
**Blocked by** — **A human.** This is the one loop that cannot be closed from a terminal, and it
cannot be closed by the person who built the thing playing it themselves either: the tester must not
already know that the mountain remembers, because discovering that *is* the game.
**Ready** — Protocol written at [`docs/PLAYTEST.md`](docs/PLAYTEST.md): what not to say (most of it),
the single control hint that is allowed, a recording template, and a table of known confounds so
placeholder art and missing onboarding are not misread as the loop failing. Run it on a **fresh save
slot** — a mountain with 150 runs of channels already carved is a different game from a virgin one,
and first-session feel is what the criterion is about.
**Risk of a false negative** — L-018 (nothing explains the first 30 seconds) is still open, and a
tester who never discovers they can steer will find the game boring for a reason that has nothing to
do with the core loop. The protocol's one permitted sentence exists to isolate that. If the result
is negative, check it was not just this.

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
| L-019 | Cascade / dam-break spectacle | Now fires — `North basin broke its banks` appears in 150-run logs. Unblocked; still nobody has seen it. |
| L-020 | Daily glyph legibility — currently near-empty | Viral spine, but pointless before retention exists. |
| L-022 | Device performance pass | Never run on a phone. No profiling of any kind, ever. |
| L-023 | Region streaming beyond one 512 m field | Scope question, not a bug. |
| L-024 | Confluence backend, visits, paper boats | Deliberately out of scope while offline-first. |
| L-025 | Monetisation surfaces | Nothing built. Premature until retention is real. |

---

## Recently closed

### L-013 · Water rendering — closed 2026-07-26
Three separate defects, and the first two attempts at this were wrong in ways worth recording.
*Attempt 1* reasoned from how the shader consumes vertex colour — shore alpha ramped to zero,
sea subdivided — and was reported back as "it's a negative". *Attempt 2* only worked because a
screenshot showed what reasoning had missed: the water sheet was visibly **climbing the terrain at
its edges**.
The cause was in the mesh, not the shader. `PooledWaterMesh` set every vertex to `Height + Water`,
which is the lake surface for a submerged cell — but the feather ring exists *because a neighbour*
holds water, so its own depth is 0 and the vertex landed at terrain height, above the waterline by
definition. Every lake was modelled as a flat disc walling upward into a collar. "Flat discs with
hard shorelines" was a literal description of the geometry.
**Evidence** — screenshots from the project owner. A lake with a visible dark-to-pale depth gradient
dissolving softly at the shore and sitting *in* its bowl; a coastline grading from deep blue through
shallows to a pale beach band; and the run ribbon clearly the brightest thing in frame. All three
*Done when* clauses met.
**Fixes** — ring vertices take the neighbouring lake's surface level so the surface is level; depth
gradient mapped over 2.5 m rather than 6 m (basins here are only metres deep, so a 6 m range pinned
every lake at the shallow colour); the shader blends toward sky instead of adding it, which had been
washing the water to a grey film; and the sea is subdivided 96² so each vertex carries real depth
instead of a 4-vertex quad that could only ever be one flat tone.

### L-021 · Biome balance — Glacier / Volcanic / Granite — closed 2026-07-26
"Implemented, never run" was worse than it sounded: `BiomeRules.BetweenRuns` is called only from
`RunController.FinishRun`, so **no headless test had ever executed it**. Glacier freeze/thaw,
volcanic vents and granite spalling had run zero times in this project's history. The first
comparison, made before that was noticed, measured generation alone and produced a tidy, wrong
answer — four biomes collapsing into two pairs on identical terrain:

| biome | summit | basin capacity | sediment | basin sites |
|---|---|---|---|---|
| Sandstone | 146.4 m | 5,591 m³ | 1,493 | `11m/157m …` |
| Granite | 146.4 m | 4,411 m³ | 1,455 | `8m/157m …` |
| Glacier | 146.4 m | 5,571 m³ | 1,508 | *identical to Sandstone* |
| Volcanic | 146.4 m | 4,415 m³ | 1,447 | *identical to Granite* |

**Evidence, with the rules actually running** — Glacier: `"Channels froze overnight" x5`, **1,705 ice
cells**. Volcanic: `terrain delta max 13.20 m` against 1.18–1.47 m everywhere else — the vents build
an order of magnitude more terrain than any other biome causes. Granite: no events, correctly, as it
only nudges `Polish` upward ("what you cut here stays cut"). Sandstone has no rules by design.
**Defect found and fixed** — Volcanic grew the mountain 13 m over 24 runs and told the player
*nothing* unless water happened to quench a vent. A mountain that grows in silence breaks the same
rule as a system that silently does nothing, so vent growth now reports its volume.
**Left open deliberately, as new loops rather than scope creep** — weather was `Drought` on every
run of every biome (it is seed-derived), so the glacier **thaw** path is still unexercised: only
freeze has ever run. And `Field.Ice` is written by `BiomeRules` and read only by
`TerrainMeshBuilder`, i.e. it is a visual tint with no simulation consequence. Both are now L-033
and L-034.

### L-033 · Glacier thaw has never run — closed 2026-07-26
Weather is derived from the date, and the default seed lands on `Drought` every run, so only the
freeze branch had ever executed. Added a test that searches for dates producing specific weather and
drives both halves explicitly.
**Evidence** — `after 12 runs of Drought   ice cells 870   headlines: NONE` then
`after 12 runs of Snowmelt  ice cells 0   headlines: The thaw released 187 m³ | 142 m³ | 96 m³`.
Ice cleared 870 → 0, meltwater non-zero. Both *Done when* conditions met.
**Weaker than asked in one respect** — the loop said "Snowmelt **and** Storm". Only Snowmelt was
driven. Both set the same `thawing` flag on the same branch, so the code path is identical, but
Storm has not literally been run and this entry should not be read as saying it has.
**Defect found while closing, fixed** — the thaw announced meltwater it never delivered. See L-035.

### L-035 · The thaw announced water it never delivered — closed 2026-07-26
`thawedVolume` was accumulated solely to build the headline string. The game told the player
"The thaw released 187 m³" and put nothing anywhere: no basin gained volume, no run gained volume,
only `Wet` was nudged. The code's own comment claimed "Meltwater is real water: a thaw is a free
run's worth of volume, spread out" — it was neither. A game whose entire trust contract is that the
world honestly records what you did cannot announce water the player never receives.
Meltwater now goes into the world, routed downhill to a basin by the same `AddWater` path as every
other drop, and the headline reports what actually arrived rather than what melted.
**Evidence** — glacier thaw test, basin water **656 m³ → 1,203 m³** across the thaw phase, and the
headline now reads `The thaw released 187 m³ (132 m³ reached the basins)`. Before the fix the same
run reported the same 187 m³ with basin water unchanged by the thaw.

### L-034 · Ice is decoration — closed 2026-07-26
`Field.Ice` was written by the glacier rules and read only by `TerrainMeshBuilder` as a colour tint,
so a frozen channel behaved exactly like an open one and the whole biome was a palette swap with a
headline. Ice now has physical consequences in `FlowSimulation`: slick to travel over
(`drag × 0.55` at full ice) and armoured against carving (`carve × 0.25`). A glacier should be fast
and grudging about being cut, which is what makes it a different game rather than a filter.
**Evidence** — 24 runs, Glacier against Sandstone on the same seed, with 1,725 ice cells present:
distance **118 → 131 m/run (+11%)**, top speed **24.8 → 28.0 m/s** (now reaching the cap). The loop
warned that the two biomes already differed by ~4% in sediment, i.e. noise; +11% distance and +13%
speed are comfortably clear of it.
**The carve-armour half did not show in the totals** — sediment moved is 63 vs 62 m³/run, a 1.3%
difference well inside noise. Runs on ice cut less *where the ice is* but travel further and faster,
so they carve more everywhere else and it nets out. The drag effect is proven; the armour effect is
not, and a per-cell measurement would be needed to claim it.

### L-027 · Only two basins in five ever receive water — closed 2026-07-26
**The premise was wrong.** The loop assumed three fifths of the basin lattice was unreachable
scenery. It is not: every basin is reachable, and a player who commits a campaign to one can fill
it. What the loop was actually measuring was its own test bot, which picked a fresh random basin
every single run — so no basin was ever worked at twice running, and carving a new route is the only
way to reach one off the incised channel.
**Evidence** — 150 runs, all aimed runs committed to a single off-channel basin: basin #0 went from
`0%` to **`85%` full**, and the lattice read `85% · 56% · 97% · 0% · 0%`. Progression of the bot,
same seed, same 150 runs throughout:

| bot behaviour | aimed hit | closest | basins > 0% |
|---|---|---|---|
| random target each run | 31% | 64 m | 2 of 5 |
| campaigns, blocks of 30 | 25% | 81 m | 2 of 5 |
| campaigns, blocks of 50 | 44% | 56 m | 3 of 5 |
| one sustained campaign | 28% | 56 m | 3 of 5, target 0% → **85%** |

**Neither *Done when* clause was met literally, and both were mis-specified.** It asked for 4 of 5
basins above 0%: a 150-run test contains ~36 aimed runs, and one basin needs roughly that many on
its own, so no single test can wet five. It asked for a >50% aimed hit rate: "hit" counts only runs
that *stop* in the target basin, so a run that delivers water and flows onward scores as a miss.
Closing on the premise being disproven rather than on the numbers, with the arrival rate carried
forward as **L-030** and the metric flaw recorded there.
**Third harness flaw in this loop** — sub-sea-level basins, a bot that could not steer, and a bot
that could not persist. Every one looked like a simulation bug.

### L-011 · See the strata pass — closed 2026-07-26
Per-pixel strata bands, seam darkening and concavity occlusion were written to fix a mountain that
rendered as a smooth orange bedsheet, and had never been looked at by anyone — the shaders compiled
and nothing more. Confirmed working in the editor.
**Evidence** — Direct observation by the project owner ("l-011 is working"), after pressing Play.
The earlier Game-view screenshot showing bare skybox was edit mode: there is nothing in the scene
until `GameBootstrap` builds it at runtime.
**Closed on weaker evidence than asked for.** *Done when* wanted a screenshot showing distinct
sediment bands and a carved channel legible as a channel from the idle camera; what closed it is a
person saying it works, with no image archived in the repo. That is a real observation and enough to
stop calling the render path unverified, but it is not the artefact the loop asked for, and nobody
later can check it. If a screenshot gets taken, add it to `docs/` and reference it here.

### L-029 · Basin crossing is built but has never mattered — closed 2026-07-26
A run whose remaining volume exceeds a lake's headroom fills that lake to its spill and continues
from the outlet. The version it replaced provably never worked — a 0.35g nudge toward the spill cell
loses to terrain gravity on the rim it has to climb, and ran for 1,187 sub-steps across 24 runs
while carrying out zero of them.
**Evidence** — 150 runs: `basin crossings 23 runs crossed a full lake; 5 of those reached the sea;
avg 42 m travelled after crossing`. The distance-after-crossing figure is the load-bearing one: the
crossing count alone was never evidence, because a counter proves the branch executed rather than
that it carried the run anywhere. 5 of the 35 sea arrivals followed a crossing.
**Caveat** — 0 crossings in 24 runs. The mechanic only engages once a basin is genuinely full, which
takes roughly 50 runs on the default seed, so nobody playing a first session will ever see it. That
is expected rather than broken, but it means L-019 (cascade spectacle) still has nothing to show
early and the visual has still never been *watched* — only measured.

### L-010 · Make secrets findable — closed 2026-07-26
Three compounding causes, and the first two fixes each looked like progress while the track stayed
dead. (1) Placement was only *biased* toward channels — any concave cell qualified and off-route
cells were accepted 20% of the time anyway. (2) Revelation required the buried cell *itself* to be
cut to depth, but a channel is metres wide and wanders, so hitting one specific 2 m cell repeatedly
is a coincidence rather than a skill. (3) The real one, only visible once measured: flow
accumulation describes drainage across the whole mountain, but every run starts at one summit
spring and converges into a single corridor — **45 of 51 sites had received no erosion at all after
150 runs**. Sites are now split half on a summit-traced corridor (240 descent walks) and half on the
wider network, sampled from candidate lists directly rather than by rejection sampling, which had
been silently placing 20 sites where 60 were asked for.
**Evidence** — `secrets placed 60`, **`secrets revealed 3 of 60`** after 24 runs (*Done when* asked
2–5), rising to `12 of 60` at 150 — a curve, not an exhausted track. Site contact went `3 of 51
touched` → `18 of 60`, average best cut 0.03 m → 0.18 m against 1.41 m needed.
**On the second clause** — "none reachable without routing water over the spot" is argued
structurally, not measured per secret: revelation now tests `Virgin[c] - Height[c] >= depth`, which
is erosion, and erosion only happens where water flowed. That is worth stating plainly because an
intermediate version compared *elevation* instead and revealed 37 of 51 without anyone playing —
caught only because it reported the same 37 after 24 runs and after 150.

### L-028 · The convergence point drills itself into a pit — closed 2026-07-26
Runs converge on one line, that line carves, the carve attracts the next run. Rule 2 working as
designed — but unbounded it cut **23.7 m below virgin** in 150 runs while the sink basin's capacity
*grew* from 2,873 m³ to 3,338 m³ as it filled: the "boring local minimum by week 6" the design
document names as a top-three risk. `HealingPerRun` could never counter it, because healing
deliberately skips the channel currently in use. Fixed at source instead: carve rate now falls with
the square of how far a cell already sits below virgin rock, reaching zero at `GradeDepth` (14 m),
which is what a real river does when it approaches a graded profile.
**Evidence** — 150 runs, `terrain delta min` **−23.68 m → −8.62 m**. It did not flatten the loop, it
improved it: `ReachedSea` 29 → **35**, delivered to sea 1,465 → **1,776 m³**, distance 131 → **136
m/run**.
**Closed on slightly weaker evidence than asked for.** *Done when* wanted sediment moved to stay
"near 74 m³/run"; it fell to **64** (−14%). Carving is genuinely slower now, which is the intended
trade, but the number is outside what the loop asked for and is recorded here rather than rounded.

*Archive of older cycles: [`docs/loops/`](docs/loops/)*
