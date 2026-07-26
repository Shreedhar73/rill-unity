using System;
using UnityEngine;
using Rill.Audio;
using Rill.Core;
using Rill.Flow;
using Rill.InputSystem;
using Rill.Meta;
using Rill.Render;
using Rill.UI;
using Rill.World;

namespace Rill.App
{
    /// <summary>
    /// The only object that needs to exist in the scene. Everything else — camera, light, terrain,
    /// water, ecosystem, UI, audio — is created here at runtime, so RILL builds from an empty
    /// scene and there are no prefabs to break.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Tuning")]
        public GameConfig Config = new GameConfig();

        [Header("Startup")]
        public bool LoadSavedMountain = true;
        public int SaveSlot = 0;
        [Tooltip("Wipes the saved mountain on play. Only for development — this is somebody's world.")]
        public bool ResetWorldOnPlay = false;

        public RillWorld World { get; private set; }
        public RunController Runner { get; private set; }

        Material _terrainMat, _ribbonMat, _waterMat, _propMat, _dropletMat;
        RillCamera _camera;
        FlowAudio _audio;
        WeatherSystem _weather;

        void Awake()
        {
            if (Config == null) Config = new GameConfig();
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            BuildMaterials();
            LoadOrCreateWorld();

            _weather = new WeatherSystem(World.Seed);

            BuildLighting();
            var cam = BuildCamera();
            var terrain = BuildTerrain();
            var pooled = BuildPooledWater();
            BuildSea();
            var ribbon = BuildRibbon();
            var ecosystem = gameObject.AddComponent<EcosystemSystem>();
            var revelation = gameObject.AddComponent<RevelationSystem>();
            var pickups = gameObject.AddComponent<Collectibles>();
            var fx = gameObject.AddComponent<SplashFX>();
            fx.Initialise(_dropletMat);
            var thumb = gameObject.AddComponent<ThumbInput>();
            var hud = BuildHud();
            var player = BuildTimeLapsePlayer();

            ecosystem.Initialise(World, _propMat);
            revelation.Initialise(World, _propMat);

            if (_restoredLife != null && _restoredLife.Length == World.Field.Count) ecosystem.RestoreLife(_restoredLife);

            var almanac = Almanac.Load(SaveSlot);
            var daily = new DailyRill();
            var archive = new TimeLapseArchive(SaveSlot);
            var confluence = new ConfluenceQueue(SaveSlot);

            Runner = gameObject.AddComponent<RunController>();
            Runner.Config = Config;
            Runner.Thumb = thumb;
            Runner.Cam = _camera;
            Runner.Terrain = terrain;
            Runner.Ribbon = ribbon;
            Runner.Pooled = pooled;
            Runner.Ecosystem = ecosystem;
            Runner.Revelation = revelation;
            Runner.Pickups = pickups;
            Runner.Fx = fx;
            Runner.Hud = hud;
            Runner.Audio = _audio;
            Runner.TerrainMaterial = _terrainMat;
            Runner.WaterMaterial = _waterMat;
            Runner.PropMaterial = _propMat;
            Runner.PendingBonusVolume = RainGatheredWhileAway(almanac);

            Runner.Initialise(World, almanac, daily, archive, player, confluence, _weather);

            // First keyframe so even a brand-new mountain has a time-lapse to grow from.
            if (!archive.Exists) archive.Append(World.Field, World.RunNumber);

            if (_audio != null) _audio.SetAmbientWater(World.Basins.TotalWater());
        }

        float[] _restoredLife;

        void LoadOrCreateWorld()
        {
            if (ResetWorldOnPlay) SaveSystem.DeleteSlot(SaveSlot);

            if (LoadSavedMountain && !ResetWorldOnPlay)
            {
                World = SaveSystem.Load(Config, out _restoredLife, SaveSlot);
                if (World != null)
                {
                    Debug.Log(string.Format("[RILL] Loaded mountain: run {0}, {1:n0} m³ moved.",
                        World.RunNumber, World.LifetimeSediment));
                    return;
                }
            }

            uint seed = Config.Seed != 0u ? Config.Seed : (uint)DateTime.UtcNow.Ticks;
            World = RillWorld.Create(Config, seed, Config.Biome);
            Debug.Log("[RILL] New mountain generated from seed " + seed);
        }

        /// <summary>
        /// Rain gathers while you are away. It is never required, never expires, and there is no
        /// energy system anywhere — the only thing time away can do in RILL is give you more water.
        /// </summary>
        float RainGatheredWhileAway(Almanac almanac)
        {
            if (almanac.Runs.Count == 0) return 0f;
            var last = almanac.Runs[almanac.Runs.Count - 1];
            var lastPlayed = new DateTime(last.UtcTicks, DateTimeKind.Utc);
            double hours = (DateTime.UtcNow - lastPlayed).TotalHours;
            if (hours < 0.5) return 0f;
            return Mathf.Clamp((float)hours * 2.5f, 0f, Config.StartVolume);
        }

        void BuildMaterials()
        {
            _terrainMat = MakeMaterial("Shaders/Strata");
            _ribbonMat = MakeMaterial("Shaders/WaterRibbon");
            _waterMat = MakeMaterial("Shaders/PooledWater");
            _propMat = MakeMaterial("Shaders/Prop");
            _propMat.enableInstancing = true;
            _dropletMat = MakeMaterial("Shaders/Droplet");
        }

        static Material MakeMaterial(string resourcePath)
        {
            var shader = Resources.Load<Shader>(resourcePath);
            if (shader == null)
            {
                Debug.LogWarning("[RILL] Shader missing at Resources/" + resourcePath + " — falling back.");
                shader = Shader.Find("Diffuse") ?? Shader.Find("Standard");
            }
            return new Material(shader) { name = resourcePath };
        }

        void BuildLighting()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.89f);
            light.intensity = 1.05f;
            light.shadows = LightShadows.None;   // the strata carry the form; shadows cost frames
            go.transform.rotation = Quaternion.Euler(46f, 35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.55f);
            RenderSettings.fog = false;
        }

        Camera BuildCamera()
        {
            var go = new GameObject("MainCamera");
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _weather != null ? _weather.SkyTint : new Color(0.72f, 0.82f, 0.92f);
            cam.fieldOfView = 48f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 2000f;
            cam.allowHDR = false;
            cam.allowMSAA = true;

            go.AddComponent<AudioListener>();
            _audio = go.AddComponent<FlowAudio>();

            _camera = go.AddComponent<RillCamera>();
            _camera.Cam = cam;
            _camera.Pitch = Config.CameraPitch;
            _camera.FollowDistance = Config.CameraDistance;
            _camera.FollowHeight = Config.CameraHeight;
            _camera.OverviewDistance = Config.WorldExtent * 0.85f;
            _camera.OverviewHeight = Config.WorldExtent * 0.6f;
            _camera.SetOverview(World.SummitWorld);
            return cam;
        }

        TerrainMeshBuilder BuildTerrain()
        {
            var go = new GameObject("Terrain");
            go.transform.SetParent(transform, false);
            var builder = go.AddComponent<TerrainMeshBuilder>();
            builder.Initialise(World.Field, World.Bands, _terrainMat);
            return builder;
        }

        PooledWaterMesh BuildPooledWater()
        {
            var go = new GameObject("PooledWater");
            go.transform.SetParent(transform, false);
            var pooled = go.AddComponent<PooledWaterMesh>();
            pooled.Initialise(World.Field, _waterMat);
            return pooled;
        }

        /// <summary>The sea: a single quad the mountain drains into. The destination of every run.</summary>
        void BuildSea()
        {
            var go = new GameObject("Sea");
            go.transform.SetParent(transform, false);
            float e = Config.WorldExtent * 3f;

            var mesh = new Mesh { name = "Sea" };
            mesh.vertices = new[]
            {
                new Vector3(-e, 0f, -e), new Vector3(e, 0f, -e),
                new Vector3(e, 0f, e), new Vector3(-e, 0f, e)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            var deep = new Color32(255, 235, 255, 235);
            mesh.colors32 = new[] { deep, deep, deep, deep };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.transform.position = new Vector3(0f, World.Field.SeaLevel - 0.15f, 0f);
        }

        WaterRibbon BuildRibbon()
        {
            var go = new GameObject("StreamRibbon");
            go.transform.SetParent(transform, false);
            var ribbon = go.AddComponent<WaterRibbon>();
            ribbon.Initialise(_ribbonMat);
            return ribbon;
        }

        HudController BuildHud()
        {
            // UGUI needs an EventSystem to route taps to the four buttons; there is no scene
            // asset to hold one, so it is created here like everything else.
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            var go = new GameObject("HUD");
            go.transform.SetParent(transform, false);
            var hud = go.AddComponent<HudController>();
            hud.Build();
            return hud;
        }

        TimeLapsePlayer BuildTimeLapsePlayer()
        {
            var go = new GameObject("TimeLapse");
            go.transform.SetParent(transform, false);
            var player = go.AddComponent<TimeLapsePlayer>();
            player.Initialise(Config.CellSize, Config.Size, World.Bands, _terrainMat);
            return player;
        }
    }
}
