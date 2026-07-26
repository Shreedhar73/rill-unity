# RILL — Verification

What has actually been checked, how to check it yourself, and — importantly — what none of it
proves.

---

## Three levels, in order of speed

### 1. Type-check without Unity — ~2 seconds

```bash
./tools/unity-stub/typecheck.sh
```

`tools/unity-stub/` contains signature-only stubs of every Unity API this game touches — no
behaviour, just shapes. A plain C# compiler (`mono`, i.e. `brew install mono`) then checks the
whole codebase in three configurations:

- runtime, **touch** input path
- runtime, **mouse** path (`UNITY_EDITOR` defined) — otherwise half the input code is never checked
- editor tools

Run this constantly. It catches typos, wrong overloads, bad conversions and dead fields without
booting the editor or taking the project lock.

**It cannot check:** shaders, anything about runtime behaviour, or any Unity API whose stub happens
to be wrong. When the stub disagrees with Unity, the stub is wrong — fix `tools/unity-stub/` and
note it.

### 2. Compile in Unity, headless — ~1 minute

```bash
/Applications/Unity/Unity-6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -nographics -projectPath . -logFile /tmp/rill.log
grep -E "error CS|Shader error" /tmp/rill.log | sort -u
```

This is the real compiler, and it also compiles shaders.

> **The editor must be closed.** Unity locks the project directory; a batch run against an open
> project exits silently after ~28 log lines with no error message. If the log is tiny and ends at
> `Package Manager Server process was shutdown`, that is what happened.

### 3. Headless smoke test — ~1 minute

```bash
/Applications/Unity/Unity-6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -nographics -projectPath . \
  -executeMethod Rill.EditorTools.RillSmokeTest.RunHeadless -logFile /tmp/rill.log
sed -n '/=== RILL headless/,/daily glyph/p' /tmp/rill.log
```

Or, with the editor open: **RILL → Run Headless Smoke Test**, output in the Console.

**RILL → Run Headless Smoke Test (long)** plays 150 runs instead of 24. Use it for anything about
basins: 24 runs move ~1.6k m³ of rock against 5.6k m³ of basin capacity, so a 24-run result cannot
tell "basins never spill" from "basins have not filled yet" — and those need opposite fixes.

It generates a mountain, plays 24 unattended runs with occasional random steering, and reports
endings, sediment, distance, top speed, basin lattice with fill percentages, secrets revealed,
terrain delta versus virgin, a save/load round-trip check, and the Daily glyph.

This works because `Rill.Core` and `Rill.Flow` contain no MonoBehaviours — the entire simulation
runs with no renderer.

---

## Why the smoke test earns its keep

Every genuine bug found so far was found by it, and none were visible by reading the code:

| Symptom in the output | Actual bug |
|---|---|
| every run `Pooled` after 0.8 s, 3 m travelled | Summit "dish" trapped the spawn; terminal speed below the pool threshold; talus not scaled to cell size |
| `water held 0 m³`, `fullest basin 0.0%` | `AddWater` discarded the run's volume whenever the stream stopped outside a depression — i.e. usually |
| basin count and capacity *identical* across a change | `CarveBasins` was silently placing zero basins on every seed; rejection sampling on slope can never pass on terraced ground |
| basins carved but capacity unchanged | Tarns had an open downhill lip, so they drained continuously and could never fill |
| lakes emptying between runs | `GatherExistingWater` deleted water in cells that were no longer inside a labelled basin |
| 9 of 14 basins pinned at exactly `0%` forever | `LabelBasins` labelled depressions *below sea level* as basins. They cannot be routed to, and they inflated reported capacity from the real 5,591 m³ to 16,712 m³ |
| a through-flow branch with a healthy step count and no effect | Steering the head toward the spill cell at 0.35g loses to terrain gravity on the rim it must climb. 1,187 sub-steps, zero runs carried out — the counter proved it *ran*, not that it *worked* |

The pattern worth internalising: **a system that silently does nothing looks exactly like a system
that works.** Print counts of things you believe you created.

Two corollaries, both learned the expensive way on 2026-07-26:

- **A counter proves execution, not effect.** `ThroughFlowSteps` was non-zero the whole time the
  branch was useless. Count the *outcome* (runs carried out), not the *visit*.
- **Check the harness before tuning the game.** Two of the three causes of `24/24 Pooled` were not
  in the simulation at all: one was in basin labelling, one was in the smoke-test bot, which
  re-rolled a random steer target every second and therefore could not route water anywhere. A bot
  that cannot express intent cannot test a mechanic that exists to reward it.
- **The bot must be able to do what the mechanic rewards.** Three separate "the simulation is
  broken" conclusions in the basin work were harness flaws: sub-sea-level basins, a bot that could
  not steer, and a bot that could not *persist*. The last one hid the answer completely — filling an
  off-channel basin takes a campaign of roughly 36 aimed runs, and a bot that picked a fresh random
  target every run could never spend them. It reported "3 of 5 basins are unreachable" for as long
  as it existed.

---

## What none of this proves

- **That the game is fun.** The design document's own M3 kill criterion is a 20-person playtest
  producing unprompted "one more run" behaviour. Nothing here speaks to that.
- **That it looks right.** Shaders compile; that is not the same as legible strata.
- **That it sounds right.** The procedural audio has never been heard by anyone.
- **That it performs.** Never run on a phone. No profiling of any kind at any time.
- **That the meta systems work.** Time-lapse playback, Daily Rill, the Almanac and the Confluence
  queue compile and are wired, but have never been exercised end to end.

---

## Regression checklist before calling anything done

1. `./tools/unity-stub/typecheck.sh` — clean, including warnings.
2. Batch compile — no `error CS`, no `Shader error`.
3. Smoke test — sane spread of endings, basins at a range of fill levels, save round-trips exactly.
4. Press Play — water leaves the notch, carves a visible channel, the carve report is not empty.
5. Delete the save (`RILL → Delete Saved Mountain`) and confirm a fresh mountain generates.
