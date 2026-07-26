using System.IO;
using UnityEditor;
using UnityEngine;
using Rill.App;
using Rill.Core;
using Rill.Flow;
using Rill.Render;
using Rill.World;

namespace Rill.EditorTools
{
    /// <summary>
    /// Renders the mountain to a PNG from a batch-mode editor script, with no play mode and nobody
    /// pressing anything.
    ///
    /// Every presentation loop in this project has been closed — or left open for months — on
    /// "implemented, unobserved", because the only way to see the game was for a person to open the
    /// editor and hit Play. That is a real bottleneck and it is the reason L-011 was closed with no
    /// screenshot archived and L-013 needed three attempts, two of them reasoned from the shader
    /// source and both wrong.
    ///
    /// This uses the game's own shaders, materials, lighting and mesh builders, so what comes out
    /// is what the game draws. It runs the simulation for N runs first, so the mountain in the
    /// picture has been played.
    ///
    /// **What it cannot show, and why.** Ecosystem props, revealed secrets and collectibles are
    /// issued with Graphics.DrawMesh from MonoBehaviour.Update, which never runs outside play mode,
    /// so they are absent from these images — their absence here is not evidence of anything. The
    /// water ribbon, spray and the carve overlay are per-run state and equally absent. This shows
    /// the terrain, the lakes and the sea: the mountain between runs, which is what the player is
    /// looking at most of the time and what L-015 is about.
    ///
    ///   Unity -batchmode -quit -projectPath . -executeMethod Rill.EditorTools.RillCapture.Capture
    ///
    /// Note the absence of -nographics. Without a graphics device Camera.Render produces nothing,
    /// and it produces it silently.
    /// </summary>
    public static class RillCapture
    {
        const int Width = 1600;
        const int Height = 900;

        [MenuItem("RILL/Capture Mountain PNG (24 runs)", false, 41)]
        public static void Capture() { Shoot(24); }

        [MenuItem("RILL/Capture Mountain PNG (150 runs)", false, 42)]
        public static void CaptureLong() { Shoot(150); }

        static void Shoot(int runs)
        {
            Debug.Log("[RILL] capture: graphics device is " + SystemInfo.graphicsDeviceType);
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError("[RILL] capture: no graphics device — run WITHOUT -nographics. " +
                               "Camera.Render would write nothing and say nothing.");
                return;
            }

            var config = new GameConfig();
            var world = RillWorld.Create(config, config.Seed, config.Biome);

            var root = new GameObject("RillCapture");
            try
            {
                var terrainMat = Load("Shaders/Strata");
                var waterMat = Load("Shaders/PooledWater");
                var propMat = Load("Shaders/Prop");

                // The ecosystem has to be driven alongside the runs, not bolted on afterwards:
                // life grows from moisture that only exists because water went past, one run at a
                // time. Created before Play so AdvanceAfterRun can be called each run.
                var ecoGo = new GameObject("Life");
                ecoGo.transform.SetParent(root.transform, false);
                var eco = ecoGo.AddComponent<EcosystemSystem>();
                eco.Initialise(world, propMat);

                Play(world, config, runs, eco);

                var terrainGo = new GameObject("Terrain");
                terrainGo.transform.SetParent(root.transform, false);
                var terrain = terrainGo.AddComponent<TerrainMeshBuilder>();
                // Initialise fills every chunk immediately; the per-frame rebuild budget only
                // applies to later edits, which is what makes this work with no Update loop.
                terrain.Initialise(world.Field, world.Bands, terrainMat);

                var waterGo = new GameObject("Lakes");
                waterGo.transform.SetParent(root.transform, false);
                var lakes = waterGo.AddComponent<PooledWaterMesh>();
                lakes.Initialise(world.Field, waterMat);
                lakes.BuildNow();

                // Props are normally drawn with Graphics.DrawMesh from Update, which never runs
                // here, so without this the pictures show bare rock and say nothing about whether
                // the mountain looks alive.
                eco.BakeStaticRenderers(ecoGo.transform);

                BuildSea(root.transform, world, waterMat);
                BuildSun(root.transform);

                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "shots"));
                Directory.CreateDirectory(dir);

                Vector3 summit = world.SummitWorld;
                float extent = world.Field.WorldExtent;

                // The idle camera, framed exactly as RillCamera.Overview does it. This is the view
                // the player spends most of their time in and the one L-015 is judged from.
                Render(world, Path.Combine(dir, "mountain_" + runs + "_overview.png"),
                       summit, extent * 0.85f, extent * 0.6f, 30f, 48f);

                // Close over the deepest cut. A channel is a metre or two deep on a 512 m mountain,
                // so an overview can be honestly rendered and still show nothing; if the channels
                // are legible anywhere it is here.
                Vector3 cut = DeepestCutWorld(world);
                Render(world, Path.Combine(dir, "mountain_" + runs + "_channel.png"),
                       cut, 120f, 85f, 30f, 42f);

                Debug.Log("[RILL] capture: wrote 2 PNGs to " + dir);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void Play(RillWorld world, GameConfig config, int runs, EcosystemSystem eco)
        {
            var sim = new FlowSimulation(world);
            var arrivals = new System.Collections.Generic.List<string>();
            for (int run = 1; run <= runs; run++)
            {
                world.BeginRun();
                var rng = new Rng(Noise.Hash((uint)run * 2654435761u ^ world.Seed));
                Vector3 spawn = world.SpawnPoint(ref rng);
                sim.Begin(spawn, config.StartVolume);

                int steps = 0;
                while (sim.Running && steps++ < 20000)
                {
                    if (steps % 30 == 0)
                        sim.SetSteer(rng.Next01() < 0.45f,
                                     sim.Head.Pos + new Vector2(rng.Range(-25f, 25f), rng.Range(-25f, 25f)));
                    sim.Advance(config.SimStep);
                }

                world.Basins.Rebuild();
                world.EndRun(sim.Ending, sim.Elapsed, sim.Distance, sim.TopSpeed, sim.WaterToSea);
                if (eco != null) eco.AdvanceAfterRun(arrivals);
                world.ApplyBetweenRunDrift();
            }
            Debug.Log(string.Format("[RILL] capture: played {0} runs, {1:n0} m³ moved, terrain {2:0.00} m to {3:0.00} m vs virgin, life {4} on {5:n0} cells",
                runs, world.LifetimeSediment, MinDelta(world), MaxDelta(world),
                eco != null ? EcosystemSystem.Describe(eco.HighestTier) : "n/a",
                eco != null ? eco.LivingCells : 0));
        }

        static Vector3 DeepestCutWorld(RillWorld w)
        {
            var f = w.Field;
            int best = 0;
            float deepest = 0f;
            for (int i = 0; i < f.Count; i++)
            {
                float cut = f.Virgin[i] - f.Height[i];
                if (cut > deepest) { deepest = cut; best = i; }
            }
            return f.GridToWorld(best % f.Size, best / f.Size);
        }

        static void Render(RillWorld world, string path, Vector3 target, float distance, float height,
                           float yaw, float fov)
        {
            var camGo = new GameObject("CaptureCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.82f, 0.92f);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 2000f;
            cam.allowHDR = false;

            Vector3 back = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -1f);
            Vector3 pos = target + back * distance + Vector3.up * height;
            camGo.transform.position = pos;
            camGo.transform.rotation = Quaternion.LookRotation((target - pos).normalized, Vector3.up);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log("[RILL] capture: " + Path.GetFileName(path));
            }
            finally
            {
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGo);
            }
        }

        static void BuildSun(Transform parent)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.89f);
            light.intensity = 1.05f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(46f, 35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.55f);
            RenderSettings.fog = false;
        }

        /// <summary>The same subdivided sea GameBootstrap builds — a flat quad renders as one tone.</summary>
        static void BuildSea(Transform parent, RillWorld world, Material waterMat)
        {
            var go = new GameObject("Sea");
            go.transform.SetParent(parent, false);
            float e = world.Field.WorldExtent * 3f;

            const int Grid = 96;
            var verts = new Vector3[(Grid + 1) * (Grid + 1)];
            var cols = new Color32[verts.Length];
            float half = world.Field.WorldExtent * 0.5f;
            float seaLevel = world.Field.SeaLevel;

            for (int gz = 0; gz <= Grid; gz++)
            {
                for (int gx = 0; gx <= Grid; gx++)
                {
                    float wx = Mathf.Lerp(-e, e, gx / (float)Grid);
                    float wz = Mathf.Lerp(-e, e, gz / (float)Grid);
                    verts[gz * (Grid + 1) + gx] = new Vector3(wx, 0f, wz);

                    float depth = 40f;
                    if (Mathf.Abs(wx) < half && Mathf.Abs(wz) < half)
                        depth = seaLevel - world.Field.SampleHeightWorld(wx, wz);

                    byte d = (byte)(Mathf.Clamp01(depth / 6f) * 255f);
                    byte a = (byte)(Mathf.Clamp01(depth / 1.5f) * Mathf.Clamp01(0.45f + depth * 0.3f) * 255f);
                    cols[gz * (Grid + 1) + gx] = new Color32(255, d, 255, a);
                }
            }

            var tris = new int[Grid * Grid * 6];
            int t = 0;
            for (int gz = 0; gz < Grid; gz++)
            {
                for (int gx = 0; gx < Grid; gx++)
                {
                    int v0 = gz * (Grid + 1) + gx;
                    int v1 = v0 + 1;
                    int v2 = v0 + Grid + 1;
                    int v3 = v2 + 1;
                    tris[t++] = v0; tris[t++] = v3; tris[t++] = v1;
                    tris[t++] = v0; tris[t++] = v2; tris[t++] = v3;
                }
            }

            var mesh = new Mesh { name = "Sea" };
            mesh.indexFormat = verts.Length > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.colors32 = cols;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.transform.position = new Vector3(0f, seaLevel - 0.15f, 0f);
        }

        static Material Load(string resourcePath)
        {
            var shader = Resources.Load<Shader>(resourcePath);
            if (shader == null)
            {
                Debug.LogError("[RILL] capture: shader missing at Resources/" + resourcePath);
                shader = Shader.Find("Diffuse") ?? Shader.Find("Standard");
            }
            return new Material(shader) { name = resourcePath };
        }

        static float MinDelta(RillWorld w)
        {
            float m = 0f;
            for (int i = 0; i < w.Field.Count; i++)
            {
                float d = w.Field.Height[i] - w.Field.Virgin[i];
                if (d < m) m = d;
            }
            return m;
        }

        static float MaxDelta(RillWorld w)
        {
            float m = 0f;
            for (int i = 0; i < w.Field.Count; i++)
            {
                float d = w.Field.Height[i] - w.Field.Virgin[i];
                if (d > m) m = d;
            }
            return m;
        }
    }
}
