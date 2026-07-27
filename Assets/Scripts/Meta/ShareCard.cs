using System.Collections.Generic;
using UnityEngine;
using Rill.App;
using Rill.Core;
using Rill.Flow;

namespace Rill.Meta
{
    /// <summary>
    /// The run as one image: the mountain from above, the path the water took, and the record in
    /// pixels — composed by hand into a Texture2D, no camera, no fonts, no UI. That constraint is
    /// the point: with every pixel painted by plain code, the card renders identically headless,
    /// so a test can prove the share actually contains the run it claims to.
    ///
    /// This is the artifact that leaves the device. The report card is the game's proudest moment
    /// and it could not be shown to anyone; Wordle is the proof that when the artifact is small
    /// and personal, the share IS the acquisition strategy.
    /// </summary>
    public static class ShareCard
    {
        public const int Width = 1080;
        public const int Height = 1350;

        static readonly Color32 Paper = new Color32(18, 22, 28, 255);
        static readonly Color32 Ink = new Color32(235, 240, 244, 255);
        static readonly Color32 InkDim = new Color32(150, 160, 170, 255);
        static readonly Color32 PathCol = new Color32(140, 214, 238, 255);

        /// <summary>Composes the card and returns the PNG bytes.</summary>
        public static byte[] Render(RillWorld world, List<Vector3> runPath, string titleLine, string recordLine)
        {
            var px = new Color32[Width * Height];
            for (int i = 0; i < px.Length; i++) px[i] = Paper;

            // --- the mountain, top-down, hillshaded. The map square sits centred with margins
            // for the text bands above and below.
            const int MapSize = 920;
            int mapX = (Width - MapSize) / 2;
            int mapY = 230;
            var field = world.Field;
            float extent = field.WorldExtent;
            Vector3 light = new Vector3(-0.55f, 0.72f, -0.42f).normalized;

            for (int y = 0; y < MapSize; y++)
            {
                for (int x = 0; x < MapSize; x++)
                {
                    float wx = (x / (float)MapSize - 0.5f) * extent;
                    float wz = (y / (float)MapSize - 0.5f) * extent;
                    float h = field.SampleHeightWorld(wx, wz);

                    Color32 c;
                    if (h <= field.SeaLevel + 0.02f)
                    {
                        c = ToC32(StrataPalette.SeaColor * 0.9f);
                    }
                    else
                    {
                        Color baseCol = StrataPalette.ColorAt(world.Bands, h);
                        // Hillshade from central differences — the same idea the terrain shader
                        // uses, cheap enough to run per pixel here.
                        float e = field.CellSize;
                        float dx = field.SampleHeightWorld(wx + e, wz) - field.SampleHeightWorld(wx - e, wz);
                        float dz = field.SampleHeightWorld(wx, wz + e) - field.SampleHeightWorld(wx, wz - e);
                        Vector3 nrm = new Vector3(-dx / (2f * e), 1f, -dz / (2f * e)).normalized;
                        float shade = Mathf.Clamp01(Vector3.Dot(nrm, light)) * 0.75f + 0.25f;
                        // Standing water reads as its lake, not as the rock under it.
                        if (field.SampleWaterWorld(wx, wz) > 0.15f)
                            baseCol = Color.Lerp(StrataPalette.SeaColor, baseCol, 0.25f);
                        c = ToC32(baseCol * shade);
                    }
                    px[(mapY + y) * Width + (mapX + x)] = c;
                }
            }

            // --- the run's path, drawn over the relief with a soft dark halo so it reads on any
            // rock colour. This is the part of the image that is *this* run and nobody else's.
            if (runPath != null && runPath.Count > 1)
            {
                for (int i = 1; i < runPath.Count; i++)
                {
                    Vector2Int a = MapPoint(runPath[i - 1], extent, mapX, mapY, MapSize);
                    Vector2Int b = MapPoint(runPath[i], extent, mapX, mapY, MapSize);
                    DrawLine(px, a, b, new Color32(10, 14, 18, 255), 5);
                }
                for (int i = 1; i < runPath.Count; i++)
                {
                    Vector2Int a = MapPoint(runPath[i - 1], extent, mapX, mapY, MapSize);
                    Vector2Int b = MapPoint(runPath[i], extent, mapX, mapY, MapSize);
                    DrawLine(px, a, b, PathCol, 3);
                }
                // Where it began, where it ended.
                DrawDisc(px, MapPoint(runPath[0], extent, mapX, mapY, MapSize), 9, Ink);
                DrawDisc(px, MapPoint(runPath[runPath.Count - 1], extent, mapX, mapY, MapSize), 9, PathCol);
            }

            // --- the words. RILL up top; the run's record under the map.
            PixelFont.Draw(px, Width, "RILL", Width / 2, Height - 120, 10, Ink, centred: true);
            if (!string.IsNullOrEmpty(titleLine))
                PixelFont.Draw(px, Width, titleLine.ToUpperInvariant(), Width / 2, 150, 5, Ink, centred: true);
            if (!string.IsNullOrEmpty(recordLine))
                PixelFont.Draw(px, Width, recordLine.ToUpperInvariant(), Width / 2, 90, 4, InkDim, centred: true);

            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }

        static Vector2Int MapPoint(Vector3 world, float extent, int mapX, int mapY, int mapSize)
        {
            int x = mapX + Mathf.RoundToInt((world.x / extent + 0.5f) * mapSize);
            int y = mapY + Mathf.RoundToInt((world.z / extent + 0.5f) * mapSize);
            return new Vector2Int(Mathf.Clamp(x, 0, Width - 1), Mathf.Clamp(y, 0, Height - 1));
        }

        static Color32 ToC32(Color c) => new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255), 255);

        static void DrawLine(Color32[] px, Vector2Int a, Vector2Int b, Color32 c, int thickness)
        {
            int dx = Mathf.Abs(b.x - a.x), dy = Mathf.Abs(b.y - a.y);
            int steps = Mathf.Max(dx, dy);
            if (steps == 0) { DrawDisc(px, a, thickness / 2, c); return; }
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));
                DrawDisc(px, new Vector2Int(x, y), thickness / 2, c);
            }
        }

        static void DrawDisc(Color32[] px, Vector2Int at, int r, Color32 c)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y > r * r) continue;
                    int ix = at.x + x, iy = at.y + y;
                    if (ix < 0 || iy < 0 || ix >= Width || iy >= Height) continue;
                    px[iy * Width + ix] = c;
                }
        }
    }

    /// <summary>
    /// A 5×7 pixel font, drawn by hand so the card needs no font asset and renders headless.
    /// Uppercase, digits, and the few marks the record lines use. Anything else prints as a
    /// small box, which is visible in a test rather than silently absent.
    /// </summary>
    public static class PixelFont
    {
        const int W = 5, H = 7;

        // Each glyph is 7 rows of 5 bits, top row first, MSB left.
        static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
        {
            {'A', new byte[]{0x0E,0x11,0x11,0x1F,0x11,0x11,0x11}},
            {'B', new byte[]{0x1E,0x11,0x11,0x1E,0x11,0x11,0x1E}},
            {'C', new byte[]{0x0E,0x11,0x10,0x10,0x10,0x11,0x0E}},
            {'D', new byte[]{0x1E,0x11,0x11,0x11,0x11,0x11,0x1E}},
            {'E', new byte[]{0x1F,0x10,0x10,0x1E,0x10,0x10,0x1F}},
            {'F', new byte[]{0x1F,0x10,0x10,0x1E,0x10,0x10,0x10}},
            {'G', new byte[]{0x0E,0x11,0x10,0x17,0x11,0x11,0x0E}},
            {'H', new byte[]{0x11,0x11,0x11,0x1F,0x11,0x11,0x11}},
            {'I', new byte[]{0x0E,0x04,0x04,0x04,0x04,0x04,0x0E}},
            {'J', new byte[]{0x07,0x02,0x02,0x02,0x02,0x12,0x0C}},
            {'K', new byte[]{0x11,0x12,0x14,0x18,0x14,0x12,0x11}},
            {'L', new byte[]{0x10,0x10,0x10,0x10,0x10,0x10,0x1F}},
            {'M', new byte[]{0x11,0x1B,0x15,0x15,0x11,0x11,0x11}},
            {'N', new byte[]{0x11,0x19,0x15,0x13,0x11,0x11,0x11}},
            {'O', new byte[]{0x0E,0x11,0x11,0x11,0x11,0x11,0x0E}},
            {'P', new byte[]{0x1E,0x11,0x11,0x1E,0x10,0x10,0x10}},
            {'Q', new byte[]{0x0E,0x11,0x11,0x11,0x15,0x12,0x0D}},
            {'R', new byte[]{0x1E,0x11,0x11,0x1E,0x14,0x12,0x11}},
            {'S', new byte[]{0x0F,0x10,0x10,0x0E,0x01,0x01,0x1E}},
            {'T', new byte[]{0x1F,0x04,0x04,0x04,0x04,0x04,0x04}},
            {'U', new byte[]{0x11,0x11,0x11,0x11,0x11,0x11,0x0E}},
            {'V', new byte[]{0x11,0x11,0x11,0x11,0x11,0x0A,0x04}},
            {'W', new byte[]{0x11,0x11,0x11,0x15,0x15,0x1B,0x11}},
            {'X', new byte[]{0x11,0x11,0x0A,0x04,0x0A,0x11,0x11}},
            {'Y', new byte[]{0x11,0x11,0x0A,0x04,0x04,0x04,0x04}},
            {'Z', new byte[]{0x1F,0x01,0x02,0x04,0x08,0x10,0x1F}},
            {'0', new byte[]{0x0E,0x11,0x13,0x15,0x19,0x11,0x0E}},
            {'1', new byte[]{0x04,0x0C,0x04,0x04,0x04,0x04,0x0E}},
            {'2', new byte[]{0x0E,0x11,0x01,0x02,0x04,0x08,0x1F}},
            {'3', new byte[]{0x1E,0x01,0x01,0x0E,0x01,0x01,0x1E}},
            {'4', new byte[]{0x02,0x06,0x0A,0x12,0x1F,0x02,0x02}},
            {'5', new byte[]{0x1F,0x10,0x1E,0x01,0x01,0x11,0x0E}},
            {'6', new byte[]{0x06,0x08,0x10,0x1E,0x11,0x11,0x0E}},
            {'7', new byte[]{0x1F,0x01,0x02,0x04,0x08,0x08,0x08}},
            {'8', new byte[]{0x0E,0x11,0x11,0x0E,0x11,0x11,0x0E}},
            {'9', new byte[]{0x0E,0x11,0x11,0x0F,0x01,0x02,0x0C}},
            {' ', new byte[]{0x00,0x00,0x00,0x00,0x00,0x00,0x00}},
            {'.', new byte[]{0x00,0x00,0x00,0x00,0x00,0x0C,0x0C}},
            {',', new byte[]{0x00,0x00,0x00,0x00,0x0C,0x04,0x08}},
            {'·', new byte[]{0x00,0x00,0x00,0x0C,0x0C,0x00,0x00}},
            {'—', new byte[]{0x00,0x00,0x00,0x1F,0x00,0x00,0x00}},
            {'-', new byte[]{0x00,0x00,0x00,0x0E,0x00,0x00,0x00}},
            {'³', new byte[]{0x0C,0x02,0x04,0x02,0x0C,0x00,0x00}},
            {'/', new byte[]{0x01,0x02,0x04,0x04,0x08,0x10,0x00}},
            {'%', new byte[]{0x19,0x1A,0x02,0x04,0x08,0x0B,0x13}},
            {':', new byte[]{0x00,0x0C,0x0C,0x00,0x0C,0x0C,0x00}},
        };
        static readonly byte[] Unknown = { 0x1F, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1F };

        public static int Measure(string text, int scale) => text.Length * (W + 1) * scale;

        public static void Draw(Color32[] px, int texWidth, string text, int x, int y, int scale,
                                Color32 colour, bool centred = false)
        {
            if (centred) x -= Measure(text, scale) / 2;
            int texHeight = px.Length / texWidth;
            foreach (char ch in text)
            {
                byte[] rows;
                if (!Glyphs.TryGetValue(ch, out rows)) rows = Unknown;
                for (int r = 0; r < H; r++)
                {
                    byte bits = rows[r];
                    for (int c = 0; c < W; c++)
                    {
                        if ((bits & (1 << (W - 1 - c))) == 0) continue;
                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                            {
                                // Row 0 is the glyph's top: screen y grows upward here, so flip.
                                int ix = x + c * scale + sx;
                                int iy = y + (H - 1 - r) * scale + sy;
                                if (ix < 0 || iy < 0 || ix >= texWidth || iy >= texHeight) continue;
                                px[iy * texWidth + ix] = colour;
                            }
                    }
                }
                x += (W + 1) * scale;
            }
        }
    }
}
