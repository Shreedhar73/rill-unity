# RILL — Open Loops

**This file drives implementation.** Read it first, work the top open loop, close it with evidence,
then update this file. It is the only place that says what happens next.

Last updated: **2026-07-26** · Open loops: **14** · Closed this cycle: **30** (19 archived)

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
**Promoted to the top 2026-07-26, and it is now the only thing that matters.** Everything else that
could be advanced from a terminal has been. The core loop is measurably 1.5–2× stronger than it was
that morning — 82 runs in 150 reach the sea against 45, no run in 150 fails to end against 12, four
of five basins fill against one — but every one of those is a proxy, and this project's own record is
that five separate "the simulation is broken" conclusions turned out to be flaws in the test harness.
**A number cannot tell you whether somebody wants one more run.**

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

---

## Next

### L-043 · The basin lattice is finished by run 500
**Why** — Measured 2026-07-26 over a 500-run season, the first time this game has been run far
enough to see its own endgame. Four of five basins sit at **100%** and the fifth at 0%; runs stopping
on open ground are `404 of 500` against `96` in a basin; `378 of 500` reach the sea. The mountain has
matured into a well-drained river system, which is the carve → speed → reach loop succeeding
completely — and it means the retention mechanic the design leans on hardest, "north basin 87%
full", has no unfinished loop left to offer.
**Done when** — There is something a 500-run mountain is still unfinished at, and it is named.
**Evidence needed** — The 500-run lattice line, and the same at 1,000 runs, before designing
anything that duplicates a recovery the world already does on its own.
**The bot was checked first, as the loop demanded, and it was not the answer this time.** One
sustained 500-run campaign per basin, fresh mountain each:

| target | final fill | entered | delivered | basin count |
|---|---|---|---|---|
| #0 | **100%** | 32/147 | 7/147 | 5–5 |
| #1 | **100%** | 34/147 | 13/147 | 5–5 |
| #2 | **0%** | **0/147** | 0/147 | 5–5 |
| #3 | 0% | 77/147 | 17/147 | **3–5** |
| #4 | **100%** | 105/147 | 7/147 | 5–5 |

**Three of five fill to 100% under a determined campaign**, which is the claim the progression track
rests on and is stronger than L-027's original 85%. The other two rows are the loop's real content:

- **#2 is never entered once in 147 aimed runs across 500.** Not hard to fill — never reached. It
  also has the highest sea arrivals of any arm, so the water is going somewhere and simply not
  there. `reach (climb 0 m)` calls it reachable, so the gap is between "a path exists" and "a run
  can be steered down it", which is the same distinction that made `aimed miss` useless in L-030.
- **#3 did not stay unfilled — it ceased to exist.** It took 17 deliveries and the basin count fell
  from 5 to 3. Filling a tarn and depositing around it merges or erases the depression, so a
  campaign can consume its own objective — and the mountain now says so out loud (L-044, closed).
  What that does *not* solve is this loop: a lattice that shrinks has less left to offer, not more.

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

### L-014 · Sense of speed
**Why** — The momentum economy is the game's skill ceiling, and at 24 m/s it currently looks the
same as 9 m/s. The player cannot feel the thing they are optimising.
**Done when** — Speed is legible without the HUD meter: FOV kick, spray, and impact on plunges.
**Implemented 2026-07-26, unobserved.** FOV kick (13° at 24 m/s) and the camera closing 22% toward
the bed were already in. Spray was not: the stream now throws it above **12 m/s**, at a rate that
climbs with how far over that it is. The threshold is not a taste number — it is roughly terminal
speed on fresh rock on a steep face, so spray appears exactly when the water is moving faster than
un-carved ground allows. It is the reward for having carved, shown rather than reported. Gated
rather than proportional from zero, because spray that is always on is weather and spray that
*starts* is information.
**Still not done** — "impact on plunges" is unchanged: `Splash` fires on drops over 1.1 m and always
has. Whether that reads as impact is a look-at-it question, and the loop should not close on the
spray half alone.

### L-015 · Persistent wet-channel darkening
**Why** — A carved channel is invisible when dry, so the player cannot see their own river system
between runs — which is most of the time they spend looking at the mountain.
**Done when** — Old channels read as channels from the idle camera with no water in them.
**Implemented 2026-07-26, unobserved. Two causes, both measured rather than guessed.** The concavity
occlusion normalised surrounding rock over **4 m**, and over 150 runs only 191 cells are cut deeper
than 1.5 m against 613 deeper than 0.5 m — so a real channel produced an occlusion of ~0.93, a 7%
darkening before `_AOStrength` scaled it down further. It now normalises over 1.6 m, the depth this
mountain actually reaches. And **nothing drew the cut at all**: polish decays at `PolishDecayPerRun`
and wetness faster, so a channel abandoned twenty runs ago has neither left and is still a channel —
the rock is gone. That made the oldest work on the mountain the least visible, which is backwards
for a game whose premise is that nothing ever resets. Terrain colour now carries a `Virgin - Height`
term saturating at 2 m.

---

## Later

| ID | Loop | Why it waits |
|---|---|---|
| L-016 | Prop silhouettes worth looking at | Cones and discs. Cosmetic until the loop is proven fun. |
| L-017 | UI pass — legacy `Text`, hand-placed rects | Placeholder is survivable; the loop is not. |
| L-019 | Cascade / dam-break spectacle | **Counted 2026-07-26: 4 overflows and 207 m³ over the lip per 150 runs, and *zero* per 24.** The mechanism works; a first-session player still never sees one. Nobody has watched one. |
| L-022 | Device performance pass | Never run on a phone. No profiling of any kind, ever. |
| L-023 | Region streaming beyond one 512 m field | Scope question, not a bug. |
| L-024 | Confluence backend, visits, paper boats | Deliberately out of scope while offline-first. |
| L-025 | Monetisation surfaces | Nothing built. Premature until retention is real. |

---

## Recently closed

### L-044 · A basin can be erased by being filled — closed 2026-07-26
A 500-run campaign against one basin ends with the lattice down from 5 to 3. The player's target
disappears mid-campaign and nothing anywhere said so. L-035 was closed for announcing water it never
delivered; this is the inverse — delivering a change and never mentioning it — and the trust
contract only survives if both directions hold.
`BasinSystem` now identifies each basin by its **deepest cell** across a rebuild (ids are assigned by
scan order every time and are *not* stable) and raises `Lost` when that cell is no longer inside any
depression. `RillWorld` turns it into a headline that says where the water went, because it is not
gone — `GatherExistingWater` routes anything outside a depression downhill until it finds one, and
saying so is the difference between an ending and a bug.
**Evidence** — 500-run campaign: `2 silted out of existence, 0 merges — raised by the world: 2 and
0; as the card's title 1 (all appear in its body)`, with the text
`"West basin silted up for good — its 80 m³ moved on downhill"` and the same for North basin at
39 m³. The one that lost the title lost it to a dam break in the same run and is still on screen,
since `HudController` lists every headline under the title. 24 and 500 runs of *ordinary* play both
report `basin count 5 throughout` and zero lattice changes, so this only fires for players who
campaign hard enough to earn it.
**Three things had to be fixed before any of it was visible, and each looked like the previous one
working.**
1. **The detection appeared to do nothing** — zero events across a season in which the lattice
   demonstrably shrank. The fault was in the *harness*: the smoke test called `EndRun` **before**
   `Basins.Rebuild()`, the opposite of `RunController.FinishRun`, so every headline a rebuild raises
   was cleared by the next `BeginRun` before anything read it. This is the fifth time a
   silently-does-nothing result in this project has turned out to be the test rather than the game.
2. **Merge detection was built on a wrong inference.** Seeing the count fall with no `Lost` events,
   I concluded the basins were merging rather than vanishing. They were vanishing. The merge path
   has since fired **zero** times in any test and is kept as unproven code, labelled as such,
   because it is a real event that can happen and the check is two lines.
3. **The headline was invisible to the player.** `CarveReport.Summary()` — the card's title — checks
   `Overflowed`, `Revealed`, `BasinChanges` and `DeepestCarve`, and would never have shown a lattice
   change at all. It now ranks one just under a dam break.
**Closed on weaker evidence than it asked for in one respect.** *Done when* wanted "a name for what
the place became". The headline says the tarn silted up and where its water went; it does not name
the successor landform. Nothing in the world currently knows whether a filled tarn became meadow,
gravel flat or bog — the ecosystem tracks moisture but not history — so naming it would be a
fabrication, which is the exact failure L-035 was closed for. Left undone deliberately.

### L-042 · The mountain silts up its own approaches — closed 2026-07-26
Opened the same day on one end-of-test number: the lattice was `5 of 5` reachable downhill at
generation and `4 of 5` after 150 runs. The fear was a slow death — a game meant to be played for
months whose basin lattice quietly closes, which is the "boring local minimum by week 6" the design
document names as a top-three risk. L-028 fixed the incision half of that; this was the deposition
half.
**Evidence — 500 runs, sampled every 25.** The mountain does do this to itself: the virgin-rock
control reads `5 of 5` at every single sample, so generation is correct and it is the river
reorganising.

```
run    1   downhill 5/5, on momentum 5/5, on virgin rock 5/5   no downhill route: none
run   50   downhill 2/5, on momentum 5/5, on virgin rock 5/5   #0 0%  #3 5%  #4 100%   <- 2 still had room
run  100   downhill 2/5, on momentum 5/5, on virgin rock 5/5   #1 84%  #3 32%  #4 100% <- 2 still had room
run  125   downhill 4/5, on momentum 5/5, on virgin rock 5/5   #1 100%
run  500   downhill 4/5, on momentum 5/5, on virgin rock 5/5   #1 100%
```

**Three findings, and the last one is the answer.**
1. It is not a decay. Strict downhill access collapses to `2 of 5` by run 50 and **reopens to
   `4 of 5` by run 125**, then holds flat for the remaining 375 runs. Something reopens what silts
   closed, which the loop asked for as an alternative to stability and which turns out to be both.
2. In the steady state the single basin with no downhill route is **the one at 100% full**. The
   mountain closes the door on lakes it has finished with. Every basin that still had room was
   reachable at every sample from run 125 to run 500.
3. **On momentum the answer is `5 of 5` at every sample, without exception.** Strictly-downhill was
   always a lower bound the simulation does not obey — water here tops 25 m/s and `v²/2g` at that
   speed is tens of metres of climb. The player never loses a basin at all.
**The real cost, recorded rather than rounded away** — between runs 50 and 100 there is a window
where two *unfinished* basins have no downhill route and can only be reached on momentum. That is a
harder game for fifty runs, not a broken one, and it is arguably the design working: the river
reorganises and the player has to carve a new way in. Nobody has played through that window, so
whether it reads as the mountain changing or as the mountain cheating is unknown.
**Nothing was clamped**, which the loop asked for explicitly. Deposition is still free to build.

### L-020 · Daily glyph legibility — closed 2026-07-26
The share unit was a scatter of marks on a void, and the cause was a count rather than a matter of
taste: a day's seven runs all leave the same summit and converge on the same corridor, so they touch
**8 of the glyph's 49 cells** and the other 41 were drawn as `⬛ nothing happened here`. The grid was
framed on a 512 m map and asked to describe a 250 m river.
The background is now the day's mountain — land and ocean — so every cell carries information, which
is what makes a Wordle grid readable at a glance. It stays comparable between players because
everyone on a Daily seed has the identical coastline underneath, which is the entire point of a
shared grid. Water is drawn over it in white, the brightest thing in the frame, the same as in the
game.
**Evidence** — the glyph is printed by the smoke test and is reproducible from the log:

```
🟦🟦🟦🟦🟫🟦🟦
🟦🟦🟫🟫🟫🟫🟦
🟦🟫🟫🟧⬜🟫🟦
🟦🟩⬜⬜🟪🟫🟫
🟦🟫🟫🟧⬜🟫🟫
🟦🟦🟫🟫🟫🟫🟫
🟦🟦🟦🟫🟫🟫🟦
```

An island, a river down the middle, an amber square where a run stopped and a green one on the west
coast where one reached the sea. Terrain cells went `0 of 49 → 49 of 49`.
**The test was also lying about what a share looks like**, and that is the more useful half. It
rendered a glyph from *every run of the session* — 24 or 150 of them — which is not the unit anybody
ever sends. It now prints the last `RunsPerDay` runs as the real case, and reports how many cells
carry water in each, because "reads as empty" is a number.
**Not closed on a person's reaction.** Nobody has pasted one into a chat and watched what happens,
which is the only test that matters for a viral mechanic.

### L-041 · Deposition builds an 8.9 m mound and nobody has looked at it — closed 2026-07-26
Opened the same day, on the observation that `terrain delta max` had gone `+1.31 m → +8.87 m` once
runs started travelling 269 m instead of 136 m. The fear was a silt wall across the runout ruining
the bottom third of the mountain; the alternative was a delta, which the design wants. A single
maximum is the same number for both, so the loop asked for the footprint.
**Evidence** — `deposits 145 cells over 2 m above virgin in 11 masses; largest 47 cells (188 m²) at
33 m elevation; spread 0-93 m`. Eleven disconnected silt bars scattered down the mountain, the
largest about 14 m across, together **0.22% of the field**. Not a wall, not a dam, not one landform
— and the maximum came down to `+6.00 m` on its own once the basins moved onto the drainage.
**Closed on weaker evidence than it asked for, deliberately.** *Done when* wanted somebody to look
at it. Nobody has. What the measurement does settle is the specific risk the loop was opened for: a
connected mass blocking the runout would show as one component of thousands of cells, and the
largest is forty-seven. The aesthetic question — whether eleven silt bars look like anything — is
still open and belongs with the other look-at-it loops.
**Nothing was clamped**, which the loop asked for explicitly. Deposition is still free to build.

### L-040 · Four of five basins cannot be reached downhill from the spring — closed 2026-07-26
Basins were scored on concavity and relief anywhere between 10 m and 110 m of elevation, which
describes where a lake *could* sit on this mountain. The game asks a different question — where can
the player put water — and on the default seed only **1 of 5** basins was reachable downhill from
the spring. The other four read as `0%` forever in the lattice the whole retention design leans on,
and the failure was invisible by construction: a basin that *cannot* be filled and one that has
*not been* filled yet produce an identical line in every report this project has.
`CarveBasins` now floods downhill from the spring before scoring anything, and reports the candidate
count it drew from, so "carved 5 basins" can never again quietly mean "carved 5 basins nobody can
reach".
**Evidence** — `reach (climb 0 m)` went **1 of 5 → 5 of 5**, from
`723 candidate cells on the spring's drainage`. One sustained campaign against basin #0, off the
incised channel, takes it **0% → 100%** — the claim the progression track rests on, which L-027
established and which had quietly stopped being true. Over 150 runs the lattice ends
`100% · 93% · 0% · 100% · 100%` against `0% · 11% · 64% · 0% · 0%` before, water held
`2,671 → 2,962 m³`, and runs stopping in a basin `39 → 66 of 150`.
**Fixed at the layer it was broken at, deliberately.** `SteerAccel 56` also fills basin #0, and was
measured doing it. It was rejected: buying reachability with steering costs first-session sea
arrivals (6 → 2 over 24 runs), brings timeouts back, and works by letting the thumb drag water along
a contour — weakening the fall-line rule established the same day to make room for a generation bug.
**The trade, recorded** — sea arrivals `101 → 82` and delivery to the sea `4,846 → 3,447 m³` over
150 runs, because there are now four fillable lakes between the spring and the coast catching water
on the way past where before there was effectively one. That is the lattice working, not a
regression, but it is a real change in where a session's water ends up.

### L-030 · An aimed run arrives about 40% of the time — closed 2026-07-26
Closed by L-038, L-039 and L-040 together rather than by anything aimed at it directly. The loop
asked for an arrival rate over 50% or a written argument that ~40% was the intended difficulty.
**Evidence** — 150 runs: `aimed delivered 11 of 36 (31%)`, and **`11 of the 21 answerable (52%)`**.
Closest approach fell `98 m → 53 m`.
**Closed on the answerable denominator, and that needs saying plainly.** The raw rate is 31%. The
other 15 aimed runs were aimed at a basin that was already full, which cannot receive water however
well the run is flown — and that is a real state of the world now that basins actually fill, not a
harness quirk that can be tuned away. The 50% clause is met against targets that could physically
answer, and is not met against all aimed runs. Anyone re-reading this should use the `aimed
answerable` line, which exists now precisely so this distinction cannot be fudged again.
**What actually moved it** — none of it was steering strength, which is what this loop kept warning
against tuning. It was: runs that stalled instead of ending (L-038), basins that did not absorb a
stream passing over them and steering that could push water uphill (L-039), and four fifths of the
lattice sitting off the spring's drainage (L-040).

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

*Archive of older cycles: [`docs/loops/`](docs/loops/)*
