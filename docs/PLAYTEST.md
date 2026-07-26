# Playtest protocol — the M3 kill criterion

This exists to answer one question honestly, and it is the only question in this repo that no
measurement can answer:

> **Does an ordinary person, unprompted, start another run?**

The design document sets this as an explicit kill criterion: if playtesting does not produce
unprompted "one more run" behaviour, **redesign before art exists**. Every number in
`docs/STATUS.md` is a proxy for that, and proxies have already been wrong several times in this
project — three separate "the simulation is broken" conclusions turned out to be flaws in the test
harness, not the game.

A playtest that is run badly is worse than none, because it produces a confident wrong answer. Hence
the rules below.

---

## The one rule: do not sell the game

The result is worthless if the tester is trying to be nice, or is playing the game you described
rather than the game that exists.

**Do not say**, before or during:

- anything about the mountain remembering, carving, persistence, or "nothing resets" — **discovering
  that the mountain keeps your channel is the entire game**, and telling them replaces the discovery
  with a chore
- anything about basins, secrets, ecosystems, or progression
- what they should aim for, or that reaching the sea is good
- that you made it, or that you want them to like it
- "just one more" / "try again" — *any* prompt to replay voids the primary result

**Do say**, once, if they look stuck for more than ~20 seconds:

> "Tap to let the water go. Hold and drag to lean it."

That is the whole control scheme and withholding it tests onboarding (**L-018**, known missing), not
the loop. Say it once, flatly, and then stop talking.

If they ask a question, the answer is: *"Whatever you think."* Write the question down — questions
are data about what the game fails to communicate.

---

## Setup

1. Open `Assets/Scenes/Rill.unity` and press **Play**. Nothing exists in the scene until
   `GameBootstrap` builds it at runtime, so the Game view is bare skybox until you do.
2. Start from a **virgin mountain**. On the `GameBootstrap` component in the scene, tick
   **`Reset World On Play`** (it deletes `Save Slot` on entering Play), or point `Save Slot` at an
   unused number. The world is the save file: a mountain with 150 runs of channels already carved is
   a different game from a virgin one, and first-session feel is what the criterion is about.
   **Untick it again afterwards** — left on, it wipes the slot every time anyone presses Play.
3. Hand it over. Sit where you can see the screen and their face. Say nothing else.
4. Start a stopwatch. Do not mention it.

---

## What to record

Write it down *during*, not afterwards from memory.

```
Tester (initials, age bracket, plays mobile games?):
Date:
Fresh slot:  yes / no

Runs completed:                        ← the primary number
Stopped because:  bored / interrupted / asked to stop / ran out of time
Unprompted replays:  did they start run N+1 without any word from you?   yes / no
First unprompted replay happened at run #:
Total time on device:

Verbatim quotes — anything said out loud, good or bad:
  -
  -

Where they got confused (and at which run):
  -

What they did that surprised you:
  -

Did they ever notice the mountain had changed?  yes / no
  If yes: at roughly which run, and what did they say?
  (Do NOT ask them. Only record it if it comes from them unprompted.)

Did they aim at anything on purpose?  yes / no
  What?

Their one-sentence answer to "what is this game?" — asked ONLY at the very end:
```

---

## How to read the result

| Outcome | What it means |
|---|---|
| 20+ runs, unprompted replays throughout | The loop compels. Proceed to art and polish. |
| 20+ runs but every replay needed a nudge | **Kill criterion not met.** Politeness, not compulsion. |
| Stops under 10 runs, bored | **Kill criterion not met.** Redesign the loop, not the art. |
| Stops early but frustrated, not bored | Probably controls or legibility. Check the confounds below before blaming the loop. |

**A negative result is the valuable one.** It is cheaper to learn this now than after art exists,
which is the entire reason the design document puts the criterion at M3. Record it plainly and do
not soften it — `OPEN-LOOPS.md` has a rule about this, and a flattering playtest write-up is the one
kind of evidence this project cannot recover from.

---

## Known confounds — do not mistake these for the loop failing

All of these are already open loops. If the tester complains about one, that is expected and is
*not* evidence against the core mechanic:

| They say | It is | Loop |
|---|---|---|
| "I can't tell how fast I'm going" | True; 24 m/s looks like 9 m/s | L-014 |
| "The mountain looks empty / the trees are cones" | True; props are placeholder solids | L-016 |
| "I don't know what I'm supposed to do" | True; nothing explains the first 30 seconds | L-018 |
| "I can't see where my old rivers went" | True; a dry channel is invisible | L-015 |
| "The buttons look rough" | True; UI is legacy `Text` in hand-placed rects | L-017 |
| Nothing dramatic ever happens with lakes | Expected; a basin needs ~50 runs to fill and spill | L-029 |
| They find nothing buried | Expected rate is ~3 finds per 24 runs | L-010 |

The thing that would be **real** evidence against the design is different from all of the above: the
water goes down the hill, and they do not care that it does.

---

## After

Paste the filled-in record into the **L-012** close entry in `OPEN-LOOPS.md`, verbatim, including
the parts that sting. Then open loops for whatever it exposed.
