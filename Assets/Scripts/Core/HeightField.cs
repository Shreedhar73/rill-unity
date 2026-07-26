using System;
using UnityEngine;

namespace Rill.Core
{
    /// <summary>
    /// The mountain. Everything persistent in RILL lives in these arrays: the terrain is the
    /// save file, so this class is the single source of truth for progression.
    ///
    /// Layout is row-major, index = z * Size + x. World space is centred on the origin:
    ///   worldX = (x - Size/2) * CellSize,  worldZ = (z - Size/2) * CellSize,  worldY = Height[i].
    /// </summary>
    public sealed class HeightField
    {
        public readonly int Size;
        public readonly float CellSize;

        /// <summary>Terrain surface elevation in metres. Sea level is y = 0.</summary>
        public readonly float[] Height;

        /// <summary>0 = fresh rock (slow, high drag), 1 = polished channel bed (fast). The momentum economy.</summary>
        public readonly float[] Polish;

        /// <summary>Standing water depth in metres. Rendered as lakes; owned by BasinSystem.</summary>
        public readonly float[] Water;

        /// <summary>Recent moisture 0..1. Decays slowly. Drives the ecosystem and infiltration.</summary>
        public readonly float[] Wet;

        /// <summary>
        /// Per-cell hardness *variation* multiplier (~0.8..1.2). Effective hardness is the
        /// strata band's hardness at the cell's current elevation times this value, so a channel
        /// that cuts into a hard band genuinely gets harder to deepen. See RillWorld.HardnessAt.
        /// </summary>
        public readonly float[] Hardness;

        /// <summary>Original generated elevation, kept so the carve total of a lifetime is knowable.</summary>
        public readonly float[] Virgin;

        /// <summary>
        /// Permanent colour the player splashed into the rock from dye-flowers. Alpha is strength.
        /// Cosmetic, persistent, and entirely earned by having routed water through a flower.
        /// </summary>
        public readonly Color32[] Dye;

        /// <summary>
        /// Frozenness 0..1. Glacier country and cold weather freeze wet ground; frozen rock is
        /// far harder to carve, and melt gives it all back. State change as a fourth verb.
        /// </summary>
        public readonly float[] Ice;

        // Accumulated dirty rectangle in grid coords; consumed by the mesh builder each frame.
        public int DirtyMinX, DirtyMinZ, DirtyMaxX, DirtyMaxZ;
        public bool Dirty;

        public float SeaLevel = 0f;

        public HeightField(int size, float cellSize)
        {
            Size = size;
            CellSize = cellSize;
            int n = size * size;
            Height = new float[n];
            Polish = new float[n];
            Water = new float[n];
            Wet = new float[n];
            Hardness = new float[n];
            Virgin = new float[n];
            Dye = new Color32[n];
            Ice = new float[n];
            ClearDirty();
        }

        public int Count => Size * Size;
        public float WorldExtent => Size * CellSize;

        public int Index(int x, int z) => z * Size + x;
        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Size && z < Size;

        public Vector3 GridToWorld(int x, int z)
        {
            return new Vector3((x - Size * 0.5f) * CellSize, Height[Index(x, z)], (z - Size * 0.5f) * CellSize);
        }

        public Vector2 GridToWorldXZ(int x, int z)
        {
            return new Vector2((x - Size * 0.5f) * CellSize, (z - Size * 0.5f) * CellSize);
        }

        /// <summary>World XZ to continuous grid coordinates (may be fractional / out of range).</summary>
        public Vector2 WorldToGrid(float worldX, float worldZ)
        {
            return new Vector2(worldX / CellSize + Size * 0.5f, worldZ / CellSize + Size * 0.5f);
        }

        public int NearestIndex(float worldX, float worldZ)
        {
            Vector2 g = WorldToGrid(worldX, worldZ);
            int x = Mathf.Clamp(Mathf.RoundToInt(g.x), 0, Size - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(g.y), 0, Size - 1);
            return Index(x, z);
        }

        static float Bilinear(float[] f, int size, float gx, float gz)
        {
            gx = Mathf.Clamp(gx, 0f, size - 1.001f);
            gz = Mathf.Clamp(gz, 0f, size - 1.001f);
            int x0 = (int)gx, z0 = (int)gz;
            int x1 = Mathf.Min(x0 + 1, size - 1), z1 = Mathf.Min(z0 + 1, size - 1);
            float tx = gx - x0, tz = gz - z0;
            float a = Mathf.Lerp(f[z0 * size + x0], f[z0 * size + x1], tx);
            float b = Mathf.Lerp(f[z1 * size + x0], f[z1 * size + x1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        public float SampleHeightWorld(float worldX, float worldZ)
        {
            Vector2 g = WorldToGrid(worldX, worldZ);
            return Bilinear(Height, Size, g.x, g.y);
        }

        public float SamplePolishWorld(float worldX, float worldZ)
        {
            Vector2 g = WorldToGrid(worldX, worldZ);
            return Bilinear(Polish, Size, g.x, g.y);
        }

        public float SampleHardnessWorld(float worldX, float worldZ)
        {
            Vector2 g = WorldToGrid(worldX, worldZ);
            return Bilinear(Hardness, Size, g.x, g.y);
        }

        public float SampleWetWorld(float worldX, float worldZ)
        {
            Vector2 g = WorldToGrid(worldX, worldZ);
            return Bilinear(Wet, Size, g.x, g.y);
        }

        public float SampleWaterWorld(float worldX, float worldZ)
        {
            Vector2 g = WorldToGrid(worldX, worldZ);
            return Bilinear(Water, Size, g.x, g.y);
        }

        /// <summary>
        /// Downhill unit-ish vector in world XZ (points the way water wants to go) plus slope
        /// magnitude in metres per metre. Central differences on the bilinear field.
        /// </summary>
        public Vector2 DownhillWorld(float worldX, float worldZ, out float slope)
        {
            float e = CellSize;
            float hL = SampleHeightWorld(worldX - e, worldZ);
            float hR = SampleHeightWorld(worldX + e, worldZ);
            float hD = SampleHeightWorld(worldX, worldZ - e);
            float hU = SampleHeightWorld(worldX, worldZ + e);
            Vector2 grad = new Vector2((hR - hL) / (2f * e), (hU - hD) / (2f * e));
            slope = grad.magnitude;
            if (slope < 1e-6f) return Vector2.zero;
            return -grad / slope; // unit downhill direction
        }

        public Vector3 NormalAt(int x, int z)
        {
            int xm = Mathf.Max(x - 1, 0), xp = Mathf.Min(x + 1, Size - 1);
            int zm = Mathf.Max(z - 1, 0), zp = Mathf.Min(z + 1, Size - 1);
            float dx = (Height[Index(xp, z)] - Height[Index(xm, z)]) / ((xp - xm) * CellSize);
            float dz = (Height[Index(x, zp)] - Height[Index(x, zm)]) / ((zp - zm) * CellSize);
            return new Vector3(-dx, 1f, -dz).normalized;
        }

        // ---------------------------------------------------------------- brushes

        /// <summary>
        /// Applies a smooth radial change to a field. Returns the total volume moved (m^3),
        /// which the carve report and the sediment budget both need.
        /// </summary>
        public float AddBrush(float[] field, float worldX, float worldZ, float radiusCells, float amountAtCentre,
                              bool clamp01 = false, bool markDirty = true)
        {
            Vector2 g = WorldToGrid(worldX, worldZ);
            int r = Mathf.CeilToInt(radiusCells);
            int cx = Mathf.RoundToInt(g.x), cz = Mathf.RoundToInt(g.y);
            float inv = 1f / Mathf.Max(radiusCells, 1e-4f);
            float moved = 0f;

            for (int z = cz - r; z <= cz + r; z++)
            {
                if (z < 0 || z >= Size) continue;
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= Size) continue;
                    float dx = x - g.x, dz = z - g.y;
                    float d = Mathf.Sqrt(dx * dx + dz * dz) * inv;
                    if (d >= 1f) continue;
                    // smoothstep falloff: no visible brush edges in the strata
                    float w = 1f - d * d;
                    w *= w;
                    int i = z * Size + x;
                    float delta = amountAtCentre * w;
                    float before = field[i];
                    float after = clamp01 ? Mathf.Clamp01(before + delta) : before + delta;
                    field[i] = after;
                    moved += Mathf.Abs(after - before);
                }
            }

            if (markDirty) MarkDirty(cx - r, cz - r, cx + r, cz + r);
            return moved * CellSize * CellSize;
        }

        public float SampleIceWorld(float worldX, float worldZ)
        {
            Vector2 g = WorldToGrid(worldX, worldZ);
            return Bilinear(Ice, Size, g.x, g.y);
        }

        /// <summary>
        /// Paints permanent colour into the rock. Dye accumulates rather than replacing, so a
        /// channel run through the same flower for weeks saturates instead of flickering.
        /// </summary>
        public void AddDye(float worldX, float worldZ, float radiusCells, Color color, float strength)
        {
            Vector2 g = WorldToGrid(worldX, worldZ);
            int r = Mathf.CeilToInt(radiusCells);
            int cx = Mathf.RoundToInt(g.x), cz = Mathf.RoundToInt(g.y);
            float inv = 1f / Mathf.Max(radiusCells, 1e-4f);

            for (int z = cz - r; z <= cz + r; z++)
            {
                if (z < 0 || z >= Size) continue;
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= Size) continue;
                    float dx = x - g.x, dz = z - g.y;
                    float d = Mathf.Sqrt(dx * dx + dz * dz) * inv;
                    if (d >= 1f) continue;
                    float w = 1f - d * d;
                    w *= w;

                    int i = z * Size + x;
                    Color32 cur = Dye[i];
                    float a = cur.a / 255f;
                    float add = Mathf.Clamp01(strength * w);
                    float newA = Mathf.Clamp01(a + add);
                    // Blend toward the new hue proportionally to how much of it we just added.
                    float t = newA > 1e-4f ? add / newA : 1f;
                    Color blended = Color.Lerp(new Color32(cur.r, cur.g, cur.b, 255), color, t);
                    Dye[i] = new Color32((byte)(blended.r * 255f), (byte)(blended.g * 255f), (byte)(blended.b * 255f), (byte)(newA * 255f));
                }
            }
            MarkDirty(cx - r, cz - r, cx + r, cz + r);
        }

        public void MarkDirty(int minX, int minZ, int maxX, int maxZ)
        {
            if (!Dirty)
            {
                DirtyMinX = minX; DirtyMinZ = minZ; DirtyMaxX = maxX; DirtyMaxZ = maxZ;
                Dirty = true;
                return;
            }
            if (minX < DirtyMinX) DirtyMinX = minX;
            if (minZ < DirtyMinZ) DirtyMinZ = minZ;
            if (maxX > DirtyMaxX) DirtyMaxX = maxX;
            if (maxZ > DirtyMaxZ) DirtyMaxZ = maxZ;
        }

        public void MarkAllDirty() => MarkDirty(0, 0, Size - 1, Size - 1);

        public void ClearDirty()
        {
            Dirty = false;
            DirtyMinX = DirtyMinZ = int.MaxValue;
            DirtyMaxX = DirtyMaxZ = int.MinValue;
        }

        public void CopyHeightTo(float[] dst) => Array.Copy(Height, dst, Height.Length);
    }
}
