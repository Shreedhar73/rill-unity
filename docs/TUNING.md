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

### `SteerSpeedCost` — the skill ceiling

Currently `0.55`. Fighting gravity must cost, because "knowing when *not* to touch" is the only
mastery the game offers. Too low and steering is free (no skill); too high and the player feels
punished for participating.

### `CarveRate` — how fast a mountain becomes yours

Currently `0.055` m/s at reference speed. Measured at ~90 m³ moved and 0.4–0.5 m of deepest cut per
run. Too fast and the mountain is used up in a week; too slow and nothing visibly changes.

---

## Measured behaviour

From `RILL → Run Headless Smoke Test` — 24 unattended runs with random occasional steering, seed
`20260726`, Sandstone.

| Metric | Value | Reading |
|---|---|---|
| Summit height | 146 m over a 512 m base | Good |
| Run duration | 12–40 s | On target (design says 20–60 s) |
| Distance | ~110–250 m | Good |
| Sediment moved | 80–100 m³/run | Good |
| Deepest cut | 0.4–0.5 m/run | Good |
| Cumulative cut after 24 runs | −5.3 m | Good — visible geology |
| Top speed | 28 m/s (clamped) | The polish loop works |
| Water held | 1,264 m³ across basins | Basin loop alive |
| Secrets found | **0 of 60** | **Broken — see below** |
| Save round-trip | exact | Good |

### The balance knife-edge

Basin strength swung twice and has not settled:

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

### Secret discovery rate — the revelation track is dead
`0 of 60` found in 24 runs. A run polishes ~1.6% of the field; 60 sites × 1.6% ≈ one hit per 24
runs at best, and only if burial depth happens to be shallow enough. Options, roughly in order of
preference:

1. Lock placement to the drainage network rather than biasing toward it (`Concavity` currently only
   biases, and 20% of sites are still placed anywhere).
2. Reveal on *proximity* to sufficient erosion rather than requiring the exact cell.
3. Shallower burial for the common kinds (currently 0.8–6 m).
4. More sites.

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
