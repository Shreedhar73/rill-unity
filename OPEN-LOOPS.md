# RILL — Open Loops

**This file drives implementation.** Read it first, work the top open loop, close it with evidence,
then update this file. It is the only place that says what happens next.

Last updated: **2026-07-27** · Open loops: **15** · Closed this cycle: **54** (43 archived)

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

### L-069 · Rain is something the mountain receives, never something the player gives — closed 2026-07-27
The last of the 2026-07-27 batch, and the batch is done: ten loops opened this morning, ten closed
tonight, each with a headless test.
`RainShower`: a held, still finger on the idle mountain (≥0.45 s, under the 18 px pan threshold,
so neither existing verb is stolen; one shower per hold) summons 24 drops of 1.5 m³ total around
the touch point. Drops prefer polished, damp rock — the shower demonstrates the player's own
network every time — and every drop obeys invariant 6: sea, basin, or infiltration, with the
ledger returned. Rain carves nothing and is ~2% of a run, so it is affection, not an economy. The
world effects land instantly; the sparkle plays back over 2.2 s.
**Two invariant-6 holes caught by the mass-balance test before anyone saw the toy.** Half the
shower vanished: drops classified "basin" by standing water were landing on *unnamed sinks*,
where `AddWater` silently discards — reclassified as infiltration. Then a brim-full tarn clamped
its share away inside `AddWater`'s overflow path (which would also have let a petting gesture
queue a dam break) — Apply now delivers only into headroom and rain on a full lake soaks away,
with the ledger settled to what actually landed.
**Evidence** — headless rain test, 7 assertions: ledger sums to the shower exactly; all 24 drops
traced; polish under traces 0.521 vs mountain mean 0.020 — **26× channel preference**; basins gain
exactly the delivered share; damp cells 1,900 → 1,946; a shower is 2.5% of a run; total terrain
height unchanged to 1e-6. Smoke and probe after: green, 0 errors. The hold gesture and the
sparkle are unobserved — the probe cannot hold a finger on the world.

### L-068 · A finished run cannot be shown to anyone — closed 2026-07-27
`ShareCard.Render`: the run as one 1080×1350 image — the mountain top-down and hillshaded in its
own strata palette, lakes as lakes, the run's path drawn with a dark halo and start/end markers,
the record in a hand-built 5×7 pixel font. No camera, no font asset, no UI: every pixel painted by
plain code, which is why it renders identically headless and a test can prove the share contains
the run it claims to. Wired into the existing Share button alongside the clipboard glyph and the
postcard; written as `card_run{N}.png` beside the save.
**Evidence** — headless share-card test, 7 assertions: real 240-point path; 787 KB PNG decoding at
1080×1350; 698 pixels of the path colour painted; **1,231 pixels of difference between the card
with the run and the card without it** — the run is provably in the image; both text bands carry
lit pixels. `docs/shots/share_card.png` is the artifact itself, and looking at it caught what the
assertions could not: the pixel font had no % glyph, so "20% full" printed as "20 full". Fixed.
The platform share sheet (iOS/Android) is deliberately not here — files-beside-the-save is the
offline-honest version, and the share sheet belongs with the device pass (L-022).

### L-067 · The mountain is silent between runs — closed 2026-07-27
`AmbienceParams.From(field, life)` — pure, headless-testable — reads three numbers off the world:
stream murmur from polished channel fraction, birdsong from living-cell density, wind from what is
still bare (floored at 0.25: a summit with no wind sounds like a room). `FlowAudio` grew three
synth voices — heavily lowpassed murmur, twin-LFO gusting wind that never loops audibly, sparse
sine chirps with a falling slide, gated to idle — all ducked while a run is loud so the player's
own water stays the foreground instrument. Pushed on world bind, after the life field lands
(BindWorld alone briefly sang with the previous world's birds on a slot switch — caught in review,
fixed), and after every run.
**Evidence** — headless ambience test, 5 assertions: virgin mountain is stream 0.00 / birds 0.00 /
wind 1.00; played mountain with a living slope is 1.00 / 1.00 / 0.25; each parameter moves the
required direction and wind never dies. **The mix itself needs ears and has had none** — the
parameters are proven, the sound is unobserved, and that distinction is the whole reason the
parameters live in a separate pure class.

### L-066 · Nothing on the mountain has a name — closed 2026-07-27
`Landmarks.Find(world)`: gorges cut more than 2.5 m below virgin rock and fans built more than
1.5 m above it, clustered, footprint-floored (10+ cells so a pothole stays nameless), named
deterministically from the seed and the feature's deepest cell — "Shale Gorge", "Dune Fan". The
card announces a christening once ("The water has cut a name into the rock: Shale Gorge"), the
almanac keeps it, the Almanac panel lists named places deepest-first.
**Recomputed from terrain, never stored.** The save format stays untouched, and a name cannot
survive the destruction of the thing it named — if the gorge silts back up the name goes with it,
which is this game's honesty applied to sentiment. Corollary owned openly: a young feature's
deepest point wanders before the trench establishes, so a place can be re-christened once or twice
early, every name kept in the almanac — the place earning its final name.
**Evidence** — headless landmarks test, 5 assertions: a virgin mountain has no names to give; 60
runs earn 2 places, one a gorge (Shale Gorge, cut 7.4 m over 96 m²; Dune Fan, built 3.2 m over
292 m²); the same mountain names its places identically twice; every name survives a save
round-trip. Smoke and probe after wiring: green, 0 errors. The card headline in play is
unobserved, as all UI here is.

### L-065 · A paper boat to prove the network — closed 2026-07-27
`PaperBoat.Sail`: released from the same spring the runs use, no steering, no carving, no water
spent — a pure reading of the network, plain C# and deterministic. Rough virgin rock eats its
momentum; polished damp channels carry it. A brim-full tarn is part of the network (the boat
drifts across toward the spill and sails on); anything less full ends the voyage honestly, by
name: "The boat sailed 101 m and came to rest on South basin". Played back live on the ribbon
with the follow camera, tap to skip, from a Boat button in the idle row (six across now, resized
to fit the same 1000-wide band). Nothing is awarded for any of it.
**The first assertion was wrong and the failure taught the design.** "Mature carries the boat
1.5× as far" failed: the mature mountain's own lake ended the voyage at 101 m vs virgin's 82 m
aground — and resting on a lake you carved is not a worse result than stranding on open rock. The
network's grade is **speed while moving**, and that gap is enormous and the real reading:
**virgin 1.20 m/s over 68.6 s; mature 20.48 m/s over 4.9 s — 17×.**
**Evidence** — headless boat test, 4 assertions: both voyages produce a drawable path; carved
network moves the boat ≥1.3× as fast (measured 17×); no voyage runs forever; the same mountain
sails the same boat twice to 0.01 m. Probe green with the six-button row: 0 failed, 0 errors,
`play_idle.png` shows Boat in the row. The live playback is unobserved — the probe cannot tap the
world.

### L-064 · Nothing happens while you are away — closed 2026-07-27
`RillWorld.ApplyAwayDrift`: the same silt-and-dry drift that already ran silently between runs,
applied once per session after ≥6 h away (one tick per 8 h, hard-capped at 6 — a month's absence
reads as "the mountain settled", never "your channels are gone") and **measured**, so the title
can say truthfully what changed: "While you were away (2 days): 1.6 m³ of silt settled in quiet
channels, and the rock dried". Shown in the forecast slot for the one boot it exists on (it
outranks the weather), cleared the moment a run starts. Absence timestamp from
`Almanac.LastPlayedUtcTicks`, which already existed.
**Evidence** — headless away test, 5 assertions: a virgin mountain reports exactly nothing
(0.000 m³ — measured zero, not unlooked-at); quiet channels settle (1.59 m³); **the reported
number is the terrain's actual change** (reported 1.59, independently measured 1.59); wet rock
dries (153 cells); the longest possible absence returns 5.28 m³ of 746 m³ carved — under 5%.
The title line itself is unobserved in play (it needs a real absence).

### L-063 · Weather arrives unannounced — closed 2026-07-27
Weather was already deterministic from the date; the game just never said what was coming.
`WeatherSystem.KindFor(DateTime)` is the old `Evaluate` roll extracted static and pure, so the
forecast is *the same function called on tomorrow* and structurally cannot disagree with the
weather that arrives. `ForecastLine` names the next change within 24 h — "This evening: a storm —
double water" — and stays silent while nothing changes, because "Tomorrow: the same" is not an
appointment. Shown under the tagline on the title screen.
**A structural-invisibility bug caught by looking, once again** — the first version appended the
forecast to the title's record line, which `SetMountains` hides permanently the moment the slot
rows exist. Built, correct, and impossible to see, exactly the L-018 shape. It got its own element.
**Evidence** — new headless forecast test over a year of half-day windows: 730/730 forecast vs
arrival, 0 mismatches; every spoken line names the weather that then arrives and silence never
hides a change (0 lies); it spoke on 588 windows and held its tongue on 142, so both branches are
real. Probe: 0 failed, 0 errors, and the crop of `play_home.png` shows the line on screen in the
running game: "This evening: a storm — double water".

### L-062 · Daily glyphs vanish the next day — closed 2026-07-27
The rollover was where they died: `DailyRill.Load` replaced any stale `daily.json` without reading
it. `GlyphJournal` now keeps every day — updated after every daily run (not only at rollover, so a
crash before midnight loses nothing), with a rollover catch for days played on builds from before
the journal existed. Global like `daily.json` itself: the Daily belongs to the player, not to a
mountain. The Almanac panel shows the collection — day count, streak, the last two glyphs in full,
older days as one line each. The streak is computed from the entries rather than stored, so it can
never drift from the record; an unplayed *today* does not break yesterday's streak, and a missed
day just starts the count again — nothing is awarded and nothing punishes, per the design.
**Evidence** — new headless test, 12 assertions: out-of-order records come back date-sorted;
re-recording a day updates rather than duplicates; disk round-trip; streak 3 on the last played
day, still 3 on an unplayed today, 0 after a missed day; panel shows last two glyphs in full and
older days as lines. Full smoke and play probe after wiring: green, 0 failed, 0 runtime errors.
The panel rendering in play is unobserved, as all UI here is.

### L-061 · The mountain's own history is invisible — closed 2026-07-27
The loop was wrong about what was missing, in a useful way: `TimeLapsePlayer` and the HUD button
already existed and were wired — what did not exist was any observation of them working, a run
counter during playback, or a way out of it. The playback held the player hostage for the whole
archive with no caption saying what they were watching.
**Built** — during playback the hint reads "Run 42 of 201 · tap to skip", live from the frame
being shown; a tap ends it early (with a 0.4 s grace so the tap that opened the playback cannot
also skip it); and props no longer draw over the history — the probe photographed today's conifers
floating above the mountain of two hundred runs ago, because `Graphics.DrawMesh` from `Update`
does not care that the terrain under it was swapped.
**Evidence** — headless archive test (`RILL/Run Headless TimeLapse Test`), 6 assertions: three
appends read back as three frames with their run numbers; 5,291 and 2,340 cells of recorded change
between frames; the last frame reconstructs the live terrain to **0.002 m** worst error; a
truncated mid-append tail is dropped cleanly with all whole frames surviving. Play probe now
enters the playback for real: `Idle -> TimeLapse`, 3.1 s of playback, `TimeLapse -> Idle` on its
own, 0 failed, 0 runtime errors — and `docs/shots/play_timelapse.png` is the running playback
photographed from inside, caption on screen, no floating props. The tap-to-skip path is the one
thing the probe cannot drive (it clicks buttons, not the world) — unobserved.

### L-060 · The end card never says what is almost about to happen — closed 2026-07-27
`NextTeaser.For(world)`: one world-derived line on the end card about what is *almost* about to
happen — a basin near its brim with the exact m³ it still wants, a secret under thin rock, or (only
when nothing is genuinely close) a basin that sits empty. Reads the world and awards nothing; every
number is recomputable from the heightfield, so the promise and the progress can never disagree.
Computed after drift and biome rules so it cannot promise a basin the run just silted shut; never
on the Daily (its world is discarded — a promise nobody can collect on) and never for cascades.
**Two failures found by the count, both the silent-nothing kind in reverse.** First version fired
on 24 of 24 runs with the same line on 23 of them — an *untouched* secret placed shallow by
generation, a promise that never moved, about a place with no channel to it. Secrets now qualify
only once the player's water has actually cut toward them (`Virgin - Height > 0.05`). Second: ranked
purely by urgency the shallowest secret won every run and the card read as a secrets ticker, so the
basin and secret promises alternate by run number when both exist.
**Evidence** — smoke test now counts it: `next teaser on 24 of 24 runs`, lines *moving* across the
session — "Something lies 0.7 m under the rock" ×1 → 0.6 ×2 → 0.4 ×6 → 0.3 ×9 → 0.2 ×1, and "North
basin wants 173 m³ more" ×1 → "109 m³ more" ×4. Converging numbers are the difference between a
promise and wallpaper. Play probe after the card change: 0 failed, 0 runtime errors. **Firing on
every run of this seed is on notice** — if it reads as noise in play, the windows tighten.

*Archive of older cycles: [`docs/loops/`](docs/loops/)*
