using UnityEngine;

namespace Rill.App
{
    /// <summary>
    /// Haptics carry flow texture through the thumb: fine grain on fresh rock, glassy smoothness
    /// in your own deep channels. The full experience survives with this switched off, which is
    /// the test every feedback channel in RILL has to pass.
    ///
    /// Unity's cross-platform vibration is coarse, so this stays deliberately sparse: it fires on
    /// events, never as a continuous buzz that would drain a battery and annoy a commuter.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;

        static float _lastFire;
        const float MinInterval = 0.18f;

        public static void Tick(float strength01)
        {
            if (!Enabled) return;
            if (strength01 < 0.35f) return;
            Fire();
        }

        /// <summary>Called on real moments: gates threaded, secrets uncovered, dam breaks.</summary>
        public static void Event()
        {
            if (!Enabled) return;
            Fire();
        }

        static void Fire()
        {
            if (Time.unscaledTime - _lastFire < MinInterval) return;
            _lastFire = Time.unscaledTime;
#if UNITY_ANDROID || UNITY_IOS
            if (!Application.isEditor) Handheld.Vibrate();
#endif
        }
    }
}
