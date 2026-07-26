# RILL — Open Loops

**This file drives implementation.** Read it first, work the top open loop, close it with evidence,
then update this file. It is the only place that says what happens next.

Last updated: **2026-07-26** · Open loops: **15** · Closed this cycle: **15** (4 archived)

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

### L-013 · Water rendering
**Why** — Lakes render as flat discs with hard shorelines; the sea is a plain blue plane. Water is
the subject of the entire game and currently looks like placeholder geometry.
**Done when** — Lakes have a depth gradient and a soft shoreline; the sea has a shoreline
treatment; the ribbon reads as the brightest thing in frame.

---

## Next

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
| L-018 | Onboarding — first 30 seconds explain nothing | Needs the loop settled first, or it teaches the wrong thing. |
| L-019 | Cascade / dam-break spectacle | Now fires — `North basin broke its banks` appears in 150-run logs. Unblocked; still nobody has seen it. |
| L-020 | Daily glyph legibility — currently near-empty | Viral spine, but pointless before retention exists. |
| L-021 | Biome balance — Glacier / Volcanic / Granite | Implemented, never run. Sandstone must be right first. |
| L-022 | Device performance pass | Never run on a phone. No profiling of any kind, ever. |
| L-023 | Region streaming beyond one 512 m field | Scope question, not a bug. |
| L-024 | Confluence backend, visits, paper boats | Deliberately out of scope while offline-first. |
| L-025 | Monetisation surfaces | Nothing built. Premature until retention is real. |

---

## Recently closed

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

### L-026 · Sea-floor depressions were being labelled as basins — closed 2026-07-26
`BasinSystem.LabelBasins` seeded the priority flood from the map border and then labelled every
depression it found, including ones under the sea. On the default seed **9 of the 14 "basins" had
floors 7–158 m below sea level**, 230–330 m from the summit, and the two largest by capacity
(4,967 m³ and 3,523 m³) were both submarine. They could not be filled by any amount of steering,
so they sat at 0% forever, they made the basin lattice display nine dead entries, and they poisoned
every routing measurement that picked a target basin at random. Two lines now skip cells at or below
`SeaLevel`. This loop did not exist while the work was done — it was found inside L-009 and is
written up here so the reason it mattered is on the record.
**Evidence** — `basins found 14, capacity 16,712 m³` → **`basins found 5, capacity 5,591 m³`**, and
the lattice went from nine permanent `0%` entries to five real tarns at 11 m, 28 m, 69 m, 42 m and
3 m elevation.

### L-009 · Confirm the basin retune — closed 2026-07-26
The numbers are now measured rather than guessed. Getting there needed three separate fixes, because
the original `24/24 Pooled` had three independent causes and each one hid the next: sea-floor
basins (L-026), a through-flow branch that never worked (L-029), and a smoke-test bot that steered
randomly and so could not route water anywhere at all.
**Evidence** — 24 runs, neutral bot: `Pooled 20 · TimedOut 2 · ReachedSea 2`, `delivered to sea
91 m³`, basin lattice `0% · 13% · 62% · 0% · 0%`. 150 runs: `Pooled 107 · TimedOut 12 ·
ReachedSea 29 · SoakedAway 2`, `delivered to sea 1,465 m³`, lattice `0% · 63% · 100% · 0% · 0%`.
**Closed on weaker evidence than asked for.** *Done when* wanted "several basins at different fill
levels". Only two of five ever receive water; the other three are at exactly 0% after 150 runs. The
mixture-of-endings and reaches-the-sea clauses are met outright. The unmet clause is now **L-027**.
**Caveat on attribution** — an intermediate measurement showed sea arrivals *falling* (13→5 per 150
runs). That was an artefact of a smoke-test bot that aimed two runs in three at a basin, i.e.
deliberately away from the sea. With a bot neutral between the two endings the loop is judged on,
the same code gives 29 per 150 runs against a 13 baseline. No conclusion should be drawn from the
intermediate figure.

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

*Archive of older cycles: [`docs/loops/`](docs/loops/)*
