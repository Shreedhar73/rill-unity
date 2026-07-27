using UnityEngine;

namespace Rill.Render
{
    /// <summary>
    /// Droplets. One code-built particle system, reused for waterfall plunges, pool entries and
    /// pickups — the only "effects" in the game, because water carrying light is already the show.
    /// </summary>
    public sealed class SplashFX : MonoBehaviour
    {
        ParticleSystem _ps;
        ParticleSystem.EmitParams _emit;

        public void Initialise(Material template)
        {
            var go = new GameObject("Splash");
            go.transform.SetParent(transform, false);
            _ps = go.AddComponent<ParticleSystem>();

            // A freshly added ParticleSystem is already playing (playOnAwake), and Unity refuses
            // to set duration on a playing system — it threw on every boot, silently, in a log
            // nobody was reading. Stop it before configuring it. Found by the play probe.
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.85f;
            main.startSpeed = 6.5f;
            main.startSize = 0.55f;
            main.gravityModifier = 1.35f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.78f, 0.93f, 1f, 0.9f);

            var emission = _ps.emission;
            emission.enabled = false;      // emitted by hand, never on a timer

            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 42f;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var sizeOverLife = _ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = template;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _ps.Stop();
        }

        /// <summary>Kills every live particle. The time-lapse must show history, not today's spray.</summary>
        public void Clear()
        {
            if (_ps != null) _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Burst(Vector3 world, float strength, Color color)
        {
            if (_ps == null) return;
            int count = Mathf.Clamp(Mathf.RoundToInt(6f + strength * 40f), 4, 90);
            _emit = new ParticleSystem.EmitParams
            {
                position = world + Vector3.up * 0.3f,
                startColor = color,
                startSize = 0.35f + strength * 0.5f,
                startLifetime = 0.5f + strength * 0.6f
            };
            _ps.Emit(_emit, count);
        }
    }
}
