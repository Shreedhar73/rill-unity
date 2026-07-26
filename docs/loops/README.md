# Loop archive

Closed loops, moved out of [`OPEN-LOOPS.md`](../../OPEN-LOOPS.md) once its **Recently closed**
section passes ~10 entries.

## Why keep them

The archive is not sentiment. It is the project's memory of **how things actually broke**, which is
the only defence against fixing the same class of bug twice. Three of the entries already here
describe systems that silently did nothing while appearing to work — that pattern is now something
this project knows to look for, and it only knows because the evidence was written down at the time.

Read the archive when:

- A metric moves and you cannot explain why — something here probably touches it.
- You are about to tune a constant — check whether it was already tuned and why.
- Behaviour regresses — find the loop that originally closed it and what evidence closed it.

## Format

One file per cycle: `YYYY-MM.md`. Append closed loops in reverse chronological order, keeping the
original ID, date and **Evidence** verbatim. Never edit a closed loop's evidence — if it turns out
to be wrong, open a *new* loop that says so and cross-reference it. The record of a wrong conclusion
is more useful than a quietly corrected one.

## Files

| Cycle | Loops | Theme |
|---|---|---|
| *(none yet)* | | The first eight loops are still in `OPEN-LOOPS.md` under **Recently closed**. |
