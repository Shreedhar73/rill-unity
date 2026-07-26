# CLAUDE.md — RILL (Unity)

## Start every session here

**Read [`OPEN-LOOPS.md`](OPEN-LOOPS.md) first. It drives implementation.**

It holds the current work in priority order, what was closed recently, and what evidence closed it.
Work the top loop under **Now**. If it is blocked, say so and take the next one. Do not invent work
that is not a loop — add it as a loop first, so the reason it matters is written down.

When you finish something:

1. Verify it (see **Verification** below).
2. Move the loop to **Recently closed** in `OPEN-LOOPS.md` with the date and the actual evidence.
3. Promote a loop from **Next** into **Now**, and add any new loops the work exposed.
4. If **Recently closed** exceeds ~10 entries, archive the oldest into `docs/loops/YYYY-MM.md`
   (see [`docs/loops/README.md`](docs/loops/README.md)).

Keep `docs/STATUS.md` and `docs/FEATURES.md` in step when a loop changes what is true.

## What this project is

A mobile game where you steer water down a mountain and the mountain never forgets. The design
document is `../RILL-game-design.md`. Three rules: water flows downhill; flowing water carves, and
carved paths attract future water; **nothing ever resets**.

**The world is the save file.** There is no XP, no level, no currency. All progression is numbers in
the arrays inside `Core/HeightField.cs`. Read that class before changing anything.

## Verification — evidence, not intention

"It compiles" proves nothing. `docs/VERIFICATION.md` has the detail; the short version:

```bash
./tools/unity-stub/typecheck.sh     # whole codebase, 3 build configs, ~2 s, no editor needed
```

Then, with the **editor closed** (it holds the project lock — a batch run against an open project
exits silently after ~28 log lines, which looks like a crash and is not):

```bash
/Applications/Unity/Unity-6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -nographics -projectPath . \
  -executeMethod Rill.EditorTools.RillSmokeTest.RunHeadless -logFile /tmp/rill.log
sed -n '/=== RILL headless/,/daily glyph/p' /tmp/rill.log
```

The smoke test plays 24 unattended runs and reports endings, sediment, distances, the basin lattice,
secrets, save round-trip and the Daily glyph. **Every real bug in this project so far was found by
it, and none were visible by reading the code.** Run it after any change to simulation, generation
or persistence.

And after any change to how the game *looks*, render it — note the missing `-nographics`:

```bash
/Applications/Unity/Unity-6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . \
  -executeMethod Rill.EditorTools.RillCapture.Capture -logFile /tmp/cap.log
```

Writes `docs/shots/*.png` using the game's own shaders and mesh builders. **With `-nographics`,
`Camera.Render` produces nothing and reports no error** — the tool checks for a graphics device and
refuses rather than writing a blank image. Props, the ribbon and spray are absent by construction
(`Graphics.DrawMesh` from `Update` never runs outside play mode), so their absence proves nothing.

### The failure mode this project keeps hitting

**A system that silently does nothing looks exactly like a system that works.** `CarveBasins` placed
zero basins on every seed for three iterations; the only tell was a capacity number that did not
change. Two separate code paths deleted the player's water without a trace.

So: if a change is supposed to create or move something, **print the count**, and read it before
believing the change landed. Identical numbers across a code edit mean the edit did nothing.

## Invariants — breaking these breaks the design

1. **Nothing clears `HeightField.Height`.** No reset, no level load, no "new game" that touches an
   existing slot.
2. **Nothing purchasable may touch terrain.** The design's trust contract is enforceable only
   because topology is not sellable.
3. **Generation is deterministic.** Use `Rill.Core.Rng` and `Noise`, never `UnityEngine.Random`, for
   anything a seed must reproduce — Daily Rill compares players on identical rock.
4. **The simulation steps at a fixed rate.** Frame-rate-dependent carving makes runs irreproducible
   and time-lapse a lie.
5. **`Rill.Core` stays MonoBehaviour-free**, so the whole simulation stays testable headlessly.
6. **Water is never destroyed silently.** It reaches the sea, seeps to a basin, or infiltrates.

## Conventions

- **Tuning lives in one place**: `App/GameConfig.cs`. The scene deliberately does not serialise a
  copy. Derive flow constants from the terminal-speed identity in `docs/TUNING.md` rather than by
  eye — that identity is what the first broken build violated.
- **No prefabs, no imported assets.** Everything — meshes, materials, UI, audio — is built in code.
  `Assets/Scenes/Rill.unity` holds one GameObject with `GameBootstrap` on it.
- **Comments explain why, not what.** Especially where a value was chosen to fix a specific
  observed failure; say what failed.
- Built-in Render Pipeline, Unity 6000.5.5f1. Shaders live in `Assets/Resources/Shaders/` so
  `Resources.Load` finds them in a build.

## Honesty rules

- Do not describe something as working until it has been observed working. `docs/FEATURES.md`
  distinguishes **Done** (exercised) from **Built** (compiles, never observed) — respect that split.
- Report measured numbers, including bad ones. A retune that made things worse is information.
- If a loop is closed on weaker evidence than its *Done when* asked for, say so in the close entry.
- Never edit a closed loop's evidence. If it turns out wrong, open a new loop that says so.

## Git flow 
Do proper commits always, and all commits should be authored by me, dont add co authered by claude tag