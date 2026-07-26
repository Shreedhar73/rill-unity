# RILL — Open Loops

**This file drives implementation.** Read it first, work the top open loop, close it with evidence,
then update this file. It is the only place that says what happens next.

Last updated: **2026-07-26** · Open loops: **15** · Closed this cycle: **43** (30 archived)

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

### L-052 · "Close game" was built as quit, and meant end the session
**Why** — L-046 shipped a "Close game" button that called `Application.Quit`. The request was for a
control that **ends the current game and returns to the main screen**. L-046's evidence is not wrong
— the 18 navigation assertions and the invariant-6 fix all still hold — so this is a separate loop
rather than an edit to a closed one.
**Fixed 2026-07-26, unobserved.** The button is now "End game" and sits on the mountain rather than
on the main screen, because "take me back" is meaningless on the screen it takes you back to. It
aborts any run in flight through the same path that leaves the run's water on the mountain, saves,
clears queued cascades and any held report, and goes home. `Application.Quit` survives only on
Android's hardware back at the root, reachable from no button at all.
**Done when** — Someone presses it mid-run and lands on the main screen with their mountain intact.
**Worth keeping in view** — this is the second requirement in this batch I inferred rather than
asked about; the first was reading "score in settings" as a records screen, which was a deliberate
call against the design's no-score rule and stands. This one was simply wrong, and the tell was that
I wrote a paragraph justifying iOS quit-button policy for a feature nobody had asked for.

**Order for this batch, and the reasoning.** L-046 first because it is the container: back, quit,
three mountains and three modes all need a level above the run loop that does not exist yet.
**Closed 2026-07-26.** Then
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
**Started 2026-07-26.** `MountainRoster.Adopt` exists and is tested: it writes a world that already
exists in memory into an **empty** slot, refusing an occupied one by the same no-overwrite rule as
`Create`, and refusing a null world rather than writing a corrupt slot. That is the load-bearing half
— keeping an expedition is a new path into the one class that can destroy six months of play.
**Still to do** — the expedition itself (a run-limited visit to an unsaved world), and putting the
three modes on the home screen as named choices rather than a HUD toggle.
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

### L-057 · The game did not boot, and every green test said it did — closed 2026-07-26
Re-reported after the L-053–L-056 fixes: "that Begin option is not there... you are fixing the
things where we are not reaching." **Correct on both counts.** `MountainRoster` was constructed in
a `RunController` *field initializer*; its constructor reads save headers from disk, and Unity
forbids `persistentDataPath` there — so it threw during `AddComponent`, silently killing every
field initializer after that line. `_projects`, `_lastPath` and `_cascades` were null, `Initialise`
died before `EnterTitle`, and every `FinishRun` threw. The visible game: no title, no Begin, no
settle beat, no report — booted straight onto a mountain whose runs could start and never finish.
**Why every test lied.** The typecheck, the smoke test, navigation, mountains and both capture
tools were all green throughout, because none of them construct the MonoBehaviour — the captures
are *staged photographs* that call the same setters the game calls, which verifies the setters and
says nothing about whether the running game reaches them. L-053's evidence (`ui_home.png`) was
exactly such a photograph: not wrong about the layout, wrong as proof the game showed it.
**The fix for the class, not the instance** — `RILL/Run Play-Mode Probe`: real play mode, real
boot, real button wiring, walks home → Begin → run → sea → settle → report → dismiss → Back →
home, screenshots each step from inside the live game, counts every runtime exception, exits
nonzero on any failure. Its first run found this loop, L-058, and the SplashFX boot error — none
visible to any existing check.
**Evidence** — probe before: `RUNTIME ERROR: get_persistentDataPath is not allowed... 'RunController'`,
`FAIL boots to the title, state=Idle`, thousands of NREs. Probe after: **18 ok, 0 failed, 0 runtime
errors**, and `docs/shots/play_*.png` are the running game photographed from inside — Begin on the
title over the player's real mountain, the settle beat, the report card with the run's ribbon, and
the L-056 shoreline trees regrowing at the lake in `play_report.png`.

### L-058 · Back from the mountain stranded the player on a dead screen — closed 2026-07-26
Found by the probe on the same first run. `Navigator.FinishLaunch` existed, was exercised by the
headless navigation test, and was called by **nothing in the game** — the test calls it by hand,
which is the same staged-photograph gap as L-057. The Navigator sat on `Launch` forever; the first
Back from the mountain popped to Launch, fell into `ShowScreen`'s panel branch, and left the player
on a screen with no back button and no title. Entering the title now finishes the launch, because
the title is the home screen.
**Evidence** — probe before: `FAIL Back returns to the main screen, state=Panel`. After: the full
loop ends back on the title with Begin visible, 18 ok. Also fixed on the way, found by the probe's
error counter: `SplashFX` set duration on an already-playing ParticleSystem and threw on every
boot since the day it was written.

### L-053 · The start button vanished from the main screen — closed 2026-07-26
Reported that evening, verbatim: "the start button is gone." `SetMountains` hid the Begin button the
moment the three slot rows existed, on the theory that the rows replace it. They do not — three stat
lines are a place to switch mountains, not an obvious way to start the game. Begin is back as the
primary control, the rows sit below it.
**Evidence** — `docs/shots/ui_home.png`: Begin above the three rows, no overlap, both capture
asserts still passing.

### L-054 · Backing out of the Daily could save it over a mountain — closed 2026-07-26
Found while investigating the same evening's reports, not reported itself, and the worst of the
batch. Back and End game both returned to the main screen with `InDaily` still set and `Active`
still the borrowed daily world; `SwitchToMountain` then cleared the flag *before* its save guard
read it, so picking a mountain after backing out of the Daily wrote the throwaway daily world over
the player's slot — invariant 1, one tap sequence away. It had not happened to the real save
(verified: slot 0's seed and record are the player's own), but nothing prevented it.
`LeaveDaily()` is now the only way off the daily world and runs at the title's door, so anything on
the main screen is the player's own mountain by construction. Also: world and almanac now save
together on pause and quit — the real slot 0 carries five phantom runs (178–182, world at 182,
almanac ending at 177) from mid-run editor stops that saved one and not the other.
**Closed on the code path, said plainly** — the sequence spans three MonoBehaviour handlers and
cannot be driven headlessly. 18/18 navigation and 16/16 mountains assertions still pass.

### L-055 · The dam break stole the run's ending — closed 2026-07-26
Reported as: "previously the whole path it took was shown before the dialog." The L-036 settle beat
still ran, but on a mature mountain the North basin overflows every couple of runs, and the
cascade-before-report ordering let the dam break re-aim the camera at its own deepest cut and
overwrite the ribbon with its own path — so the beat built to show the player their run showed them
the mountain's instead. The player's own slot is exactly such a mountain; its almanac logs overflow
after overflow, which is why *every* ending looked wrong to them.
When the held report is handed back, the ribbon is restored to the player's path and the camera
re-framed on their deepest cut. Cascade-first ordering stays: consequence, then the card.
**Unobserved in play**, like all UI here.

### L-056 · A full basin drowned the entire visible forest — closed 2026-07-26
Reported as: "the trees are gone." Forensics on the real save found them: **128 living cells in the
whole world, every one at tier 6.0, every one under the brim-full North basin** — the life indices
match the standing-water indices exactly. The moisture rule counted submerged cells as maximally
wet, so growth concentrated inside the basin; `RebuildInstances` rightly refuses to draw props
underwater; the basin filling to the brim (almanac: runs 169, 173, 176) sank everything visible in
one evening, while land life elsewhere had starved as the channels dried between runs.
Now: cells under more than 0.25 m of standing water lose life — a filled tarn drowns what grew in
it — and cells *adjacent* to standing water count as fully moist with the lake bonus, so the growth
goes to a ring at the waterline the player can actually see.
**Evidence** — measured at 24 runs, same seed, before → after: visible props up (moss 110→120,
reeds 103→109) while living cells fall 1,496→1,338, which is the drowned invisible cells dying and
the shore replacing them. `docs/shots/mountain_24_life.png` shows the ring: a conifer stand and
reeds on the lake edge. On the damaged save the shoreline regrows from the next run played — moss
in ~2 runs, trees in ~11. The sunken tier-6 grove is left to drown honestly rather than restored by
hand.

### L-049 · The app appears rather than opens — closed 2026-07-26
L-037 gave the game a title screen; it still had no launch. The title faded up over an already-built
mountain, which is a screen rather than an arrival.
**No logo sting, on purpose.** The honest opening for this game is its own premise: the mountain *is*
the save file, so the app opens on the world the player left and the camera travels to it. A
returning player watches their own river system resolve out of the distance before anything is asked
of them.
**Built** — the camera starts 2.6× out and 2.2× up and eases to the title framing over 3.4 s. The
easing is cubic and explicit rather than left to the existing exponential damping, because
exponential damping never quite lands: an approach still creeping when the player reaches for the
screen reads as drift rather than as a destination.
**It plays exactly once.** Every later return to the main screen — Back, End game, switching
mountains — cuts straight to the framing, because an arrival you sit through every time is an
obstacle. A tap skips it from the first frame, since the second time somebody sees an opening they
have already arrived.
**Unobserved.** Nobody has watched it, and the thing most likely to be wrong is the duration: 3.4 s
is a guess, and the difference between an arrival and a wait is about a second either way.

### L-050 · The sun does not move — closed 2026-07-26
One fixed directional at `Euler(46, 35, 0)`, set once at boot and never touched, so every session of
every day looked identical — in a game whose premise is a world that carries time in it.
**Evidence** — four hours archived in [`docs/shots/`](docs/shots/): `hour_07`, `hour_13`, `hour_20`,
`hour_23`, same mountain and framing, obviously different. `DayCycle` is plain C# so the capture tool
can ask for any hour, which was the whole argument for doing this loop out of order — it is the only
item in the batch that can be proven from a terminal.
**Two design calls** — elevation peaks at **62°, not 90°**, because an overhead sun lights every face
equally and erases exactly the strata terracing this game spends all its shading on; and time of day
comes from the player's **local clock**, not the seed, so opening the game in the evening opens it at
evening. The Daily overrides to a fixed hour, since everyone competing on one seed must see the same
mountain.
**Three things the renders caught that reasoning had not.**
1. **Warm orange does not survive as a flat sky.** The camera clear colour is one fill; a dusk-orange
   one turned the whole top of the screen muddy brown. Warmth belongs on the *light*, where it falls
   across the terracing and reads as morning.
2. **The sea did not darken at night.** The water shader is unlit by design — its colour comes from
   depth, not a normal — so at midnight the ocean was full daytime blue beside a dark mountain.
   `SkyState.SurfaceTint` is now a global the water multiplies by.
3. **Twilight did not exist.** The band ran −8° to +12° of elevation, about half an hour of real
   time, so **19:30 rendered as a pixel-identical copy of midnight**. Rendered both, compared,
   widened it.
**Unobserved in play**, like everything else here — `SkyDriver` applies it live, damped so switching
in and out of the Daily reads as time passing rather than as a glitch, and nobody has watched that.

### L-047 · One mountain, forever, and no way to have another — closed 2026-07-26
`SaveSystem` had taken a `slot` argument since it was written and nothing ever passed anything but 0.
The almanac, the time-lapse archive and the confluence queue were all per-slot already. The plumbing
existed and there was no way to reach it.
**Evidence — 14 headless assertions** (`RILL/Run Headless Mountains Test`), including both refusals
exercised against a real occupied slot rather than a fixture. `SaveSystem.ReadSummary` reads the
header only — it sits ahead of the terrain arrays, so the gzip stream is pulled about sixty bytes
rather than several megabytes, because drawing a three-slot menu must not deserialise three mature
worlds.
**The guards, which are the actual work.** A slot picker is a new-game button standing next to three
save files, so the rules live in `MountainRoster` rather than in the UI: `Create` refuses an occupied
slot outright, with no overwrite path and **no force flag, because a force flag is a thing a future
caller passes `true` to**; `Delete` requires the *seed* of the mountain being deleted, which a caller
can only know by having read that slot's summary — so it is impossible to destroy a mountain you have
not looked at, or the wrong one via a stale index. There is no delete control on the main screen at
all.
**Three real hazards found and closed while wiring it.**
1. `RunController` had six calls to `SaveSystem.Save`; two had quietly kept the defaulted `slot = 0`,
   so ending a session on mountain 3 would have written **mountain 3 over mountain 1**. The fix is
   not the two call sites — `slot` no longer has a default on `Save` or `Load`, so that entire class
   of bug is a compile error rather than a lost world.
2. `GameBootstrap.ResetWorldOnPlay` is a serialised bool that deletes somebody's mountain — the exact
   shape invariant 1 forbids, one mis-click in the inspector away. Now compiled out of player builds
   and it names the slot in a warning on its way past.
3. `SwitchToMountain` is mostly about ordering: the mountain being left is written to disk **before**
   anything is rebound, because every later step overwrites the live world.
**Unobserved, as all UI here is.** The roster, the guards and the switching are tested; whether three
rows on the main screen read well on a phone is a look-at-it question. The biome for a new slot is
chosen as "whichever of Sandstone / Glacier / Volcanic no slot has yet", so filling all three gives
three different games without a menu — that is a design call nobody has played.

### L-046 · The app is one screen with no way out of it — closed 2026-07-26
Everything hung off `RunController`, which booted into a title and then never left the mountain: no
home, no back, no quit, and no level above the run loop for a second mountain or a second mode.
**Evidence — 18 assertions, run headlessly** (`RILL/Run Headless Navigation Test`). The state machine
is a plain class with no Unity types in it, which is the point rather than tidiness: UI cannot be
checked from a terminal, and L-018 is what happens when the only thing that could catch a broken gate
is a person pressing Play. Navigation has more ways to strand a player than onboarding did — the
launch is *replaced* rather than pushed so no Back can re-enter it, Back from a panel opened on a
mountain returns to the mountain rather than Home, pushing the screen you are already on is not two
screens deep, and Back at the root asks to quit only where the platform allows it.
**It found a real bug on the way, which is why the mid-run case was worth modelling.** Back must not
unwind the screen out from under a live simulation, so `Navigator` refuses to move and asks for the
run to be abandoned first. Following that: `FlowSimulation.Abort()` called
`Finish(Abandoned, deliverVolume: false)`, and `Finish` only routed water for `Pooled` and
`TimedOut`. **Abandoned fell through both branches and zeroed `Head.Volume`** — invariant 6, the one
this project has already broken twice. It survived because `Abort()` had *no callers at all*; a back
button is the first thing that would ever have called it. Proven fixed: `56.2 m³` in the head,
basins `0 → 56 m³` held.
**Two platform decisions, made rather than deferred** — Android's hardware back raises the same
action as the button (without it the OS wins and closes the app mid-run), and **Close game ships
everywhere except iOS**, whose guidelines are explicit that an app must not offer to close itself.
Absent there rather than present and inert.
**Also caught by the toolchain, and worth recording** — the screen enum was called `Screen`, which
shadows `UnityEngine.Screen` for every file in `Rill.App`; `GameBootstrap.Screen.sleepTimeout`
stopped compiling immediately. That is the *good* version of that mistake. And the quit path has no
`UnityEditor` branch on purpose: a runtime assembly referencing `UnityEditor` compiles in the editor
and breaks the player build, guarded or not — the stub toolchain compiles runtime with
`UNITY_EDITOR` defined and no `UnityEditor` reference, which is exactly a player build's shape.
**The UI half is unobserved**, as all UI here is. The state machine is tested; whether the back
button is in the right place on a phone is a look-at-it question. The home screen is currently the
existing title screen; giving it something to choose between is L-047.

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

*Archive of older cycles: [`docs/loops/`](docs/loops/)*
