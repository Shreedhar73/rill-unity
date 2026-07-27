using UnityEngine;

namespace Rill.Render
{
    /// <summary>
    /// Camera work is half the feel. During a run it rides just behind and above the stream with
    /// lookahead proportional to speed; at the end it pulls back and frames what changed, which
    /// is the moment the player is told their run mattered.
    /// </summary>
    public sealed class RillCamera : MonoBehaviour
    {
        public enum Mode { Overview, Follow, Report, Title }

        public Camera Cam;
        public float Pitch = 42f;
        public float Yaw = 30f;
        public float FollowDistance = 62f;
        public float FollowHeight = 46f;

        // Speed had no camera consequence at all: 24 m/s framed identically to 9 m/s, so the
        // momentum economy — the whole skill ceiling — was invisible without reading the HUD meter.
        // Widening the lens and dropping toward the bed at speed puts motion in the periphery and
        // brings the ground closer, which is what actually reads as fast.
        [Tooltip("Speed treated as 'flat out' for camera response, m/s.")]
        public float SpeedReference = 24f;
        [Tooltip("Degrees of extra field of view at full speed.")]
        public float SpeedFovKick = 13f;
        [Tooltip("Fraction of follow distance and height pulled in at full speed.")]
        public float SpeedCloseIn = 0.22f;

        float _speed01;
        float _baseFov;

        /// <summary>
        /// The arrival. The camera starts far out and high, and comes to rest at the title framing
        /// over a few seconds.
        ///
        /// Deliberately not a logo sting: the honest opening for this game is its own premise. The
        /// mountain IS the save file, so the app opens on the world the player left and the camera
        /// travels to it. A returning player watches their own river system resolve out of the
        /// distance before they are asked to do anything.
        /// </summary>
        public float ArrivalSeconds = 3.4f;
        public float ArrivalDistanceMultiplier = 2.6f;
        public float ArrivalHeightMultiplier = 2.2f;
        float _arrival;      // 1 at the start of the approach, 0 once it has landed

        [Header("Title framing")]
        public float TitleDistance = 190f;
        public float TitleHeight = 78f;
        [Tooltip("Degrees per second the title camera orbits the summit.")]
        public float TitleOrbitSpeed = 3.5f;
        public float OverviewDistance = 210f;
        public float OverviewHeight = 150f;
        public float ReportDistance = 115f;
        public float Damping = 4.5f;
        public float LookaheadPerSpeed = 0.65f;

        Mode _mode = Mode.Overview;
        Vector3 _target;
        Vector3 _currentTarget;
        float _distance;
        float _height;
        Vector3 _panOffset;
        float _overviewSpin;

        /// <summary>
        /// Height of the ground at a world XZ, supplied by whoever owns the active world. The
        /// camera cannot know about HeightField directly without dragging the simulation into the
        /// renderer, and it must be rebound when the player switches mountains — a framing that is
        /// safe on slot 1's topology walks straight into a ridge on slot 2's.
        /// </summary>
        public System.Func<float, float, float> SampleGround;

        [Tooltip("Minimum metres of air kept between the camera and the ground beneath it.")]
        public float GroundClearance = 10f;

        float _lift;   // extra height forced by terrain; rises instantly, settles back smoothly

        [Tooltip("Metres the camera dips on a full-strength plunge impact.")]
        public float ImpactDip = 2.2f;
        [Tooltip("Degrees of momentary FOV pop on a full-strength plunge impact.")]
        public float ImpactFovPop = 5f;
        float _impact;

        /// <summary>
        /// A plunge landed. The camera takes the hit with the water: a fast dip-and-recover plus
        /// a small FOV pop. This is the missing third leg of L-014 — speed had a camera
        /// consequence and falling did not, so a 15 m plunge read no differently from a riffle.
        /// </summary>
        public void Impact(float strength01)
        {
            _impact = Mathf.Max(_impact, Mathf.Clamp01(strength01));
        }

        public Mode CurrentMode => _mode;

        void Awake()
        {
            if (Cam == null) Cam = GetComponent<Camera>();
            _distance = OverviewDistance;
            _height = OverviewHeight;
        }

        /// <summary>
        /// Title framing: closer and lower than the idle overview, orbiting continuously. The idle
        /// camera sits high and barely moves — correct for a garden you are tending, wrong for a
        /// first impression, where the mountain should have some silhouette and some motion.
        /// </summary>
        public void SetTitle(Vector3 worldCentre)
        {
            _mode = Mode.Title;
            _target = worldCentre;
            _currentTarget = worldCentre;
            _panOffset = Vector3.zero;
        }

        /// <summary>
        /// Title framing, arrived at rather than cut to. Called once on launch; every later return
        /// to the main screen uses SetTitle, because an opening you sit through twice is an
        /// obstacle rather than an arrival.
        /// </summary>
        public void SetTitleArriving(Vector3 worldCentre)
        {
            SetTitle(worldCentre);
            _arrival = 1f;
            _distance = TitleDistance * ArrivalDistanceMultiplier;
            _height = TitleHeight * ArrivalHeightMultiplier;
        }

        /// <summary>True while the opening camera move is still running.</summary>
        public bool Arriving => _arrival > 0.001f;

        /// <summary>Cuts the arrival short. A player who taps has asked to get on with it.</summary>
        public void SkipArrival() { _arrival = 0f; }

        public void SetOverview(Vector3 worldCentre)
        {
            _mode = Mode.Overview;
            _target = worldCentre;
            _currentTarget = worldCentre;
            _panOffset = Vector3.zero;
        }

        public void Follow(Vector3 headWorld, Vector2 velocityXZ)
        {
            _mode = Mode.Follow;
            Vector3 lookahead = new Vector3(velocityXZ.x, 0f, velocityXZ.y) * LookaheadPerSpeed;
            _target = headWorld + lookahead;
            _speed01 = Mathf.Clamp01(velocityXZ.magnitude / Mathf.Max(1f, SpeedReference));
        }

        public void FrameReport(Vector3 focus)
        {
            _mode = Mode.Report;
            _target = focus;
        }

        /// <summary>Idle-mode panning: the player wanders their own mountain like a garden.</summary>
        public void Pan(Vector2 screenDelta)
        {
            if (_mode != Mode.Overview) return;
            Vector3 right = Quaternion.Euler(0f, Yaw, 0f) * Vector3.right;
            Vector3 fwd = Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
            _panOffset -= (right * screenDelta.x + fwd * screenDelta.y) * (_distance * 0.0016f);
            float limit = 260f;
            _panOffset = Vector3.ClampMagnitude(_panOffset, limit);
        }

        public void Zoom(float delta)
        {
            OverviewDistance = Mathf.Clamp(OverviewDistance - delta, 45f, 320f);
            OverviewHeight = OverviewDistance * 0.72f;
        }

        void LateUpdate()
        {
            float wantDistance, wantHeight;
            switch (_mode)
            {
                case Mode.Follow:
                    wantDistance = FollowDistance * (1f - SpeedCloseIn * _speed01);
                    wantHeight = FollowHeight * (1f - SpeedCloseIn * _speed01);
                    break;
                case Mode.Report:
                    wantDistance = ReportDistance;
                    wantHeight = ReportDistance * 0.7f;
                    break;
                case Mode.Title:
                    _speed01 = 0f;
                    wantDistance = TitleDistance;
                    wantHeight = TitleHeight;
                    // Ease the approach out rather than letting the existing damping do it: plain
                    // exponential damping never quite lands, and an arrival that is still creeping
                    // when the player reaches for the screen reads as drift, not as a destination.
                    if (_arrival > 0f)
                    {
                        _arrival = Mathf.Max(0f, _arrival - Time.deltaTime / Mathf.Max(0.01f, ArrivalSeconds));
                        float e = _arrival * _arrival * _arrival;   // most of the travel happens early
                        wantDistance = Mathf.Lerp(TitleDistance, TitleDistance * ArrivalDistanceMultiplier, e);
                        wantHeight = Mathf.Lerp(TitleHeight, TitleHeight * ArrivalHeightMultiplier, e);
                    }
                    _overviewSpin += Time.deltaTime * TitleOrbitSpeed;
                    break;
                default:
                    _speed01 = 0f;
                    wantDistance = OverviewDistance;
                    wantHeight = OverviewHeight;
                    _overviewSpin += Time.deltaTime * 0.9f; // barely-there drift; the world breathes
                    break;
            }

            float k = 1f - Mathf.Exp(-Damping * Time.deltaTime);
            _distance = Mathf.Lerp(_distance, wantDistance, k);
            _height = Mathf.Lerp(_height, wantHeight, k);
            Vector3 goal = _target + (_mode == Mode.Overview ? _panOffset : Vector3.zero);
            _currentTarget = Vector3.Lerp(_currentTarget, goal, k);

            float yaw = Yaw;
            if (_mode == Mode.Overview) yaw += Mathf.Sin(_overviewSpin * 0.05f) * 6f;
            else if (_mode == Mode.Title) yaw += _overviewSpin;   // a slow, continuous turn
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 back = rot * new Vector3(0f, 0f, -1f);
            Vector3 pos = _currentTarget + back * _distance + Vector3.up * _height;

            // The plunge dip, before the terrain clamp so an impact can never push the camera
            // into the ground it is reacting to. Sharp decay: an impact is a beat, not a bounce.
            if (_impact > 0.001f)
            {
                pos.y -= _impact * ImpactDip;
                _impact -= _impact * 9f * Time.deltaTime;
            }

            // Keep the camera out of the rock. A framing computed purely from distance and height
            // lands inside the hillside whenever the ground behind the subject rises — the capture
            // tool hit exactly this, twice, and got this clamp (RillCapture); the live camera never
            // did, so following a stream around a ridge on the second or third mountain put the
            // player inside the terrain. Lift is applied instantly (being underground for even one
            // frame is a wall filling the screen) and released through the same damping as
            // everything else, so cresting a ridge reads as the camera breathing rather than
            // popping.
            if (SampleGround != null)
            {
                float needed = RequiredCameraY(pos, _currentTarget, SampleGround, GroundClearance);
                float lift = Mathf.Max(0f, needed - pos.y);
                _lift = lift > _lift ? lift : Mathf.Lerp(_lift, lift, k);
                pos.y += _lift;
            }

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation((_currentTarget - pos).normalized, Vector3.up);

            var cam = Cam;
            if (cam != null)
            {
                if (_baseFov <= 0f) _baseFov = cam.fieldOfView;
                float wantFov = _baseFov + SpeedFovKick * _speed01 + ImpactFovPop * _impact;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, wantFov, k);
            }
        }

        /// <summary>
        /// The lowest camera height that keeps the camera itself out of the ground *and* keeps the
        /// view ray to the target clear of any ridge between them — a camera that is technically
        /// above ground but looking through a hill shows the same wall of rock as one buried in it.
        ///
        /// Static and pure so the headless camera test can drive it over real biome topology
        /// without a scene; the required height is the answer to "where would the camera have to
        /// be", which is checkable, where "does it look right" is not.
        /// </summary>
        public static float RequiredCameraY(Vector3 camPos, Vector3 target,
                                            System.Func<float, float, float> ground, float clearance)
        {
            float required = ground(camPos.x, camPos.z) + clearance;

            // Walk the sight line from the target back to the camera. Samples close to the target
            // are skipped: the subject is ON the ground, so demanding clearance there would launch
            // the camera skyward every frame. The margin tapers toward the target for the same
            // reason — full clearance matters at the camera, none at the subject.
            const int Samples = 6;
            for (int i = 1; i <= Samples; i++)
            {
                float t = i / (float)(Samples + 1);   // 0 at target, 1 at camera
                if (t < 0.25f) continue;
                float x = Mathf.Lerp(target.x, camPos.x, t);
                float z = Mathf.Lerp(target.z, camPos.z, t);
                float clear = ground(x, z) + clearance * t;
                // Ray height at t is target.y + (camY - target.y) * t; solve for the camY that
                // puts the ray exactly at the clearance height here.
                float need = target.y + (clear - target.y) / t;
                if (need > required) required = need;
            }
            return required;
        }
    }
}
