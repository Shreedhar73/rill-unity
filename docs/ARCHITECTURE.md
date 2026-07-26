# RILL — Architecture

## The one idea

**The world is the save file.** There is no XP, no level, no currency. Every bit of progression the
player has is a number in one of the arrays inside `HeightField`. Read that class first; the rest
of the codebase is machinery for changing it, drawing it, and writing it to disk.

Everything else follows from that:

- Nothing resets, so there is no "level" concept anywhere and no scene reloading.
- Progression cannot be granted, only carved — no code path awards anything.
- The save is the terrain, so `SaveSystem` is load-bearing in a way a settings file never is.

## Boot

There are no prefabs and no authored scene contents. `Assets/Scenes/Rill.unity` contains exactly one
GameObject with `GameBootstrap` on it; camera, sun, terrain, water, ecosystem, UI and audio are all
constructed at runtime. This means an empty scene plus that one component is a working game, and
there is no asset to break in a merge.

```
GameBootstrap.Awake()
  ├─ BuildMaterials()          Resources/Shaders/* → Material instances
  ├─ LoadOrCreateWorld()       SaveSystem.Load, else MountainGenerator.Generate
  ├─ WeatherSystem             deterministic from UTC date
  ├─ BuildLighting/Camera/Terrain/PooledWater/Sea/Ribbon/Hud/TimeLapse
  ├─ Ecosystem + Revelation + Collectibles + SplashFX + ThumbInput
  └─ RunController.Initialise(world, almanac, daily, archive, player, confluence, weather)
```

## Namespaces

| Namespace | Folder | Responsibility |
|---|---|---|
| `Rill.Core` | `Scripts/Core` | The world data and everything pure. No MonoBehaviours. |
| `Rill.Flow` | `Scripts/Flow` | The run: simulation, state machine, carve report. |
| `Rill.App` | `Scripts/App` | Composition root, tuning, the `RillWorld` aggregate. |
| `Rill.Render` | `Scripts/Render` | Meshes, camera, effects. Reads the world, never writes it. |
| `Rill.World` | `Scripts/World` | Systems that change the world between runs. |
| `Rill.Meta` | `Scripts/Meta` | Persistence and everything outside a run. |
| `Rill.UI` / `Rill.Audio` / `Rill.InputSystem` | | Presentation and input. |

`Rill.Core` deliberately has no Unity component dependencies, which is what lets the headless smoke
test run the entire simulation in batchmode with no renderer.

## The run loop

```
Idle ──tap──► Flowing ──water stops──► Report ──dismiss──► Idle
                 │                                   │
                 └── cascade queued? ────────────────┘
```

`RunController` owns the machine. One frame of `Flowing`:

1. `ThumbInput` projects the thumb onto the plane at the stream head's elevation.
2. `FlowSimulation.Advance` steps in fixed `SimStep` sub-steps (reproducibility for time-lapse and
   Daily Rill).
3. Each sub-step: gravity → steering (and its speed cost) → drag → clamp → move → waterfall check
   → pickups → carve/deposit → polish/wetness → volume loss → end conditions.
4. Ribbon, camera and audio read the head.

On finish: rebuild basins → diff the field into a `CarveReport` → advance ecosystem → refresh
revelation → apply between-run drift → biome rules → refresh projects → persist.

## Data flow of a single carve

```
FlowSimulation.Step
   └─ HeightField.AddBrush(Height, −depth)      terrain lowered, volume returned as sediment
   └─ HeightField.AddBrush(Polish, +rate)       drag falls here next run  ← the whole economy
   └─ HeightField.AddBrush(Wet,    +rate)       ecosystem grows here later
        │
        ├─ marks a dirty rect
        ▼
TerrainMeshBuilder.LateUpdate    rebuilds only dirty chunks, ≤3 per frame
        │
        ├─ vertex colour  = strata palette + polish + wet + dye + ice, alpha = carve glow
        └─ uv             = (concavity occlusion, wetness)
                 ▼
        Strata.shader    bands computed PER PIXEL from world Y, not interpolated
```

That last line matters: at 2 m vertex spacing and ~3 m strata, vertex interpolation smears the
bands into a gradient and the mountain reads as a bedsheet. Banding must happen in the fragment
shader.

## Key invariants

Break these and the design breaks with them.

1. **Nothing clears `HeightField.Height`.** No reset, no level load, no "new game" that touches an
   existing slot.
2. **Nothing purchasable may touch the terrain.** The trust contract in the design document is
   enforceable only because topology is not sellable.
3. **Generation is deterministic.** Use `Rill.Core.Rng` and `Noise`, never `UnityEngine.Random`,
   for anything a seed must reproduce — Daily Rill compares players' results on identical rock.
4. **The simulation steps at a fixed rate.** Frame-rate-dependent carving would make runs
   irreproducible and time-lapse a lie.
5. **`Rill.Core` stays MonoBehaviour-free**, so the whole sim stays testable headlessly.
6. **Water is never destroyed silently.** It reaches the sea, seeps to a basin, or infiltrates.
   Two of this project's worst bugs were water being deleted without anyone noticing.

## Where the bodies are buried

- `BasinSystem` rebuilds from scratch after every run — basins are *derived*, per-cell water is the
  stored truth. Orphaned water is routed downhill rather than deleted.
- `RunController.BindWorld` is called both at boot and whenever the Daily mountain borrows the
  renderers. Anything subscribing to events there must unsubscribe first.
- `EcosystemSystem` holds the life array for whichever world is bound; `RunController` swaps the
  home array back when leaving the Daily.
- The Daily world is generated fresh each toggle and never saved.
