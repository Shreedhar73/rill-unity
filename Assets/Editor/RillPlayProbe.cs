using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Rill.Flow;
using Rill.UI;

namespace Rill.EditorTools
{
    /// <summary>
    /// Plays the actual game. Real play mode, real boot path, real Update loops, real button
    /// wiring — then walks the loop a player walks: home → Begin → release → run → settle →
    /// report → dismiss → Back → home, photographing each step from inside the live game and
    /// counting every runtime exception on the way.
    ///
    /// This exists because everything else here proved to be a staged photograph. The capture
    /// tool builds the HUD by hand and calls the same setters the game calls — which verifies
    /// the setters and says NOTHING about whether the running game reaches them. Begin was
    /// present in every captured frame and reported missing in play; the probe is the tool that
    /// can tell those two worlds apart.
    ///
    ///   Unity -batchmode -projectPath . -executeMethod Rill.EditorTools.RillPlayProbe.Run
    ///
    /// No -quit: the probe exits the editor itself when it finishes, non-zero if anything
    /// failed. Back up the save directory first — the probe plays a real run on the real slot.
    /// </summary>
    public static class RillPlayProbe
    {
        const string Flag = "rill_probe_active";
        const string PhaseKey = "rill_probe_phase";

        [MenuItem("RILL/Run Play-Mode Probe", false, 80)]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Rill.unity");
            SessionState.SetBool(Flag, true);
            SessionState.SetInt(PhaseKey, 0);
            EditorApplication.EnterPlaymode();
        }

        // Survives the domain reload that entering play mode performs; SessionState is the only
        // static thing that does, which is why the phase lives there and not in a field.
        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        static int _errors;
        static int _fails;

        static void OnLog(string msg, string stack, LogType type)
        {
            if (!SessionState.GetBool(Flag, false)) return;
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            {
                _errors++;
                // Re-logged at Log level so it cannot recurse, prefixed so it is grep-able.
                Debug.Log("[PROBE] RUNTIME ERROR: " + msg + "\n" + stack);
            }
        }

        static int _lastPhase = -1;
        static float _phaseStart;
        static RunController.State _lastState = (RunController.State)(-1);

        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            if (!EditorApplication.isPlaying) return;

            var runner = Object.FindFirstObjectByType<RunController>();
            var hud = Object.FindFirstObjectByType<HudController>();
            if (runner == null || hud == null) return;

            // A probe that can hang is a probe that blocks the pipeline. Anything not finished in
            // five minutes of play is a failure by definition.
            if (Time.time > 300f)
            {
                Debug.Log("[PROBE] TIMEOUT after " + Time.time + "s in phase " + SessionState.GetInt(PhaseKey, 0)
                          + " — " + _fails + " checks failed, " + _errors + " runtime errors");
                SessionState.SetBool(Flag, false);
                EditorApplication.Exit(1);
                return;
            }

            int phase = SessionState.GetInt(PhaseKey, 0);
            if (phase != _lastPhase) { _lastPhase = phase; _phaseStart = Time.time; }
            float inPhase = Time.time - _phaseStart;

            if (runner.Current != _lastState)
            {
                Debug.Log(string.Format("[PROBE] t={0:0.0}s  state {1} -> {2}", Time.time, _lastState, runner.Current));
                _lastState = runner.Current;
            }

            try { Drive(runner, hud, phase, inPhase); }
            catch (System.Exception e)
            {
                // A phase that throws — usually a button handler blowing up inside Invoke — is a
                // finding, not a reason to spin forever re-throwing every editor tick.
                Check(false, "phase " + phase + " threw: " + e.GetBaseException().Message);
                Next();
            }
        }

        static void Drive(RunController runner, HudController hud, int phase, float inPhase)
        {
            switch (phase)
            {
                // Boot and the arrival. 4.5 s covers the 3.4 s camera move plus the title fade.
                case 0:
                    if (inPhase < 4.5f) return;
                    Check(runner.Current == RunController.State.Title, "boots to the title, state=" + runner.Current);
                    Check(hud.TitleOnScreen, "the main screen is on screen");
                    Check(Visible(hud, "Start"), "Begin is visible on the main screen — " + Describe(hud, "Start"));
                    Check(Visible(hud, "Slot0"), "the first mountain row is visible — " + Describe(hud, "Slot0"));
                    Shot(hud, "play_home.png");
                    Next(); return;

                case 1:
                    Check(Click(hud, "Start"), "Begin can be pressed");
                    Next(); return;

                // The mountain, idle. The title must be gone; the idle row must be there.
                case 2:
                    if (inPhase < 1.0f) return;
                    Check(runner.Current == RunController.State.Idle, "Begin leads to the mountain, state=" + runner.Current);
                    Check(!hud.TitleOnScreen, "the main screen is gone after Begin");
                    Check(Visible(hud, "BtnEnd game"), "End game sits in the idle row — " + Describe(hud, "BtnEnd game"));
                    Shot(hud, "play_idle.png");
                    // Release the water exactly where a tap would: the same private entry point.
                    typeof(RunController).GetMethod("StartRun", BindingFlags.NonPublic | BindingFlags.Instance)
                                         .Invoke(runner, null);
                    Time.timeScale = 3f;
                    Next(); return;

                // The run, and whatever cascades follow it. Ends when the state machine leaves
                // Flowing for good.
                case 3:
                    if (runner.Current == RunController.State.Flowing && inPhase < 150f) return;
                    Time.timeScale = 1f;
                    Check(runner.Current == RunController.State.Settling || runner.Current == RunController.State.Report,
                          "the run ends in the settle beat or the report, state=" + runner.Current);
                    if (runner.Current == RunController.State.Settling) Shot(hud, "play_settle.png");
                    Next(); return;

                // The report card must arrive on its own and be visible.
                case 4:
                    if (runner.Current != RunController.State.Report && inPhase < 8f) return;
                    Check(runner.Current == RunController.State.Report, "the settle beat hands over to the report, state=" + runner.Current);
                    Check(hud.ReportVisible, "the report card is visible");
                    Shot(hud, "play_report.png");
                    Check(Click(hud, "CarveReport"), "the report card can be tapped away");
                    Next(); return;

                // Dismissing the card is 'start again': back at idle, ready for the next tap.
                case 5:
                    if (inPhase < 1.0f) return;
                    Check(runner.Current == RunController.State.Idle, "dismissing the card returns to idle, state=" + runner.Current);
                    Check(Visible(hud, "Back"), "Back is visible on the mountain — " + Describe(hud, "Back"));
                    Shot(hud, "play_after_report.png");
                    Check(Click(hud, "BtnTime-lapse"), "Time-lapse can be pressed");
                    Next(); return;

                // The player's own history plays back. This path was wired for weeks and no test
                // had ever entered it — playback state, the hand-back to idle, none of it. (L-061)
                case 6:
                    if (inPhase < 0.7f) return;
                    if (runner.Current == RunController.State.Idle)
                    {
                        // A save too young to have two keyframes refuses politely; that is correct
                        // behaviour, not a probe failure — but say so, or a broken Play() that
                        // always refuses would photograph as a pass.
                        Debug.Log("[PROBE] note  time-lapse declined to play (not enough history on this save)");
                        Check(Click(hud, "Back"), "Back can be pressed");
                        SessionState.SetInt(PhaseKey, 8); return;
                    }
                    Check(runner.Current == RunController.State.TimeLapse,
                          "Time-lapse plays the mountain's history, state=" + runner.Current);
                    Shot(hud, "play_timelapse.png");
                    Next(); return;

                // Playback must end on its own and hand the mountain back.
                case 7:
                    if (runner.Current == RunController.State.TimeLapse && inPhase < 90f) return;
                    Check(runner.Current == RunController.State.Idle,
                          "the time-lapse hands back to idle when it ends, state=" + runner.Current);
                    Check(Click(hud, "Back"), "Back can be pressed");
                    Next(); return;

                // And Back is 'go home': the title again, Begin again.
                case 8:
                    if (inPhase < 1.5f) return;
                    Check(runner.Current == RunController.State.Title, "Back returns to the main screen, state=" + runner.Current);
                    Check(hud.TitleOnScreen, "the main screen is back");
                    Check(Visible(hud, "Start"), "Begin is there again — " + Describe(hud, "Start"));
                    Shot(hud, "play_home_again.png");
                    Debug.Log(string.Format("[PROBE] finished: {0} checks failed, {1} runtime errors", _fails, _errors));
                    SessionState.SetBool(Flag, false);
                    EditorApplication.Exit(_fails == 0 && _errors == 0 ? 0 : 1);
                    return;
            }
        }

        static void Next() { SessionState.SetInt(PhaseKey, SessionState.GetInt(PhaseKey, 0) + 1); }

        static void Check(bool ok, string what)
        {
            if (ok) Debug.Log("[PROBE] ok    " + what);
            else { _fails++; Debug.Log("[PROBE] FAIL  " + what); }
        }

        static GameObject Find(HudController hud, string name)
        {
            foreach (var t in hud.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        /// <summary>Visible the way a player means it: active, and no CanvasGroup above it faded out.</summary>
        static bool Visible(HudController hud, string name)
        {
            var go = Find(hud, name);
            if (go == null || !go.activeInHierarchy) return false;
            for (var t = go.transform; t != null; t = t.parent)
            {
                var g = t.GetComponent<CanvasGroup>();
                if (g != null && g.alpha < 0.05f) return false;
            }
            return true;
        }

        static string Describe(HudController hud, string name)
        {
            var go = Find(hud, name);
            if (go == null) return name + ": DOES NOT EXIST";
            string s = name + ": activeInHierarchy=" + go.activeInHierarchy;
            for (var t = go.transform; t != null; t = t.parent)
            {
                var g = t.GetComponent<CanvasGroup>();
                if (g != null) s += string.Format(" group({0} a={1:0.00} i={2})", t.name, g.alpha, g.interactable);
            }
            return s;
        }

        static bool Click(HudController hud, string name)
        {
            var go = Find(hud, name);
            var btn = go != null ? go.GetComponent<Button>() : null;
            if (btn == null) return false;
            btn.onClick.Invoke();
            return true;
        }

        /// <summary>
        /// Photographs the live game — world and UI together — through the game's own camera.
        /// The canvas is screen-space-overlay and invisible to Camera.Render, so it is pointed
        /// through the camera first, exactly as the capture tool does; unlike the capture tool,
        /// everything in the frame was put there by the game running.
        /// </summary>
        static void Shot(HudController hud, string file)
        {
            var cam = Camera.main;
            if (cam == null) { Debug.Log("[PROBE] no main camera for " + file); return; }
            hud.RenderThroughCamera(cam);

            const int W = 900, H = 1600;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var prev = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "docs", "shots"));
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, file), tex.EncodeToPNG());
                Debug.Log("[PROBE] shot " + file);
            }
            finally
            {
                RenderTexture.active = null;
                cam.targetTexture = prev;
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
