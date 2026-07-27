# RILL — Open Loops

**This file drives implementation.** Read it first, work the top open loop, close it with evidence,
then update this file. It is the only place that says what happens next.

Last updated: **2026-07-27** · Open loops: **10** · Closed this cycle: **61** (50 archived)

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

### L-014 · Sense of speed — closed 2026-07-27, on weaker evidence than asked
All three legs now exist: FOV kick and camera close-in at speed (built earlier), spray above
terminal-on-fresh-rock speed (built earlier), and now **impact on plunges** — the missing third.
On a splash over strength 0.55 the camera takes the hit (a sharp dip-and-recover, applied before
the terrain clamp so an impact can never push the camera into the ground it is reacting to, plus
a momentary FOV pop), the mix gets a 72 Hz thud under the splash's hiss, and a second wider mist
burst sells the landing. Thresholded on purpose: the patter of small drops must not shake the
camera into soup — impact that never stops is noise.
**Weaker evidence, said plainly:** "speed is legible without the HUD meter" is a claim about a
person's eyes, and none have looked. What is verified: typecheck clean, full probe green with the
impact path live in its runs (0 failed, 0 runtime errors). Legibility itself belongs to L-012's
playtest, and this reopens by the rules if speed still cannot be felt.

### L-048 · There is one mode and it is unnamed — closed 2026-07-27
The three modes are on the home screen, named, each explaining itself in one line: **Mountains**
(Begin and the three rows, as before), **Daily Rill** ("Today's rock, same for everyone · seven
runs" — enterable from the title now, not only the HUD toggle), and **Expedition** ("Meet a new
mountain · keep it or walk away").
**The expedition is the load-bearing new piece.** A fresh, unsaved world — a biome the player
lacks while any slot is free, any of the four (including Granite, which no slot default ever
offers) once all three are taken — for `ExpeditionRuns = 5` runs, then a choice panel: **Keep it**
(only when a slot is free; `Roster.Adopt`, already tested, is the single path to disk) or **Walk
away**. A visit leaves no records: no almanac, no time-lapse, no confluence, and above all no
autosave.
**The dangerous part was the saves, and it was treated as the L-054 class it is.** Four separate
sites save `Active` to `CurrentSlot` — quit, End game, pause, autosave-after-run — and every one
would have written the borrowed expedition world over the player's slot. All four now guard on
`InExpedition` as they already did on `InDaily`, and End game / Back step off the expedition at
the title's door exactly as `LeaveDaily` does.
**Evidence** — probe walks the whole visit live: Expedition and Daily Rill named on the home
screen; chosen; `state=Idle` on a world that is not the player's with `InExpedition` true; End
game pressed on it; home again with `InExpedition` false, `Active == Home`, and **slot 0 on disk
byte-identical before and after the visit** (seed checked through `ReadSummary`). 0 failed,
0 runtime errors. `play_expedition.png` is the visit itself — "Expedition · Sandstone · run 0/5".
**Unobserved** — the five-runs-then-choice flow end to end (the probe walks in and out, not
through five runs), and the Keep path in play; `Adopt`'s refusals are covered headlessly.

### L-070 · Basin names collide — three "North basin"s on one mountain — closed 2026-07-27
Opened and closed the same day, but not the same hour, and with its own test. `ReconcileNames`
runs after the whole lattice is labelled, two rules in order: a basin that still contains a
remembered deepest point **keeps its name** (a name is a handle the player has read on cards;
geometry shifting under it must not change it — and a merge keeps the larger parent's name, which
is what the Merged event reports); everything else gets its compass octant with the first unused
water-word — basin, tarn, hollow, pool, mere, lochan — so six hollows due north are six different
places. Existing saves heal on their next Rebuild since names were never persisted.
**The first fixture tested the fixture.** Three pits on a cone shared drainage routes and landed
in three octants, so the water-word chain — the actual fix — never executed and the "distinct
northern names" check passed on octant luck. Rebuilt on flat ground with all three pits due
north: `North basin · North tarn · North hollow`.
**Evidence** — 8 assertions: three same-octant pits walk the chain; names distinct; unchanged
terrain keeps every name across a rebuild; a merge is reported and the survivor carries the
larger parent's name; a played generated mountain has 6 basins all named apart. Full smoke after:
green, save round-trips, existing basin references unchanged.

### L-051 · There is nowhere to see what you have done — closed 2026-07-27
`Records.Text(world, almanac, life, secrets)` — plain C#, takes the world and nothing else, which
is the design rule made structural: if a number cannot be read off the heightfield or the almanac
it cannot appear. Seed (so the record is falsifiable), first rain, runs and play span, the m³
ledgers, deepest cut and tallest build recomputed from `Virgin - Height` at the moment of asking,
the lattice by basin name with fills, named places, secrets found-of-placed, life, day streak. No
score, no rank, no total that rises for playing rather than doing — the test asserts the words
"score", "points", "level" and "XP" appear nowhere. Reached by a Records button under the mountain
rows on the title; the panel draws over the title and leaves the state machine alone.
**Evidence** — headless records test, 8 assertions, each figure held against the array it claims
to read (deepest cut recomputed independently and matched to the printed string). Probe walks it
live: pressed on the title, panel up, `state=Title` untouched, closed, title exactly as it was —
0 failed, 0 runtime errors. `play_records.png` is the real save's record: 220 runs, 18.3 hours,
292,051 m³ moved, Shale Gorge cut 6.3 m.
**Exposed by looking, and opened as L-070** — the real save's lattice lists "South basin" twice
and "North basin" three times: compass naming collides on a 6-basin lattice, so the record (and
the teaser, and the overflow headlines) cannot say which North basin it means.

### L-052 · "Close game" was built as quit, and meant end the session — closed 2026-07-27
The button had been rebuilt as "End game" and `EndGame()` always handled the mid-run case — but it
sat in the idle row, which hides during a run, so the one thing the loop's *Done when* asked for
("someone presses it mid-run") was **structurally impossible to do**. The same shape as L-018's
gate and L-063's forecast: built, correct, and unreachable. End game now lives in its own
CanvasGroup (`ignoreParentGroups`, or the hidden row's zero alpha multiplies it invisible), shown
at rest and during the run, never over the title, report or playbacks. The probe's Visible helper
had to learn the same Unity semantics or it reported the button invisible while the screen showed
it.
**Evidence — the probe now does exactly what the Done-when says**: starts a second run, asserts
`state=Flowing`, sees End game (`EndGameHolder a=1.00` over `Buttons a=0.00`), presses it, and
lands on the main screen in one press — `Flowing -> Title`, Begin present, End game gone from the
title, mountain intact, 0 failed, 0 runtime errors. `play_endgame_midrun.png` is the live run with
the button on it.

### L-037 · The app has no front door — closed 2026-07-27
Implemented 2026-07-26; closed now because the probe proves the *Done when* verbatim: opening the
app shows a title with the game's name and a Begin button, and play starts only when the player
chooses it. Boot lands on `state=Title` after the arrival move, `RILL` + tagline + Begin + the
three mountain rows are photographed from inside the running game (`play_home.png`), and the run
state is only ever entered through the Begin click. The record line under the title has since been
superseded by the per-mountain rows (L-053) and the forecast (L-063).
**Left open deliberately, elsewhere** — "nice graphics" was part of the original request and is a
look-at-it question; nothing here closes it.

### L-036 · The run ends without a beat — closed 2026-07-27, on weaker evidence than asked
**The *Done when* asked for a person** ("someone plays and does not describe the ending as
abrupt") **and no person has played it — said plainly.** What the probe does prove: every run ends
`Flowing -> Settling`, the settle beat holds (probe measured 1.1 s before the hand-off), then
`Settling -> Report` with the card visible, and a tap skips the wait. `play_settle.png` shows the
beat itself: the ribbon fading over the framed carve with no UI over it. The mechanism is real and
observed; whether it *feels* like an ending is L-012's business, and if the playtest calls the
ending abrupt this loop reopens by the rules.

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

*Archive of older cycles: [`docs/loops/`](docs/loops/)*
