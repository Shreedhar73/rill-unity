using System;
using UnityEngine;
using Rill.Core;

namespace Rill.App
{
    /// <summary>
    /// Every tuning number in RILL, in one place. The whole design lives or dies on the
    /// carve -> speed -> reach loop, so those constants are grouped and commented first.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        [Header("World")]
        public int Size = 256;
        public float CellSize = 2.0f;
        public float PeakHeight = 150f;
        public Biome Biome = Biome.Sandstone;
        public uint Seed = 20260726u;

        [Header("Flow — the momentum economy")]
        // Terminal speed on a slope is Gravity * sin(theta) / drag. These numbers are chosen from
        // that identity, not by feel: on a 30° face fresh rock settles near 9 m/s and a fully
        // polished channel near 24 m/s. That ~2.6x gap IS the game — it is what a carved channel
        // buys you, and it has to be large enough to feel like a different vehicle.
        [Tooltip("Downhill acceleration per unit slope. Higher = the mountain feels steeper than it looks.")]
        public float Gravity = 30f;
        [Tooltip("Drag on fresh rock. This is the tax the player is paying off, run after run.")]
        public float DragFresh = 1.65f;
        [Tooltip("Drag inside a fully polished channel. The reward for having carved.")]
        public float DragPolished = 0.42f;
        public float MaxSpeed = 28f;
        public float StartSpeed = 1.5f;
        [Tooltip("Simulation step. Fixed so runs are reproducible for time-lapse and Daily Rill.")]
        public float SimStep = 1f / 90f;

        [Header("Steering — restraint is the skill ceiling")]
        [Tooltip("Lateral acceleration at full thumb offset. Much larger than the 20 it used to be, " +
                 "because the simulation now discards whatever part of the lean points up the fall " +
                 "line — the thumb steers across the mountain and never up it — so a hard lean is " +
                 "spent turning rather than climbing, and the old number bought almost no turning " +
                 "at all once the uphill half was gone.\n\n" +
                 "42 measured over 150 runs per arm against 20 / 30 / 42 / 56 / 70 / 90. It is the " +
                 "largest value with zero timeouts: above it the stream can be spun in circles " +
                 "along a contour, which the fall-line rule does not forbid (2 per 150 at 56, 6 at " +
                 "70, 14 at 90). Closest approach to an aimed basin over a session is 41 m against " +
                 "111 m before any of this, so the player routes water *better* than they used to, " +
                 "not worse.\n\n" +
                 "Known cost, recorded in L-040 rather than tuned away: 56 is the value at which a " +
                 "sustained campaign can fill basin #0, which is not reachable downhill from the " +
                 "spring. Buying that with steering costs first-session sea arrivals (6 → 2 over " +
                 "24 runs) and brings timeouts back. Four of five basins on this seed cannot be " +
                 "reached downhill at all, and that is a generation problem wearing a tuning " +
                 "problem's clothes.")]
        public float SteerAccel = 42f;
        [Tooltip("Thumb offset (metres) at which steering saturates. Close = fine, far = hard lean.")]
        public float SteerRange = 22f;
        [Tooltip("Fraction of speed bled per second at full steer. Fighting gravity must cost.")]
        public float SteerSpeedCost = 0.55f;
        [Tooltip("Speed at which the thumb has full authority. Below it, steering fades out with " +
                 "speed: you can only lean water that is already moving. Without this, SteerAccel " +
                 "(20) exceeds downhill acceleration on a 30° face (30·sin30° = 15), so a held " +
                 "lean could spiral the stream in place indefinitely — traced doing exactly that " +
                 "for 70 of one run's 75 seconds, descending 4 m in the process. It also makes " +
                 "speed mean two things instead of one: reach, and control.\n\n" +
                 "11 is the knee measured over 150 runs per arm: timeouts go 15 / 7 / 4 / 0 / 0 at " +
                 "7 / 9 / 10 / 11 / 12, so it is the most authority the player can keep while a " +
                 "held lean is still incapable of stopping the descent. Closest approach to an " +
                 "aimed basin is unchanged from unscaled steering (59 m vs 59 m), so this costs " +
                 "nothing in routing — only in the ability to fight the mountain to a draw.")]
        public float SteerFullSpeed = 11f;

        [Header("Carving")]
        public float CarveRate = 0.055f;         // metres per second at reference speed/volume
        public float CarveReferenceSpeed = 12f;
        public float DepositRate = 2.2f;         // sediment settling rate when slow
        public float SedimentCapacity = 0.22f;   // per unit speed*volume
        public float BrushRadiusCells = 1.35f;
        public float BrushRadiusPerVolume = 0.9f;
        public float PolishRate = 0.55f;         // per second of fast flow
        public float MaxCarvePerStep = 0.05f;    // safety clamp; no spikes, ever
        [Tooltip("Metres below virgin rock at which carving stops. Runs converge on one line and " +
                 "that line carves, which is rule 2 working — but unbounded it drilled 23.7 m in " +
                 "150 runs and the sink basin's capacity GREW while filling. A real river reaches " +
                 "a graded profile instead of cutting forever; this is that floor.")]
        public float GradeDepth = 14f;

        [Header("Volume")]
        public float StartVolume = 60f;          // m^3 of water in a normal run
        public float StormVolumeMultiplier = 2f;
        public float InfiltrationRate = 1.6f;    // m^3/s lost into dry ground
        public float MinVolume = 2f;             // run ends below this
        [Tooltip("m³/s a basin with headroom takes from a stream passing over it. The design has " +
                 "always said a lake with room absorbs the run; the code only ever implemented the " +
                 "opposite case (a full lake, which spills). A head entering an empty bowl simply " +
                 "sailed across the dry floor and climbed out the far side on momentum — measured, " +
                 "8 of the 15 aimed runs that reached their target basin left it again. This is a " +
                 "drain rather than a hard capture on purpose: being stopped dead by scenery is a " +
                 "punishment, whereas watching your stream feed the lake you aimed at is the point.")]
        public float BasinSoakRate = 8f;

        [Header("Run end")]
        [Tooltip("Below this speed the water is considered to have stopped. Must sit well under " +
                 "terminal speed on the gentlest slope worth flowing down, or runs end in the first seconds.")]
        public float PoolSpeedThreshold = 0.5f;
        [Tooltip("How long it must stay stopped. Long enough to survive snagging on one rough cell.")]
        public float PoolDwellTime = 0.9f;
        public float MaxRunSeconds = 75f;
        public float SeaMargin = 0.4f;           // metres above sea level that still counts as arrival

        [Header("World memory")]
        [Tooltip("Metres of silt an abandoned channel recovers per run. Geology's respawn: gentle, thematic, slow.")]
        public float HealingPerRun = 0.006f;
        public float WetDecayPerRun = 0.04f;
        public float PolishDecayPerRun = 0.012f;

        [Header("Ecosystem")]
        public float LifeMoistureThreshold = 0.35f;
        public int LifeTierRunSpacing = 6;

        [Header("Presentation")]
        public float CameraHeight = 46f;   // follow height above the stream
        public float CameraDistance = 62f; // follow distance behind it
        public float CameraPitch = 42f;
        public bool ShowCarveOverlay = true;

        public float WorldExtent => Size * CellSize;
    }
}
