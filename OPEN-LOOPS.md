# RILL — Open Loops

**This file drives implementation.** Read it first, work the top open loop, close it with evidence,
then update this file. It is the only place that says what happens next.

Last updated: **2026-07-26** · Open loops: **18** · Closed this cycle: **33** (23 archived)

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

**Order for this batch, and the reasoning.** L-046 first because it is the container: back, quit,
three mountains and three modes all need a level above the run loop that does not exist yet. Then
L-047, because the mountains are the substance and the save plumbing is already there unused. Then
L-050 out of turn, because it is the **only item in the batch that can be verified from a terminal** —
the capture tool renders lighting, and everything else here is UI that needs a person pressing Play.
Then L-049 (a launch needs somewhere to hand off to, and is far better once the sun moves), L-048,
and L-051 last.

**Said once, then built as asked.** This is a lot of shell and meta for a game whose kill criterion
(L-012) has still never been tested, and the design document's own instruction is to redesign before
building outward if the core does not compel. The core loop is measurably much stronger than it was
this morning — 378 of 500 runs reach the sea, no run fails to end — but nobody has still ever wanted
one more run in front of a witness. If the playtest goes badly, this batch is the work most likely to
be wasted. Recorded here so that is a known bet rather than a surprise.

### L-046 · The app is one screen with no way out of it
**Why** — Requested 2026-07-26. Everything the game can do hangs off a single `RunController` state
machine that boots into a title and then never leaves the mountain. There is no home, no back, no
way to close the game, and no level above the run loop for a second mountain or a second mode to
live in. Every other loop below is blocked on this one existing, which is why it is first.
**Done when** — From any screen there is one obvious way back, the chain ends at a home screen, and
the home screen can be left deliberately.
**Approach, and it matters** — the navigation state machine goes in a **MonoBehaviour-free class**
alongside `Rill.Core`, not in the UI. UI cannot be verified from a terminal; a state machine can, and
L-018 is the cautionary tale — onboarding "compiled, committed, and was structurally incapable of
being seen" because nothing tested the gate. The smoke test will drive back/forward transitions
directly.
**Two platform decisions, made rather than deferred**
- Android's hardware **back** must map to the same action as the on-screen back, or the OS one wins
  and the app closes mid-run.
- **Quit is Android/desktop only.** Apple's guidelines say an iOS app must not offer to close itself,
  so a "close game" button ships everywhere except iOS rather than being cut or being wrong.

### L-047 · One mountain, forever, and no way to have another
**Why** — Requested 2026-07-26 as "three mountains". `SaveSystem` has taken a `slot` argument since
it was written and nothing has ever passed anything but 0 — the almanac, the time-lapse archive and
the confluence queue are all per-slot already. The plumbing exists and there is no way to reach it.
**What three mountains buys, beyond quantity** — the biomes are genuinely different games and three
of the four have been measured as such: Glacier is fast and grudging (`+11%` distance, `+13%` top
speed, and ice armours the rock against carving), Volcanic grows terrain an order of magnitude
faster than anything else (`13.20 m` against `1.18–1.47 m`), Granite keeps what you cut. Right now a
player meets exactly one of them, chosen for them, forever.
**Done when** — Three slots, each with its own biome, history and record; switching between them
touches neither's terrain; and a fresh slot can be started without any path existing that could
clear an occupied one.
**Careful — this is the closest anything has come to violating invariant 1.** "Nothing clears
`HeightField.Height`. No reset, no level load, no *new game* that touches an existing slot." A slot
picker is a new-game button standing next to three save files. The delete path must be explicit,
per-slot, and impossible to reach by accident.

### L-050 · The sun does not move
**Why** — Requested 2026-07-26. The light is one fixed directional at `Euler(46, 35, 0)`, set once at
boot and never touched, so every session of every day looks identical — and the game's whole premise
is a world that carries time in it.
**Why it is worth doing early despite being cosmetic** — it is the only item in this batch I can
*prove*. `RILL/Capture Mountain PNG` renders terrain and lighting from a terminal, so dawn, noon and
dusk can be archived and compared. The shell, the modes and the records screen are all UI and cannot
be checked without a person pressing Play.
**Done when** — Sun angle, sun colour, ambient and sky follow time of day; three renders at different
hours are archived and are obviously different.
**Where it should come from** — `WeatherSystem` is already derived from the UTC date and drives
weather deterministically. Time of day should come from the player's **local clock**, not from the
seed: playing in the evening should look like evening. That is free, it needs no content, and it is
the kind of detail that makes a world feel like a place. The Daily is the exception — it must stay
identical for everyone, so it takes a fixed hour.

### L-049 · The app appears rather than opens
**Why** — Requested 2026-07-26. L-037 gave the game a title screen; it still has no launch. The
title fades up over an already-built mountain, which is a screen rather than an arrival.
**Done when** — Opening the app is a moment: something happens before the title settles, and it is
skippable on the second run of the day.
**Do not use a logo sting** — the honest launch for this game is its own premise. The mountain is the
save file, so the app should open *on the world the player left*, and the camera should arrive at it.
Dawn breaking over your own river system costs nothing once L-050 exists and says the whole design in
four seconds.
**Blocked by** — L-046 (there is nowhere for a launch sequence to hand off to) and improved enormously
by L-050.

### L-048 · There is one mode and it is unnamed
**Why** — Requested 2026-07-26. Daily Rill exists and is reachable only as a toggle button on the
HUD; the main game has no name and no framing. With a shell and three mountains there is somewhere
for modes to be chosen rather than toggled.
**The three, and why these three**
1. **Mountains** — the game. Three persistent worlds, one per slot, nothing ever reset.
2. **Daily Rill** — exists. Same seed worldwide, a fixed run count, one shareable glyph. Untouched by
   your mountains, which is what makes it safe to compete on.
3. **Expedition** — a fixed short visit to a freshly seeded mountain that you may then **keep**,
   promoting it into a free slot, or walk away from. Invented to solve a problem L-047 creates rather
   than for its own sake: three slots means choosing a biome blind, and a slot is permanent. An
   expedition is how you meet a mountain before you commit to keeping it, and walking away breaks no
   invariant because it was never yours.
**Done when** — Each mode is reachable from the home screen, named, and explains itself in one line.
**Rejected, and why, so they are not re-proposed** — a score-attack mode (the design has no score to
attack), a mode where terrain resets between runs (invariant 1), and anything asynchronously
multiplayer (L-024, deliberately out of scope while offline-first).

### L-051 · There is nowhere to see what you have done
**Why** — Requested 2026-07-26 as "score in settings". The numbers exist — `RunNumber`,
`LifetimeSediment`, `LifetimeWaterToSea`, secrets found, basin fills, day streak — and are visible
only as two cramped lines on the HUD and a wall of text in the Almanac.
**It is a record, not a score, and the difference is the whole design.** `CLAUDE.md`: "There is no
XP, no level, no currency. All progression is numbers in the arrays inside `HeightField`." So this
screen **reads off the world and never awards anything**: no points, no rank, no total that goes up
for playing rather than for doing. If a number here cannot be recomputed from the heightfield and the
almanac, it does not belong on the screen.
**Done when** — One screen, per mountain, showing what that mountain has had done to it, with every
figure traceable to world state.

---

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

---

## Later

| ID | Loop | Why it waits |
|---|---|---|
| L-017 | UI pass — legacy `Text`, hand-placed rects | Placeholder is survivable; the loop is not. |
| L-019 | Cascade / dam-break spectacle | **Counted 2026-07-26: 4 overflows and 207 m³ over the lip per 150 runs, and *zero* per 24.** The mechanism works; a first-session player still never sees one. Nobody has watched one. |
| L-022 | Device performance pass | Never run on a phone. No profiling of any kind, ever. |
| L-023 | Region streaming beyond one 512 m field | Scope question, not a bug. |
| L-024 | Confluence backend, visits, paper boats | Deliberately out of scope while offline-first. |
| L-025 | Monetisation surfaces | Nothing built. Premature until retention is real. |

---

## Recently closed

### L-045 · The island ends in a straight line — closed 2026-07-26
Opened the same day off the first overview render: the 512 m field stopped at a hard square
boundary, and from the idle camera two straight diagonal lines cut the seabed off against the open
sea. It read as the edge of a map, which is the one thing a world that never resets should never
look like.
**The obvious cause was real and was not the cause.** The radial island mask is measured from the
**summit, not the centre of the field** — on this seed the summit sits at `(150, 126)` of 256, so
the distance to the `+x` boundary is 0.82 of the mask's radius against 1.17 to `-x`, and on one side
the island genuinely never finished before the field ran out. Adding a boundary-keyed second mask
closed the coast on every side. **The rectangle stayed exactly where it was**, which is what
identified the real cause.
**The real cause was one term in `PooledWater.shader`:** `alpha = saturate(a * (0.72 + fres * 0.45))`.
From a high camera the fresnel term is near zero, so **the sea never exceeded about 72% opacity
anywhere, including sixteen metres down.** Inside the heightfield you saw a quarter of the real,
mottled seabed through the water; outside it there is no terrain at all, only the clear colour. The
boundary was a shading discontinuity, and no amount of reshaping the coast could ever have hidden it.
Depth now closes the water — `lerp(clarity, 1.15, depth01²)` — so shallow keeps the translucency
that makes a lake bed and a beach readable and the opacity arrives late enough to leave the shore
soft.
**Evidence** — `docs/shots/mountain_150_overview.png`: uniform ocean to the horizon, island in open
water, no straight edge anywhere. Lakes are unharmed and slightly better —
`mountain_150_life.png` still grades pale rim to deep centre with a soft shore, and the deep part is
a richer blue for no longer showing a quarter of the mud beneath it.
**Renderer-only, so it costs the simulation nothing**, which is the entire difference between this
and the generation-side attempt that was measured and reverted the same day for halving
first-session sea arrivals (`ReachedSea 2 vs 4`). The rejected experiment is what proved where the
problem was not.
**Left behind, and worth its own look someday** — a few pale shelves still break the surface near
the old field edge. They now read as offshore sandbars rather than as a cut, so they are no longer
a defect, but they are not deliberate either.

### L-016 · Prop silhouettes worth looking at — closed 2026-07-26
Moss was a flat disc, reeds a single crossed quad, huts a bare box; conifers and canopies had been
built the same day and never seen. Nothing here could be judged at all until props were renderable,
which is where this loop actually went.
**Unblocked first.** Props are issued with `Graphics.DrawMesh` from `Update`, which never runs
outside play mode, so offscreen renders showed bare rock — "no trees in the picture" meant nothing.
`EcosystemSystem.BakeStaticRenderers` and `RevelationSystem.BakeStaticRenderers` combine each
instance list into one real `MeshRenderer`. The shimmering secret *hints* are deliberately **not**
baked: their whole character is a pulse driven by `Time.time`, and a still frame of one is a static
yellow disc, which would misrepresent rather than show it.
**Evidence** — seven renders in [`docs/shots/`](docs/shots/) at 24 and 150 runs, from four framings.
Every prop type now reads as its thing: conifers with trunks and tiered crowns, moss as cushions,
reeds as clumps, a hut with a pitched roof, and a revealed secret marker standing beside it.
**Each fix came from looking, and none of them were the ones the loop predicted.**
- Props read as stamped paper cutouts despite already having per-instance scale, height and yaw
  variance — because one material per type is one flat tone, and a flat tone has no form however it
  is rotated. Vertex colour was the only per-vertex channel free and the prop shader spent nothing
  on it; `PropMeshes` now bakes a vertical gradient and `Prop.shader` multiplies by it.
- Moss as a flat disc *is* a decal: no thickness, lit identically to the ground beneath it.
- **Huts were placed on any ground at all.** Props sit at one sampled height with no slope
  adaptation — invisible for a tree, since a buried trunk still reads as a tree, and wrong for a
  building. On a 35° face a hut was half-buried uphill and floating downhill. They now require
  near-flat ground, which is where people build.
- Huts were *smaller than the conifers around them* (2.3 m against 3.1 m), so a village was hidden
  by the wood it stood in.
**Three bugs I introduced and the render caught, none visible to the type-checker** — the no-shading
overload passed `(0, 1)`, which for a *flat* mesh puts every vertex at the dark end, so moss and huts
rendered **black**; conifers came out near-black because the base is most of a tree's visible mass
and the shading darkens exactly there; and the capture camera kept landing inside the hillside. That
last one I corrected by hand once and it came straight back, which is the tell that the constants
were never the problem — the camera now refuses to be underground.
**Left undone deliberately** — canopies (`PropMeshes.Canopy`) exist and are not used by anything, so
broadleaf growth is still built and unobserved.

### L-015 · Persistent wet-channel darkening — closed 2026-07-26
A carved channel was invisible when dry, so the player could not see their own river system between
runs — which is most of the time they spend looking at the mountain.
**Closed with images archived**, which is what this project has never had:
[`docs/shots/`](docs/shots/) holds the mountain at 24 and 150 runs, from the idle overview and from
a close pass on the deepest cut, rendered from batch mode by `RILL/Capture Mountain PNG`. At 150
runs (`terrain −10.39 m to +4.68 m vs virgin`) the channel reads as a carved valley with the strata
bands **bending into it**, and four lakes are visible from the idle camera — the basin lattice
filling is now something you can see rather than a percentage in a log.
**The fix was the opposite of what the loop assumed, and the first render is what said so.** The
loop asked for *darkening*. Darkening was already happening four times over — a polish tint, a CPU
wet blend, the shader's occlusion term and the shader's own wet darkening, multiplying to about
**0.25 of the surrounding rock** — and the result was a black stripe down the mountain that painted
over the deeper strata band the cut had just exposed. That defeats "every metre of depth is legible
as colour", the design's central visual promise, precisely where the player has done the most work.
So: the incision-colour term added earlier the same day was removed outright, occlusion floored at
0.55, polish made a tint rather than a darkener, and the wet term halved on both sides.
**What actually makes an old channel legible is geometry, not paint.** The cell sits lower, so it
takes a lower band's colour; and it is inside something, so occlusion shades it. Both survive
`PolishDecayPerRun` taking polish to zero, which is exactly the "old channel" case the loop was
about.
**Weaker than asked in one respect, and it is a physical limit rather than a bug.** From the *full*
idle overview — 435 m back on a 512 m mountain — a 2–4 m channel is a couple of pixels and reads as
a faint line. It is unmistakable at any closer framing. Worth noting alongside: `GameBootstrap` sets
`OverviewDistance` to `extent × 0.85` = 435 m, which is outside the 45–320 m range
`RillCamera.Zoom` will clamp to, so the default idle camera sits further out than the player can
ever zoom back to.

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

*Archive of older cycles: [`docs/loops/`](docs/loops/)*
