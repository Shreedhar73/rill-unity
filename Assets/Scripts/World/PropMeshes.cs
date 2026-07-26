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
            return Build("Disc", verts, tris);
        }

        public static Mesh Blade(float width, float height)
        {
            // Two crossed quads: reads as a tuft from any angle, costs 4 triangles.
            var verts = new List<Vector3>();
            var tris = new List<int>();
            AddQuad(verts, tris, new Vector3(-width, 0f, 0f), new Vector3(width, 0f, 0f), height);
            AddQuad(verts, tris, new Vector3(0f, 0f, -width), new Vector3(0f, 0f, width), height);
            return Build("Blade", verts, tris);
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
            return Build("Cone", verts, tris);
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
            m.vertices = v;
            m.triangles = t;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
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

        static Mesh Build(string name, List<Vector3> verts, List<int> tris)
        {
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
