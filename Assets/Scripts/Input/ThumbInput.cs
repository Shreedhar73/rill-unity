using UnityEngine;

namespace Rill.InputSystem
{
    /// <summary>
    /// One thumb, anywhere on screen. There is no other control in RILL, and there never will be:
    /// touch position relative to the stream head is a lateral pull, release lets the water choose.
    /// Works with mouse in the editor so the game is playable without a device.
    /// </summary>
    public sealed class ThumbInput : MonoBehaviour
    {
        public bool Held { get; private set; }
        public bool PressedThisFrame { get; private set; }
        public bool ReleasedThisFrame { get; private set; }
        public Vector2 ScreenPos { get; private set; }
        public float HoldDuration { get; private set; }

        /// <summary>Screen distance travelled while held — used to tell a drag from a tap.</summary>
        public float DragDistance { get; private set; }

        Vector2 _pressPos;

        void Update()
        {
            PressedThisFrame = false;
            ReleasedThisFrame = false;

#if UNITY_EDITOR || UNITY_STANDALONE
            bool down = Input.GetMouseButton(0);
            Vector3 mouse = Input.mousePosition;
            Vector2 pos = new Vector2(mouse.x, mouse.y);
            if (Input.GetMouseButtonDown(0)) { PressedThisFrame = true; }
            if (Input.GetMouseButtonUp(0)) { ReleasedThisFrame = true; }
#else
            bool down = false;
            Vector2 pos = ScreenPos;
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                pos = t.position;
                down = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
                if (t.phase == TouchPhase.Began) PressedThisFrame = true;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) ReleasedThisFrame = true;
            }
            else if (Held)
            {
                ReleasedThisFrame = true;
            }
#endif

            ScreenPos = pos;

            if (PressedThisFrame)
            {
                _pressPos = pos;
                HoldDuration = 0f;
                DragDistance = 0f;
            }
            if (down)
            {
                HoldDuration += Time.deltaTime;
                DragDistance = Mathf.Max(DragDistance, Vector2.Distance(pos, _pressPos));
            }
            Held = down;
        }

        /// <summary>
        /// Projects the thumb onto the horizontal plane at the stream head's elevation. Using the
        /// head's own plane keeps the pull feeling anchored to the water, not to the terrain.
        /// </summary>
        public bool WorldTargetOnPlane(Camera cam, float planeY, out Vector2 worldXZ)
        {
            worldXZ = Vector2.zero;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(ScreenPos);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
            float t = (planeY - ray.origin.y) / ray.direction.y;
            if (t < 0f) return false;
            Vector3 p = ray.origin + ray.direction * t;
            worldXZ = new Vector2(p.x, p.z);
            return true;
        }

        public bool WasTap(float maxPixels = 24f, float maxSeconds = 0.4f)
        {
            return ReleasedThisFrame && DragDistance <= maxPixels && HoldDuration <= maxSeconds;
        }
    }
}
