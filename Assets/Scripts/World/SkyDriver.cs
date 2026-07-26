using UnityEngine;

namespace Rill.World
{
    /// <summary>
    /// Applies <see cref="DayCycle"/> to the live scene: the sun's angle and colour, the ambient,
    /// the camera's sky, and the global tint that unlit surfaces multiply by.
    ///
    /// Follows the player's local clock, so opening the game in the evening opens it at evening.
    /// The Daily overrides that with a fixed hour, because everybody competing on the same seed has
    /// to be looking at the same mountain.
    /// </summary>
    public sealed class SkyDriver : MonoBehaviour
    {
        public Light Sun;
        public Camera Cam;

        /// <summary>Set while the Daily is being played, so every player sees identical light.</summary>
        public bool UseFixedHour;
        public float FixedHour = DayCycle.DailyHour;

        /// <summary>
        /// How fast the applied sky chases the target. Not instant: switching to the Daily and back
        /// would otherwise snap the whole world between two lightings in one frame, which reads as
        /// a glitch rather than as time passing.
        /// </summary>
        public float Damping = 2.5f;

        SkyState _shown;
        bool _primed;

        /// <summary>Seconds between recomputes. The sun moves 15° an hour; four times a second is
        /// four hundred times more often than anyone could notice, and it is still nothing.</summary>
        const float RecomputeInterval = 0.25f;
        float _timer;
        SkyState _target;

        void Start()
        {
            _target = Compute();
            _shown = _target;
            _primed = true;
            Apply(_shown);
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = RecomputeInterval;
                _target = Compute();
            }
            if (!_primed) return;

            float k = 1f - Mathf.Exp(-Damping * Time.deltaTime);
            _shown.SunRotation = Quaternion.Slerp(_shown.SunRotation, _target.SunRotation, k);
            _shown.SunColor = Color.Lerp(_shown.SunColor, _target.SunColor, k);
            _shown.SunIntensity = Mathf.Lerp(_shown.SunIntensity, _target.SunIntensity, k);
            _shown.Ambient = Color.Lerp(_shown.Ambient, _target.Ambient, k);
            _shown.Sky = Color.Lerp(_shown.Sky, _target.Sky, k);
            _shown.SurfaceTint = Color.Lerp(_shown.SurfaceTint, _target.SurfaceTint, k);
            _shown.Night01 = Mathf.Lerp(_shown.Night01, _target.Night01, k);
            Apply(_shown);
        }

        SkyState Compute()
        {
            return DayCycle.At(UseFixedHour ? FixedHour : DayCycle.LocalHour());
        }

        void Apply(SkyState s)
        {
            if (Sun != null)
            {
                Sun.transform.rotation = s.SunRotation;
                Sun.color = s.SunColor;
                Sun.intensity = s.SunIntensity;
            }
            RenderSettings.ambientLight = s.Ambient;
            if (Cam != null) Cam.backgroundColor = s.Sky;

            // Water is unlit — its colour comes from depth rather than from a normal — so without
            // this the sea stays full daytime blue at midnight beside a mountain that has gone dark.
            Shader.SetGlobalColor("_RillDayTint", s.SurfaceTint);
        }
    }
}
