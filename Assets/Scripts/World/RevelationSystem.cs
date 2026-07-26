using System.Collections.Generic;
using UnityEngine;
using Rill.App;
using Rill.Core;

namespace Rill.World
{
    /// <summary>
    /// Curiosity in RILL always has a price paid in play. You cannot dig. You route water over a
    /// spot, run after run, and watch the ground lower toward the reveal — so every discovery is
    /// earned by the same verb the whole game is made of.
    /// </summary>
    public sealed class RevelationSystem : MonoBehaviour
    {
        public float HintRadius = 1.5f;      // metres of remaining burial that start to shimmer
        public float MarkerScale = 1.6f;

        RillWorld _world;
        Mesh _markerMesh, _hintMesh;
        Material _hintMat;

        readonly List<Matrix4x4> _hints = new List<Matrix4x4>();
        readonly Dictionary<SecretKind, List<Matrix4x4>> _revealed = new Dictionary<SecretKind, List<Matrix4x4>>();
        readonly Dictionary<SecretKind, Material> _mats = new Dictionary<SecretKind, Material>();
        readonly Matrix4x4[] _batch = new Matrix4x4[1023];

        public void Initialise(RillWorld world, Material propTemplate)
        {
            _world = world;
            _markerMesh = PropMeshes.Cone(0.9f, 1.4f, 5);
            _hintMesh = PropMeshes.Disc(1.4f, 8);

            _hintMat = new Material(propTemplate) { color = new Color(1f, 0.95f, 0.7f, 0.35f) };
            _hintMat.enableInstancing = true;

            foreach (SecretKind k in System.Enum.GetValues(typeof(SecretKind)))
            {
                var s = new SecretSite { Kind = k };
                var m = new Material(propTemplate) { color = s.Tint };
                m.enableInstancing = true;
                _mats[k] = m;
                _revealed[k] = new List<Matrix4x4>();
            }

            Refresh();

            // Initialise runs again whenever the renderers are rebound (the Daily mountain
            // borrows them), so the handler is detached first or Home ends up double-subscribed.
            if (_onRevealed == null) _onRevealed = _ => Refresh();
            _world.SecretRevealed -= _onRevealed;
            _world.SecretRevealed += _onRevealed;
        }

        System.Action<SecretSite> _onRevealed;

        /// <summary>Recomputes markers. Cheap; called after runs, not per frame.</summary>
        public void Refresh()
        {
            if (_world == null) return;
            _hints.Clear();
            foreach (var kv in _revealed) kv.Value.Clear();

            var f = _world.Field;
            for (int i = 0; i < _world.Secrets.Count; i++)
            {
                var s = _world.Secrets[i];
                int x = s.Cell % f.Size, z = s.Cell / f.Size;
                Vector3 p = f.GridToWorld(x, z);

                if (s.Revealed)
                {
                    _revealed[s.Kind].Add(Matrix4x4.TRS(p, Quaternion.Euler(0f, x * 37f % 360f, 0f), Vector3.one * MarkerScale));
                }
                else
                {
                    float remaining = f.Height[s.Cell] - s.RevealElevation;
                    if (remaining <= HintRadius && remaining > 0f)
                    {
                        // Almost there. A faint shimmer is the only hint the game ever gives.
                        float t = 1f - remaining / HintRadius;
                        _hints.Add(Matrix4x4.TRS(p + Vector3.up * 0.05f, Quaternion.identity, Vector3.one * (0.6f + t)));
                    }
                }
            }
        }

        public int RevealedCount()
        {
            int c = 0;
            for (int i = 0; i < _world.Secrets.Count; i++) if (_world.Secrets[i].Revealed) c++;
            return c;
        }

        /// <summary>
        /// Bakes the revealed markers into real MeshRenderers under <paramref name="parent"/>, the
        /// same seam EcosystemSystem has, and for the same reason: these are drawn with
        /// Graphics.DrawMesh from Update, which never runs outside play mode, so an offscreen
        /// render showed a mountain with no discoveries on it and that meant nothing at all.
        ///
        /// The shimmering hints are deliberately NOT baked. Their whole character is a pulse driven
        /// by Time.time; a still frame of one is a static yellow disc, which would misrepresent it
        /// rather than show it.
        /// </summary>
        public int BakeStaticRenderers(Transform parent)
        {
            int baked = 0;
            foreach (var kv in _revealed)
            {
                if (kv.Value.Count == 0) continue;
                var combines = new CombineInstance[kv.Value.Count];
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    combines[i].mesh = _markerMesh;
                    combines[i].transform = kv.Value[i];
                }

                var combined = new Mesh { name = "Secret_" + kv.Key };
                combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                combined.CombineMeshes(combines, true, true);

                var go = new GameObject("Secret_" + kv.Key);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = combined;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _mats[kv.Key];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                baked += kv.Value.Count;
            }
            if (baked > 0) Debug.Log("[RILL] baked secret markers x" + baked);
            return baked;
        }

        void Update()
        {
            if (_markerMesh == null) return;

            if (_hints.Count > 0)
            {
                // Breathing shimmer: pre-attentive, easy to ignore, impossible to un-notice.
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 2.2f);
                _hintMat.color = new Color(1f, 0.95f, 0.7f, 0.18f + 0.22f * pulse);
                Draw(_hintMesh, _hintMat, _hints);
            }

            foreach (var kv in _revealed)
            {
                if (kv.Value.Count == 0) continue;
                Draw(_markerMesh, _mats[kv.Key], kv.Value);
            }
        }

        void Draw(Mesh mesh, Material mat, List<Matrix4x4> list)
        {
            int i = 0;
            while (i < list.Count)
            {
                int count = Mathf.Min(1023, list.Count - i);
                list.CopyTo(i, _batch, 0, count);
                Graphics.DrawMeshInstanced(mesh, 0, mat, _batch, count, null,
                    UnityEngine.Rendering.ShadowCastingMode.Off, false);
                i += count;
            }
        }
    }
}
