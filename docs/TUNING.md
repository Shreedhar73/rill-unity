# RILL — Tuning

Every constant lives in `Assets/Scripts/App/GameConfig.cs`. The scene deliberately does **not**
serialise a copy, so the C# defaults are the single source of truth.

---

## The three numbers that decide whether it is a game

### Terminal speed, and the gap between fresh rock and your own channel

Terminal speed on a slope is an identity, not a feel:

```
v_terminal = Gravity · sin(θ) / drag
```

With `Gravity = 30`, on a 30° face:

| Surface | drag | terminal speed |
|---|---|---|
| Fresh rock | `DragFresh = 1.65` | ~9 m/s |
| Fully polished channel | `DragPolished = 0.42` | ~24 m/s |

That **~2.6× gap is the entire game.** It is what a carved channel buys, and it has to be large
enough that riding your own river feels like a different vehicle. Tune these two numbers from the
identity, never by eye.

> **This is where the first build failed.** `DragFresh` was 3.1 with `Gravity` 26, giving a
> terminal speed of ~2.5 m/s — below `PoolSpeedThreshold` of 0.75. Every run stalled within
> seconds. If runs are dying young, check this ratio before anything else.

### Steering — three numbers, and only one of them is about strength

`SteerSpeedCost = 0.55`. Fighting gravity must cost, because "knowing when *not* to touch" is the
only mastery the game offers. Too low and steering is free (no skill); too high and the player feels
punished for participating.

`SteerFullSpeed = 11`. The speed at which the thumb has full authority; below it, authority fades
out with speed. **You can only lean water that is already moving.** Without this, `SteerAccel`
exceeded downhill acceleration on a 30° face and a held lean could spiral the stream in place
indefinitely — traced doing exactly that for 70 of one run's 75 seconds, descending 4 m in the
process. 11 is a knee, not a taste: over 150 runs per arm, timeouts go `15 / 7 / 4 / 0 / 0` at
`7 / 9 / 10 / 11 / 12`.

`SteerAccel = 42`, and the reason it is so much larger than the 20 it used to be is that the
simulation now **discards whatever part of a lean points up the fall line**. The thumb
steers across the mountain and never up it. That makes the spiral impossible by construction rather
than by tuning, and it costs nothing anybody wanted, because nobody is trying to push water uphill
on purpose — but it also means most of the old number was being spent on climbing. Re-centred over
150 runs per arm at `20 / 30 / 42 / 56 / 70 / 90`: 42 is the largest value with **zero** timeouts.
Above it the stream can be spun in circles along a *contour*, which the fall-line rule does not
forbid (2 timeouts per 150 at 56, 6 at 70, 14 at 90).

> **The trap to avoid here.** These three numbers pull in opposite directions and it is very easy to
> trade the wrong one away. Authority *at rest* is what caused the deadlock; authority *at speed* is
> what lets a player carve a route to a basin off the incised channel. They are only separable once
> the lean cannot fight gravity at all. Sweep with `RILL → Run Headless Steering Sweep (long)` and
> `RILL → Run Headless Campaign Sweep`, and read **closest approach**, never final miss distance —
> a run that flows *past* its target scores identically to one that never arrived.

### `BasinSoakRate` — what a lake takes from a stream crossing it

`8` m³/s. The design has always said a lake with room absorbs the run; only the opposite case (a
full lake, which spills) was ever implemented, so a head entering an empty bowl sailed across the
dry floor and climbed out the far side on momentum. A drain rather than a hard capture on purpose:
being stopped dead by scenery is a punishment, whereas watching your water feed the lake you aimed
at is the point. Measured at **2,388 m³ per 150 runs** left in lakes in passing.

### `CarveRate` — how fast a mountain becomes yours

Currently `0.055` m/s at reference speed. Measured at ~90 m³ moved and 0.4–0.5 m of deepest cut per
run. Too fast and the mountain is used up in a week; too slow and nothing visibly changes.

---

## Measured behaviour

From `RILL → Run Headless Smoke Test` — 24 unattended runs with random occasional steering, seed
`20260726`, Sandstone.

| Metric | 24 runs | 150 runs | Reading |
|---|---|---|---|
| Summit height | 146 m over a 512 m base | — | Good |
| Run duration | 15–45 s | — | On target (design says 20–60 s) |
| Distance | 178 m/run | **253 m/run** | Good — was 118 / 136 before 2026-07-26 |
| Descent used | 110 m of 145 | 127 m of 141 | The run gets most of the way down the mountain |
| Sediment moved | 83 m³/run | 88 m³/run | Good |
| Reached the sea | 4 of 24 | **82 of 150** | Was 2 and 45 |
| Delivered to sea | 175 m³ | 3,447 m³ | Was 92 and 2,322 |
| Runs that never ended | **0** | **0** | Was 4 and 12–14 |
| Cumulative cut | −3.4 m | −9.4 m | Good — visible geology, and inside `GradeDepth` |
| Deposits | 1 cell over 2 m | 145 cells in 11 masses, largest 188 m² | Silt bars, not a dam |
| Top speed | 28 m/s (clamped) | 28 m/s | The polish loop works |
| Water held | 1,065 m³ | 2,962 m³ | Basin loop alive |
| Basin lattice | `0 · 27 · 0 · 0 · 74 %` | `100 · 93 · 0 · 100 · 100 %` | Four of five fill |
| Dam breaks | **0** | 4, 207 m³ over the lip | Works; invisible in a first session |
| Secrets found | 3 of 60 | 11 of 60 | A curve, not an exhausted track |
| Save round-trip | exact | exact | Good |

### The balance knife-edge

**Settled 2026-07-26.** The swing below was real, and what resolved it was not a basin constant at
all: runs were stalling instead of ending, the thumb could fight gravity, lakes with room absorbed
nothing, and four of five basins sat off the spring's drainage. With those fixed, 82 runs in 150
reach the sea *and* 2,962 m³ is held in basins, which the table below treats as mutually exclusive.
The lesson is that "reaches the sea" and "fills basins" were never opposed — both were being eaten
by the same defects. History kept for the record:

| Config | Result |
|---|---|
| No real basins | 21/24 reached sea, **0 m³ held** — no retention loop at all |
| 7 large tarns, through-flow at 75% fill | **24/24 pooled**, 0 reached sea, run distance halved |
| 5 smaller tarns, through-flow at 50% fill | **unverified** — the batch run could not start |

The target is a *mixture*: early runs mostly pool and fill basins, and as channels deepen more runs
carry through to the sea. If the smoke test shows either extreme, adjust `CarveBasins(field, N)`,
the radius/depth ranges beside it, and the `FillFraction > 0.5f` through-flow threshold in
`FlowSimulation`.

---

## Known-bad tuning

### ~~Secret discovery rate — the revelation track is dead~~ — fixed (L-010, 2026-07-26)
`0 of 60` became **3 of 60 after 24 runs and 11 of 60 after 150** — a curve, not an exhausted track,
and inside the 2–5 the loop asked for at session length. Options 1 and 2 in the original list were
the fix; the burial depths were never the problem. Kept here because the *reasoning* that led to the
wrong first two attempts is the useful part: a run polishes ~1.6% of the field, so a rate argument
made it look like a pricing problem, when the real cause was that 45 of 51 sites sat where water
never went at all. Placement, not price.

### Daily glyph reads as empty

24 runs produced a handful of marks on the 7×7 grid. The share unit has to look like something.

---

## Generation pipeline

Order matters; each step assumes the previous one.

1. **Domain-warped ridged noise** — warping bends the spines into connected ridgelines. Un-warped
   ridged noise makes a radial starburst.
2. **Thermal relax** (2 iterations, talus `0.9 × cellSize`) — talus is *per cell*, so it must scale
   with cell size or the mountain comes out a pillow. An early build capped slopes at 17°.
3. **Hydraulic erosion** (60k droplets) — the step that makes it a landscape rather than noise.
   Dendritic valleys and sharp ridges are a *record of a process*; the only honest way to get them
   is to run the process.
4. **Terrace** (3.2 m steps, strength × band hardness) — hard bands become cliffs, soft ones ledges.
5. **Normalise** to `PeakHeight`.
6. **Carve basins** (5 tarns) — erosion is a pit-*filling* process, so tarns must be cut afterwards
   or there is nowhere for water to collect and the retention loop cannot exist.
7. **Thermal relax** (1 light pass).
8. **Spawn notch** below the summit — open on the downhill side.

> Two failures worth remembering. A summit *dish* ("so rain gathers") trapped every run within 3 m
> of the spawn. And basins carved as a subtracted bump with an open downhill lip drain continuously
> and never fill — a tarn must be excavated to an absolute floor with a rim that closes all the way
> round and one low spill point.
