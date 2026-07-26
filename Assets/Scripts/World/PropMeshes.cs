using System.Collections.Generic;
using UnityEngine;

namespace Rill.World
{
    /// <summary>
    /// Every prop in RILL is generated in code. No purchased assets, no artist bottleneck on the
    /// ecosystem, and the whole build stays tiny enough to ship as an offline game.
    /// </summary>
    public static class PropMeshes
    {
        /// <summary>
        /// A low clustered cushion for moss. The flat disc it replaces read as a decal — a green
        /// sticker lying on the rock — because it had no thickness and therefore caught the light
        /// identically to the ground it sat on. Three overlapping domes of slightly different
        /// height give it a lumpy edge and a top that lights differently from its sides, which is
        /// the whole difference between ground cover and a coloured patch.
        /// </summary>
        public static Mesh Cushion(float radius, int segments = 7)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            for (int lobe = 0; lobe < 3; lobe++)
            {
                float ang = lobe / 3f * Mathf.PI * 2f;
                float off = radius * 0.42f;
                var centre = new Vector3(Mathf.Cos(ang) * off, 0f, Mathf.Sin(ang) * off);
                float r = radius * (0.62f - lobe * 0.06f);
                float h = radius * (0.46f - lobe * 0.07f);

                int apex = verts.Count;
                verts.Add(centre + new Vector3(0f, h, 0f));
                int ring = verts.Count;
                for (int i = 0; i < segments; i++)
                {
                    float a = (i / (float)segments + lobe * 0.13f) * Mathf.PI * 2f;
                    verts.Add(centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }
                for (int i = 0; i < segments; i++)
                {
                    tris.Add(apex);
                    tris.Add(ring + (i + 1) % segments);
                    tris.Add(ring + i);
                }
            }
            return Build("Cushion", verts, tris, 0.58f, 1.06f);
        }

        /// <summary>
        /// A clump of reeds rather than one blade. A single crossed quad is a plant; reeds grow in
        /// stands, and a stand is what the player sees at the water's edge. Five blades at mixed
        /// heights and lean, which costs twenty triangles and is the difference between a marsh and
        /// a scattering of green ticks.
        /// </summary>
        public static Mesh ReedClump(float width, float height)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            for (int i = 0; i < 5; i++)
            {
                // Deterministic spread; no RNG, because generation must stay reproducible.
                float a = i * 2.399f;                       // golden angle, so they never line up
                float rad = width * (0.5f + i * 0.55f);
                var at = new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad);
                float h = height * (0.62f + (i % 3) * 0.19f);
                float lean = width * 0.35f * ((i % 2 == 0) ? 1f : -1f);

                int b = verts.Count;
                verts.Add(at + new Vector3(-width * 0.35f, 0f, 0f));
                verts.Add(at + new Vector3(width * 0.35f, 0f, 0f));
                verts.Add(at + new Vector3(width * 0.12f + lean, h, 0f));
                verts.Add(at + new Vector3(-width * 0.12f + lean, h, 0f));
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
            return Build("ReedClump", verts, tris, 0.56f, 1.12f);
        }

        /// <summary>
        /// A hut: walls plus a pitched roof. The bare box it replaces was the one prop that read as
        /// a programmer placeholder from any distance, because nothing in nature or architecture is
        /// a cuboid with a flat top. The ridge is what makes it a dwelling.
        /// </summary>
        public static Mesh Hut(Vector3 size)
        {
            float x = size.x * 0.5f, z = size.z * 0.5f;
            float wall = size.y * 0.62f;
            float ridge = size.y;

            var verts = new List<Vector3>
            {
                new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z), new Vector3(x, 0f, z), new Vector3(-x, 0f, z),
                new Vector3(-x, wall, -z), new Vector3(x, wall, -z), new Vector3(x, wall, z), new Vector3(-x, wall, z),
                // Ridge line, overhanging the walls a little so the roof casts an edge.
                new Vector3(0f, ridge, -z * 1.12f), new Vector3(0f, ridge, z * 1.12f)
            };
            var tris = new List<int>
            {
                0,2,1, 0,3,2,                 // floor
                0,1,5, 0,5,4,                 // walls
                1,2,6, 1,6,5,
                2,3,7, 2,7,6,
                3,0,4, 3,4,7,
                4,5,8, 5,9,8,                 // roof: two slopes and two gables
                6,7,9, 7,8,9,
                5,6,9, 4,8,7
            };
            // Walls only lightly shaded: a hut is small and a steep gradient turns it to mud.
            return Build("Hut", verts, tris, 0.74f, 1.06f);
        }

        public static Mesh Disc(float radius, int segments = 8)
        {
            var verts = new List<Vector3> { Vector3.zero };
            var tris = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            for (int i = 0; i < segments; i++)
            {
                tris.Add(0);
                tris.Add(1 + (i + 1) % segments);
                tris.Add(1 + i);
            }
            // Flat, so no vertical gradient is possible; a flat disc of moss is fine as one
            // tone because it is read as ground cover rather than as an object.
            return Build("Disc", verts, tris);
        }

        public static Mesh Blade(float width, float height)
        {
            // Two crossed quads: reads as a tuft from any angle, costs 4 triangles.
            var verts = new List<Vector3>();
            var tris = new List<int>();
            AddQuad(verts, tris, new Vector3(-width, 0f, 0f), new Vector3(width, 0f, 0f), height);
            AddQuad(verts, tris, new Vector3(0f, 0f, -width), new Vector3(0f, 0f, width), height);
            // A blade is dark where it leaves the mud and pale at the tip.
            return Build("Blade", verts, tris, 0.45f, 1.10f);
        }

        public static Mesh Cone(float radius, float height, int segments = 6)
        {
            var verts = new List<Vector3> { new Vector3(0f, height, 0f) };
            var tris = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            for (int i = 0; i < segments; i++)
            {
                tris.Add(0);
                tris.Add(1 + i);
                tris.Add(1 + (i + 1) % segments);
            }
            return Build("Cone", verts, tris, 0.50f, 1.05f);
        }

        /// <summary>
        /// A conifer: three stacked skirts on a short trunk. A single open cone — which is what
        /// this was — reads as a flat paper triangle from the side, because there is only one
        /// silhouette edge and nothing to catch light differently at different heights. Stacked
        /// tiers give it a profile and a bit of self-shadowing for nothing.
        /// </summary>
        public static Mesh Conifer(float radius, float height, int segments = 7)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            // Trunk: a stubby prism so the tree meets the ground rather than floating on a point.
            float trunkR = radius * 0.13f;
            float trunkH = height * 0.18f;
            int trunkBase = verts.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
                verts.Add(new Vector3(cx * trunkR, 0f, cz * trunkR));
                verts.Add(new Vector3(cx * trunkR, trunkH, cz * trunkR));
            }
            for (int i = 0; i < segments; i++)
            {
                int a0 = trunkBase + i * 2, a1 = a0 + 1;
                int b0 = trunkBase + ((i + 1) % segments) * 2, b1 = b0 + 1;
                tris.Add(a0); tris.Add(a1); tris.Add(b0);
                tris.Add(b0); tris.Add(a1); tris.Add(b1);
            }

            // Three skirts, each narrower and shorter than the one below it.
            const int Tiers = 3;
            float tierBase = trunkH * 0.75f;
            float remaining = height - tierBase;
            for (int t = 0; t < Tiers; t++)
            {
                float f = t / (float)Tiers;
                float r = radius * (1f - f * 0.55f);
                float y0 = tierBase + remaining * f * 0.62f;
                float y1 = y0 + remaining * (0.52f - f * 0.10f);

                int apex = verts.Count;
                verts.Add(new Vector3(0f, y1, 0f));
                int ring = verts.Count;
                for (int i = 0; i < segments; i++)
                {
                    float a = (i / (float)segments + t * 0.17f) * Mathf.PI * 2f;   // twist each tier
                    verts.Add(new Vector3(Mathf.Cos(a) * r, y0, Mathf.Sin(a) * r));
                }
                for (int i = 0; i < segments; i++)
                {
                    tris.Add(apex);
                    tris.Add(ring + i);
                    tris.Add(ring + (i + 1) % segments);
                }
            }
            // Shade under the skirts, full colour at the crown: that contrast is the whole reason
            // a conifer reads as a tree rather than a green triangle. 0.62 and not 0.42 — at the
            // lower value the trees rendered as near-black silhouettes, because the base is most of
            // a conifer's visible mass and that is exactly where this darkens.
            return Build("Conifer", verts, tris, 0.62f, 1.08f);
        }

        /// <summary>
        /// A rounded canopy for broadleaf growth: two stacked rings capped top and bottom, so a
        /// bush is a mass rather than a spike.
        /// </summary>
        public static Mesh Canopy(float radius, float height, int segments = 8)
        {
            var verts = new List<Vector3> { new Vector3(0f, 0f, 0f) };
            var tris = new List<int>();

            int lower = verts.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, height * 0.42f, Mathf.Sin(a) * radius));
            }
            int upper = verts.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments + 0.5f / segments) * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius * 0.62f, height * 0.80f, Mathf.Sin(a) * radius * 0.62f));
            }
            int top = verts.Count;
            verts.Add(new Vector3(0f, height, 0f));

            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                tris.Add(0); tris.Add(lower + i); tris.Add(lower + n);                       // skirt
                tris.Add(lower + i); tris.Add(upper + i); tris.Add(lower + n);               // band
                tris.Add(lower + n); tris.Add(upper + i); tris.Add(upper + n);
                tris.Add(upper + i); tris.Add(top); tris.Add(upper + n);                     // cap
            }
            return Build("Canopy", verts, tris, 0.50f, 1.08f);
        }

        public static Mesh Box(Vector3 size)
        {
            var m = new Mesh { name = "Box" };
            float x = size.x * 0.5f, y = size.y, z = size.z * 0.5f;
            var v = new[]
            {
                new Vector3(-x,0,-z), new Vector3(x,0,-z), new Vector3(x,0,z), new Vector3(-x,0,z),
                new Vector3(-x,y,-z), new Vector3(x,y,-z), new Vector3(x,y,z), new Vector3(-x,y,z)
            };
            var t = new[]
            {
                0,2,1, 0,3,2,       // bottom
                4,5,6, 4,6,7,       // top
                0,1,5, 0,5,4,
                1,2,6, 1,6,5,
                2,3,7, 2,7,6,
                3,0,4, 3,4,7
            };
            return Build("Box", new List<Vector3>(v), new List<int>(t), 0.62f, 1.04f);
        }

        static void AddQuad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, float height)
        {
            int i = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(b + Vector3.up * height);
            verts.Add(a + Vector3.up * height);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>
        /// No baked shading. Note the equal pair: passing (0, 1) here looked like the obvious
        /// default and was wrong for anything FLAT — a moss disc has no height span, so every
        /// vertex lands at t = 0 and the whole prop came out black. It rendered exactly like that.
        /// </summary>
        static Mesh Build(string name, List<Vector3> verts, List<int> tris)
        {
            return Build(name, verts, tris, 1f, 1f);
        }

        /// <summary>
        /// Builds the mesh and bakes a vertical shading gradient into vertex colour: dark at the
        /// base, full colour toward the tip.
        ///
        /// Every prop shares one material — that is what keeps them instanced and cheap — so
        /// without this each one is a single flat tone with no internal form, and a stand of
        /// conifers reads as stamped paper cutouts however much its transforms are varied. It was
        /// rendering exactly like that. Colour is the only per-vertex channel available and the
        /// prop shader spends nothing else on it.
        /// </summary>
        static Mesh Build(string name, List<Vector3> verts, List<int> tris, float shadeBase, float shadeTip)
        {
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);

            // Always written, even when flat. A mesh with no colour array leaves the shader's
            // COLOR semantic undefined, and the prop shader now multiplies by it.
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < verts.Count; i++)
            {
                if (verts[i].y < lo) lo = verts[i].y;
                if (verts[i].y > hi) hi = verts[i].y;
            }
            float span = hi - lo;
            bool graded = shadeTip > shadeBase && span > 1e-3f;

            var cols = new Color[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                // Squared so the darkening hugs the base rather than greying the whole prop.
                float t = graded ? (verts[i].y - lo) / span : 1f;
                float k = graded ? Mathf.Lerp(shadeBase, shadeTip, t * t) : 1f;
                cols[i] = new Color(k, k, k, 1f);
            }
            m.colors = cols;

            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
