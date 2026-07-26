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
        public enum Mode { Overview, Follow, Report }

        public Camera Cam;
        public float Pitch = 42f;
        public float Yaw = 30f;
        public float FollowDistance = 62f;
        public float FollowHeight = 46f;
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

        public Mode CurrentMode => _mode;

        void Awake()
        {
            if (Cam == null) Cam = GetComponent<Camera>();
            _distance = OverviewDistance;
            _height = OverviewHeight;
        }

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
                    wantDistance = FollowDistance;
                    wantHeight = FollowHeight;
                    break;
                case Mode.Report:
                    wantDistance = ReportDistance;
                    wantHeight = ReportDistance * 0.7f;
                    break;
                default:
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

            float yaw = Yaw + (_mode == Mode.Overview ? Mathf.Sin(_overviewSpin * 0.05f) * 6f : 0f);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 back = rot * new Vector3(0f, 0f, -1f);
            Vector3 pos = _currentTarget + back * _distance + Vector3.up * _height;

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation((_currentTarget - pos).normalized, Vector3.up);
        }
    }
}
