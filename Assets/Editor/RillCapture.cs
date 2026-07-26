using System.IO;
using UnityEditor;
using UnityEngine;
using Rill.App;
using Rill.Core;
using Rill.Flow;
using Rill.Render;
using Rill.UI;
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

        static Color _skyColor = new Color(0.72f, 0.82f, 0.92f);

        [MenuItem("RILL/Capture Mountain PNG (24 runs)", false, 41)]
        public static void Capture() { Shoot(24); }

        [MenuItem("RILL/Capture Mountain PNG (150 runs)", false, 42)]
        public static void CaptureLong() { Shoot(150); }

        /// <summary>
        /// Photographs the interface over a real mountain.
        ///
        /// Every piece of UI in this project has shipped unlooked-at, because a screen-space-overlay
        /// canvas is composited after everything and never appears in a Camera.Render — so the
        /// capture tool could show the world and never the thing sitting on top of it. That is how a
        /// back button ended up as a labelled slab floating in the middle of the sky. The HUD is
        /// pointed through the capture camera instead, and the result is a picture of the actual
        /// composition.
        /// </summary>
        [MenuItem("RILL/Capture Interface PNG", false, 43)]
        public static void CaptureInterface()
        {
            Debug.Log("[RILL] capture: graphics device is " + SystemInfo.graphicsDeviceType);
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError("[RILL] capture: no graphics device — run WITHOUT -nographics.");
                return;
            }

            var config = new GameConfig();
            var world = RillWorld.Create(config, config.Seed, config.Biome);
            var root = new GameObject("RillCaptureUI");
            try
            {
                var terrainMat = Load("Shaders/Strata");
                var waterMat = Load("Shaders/PooledWater");
                var propMat = Load("Shaders/Prop");

                var ecoGo = new GameObject("Life");
                ecoGo.transform.SetParent(root.transform, false);
                var eco = ecoGo.AddComponent<EcosystemSystem>();
                eco.Initialise(world, propMat);
                Play(world, config, 60, eco);

                var terrainGo = new GameObject("Terrain");
                terrainGo.transform.SetParent(root.transform, false);
                terrainGo.AddComponent<TerrainMeshBuilder>().Initialise(world.Field, world.Bands, terrainMat);

                var waterGo = new GameObject("Lakes");
                waterGo.transform.SetParent(root.transform, false);
                var lakes = waterGo.AddComponent<PooledWaterMesh>();
                lakes.Initialise(world.Field, waterMat);
                lakes.BuildNow();

                eco.BakeStaticRenderers(ecoGo.transform);
                BuildSea(root.transform, world, waterMat);
                var sun = BuildSun(root.transform);
                _skyColor = ApplyHour(sun, 13f);

                var hudGo = new GameObject("Hud");
                hudGo.transform.SetParent(root.transform, false);
                var hud = hudGo.AddComponent<HudController>();
                hud.Build();

                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "shots"));
                Directory.CreateDirectory(dir);

                // The main screen: title, record line, three mountains, back in the corner.
                var roster = new MountainRoster();
                var rows = new string[MountainRoster.Slots];
                for (int i = 0; i < MountainRoster.Slots; i++)
                    rows[i] = (i == 0 && roster.Occupied(i) ? "▸  " : "    ") + roster.Describe(i);
                hud.SetMountains(rows);
                hud.SetTitle(true, "60 runs · 4,900 m³ moved · 780 m³ to the sea", null);
                hud.SettleTitle();
                hud.SetBackVisible(false);
                hud.SetIdleUI(false);
                RenderUI(world, hud, Path.Combine(dir, "ui_home.png"));

                // On the mountain: idle row along the bottom, back glyph in the corner, stat lines.
                // Exactly what Begin does: hide the title, then show the mountain UI. Reported as
                // "after Begin is pressed, the main screen doesn't go away", so it gets photographed
                // rather than reasoned about.
                hud.SetTitle(false, "", null);
                hud.SettleTitle();
                hud.SetIdleUI(true);

                // Asserted rather than eyeballed. This has been reported twice, and both times the
                // pixels disagreed with what the code believed, so the check reads the objects.
                if (hud.TitleOnScreen)
                    Debug.LogError("[RILL] capture: FAIL — the main screen is still on top after being hidden");
                else
                    Debug.Log("[RILL] capture: ok — the main screen is gone once hidden");

                // And again through the state-driven path the run loop actually uses, which is what
                // hides it when a run begins.
                hud.SetTitleShown(true);
                hud.SettleTitle();
                bool cameBack = hud.TitleOnScreen;
                hud.SetTitleShown(false);
                if (!cameBack || hud.TitleOnScreen)
                    Debug.LogError("[RILL] capture: FAIL — SetTitleShown does not round-trip");
                else
                    Debug.Log("[RILL] capture: ok — SetTitleShown round-trips show/hide");
                hud.SetBackVisible(true);
                hud.SetTopLine("Run 61 · Clear", "4,900 m³ moved · 1,204 m³ held");
                hud.SetHint("Tap to release the water");
                RenderUI(world, hud, Path.Combine(dir, "ui_mountain.png"));

                // The end card, with every kind of line it can carry: events, the stat columns,
                // the optional pickups block. This is the run's reward and it shipped unlooked-at
                // like everything else; a fixture keeps the photograph repeatable.
                hud.SetHint("");
                hud.SetIdleUI(false);   // the game hides the idle row for the whole run and report
                var rep = new CarveReport
                {
                    RunNumber = 61,
                    Ending = RunEnding.ReachedSea,
                    DistanceTravelled = 236f,
                    TopSpeed = 19.4f,
                    WaterToSea = 43f,
                    SedimentMoved = 88.2f,
                    DeepestCarve = 0.62f,
                    NewChannelMetres = 34f,
                    SeedsCaught = 3,
                    GatesThreaded = 1
                };
                rep.Headlines.Add("North basin broke its banks");
                rep.LifeArrivals.Add("Reeds arrived");
                rep.BasinChanges.Add(new CarveReport.BasinDelta { Name = "North basin", Before01 = 0.62f, After01 = 0.74f });
                hud.ShowReport(rep, 5, 60);
                RenderUI(world, hud, Path.Combine(dir, "ui_report.png"));
                hud.HideAllPanels();

                Debug.Log("[RILL] capture: interface PNGs in " + dir);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void RenderUI(RillWorld world, HudController hud, string path)
        {
            var camGo = new GameObject("UICamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _skyColor;
            cam.fieldOfView = 48f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 2000f;
            cam.allowHDR = false;

            Vector3 target = world.SummitWorld;
            float extent = world.Field.WorldExtent;
            Vector3 back = Quaternion.Euler(0f, 30f, 0f) * new Vector3(0f, 0f, -1f);
            Vector3 pos = target + back * (extent * 0.5f) + Vector3.up * (extent * 0.34f);
            camGo.transform.position = pos;
            camGo.transform.rotation = Quaternion.LookRotation((target - pos).normalized, Vector3.up);

            hud.RenderThroughCamera(cam);

            // Portrait, because that is the shape of the device this is played on and a 16:9
            // landscape frame would flatter a layout that has to survive a phone.
            const int W = 900, H = 1600;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                UnityEngine.Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
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

                var revGo = new GameObject("Secrets");
                revGo.transform.SetParent(root.transform, false);
                var revelation = revGo.AddComponent<RevelationSystem>();
                revelation.Initialise(world, propMat);

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
                revelation.Refresh();
                revelation.BakeStaticRenderers(revGo.transform);

                BuildSea(root.transform, world, waterMat);
                var sun = BuildSun(root.transform);
                _skyColor = ApplyHour(sun, 13f);

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

                // Close on the thickest life. Moss, reeds and huts are one to two metres tall on a
                // 512 m mountain, so the two framings above can be honestly rendered and still say
                // nothing at all about whether a prop reads as its thing — which is the entire
                // question L-016 asks.
                Vector3 grove = RichestLifeWorld(world, eco);
                // 34 m back at 15 m up put the camera inside the hillside. Far enough out to see a
                // shoreline, close enough that a two-metre prop is still tens of pixels.
                Render(world, Path.Combine(dir, "mountain_" + runs + "_life.png"),
                       grove, 72f, 34f, 30f, 40f);

                // A settlement, if the mountain has grown one. Twelve huts on a 512 m mountain will
                // never turn up in a general framing by luck, and a prop nobody has seen is a prop
                // nobody has checked — the pitched roof was built and unobserved for exactly that
                // reason.
                Vector3 village;
                if (eco.TryGetVillage(out village))
                    Render(world, Path.Combine(dir, "mountain_" + runs + "_village.png"),
                           village, 42f, 20f, 30f, 40f);
                else
                    Debug.Log("[RILL] capture: no settlement yet, village framing skipped");

                // The whole reason day/night was done early: it is the one thing in this batch that
                // can be checked from a terminal. Three hours, same mountain, same framing.
                foreach (var h in new[] { 7f, 13f, 19.5f, 23f })
                {
                    _skyColor = ApplyHour(sun, h);
                    Render(world, Path.Combine(dir, string.Format("hour_{0:00}.png", Mathf.RoundToInt(h))),
                           summit, extent * 0.85f, extent * 0.6f, 30f, 48f);
                }
                _skyColor = ApplyHour(sun, 13f);

                Debug.Log("[RILL] capture: done, PNGs in " + dir);
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

        /// <summary>Centre of the densest patch of life, by a coarse box sum over the life field.</summary>
        static Vector3 RichestLifeWorld(RillWorld w, EcosystemSystem eco)
        {
            var life = eco.LifeField;
            var f = w.Field;
            if (life == null) return w.SummitWorld;

            const int R = 6;
            int best = -1;
            float bestSum = 0f;
            for (int z = R; z < f.Size - R; z += 3)
            {
                for (int x = R; x < f.Size - R; x += 3)
                {
                    float sum = 0f;
                    for (int dz = -R; dz <= R; dz += 2)
                        for (int dx = -R; dx <= R; dx += 2)
                            sum += life[(z + dz) * f.Size + (x + dx)];
                    if (sum > bestSum) { bestSum = sum; best = z * f.Size + x; }
                }
            }
            if (best < 0) return w.SummitWorld;
            Debug.Log(string.Format("[RILL] capture: richest life patch scores {0:0}", bestSum));
            return f.GridToWorld(best % f.Size, best / f.Size);
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
            cam.backgroundColor = _skyColor;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 2000f;
            cam.allowHDR = false;

            Vector3 back = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -1f);
            Vector3 pos = target + back * distance + Vector3.up * height;

            // Keep the camera out of the rock. A framing computed purely from distance and height
            // lands inside the hillside whenever the ground behind the subject rises, and the
            // result is a screen filled by one enormous smooth face — which happened twice, was
            // corrected by hand the first time, and came straight back. Correcting the constants
            // treats the symptom; the camera should simply refuse to be underground.
            float ground = world.Field.SampleHeightWorld(pos.x, pos.z);
            if (pos.y < ground + 14f) pos.y = ground + 14f;

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

        static Light BuildSun(Transform parent)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = false;
            return light;
        }

        /// <summary>Puts the scene at an hour of the day. Returns the sky colour for the camera.</summary>
        static Color ApplyHour(Light sun, float hour)
        {
            var sky = DayCycle.At(hour);
            sun.transform.rotation = sky.SunRotation;
            sun.color = sky.SunColor;
            sun.intensity = sky.SunIntensity;
            RenderSettings.ambientLight = sky.Ambient;
            Shader.SetGlobalColor("_RillDayTint", sky.SurfaceTint);
            return sky.Sky;
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
