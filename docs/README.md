# RILL — Documentation

*"Water flows downhill. Water remembers."*

A Unity implementation of the RILL design document: one-thumb steering, persistent hydraulic
erosion, and a mountain that never resets.

## Start here

**[`../OPEN-LOOPS.md`](../OPEN-LOOPS.md) is the driver** — it says what happens next and why. These
documents are reference; that one is the plan.

| Document | Read it when |
|---|---|
| **[../OPEN-LOOPS.md](../OPEN-LOOPS.md)** | Always, first. Current work, priority order, evidence. |
| **[loops/](loops/README.md)** | Something regressed and you want to know how it broke last time. |
| **[STATUS.md](STATUS.md)** | You want to know what state the project is in and what to do next. |
| **[FEATURES.md](FEATURES.md)** | You want a per-feature checklist: done / built / partial / not started. |
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | You are about to change code. |
| **[TUNING.md](TUNING.md)** | Runs feel wrong, or you are balancing anything. |
| **[VERIFICATION.md](VERIFICATION.md)** | Before you claim something works. |

## Running it

Open the project in Unity **6000.5.5f1** (Built-in Render Pipeline, no packages beyond uGUI), open
`Assets/Scenes/Rill.unity`, press Play.

The Scene view being empty is correct. `Assets/Scenes/Rill.unity` holds exactly one GameObject with
`GameBootstrap` on it; the camera, sun, terrain, water, ecosystem, UI and audio are all built at
runtime. There are no prefabs and no imported assets of any kind — every mesh, material, sound and
UI element is generated in code.

**Controls:** click to release the water · hold and lean to pull the stream sideways · release to
let it choose · drag while idle to pan.

## Editor menu

| Item | Does |
|---|---|
| `RILL → Run Headless Smoke Test` | 24 unattended runs, full stats to the Console. The most useful thing in the repo. |
| `RILL → Preview Mountain Seed` | Top-down PNG of a random seed, written beside the project. |
| `RILL → Build Scene` | Recreates the one-object scene. |
| `RILL → Open Save Folder` | Reveals the save directory. |
| `RILL → Delete Saved Mountain` | Wipes slot 0, with a confirmation. |

## Fast type-check without Unity

```bash
./tools/unity-stub/typecheck.sh
```

Checks the entire codebase in about two seconds using signature-only Unity stubs. See
[VERIFICATION.md](VERIFICATION.md).

## Layout

```
Assets/Scripts/
  Core/     HeightField (the save file), Noise, MountainGenerator, HydraulicErosion,
            BasinSystem, StrataPalette, SecretSite, MinHeap
  Flow/     FlowSimulation (the three rules), RunController (the loop), CarveReport
  Input/    ThumbInput — the entire control scheme
  Render/   TerrainMeshBuilder, WaterRibbon, PooledWaterMesh, RillCamera, SplashFX
  World/    EcosystemSystem, RevelationSystem, WeatherSystem, BiomeRules, Collectibles, PropMeshes
  Meta/     SaveSystem, Almanac, TimeLapse, DailyRill, GlyphGenerator, ProjectSystem, ConfluenceQueue
  UI/       HudController, UIFactory        Audio/  FlowAudio        App/  GameBootstrap, RillWorld, GameConfig, Haptics
Assets/Resources/Shaders/   Strata, WaterRibbon, PooledWater, Prop, Droplet
Assets/Editor/              RillEditorTools, RillSmokeTest
tools/unity-stub/           Unity API stubs + typecheck.sh
```

## Save data

`Application.persistentDataPath/rill/` — `world_0.rill` (gzipped terrain arrays, format v4),
`almanac_0.json`, `timelapse_0.bin`, `daily.json`, `confluence_queue_0.bin`, `postcard_run*.png`.

The terrain **is** the save file. Deleting it deletes the player's entire progression, because in
RILL there is no other progression to delete.
