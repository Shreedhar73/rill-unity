using System.Collections.Generic;

namespace Rill.App
{
    /// <summary>
    /// Every screen the app can be on. The run loop itself is <see cref="Mountain"/>.
    ///
    /// Named AppScreen and not Screen: <c>UnityEngine.Screen</c> exists, and an enum called Screen
    /// inside Rill.App shadows it for every file in the namespace. GameBootstrap's
    /// <c>Screen.sleepTimeout</c> stopped compiling the moment this type was added, which is the
    /// good version of that mistake — the quiet version is a future file silently resolving the
    /// wrong Screen.
    /// </summary>
    public enum AppScreen
    {
        /// <summary>The opening beat. Cannot be returned to — you only arrive once.</summary>
        Launch,
        /// <summary>Choosing a mountain or a mode. The root; back from here leaves the app.</summary>
        Home,
        /// <summary>On a mountain, with RunController owning the sub-state.</summary>
        Mountain,
        Records,
        Almanac,
        TimeLapse,
        Settings
    }

    /// <summary>What the shell wants the platform to do, once, in response to a Back.</summary>
    public enum NavAction
    {
        None,
        /// <summary>The screen changed. Read <see cref="Navigator.Current"/>.</summary>
        Changed,
        /// <summary>Back was pressed on a run in progress. The run must be abandoned first.</summary>
        AbandonRun,
        /// <summary>Back at the root. Quit if the platform allows it; otherwise do nothing.</summary>
        Quit
    }

    /// <summary>
    /// Where the player is, and what Back means from here.
    ///
    /// Deliberately a plain class with no Unity types in it, so the smoke test can drive every
    /// transition directly. That is not tidiness: L-018 shipped onboarding that "compiled,
    /// committed, and was structurally incapable of being seen" because the only thing that could
    /// have caught it was a person pressing Play. Navigation has more edge cases than onboarding
    /// did — back out of a run, back out of a panel opened from a run, back at the root — and every
    /// one of them is a place to strand the player.
    /// </summary>
    public sealed class Navigator
    {
        readonly List<AppScreen> _stack = new List<AppScreen>(8);

        /// <summary>True while a run is in flight. Set by the run loop; Back consults it.</summary>
        public bool RunInProgress;

        /// <summary>
        /// Whether this platform may close its own app. False on iOS, where Apple's guidelines are
        /// explicit that an app must not offer to quit — so the button is absent there rather than
        /// present and doing nothing, which is worse.
        /// </summary>
        public bool CanQuit = true;

        public Navigator() { _stack.Add(AppScreen.Launch); }

        public AppScreen Current => _stack[_stack.Count - 1];
        public int Depth => _stack.Count;

        /// <summary>True when there is somewhere to go back to, or something for Back to do.</summary>
        public bool CanGoBack => Current != AppScreen.Launch && (_stack.Count > 1 || CanQuit);

        /// <summary>
        /// Leaves the launch sequence for the root. Launch is replaced rather than pushed, so no
        /// Back can ever return to it — an opening beat you can re-enter is a menu, and the second
        /// time you see it, it is an obstacle.
        /// </summary>
        public void FinishLaunch()
        {
            _stack.Clear();
            _stack.Add(AppScreen.Home);
        }

        public void Push(AppScreen screen)
        {
            if (screen == AppScreen.Launch) return;      // never re-enterable
            if (Current == screen) return;               // double-tap is not two screens deep
            _stack.Add(screen);
        }

        /// <summary>
        /// Drops everything and returns to the root. What a "home" button does, and what has to
        /// happen after abandoning a run rather than unwinding one screen at a time.
        /// </summary>
        public void GoHome()
        {
            _stack.Clear();
            _stack.Add(AppScreen.Home);
        }

        /// <summary>
        /// What Back means from where the player is standing.
        ///
        /// A run in progress is the interesting case: Back must not silently unwind the screen out
        /// from under a live simulation, because the run's water has to be put somewhere first.
        /// The shell is told to abandon it and calls Back again.
        /// </summary>
        public NavAction Back()
        {
            if (Current == AppScreen.Launch) return NavAction.None;
            if (Current == AppScreen.Mountain && RunInProgress) return NavAction.AbandonRun;

            if (_stack.Count > 1)
            {
                _stack.RemoveAt(_stack.Count - 1);
                return NavAction.Changed;
            }
            return CanQuit ? NavAction.Quit : NavAction.None;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _stack.Count; i++)
            {
                if (i > 0) sb.Append(" > ");
                sb.Append(_stack[i]);
            }
            return sb.ToString();
        }
    }
}
