# RILL — Open Loops

**This file drives implementation.** Read it first, work the top open loop, close it with evidence,
then update this file. It is the only place that says what happens next.

Last updated: **2026-07-26** · Open loops: **17** · Closed this cycle: **24** (14 archived)

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

### L-037 · The app has no front door
**Why** — Requested 2026-07-26: the game opened straight into a playable mountain. There was no
title, no moment of arrival, no deliberate act of starting — the app simply appeared, mid-game. A
player has no idea what they are looking at or that they are already able to play it, and there is
nowhere for the game to state its own name.
**Done when** — Opening the app shows a title with the game's name and a Begin button; play starts
only when the player chooses it.
**Evidence needed** — Someone opens the app cold and knows what it is before they touch anything.
**Implemented 2026-07-26, unobserved.** A `Title` state that boots first. Deliberately shows **the
player's own mountain** drifting behind the title rather than any art — the world is the save file,
so the most honest splash screen this game can have is the river system they built last time. Under
the name sits their record, read off the world rather than awarded: `47 runs · 12,805 m³ moved ·
2,322 m³ to the sea`, or "A new mountain, untouched" on a fresh save.
**Not done, and deliberately not guessed at** — "nice graphics" was part of the request and this is
typography over a live mountain, nothing more. Whether that is enough, or whether it wants a logo
treatment, a vignette, or a scripted camera move over the terrain, is a look-at-it question.

### L-036 · The run ends without a beat
**Why** — Observed in play 2026-07-26: "the closing is also too sudden." The report card appeared on
the same frame the water stopped, so the player never saw what they had just carved — the camera
frames the deepest cut and the carve overlay comes up, and both were immediately covered by a UI
panel. The run's *result* is the whole reward loop, and it was being skipped past.
**Done when** — The end of a run reads as an ending: the stream settles, the carve is visible for a
moment, then the card arrives. A player who wants to skip it can.
**Evidence needed** — Someone plays and does not describe the ending as abrupt.
**Implemented 2026-07-26, unobserved.** New `Settling` state holds for 1.1 s between the run ending
and the card, fading the ribbon at 0.6× speed while the already-framed camera and carve overlay are
visible. A tap skips it, so a returning player never sits through it.

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
**Superseded 2026-07-26 by L-038.** The dynamics this was measured against no longer exist: runs now
travel 226 m instead of 155 m and no longer stall short. Re-measure before arguing either way, and
read L-039 first — the arrival rate moved, and not in the direction this loop was hoping for.

---

## Next

### L-040 · Four of five basins cannot be reached downhill from the spring
**Why** — `reach (climb 0 m): #0 NO  #1 yes  #2 NO  #3 NO  #4 NO   (1 of 5)`. The basin lattice is
the progression track — "north basin 87% full" is the open loop the design leans on for retention —
and it is only a track if the player can decide to close it. Water that has to be driven *uphill* to
reach a basin is not a route; it is the player fighting rule 1, and rule 1 is the game.
**This is a generation problem wearing a tuning problem's clothes**, and that is worth stating
plainly because it was nearly fixed at the wrong layer. Steering authority *can* buy it:
`SteerAccel 56` takes basin #0 from 0% to **100%** under one sustained campaign where 42 leaves it
at 0%. But it costs first-session sea arrivals (6 → 2 over 24 runs), brings timeouts back (0 → 2 per
150), and it buys reachability by letting the thumb spin the stream along a contour — i.e. by
weakening the fall-line rule that had just been established. A basin should be somewhere water can
*flow*, not somewhere it can be dragged.
**Done when** — At least 4 of 5 basins are downhill-reachable from the spring on the default seed,
without raising `SteerAccel` above 42, and a sustained campaign can fill an off-channel one.
**Evidence needed** — The `reach (climb 0 m)` line, and `RILL/Run Headless Basin Campaign` taking a
basin that is *not* the sink from 0% to over 60%.
**Where to look first** — `MountainGenerator` and `BasinSystem.LabelBasins`. Basins are found by
priority-flood over whatever the generator produced; nothing has ever asked whether they sit on a
drainage path from the summit spring. Placing them on traced descent paths is the same technique
that fixed secret placement in L-010, where 45 of 51 sites had received no erosion at all because
the code reasoned about the whole mountain's drainage instead of about where runs actually go.

### L-041 · Deposition builds an 8.9 m mound and nobody has looked at it
**Why** — `terrain delta max` was `+1.31 m` before the flow work and is **`+8.87 m`** after 150
runs. Runs now travel 269 m instead of 136 m and drop their sediment much further down the mountain,
so a real landform is being built. That may be a delta — which would be one of the best things in the
game, since the design wants landforms to emerge rather than be authored — or it may be a silt wall
across the runout that quietly ruins the bottom third of the mountain. Both look identical in this
number.
**Done when** — Somebody has looked at it, and it is either named as a feature or bounded.
**Evidence needed** — A screenshot of the runout after 150 runs, plus the footprint: how many cells
are more than 2 m above virgin, and whether they form one mass or a scatter.
**Careful** — Do not clamp it before looking. L-028 bounded *incision* for a measured reason (a
23.7 m shaft, and the design document naming it as a top-three risk); there is no equivalent
evidence here yet, and clamping deposition would remove deltas along with the problem.

### L-018 · Onboarding — the first 30 seconds explain nothing
**Why** — There is no button and nothing moves on its own to suggest steering exists, so a player
can complete several runs without discovering the only verb that matters. This is the single largest
false-negative risk for the L-012 playtest: a tester who never finds steering concludes the game is
boring for a reason that has nothing to do with the game.
**Done when** — A first-time player discovers both verbs (tap to release, hold-drag to lean) inside
their first two runs without being told by a person.
**Evidence needed** — Watch someone start cold. Their questions are the measurement.
**Attempt 1 failed — reported 2026-07-26 as "onboarding is not here".** It was gated on
`Active.RunNumber < 6`, so it could never appear on an existing mountain — which is every mountain
except a brand new one. It compiled, it committed, and it was structurally incapable of being seen.
**Attempt 2, unobserved.** Now gated on whether the player has actually steered, which is the thing
being taught: "Tap to let the water go" before their first run, "Hold and drag while it runs to lean
the water" on idle afterwards, and a mid-run prompt if two seconds pass untouched. The moment they
steer, it clears and does not return. Session-scoped, because an existing save has no record of
whether its owner ever learned. Says **nothing** about the mountain remembering — that discovery is
the game.

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

### L-039 · Aimed delivery halved while delivery to the sea doubled — closed 2026-07-26
**Three causes, and only one of them was a defect in the game.**

1. **The harness was aiming at basins that were already full.** The campaign target cycled by index,
   and basin #2 reaches 100% by run 24 on this seed, so `15 of 36` aimed runs were aimed at
   something that could not receive water however well it was flown — and that fraction grew over a
   session *because the game was working*. Every aimed-delivery figure in this file since L-027 is
   partly a measure of the test picking impossible targets. The bot now picks the basin with the
   most headroom, and `aimed answerable` is printed so it cannot hide again.
2. **A basin with room absorbed nothing.** The design has always said a lake with headroom swallows
   the run; only the opposite case (a full lake, which spills) was ever implemented. A head entering
   an empty bowl sailed across the dry floor and climbed out the far side on momentum. Basins now
   drink from a stream passing over them — a drain, not a capture, because being stopped dead by
   scenery is a punishment while feeding the lake you aimed at is the point.
3. **Steering could push water uphill**, which is the real defect and the one hiding underneath.
   L-038 stopped a held lean spiralling the stream by scaling authority with speed, but that also
   removed authority the player needs to carve a route off the incised channel. The two are
   separable — deadlock is authority at *rest*, route-carving is authority at *speed* — but only
   once the lean cannot fight gravity at all. The simulation now discards whatever part of the
   steering force points up the fall line.
**Evidence** — `aimed entered 5 of 36 got inside the target basin; 0 of those sailed across and
left` (was 15 entered, 8 of them leaving again) and `fed in passing 1,917 m³ left in basins the runs
flowed over`. Over a 24-run session, closest approach to an aimed basin is **41 m against the 111 m
this project has measured since the beginning** — the player routes water *better* than before the
whole episode, which is the opposite of what throttling steering was expected to cost.
**`SteerAccel` re-centred on measurement, not restored.** With the uphill half discarded the old 20
bought almost no turning. 150 runs per arm at 20 / 30 / 42 / 56 / 70 / 90: **42 is the largest value
with zero timeouts**, because above it the stream can be spun in circles along a contour, which the
fall-line rule does not forbid.
**Closed on stronger evidence than it asked for in one respect and weaker in another.** *Done when*
wanted aimed delivery above 35% or a measurement separating the causes. The separation is here and
is conclusive; the rate itself is **14%**, and the reason is L-040, not steering — the headroom-max
bot now correctly spends its campaigns on the three basins that cannot be reached downhill at all.

### L-038 · Four runs in twenty-four never ended at all — closed 2026-07-26
**Why** — `TimedOut 4 per 24 runs` had been sitting in every smoke test result since the harness
was written, and was read as "some runs are slow". It is not the same thing. A run that ends is
feedback; a run that neither moves nor ends is 75 seconds of watching nothing happen, and the design
document's kill criterion is about whether someone wants one more run.
**Done when** — No run in a session spends its clock without getting anywhere, and the reach of a
normal run is measured rather than assumed.
**Evidence** — A once-a-second trace of the head, which is what no aggregate could give. Run 3 spent
68 of its 75 seconds oscillating between 69 m and 84 m of elevation at 0.1–3 m/s while polishing the
same two metres of ground from 0.10 to 0.99. Run 10's stall elevation *rose* every second — a slow
head is over its sediment capacity, so it deposits, so it buries itself.

Two causes, both fixed. (1) **675 cells of this mountain are closed depressions the basin lattice
declines to name** (it ignores anything under 24 cells, deliberately), and the simulation had no
fill-and-spill for them at all. It does now: minimax flood to the rim, pay for the fill out of the
run's own volume, write the pond into the field, cut the lip by 22 cm because overtopping erodes,
and swim to the lip using the crossing machinery rather than jumping to it. (2) **`SteerAccel` (20)
exceeds downhill acceleration on a 30° face (15)**, so a held lean could fight gravity to a draw
indefinitely. Steering authority is now bought with momentum.

24 runs, before → after: `ReachedSea 2 → 5`, `TimedOut 4 → 0`, distance `118 → 161 m/run`, descent
`85 → 109 m of 145`, sediment `62 → 73 m³/run`, delivered to sea `92 → 219 m³`, stopped in a basin
`10 → 15`, basin crossings `0 → 2`, fullest basin `63.9% → 99.9%`. At 150 runs, sea arrivals
`45 → 96` and delivery `2,322 → 4,719 m³`.

`SteerFullSpeed = 11` is measured, not chosen: over 150 runs per arm, timeouts go `15 / 7 / 4 / 0 / 0`
at `7 / 9 / 10 / 11 / 12`, so it is the most authority the player keeps while a held lean stays
incapable of stopping the descent.
**The session shape changed more than the numbers did** — a basin fills inside a first session
rather than after fifty runs, water crosses a full lake by run 24, and run 2 of a new mountain
reaches the sea.
**Two false starts worth keeping.** Triggering the escape on *speed* missed the exact case it was
written for: an oscillating head reads 0.5–2.7 m/s and never spends a continuous second slow. Stuck
is net displacement, not speed. And seeding the flood where the head was caught — part-way up one
side, since it is oscillating — made the search find the hollow's own floor and report no rim, so it
fired 3 times in 24 runs instead of the ~95 those runs needed.
**Cost, recorded rather than rounded** — see L-039. Aimed delivery into a *specific* basin fell.

### L-032 · Basin crossing teleports the stream — closed 2026-07-26
`CrossBasin` set `Head.Pos` to the outlet in a single step, drawing the ribbon as a straight line
across open water. Correct as physics, wrong as a picture, and reported as exactly that. The head now
swims across the *water surface* to the outlet, ignoring terrain (the bed slopes back toward the
basin centre, which defeated every earlier "steer toward the spill" attempt) and carving nothing.
A second defect surfaced underneath it: the traverse aimed at `SpillCell`, the saddle — flat, and at
water level once the basin is full — so arriving there left the head with no slope and it pooled on
the rim. `BasinSystem.OutletCell` now walks past the lip to ground 1.5 m below spill level.
**Evidence** — confirmed in play by the project owner. Measured alongside, 150 runs:

| | teleport | traverse | traverse + outlet |
|---|---|---|---|
| ReachedSea | 35 | 33 | **45** |
| delivered to sea | 1,776 m³ | 1,725 m³ | **2,322 m³** |
| crossings reaching the sea | 5 of 23 | 2 of 22 | **16 of 24** |
| distance after crossing | 42 m | 25 m | **103 m** |

The middle column is a regression I introduced and the smoke test caught: decaying drift speed by
`0.98` per **sub-step** at 90 Hz reached `StartSpeed` within a second, so a 40 m lake ate ~27 s of
the 75 s run clock. Fixed by separating drift speed from the exit speed banked at crossing time.

### L-031 · A cascade steals the player's next run — closed 2026-07-26
Three faults, only the first of which the original report pointed at. (1) The dam break ran off the
tap that *dismissed the report card* — a tap indistinguishable from the one that starts a run — so
the player believed their own run had begun inside a lake. It now plays **before** the report, as a
consequence of the run rather than as the next one. (2) It launched at `SpillCell`, the flat saddle,
where `down * StartSpeed` is ~zero and a full basin puts the lip at water level for another
`drag += 2.5`: the overflow stalled on the rim, which the owner described as "there is no way out".
It now starts below the lip. (3) It was bookkept as one of the player's runs — `BeginRun` incremented
`RunNumber` so the report read "run 12" while the world had moved to 13, and it was written into the
almanac, confluence queue, time-lapse and autosave, and consumed a Daily run. `BeginAutomaticEvent`
takes the same snapshot without the run number and the recording block is skipped.
**Evidence** — confirmed in play by the project owner.
**Worth recording** — this was reported twice before it was fixed. The first attempt addressed *when*
the cascade happened because that is what the description pointed at, but a reordered cascade that
still cannot move was never going to help. "No way out" was the diagnosis, and it was read as a
symptom.

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

*Archive of older cycles: [`docs/loops/`](docs/loops/)*
