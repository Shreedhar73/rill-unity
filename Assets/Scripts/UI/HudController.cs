using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Rill.App;
using Rill.Flow;
using Rill.Meta;

namespace Rill.UI
{
    /// <summary>
    /// The entire interface. Four buttons when idle, one line of text during a run, and a card at
    /// the end that proves the run mattered. Everything else is the mountain.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        public event Action AlmanacRequested;
        public event Action TimeLapseRequested;
        public event Action DailyRequested;
        public event Action ShareRequested;
        public event Action BoatRequested;
        public event Action RecordsRequested;
        public event Action ExpeditionRequested;
        public event Action ReportDismissed;
        public event Action PanelClosed;
        /// <summary>The on-screen back affordance. The hardware back key raises the same action.</summary>
        public event Action BackRequested;
        /// <summary>Leave the mountain and go back to the main screen. Not an app quit.</summary>
        public event Action EndGameRequested;
        /// <summary>A mountain row on the main screen was chosen. Argument is the slot index.</summary>
        public event Action<int> MountainPicked;

        Canvas _canvas;
        Text _topLeft, _topRight, _hint, _reportTitle, _panelBody, _panelTitle;
        Image _reportCard, _panel, _speedFill;
        CanvasGroup _reportGroup, _panelGroup, _buttonsGroup, _speedGroup, _titleGroup, _endGameGroup;
        Text _titleWord, _titleTag, _titleRecord, _titleForecast;
        Button _startButton, _backButton;
        GameObject _titleHolder;
        Button[] _slotButtons;
        Text[] _slotLabels;
        CanvasGroup _backGroup;
        bool _titleShown;
        float _titleFade;
        const float TitleFadeSeconds = 1.4f;
        RectTransform _buttons;

        /// <summary>
        /// Draws the interface through a specific camera instead of straight to the screen.
        ///
        /// Screen-space-overlay canvases are composited after everything and never appear in a
        /// Camera.Render, which is why every piece of UI in this project has shipped unlooked-at.
        /// This is the seam that lets the offscreen capture tool photograph the HUD.
        /// </summary>
        public void RenderThroughCamera(Camera cam)
        {
            if (_canvas == null || cam == null) return;
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = cam;
            _canvas.planeDistance = 1f;
        }

        /// <summary>Back to the normal overlay after an offscreen capture borrowed the canvas.</summary>
        public void RenderOverlay()
        {
            if (_canvas == null) return;
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.worldCamera = null;
        }

        public bool ReportVisible { get; private set; }
        public bool PanelVisible { get; private set; }

        public void Build()
        {
            _canvas = UIFactory.CreateCanvas("RillCanvas");
            _canvas.transform.SetParent(transform, false);

            // Everything hangs off a safe-area frame rather than the raw canvas. Corner controls are
            // exactly what this protects: a button pinned to the true corner of a modern phone is
            // pinned under its camera cutout or its home indicator.
            var safe = new GameObject("SafeArea", typeof(RectTransform));
            safe.transform.SetParent(_canvas.transform, false);
            var safeRect = UIFactory.Rect(safe);
            UIFactory.ApplySafeArea(safeRect);
            var root = safe.transform;

            _topLeft = UIFactory.MakeText(root, "TopLeft", "", 32, TextAnchor.UpperLeft, UIFactory.Ink);
            // Starts clear of the back glyph in the corner, so the two never overlap on any device.
            UIFactory.Place(_topLeft.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(Edge + IconSize + 24f, -52f), new Vector2(620f, 90f));

            _topRight = UIFactory.MakeText(root, "TopRight", "", 28, TextAnchor.UpperRight, UIFactory.InkDim);
            UIFactory.Place(_topRight.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-46f, -52f), new Vector2(460f, 90f));

            _hint = UIFactory.MakeText(root, "Hint", "", 34, TextAnchor.LowerCenter, UIFactory.InkDim);
            UIFactory.Place(_hint.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(900f, 90f));

            BuildTitle(root);
            BuildBack(root);
            BuildSpeedMeter(root);
            BuildButtons(root);
            BuildReportCard(root);
            BuildPanel(root);

            SetReportVisible(false);
            SetPanelVisible(false);
        }

        /// <summary>
        /// The title. It deliberately shows the player's own mountain drifting behind it rather
        /// than any art: the world is the save file, so the most honest splash screen this game can
        /// have is the thing they made last time. A returning player sees their own river system
        /// before they see a button.
        /// </summary>
        void BuildTitle(Transform root)
        {
            var holder = _titleHolder = new GameObject("Title", typeof(RectTransform));
            holder.transform.SetParent(root, false);
            var hrt = UIFactory.Rect(holder);
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;

            // Clear at the top so the summit keeps its silhouette, dense at the bottom where the
            // name and the button need to be legible against whatever terrain drifts past.
            UIFactory.MakeGradient(holder.transform, "Wash",
                                   new Color(0.04f, 0.05f, 0.07f, 0.06f),
                                   new Color(0.03f, 0.04f, 0.06f, 0.80f));

            _titleWord = UIFactory.MakeText(holder.transform, "Word", "RILL", 150, TextAnchor.MiddleCenter, UIFactory.Ink);
            UIFactory.Place(_titleWord.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1000f, 190f));
            _titleWord.raycastTarget = false;

            _titleTag = UIFactory.MakeText(holder.transform, "Tag", "Steer the water. The mountain remembers.",
                                           34, TextAnchor.MiddleCenter, UIFactory.InkDim);
            UIFactory.Place(_titleTag.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1100f, 60f));
            _titleTag.raycastTarget = false;

            _titleRecord = UIFactory.MakeText(holder.transform, "Record", "", 30, TextAnchor.MiddleCenter, UIFactory.InkDim);
            UIFactory.Place(_titleRecord.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1100f, 56f));
            _titleRecord.raycastTarget = false;

            // The weather appointment. Its own element rather than a line of Record, because
            // SetMountains hides Record permanently once the slot rows exist — a forecast
            // appended there was built, correct, and structurally invisible.
            _titleForecast = UIFactory.MakeText(holder.transform, "Forecast", "", 28, TextAnchor.MiddleCenter, UIFactory.InkDim);
            UIFactory.Place(_titleForecast.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 96f), new Vector2(1100f, 48f));
            _titleForecast.raycastTarget = false;

            _startButton = UIFactory.MakeButton(holder.transform, "Start", "Begin", 40);
            UIFactory.Place(_startButton.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(420f, 108f));

            // One row per mountain, stacked BELOW Begin. Built once and re-labelled, because a
            // menu that destroys and rebuilds its own buttons is a menu that loses a click.
            //
            // The rows do not replace Begin. They did for one build, and the report was immediate:
            // "the start button is gone". Three stat lines are a place to switch mountains, not an
            // obvious way to start the game — the primary verb keeps a primary button.
            _slotButtons = new Button[MountainRoster.Slots];
            _slotLabels = new Text[MountainRoster.Slots];
            for (int i = 0; i < MountainRoster.Slots; i++)
            {
                Text label;
                var b = UIFactory.MakeButton(holder.transform, "Slot" + i, "", 26, out label);
                UIFactory.Place(b.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                new Vector2(0f, -210f - i * 112f), new Vector2(880f, 96f));
                _slotButtons[i] = b;
                _slotLabels[i] = label;
                int index = i;
                b.onClick.AddListener(() => { if (MountainPicked != null) MountainPicked(index); });
            }
            SetMountainsVisible(false);

            // The record, not a score: one modest control under the rows, opening the screen
            // that says what the current mountain has had done to it. (L-051)
            var records = UIFactory.MakeButton(holder.transform, "Records", "Records", 26);
            UIFactory.Place(records.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, -210f - MountainRoster.Slots * 112f), new Vector2(300f, 84f));
            records.onClick.AddListener(() => { if (RecordsRequested != null) RecordsRequested(); });

            // The other two modes, named, each explaining itself in one line. (L-048) Mountains
            // is Begin and the rows above; these are the deliberate departures from it — the
            // Daily's shared rock, and the expedition's blind date with a new one.
            float modeY = -210f - MountainRoster.Slots * 112f - 118f;
            var dailyBtn = UIFactory.MakeButton(holder.transform, "DailyMode", "Daily Rill", 26);
            UIFactory.Place(dailyBtn.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(-235f, modeY), new Vector2(450f, 84f));
            dailyBtn.onClick.AddListener(() => { if (DailyRequested != null) DailyRequested(); });

            var expBtn = UIFactory.MakeButton(holder.transform, "Expedition", "Expedition", 26);
            UIFactory.Place(expBtn.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(235f, modeY), new Vector2(450f, 84f));
            expBtn.onClick.AddListener(() => { if (ExpeditionRequested != null) ExpeditionRequested(); });

            var modeCaption = UIFactory.MakeText(holder.transform, "ModeCaptions",
                "Today's rock, same for everyone · seven runs        Meet a new mountain · keep it or walk away",
                20, TextAnchor.MiddleCenter, UIFactory.InkDim);
            UIFactory.Place(modeCaption.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, modeY - 62f), new Vector2(1000f, 36f));
            modeCaption.raycastTarget = false;

            _titleGroup = UIFactory.Group(holder);
        }

        /// <summary>
        /// The three mountains, on the main screen. Each row says what has been done to that
        /// mountain — read off the world, never awarded — or that the slot is empty.
        ///
        /// There is no delete button here on purpose. Destroying a mountain goes through
        /// MountainRoster.Delete, which demands the seed of the thing being destroyed, and a
        /// control that sits one mis-tap away from six months of somebody's play does not belong
        /// on the screen they see every time they open the app.
        /// </summary>
        public void SetMountains(string[] rows)
        {
            if (_slotButtons == null) return;

            // The aggregate record line is redundant the moment each row carries its own, and it
            // sat right on top of the second one. Rendered, seen, removed.
            if (_titleRecord != null) _titleRecord.gameObject.SetActive(false);
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                bool has = rows != null && i < rows.Length && !string.IsNullOrEmpty(rows[i]);
                _slotButtons[i].gameObject.SetActive(has);
                if (has && _slotLabels[i] != null) _slotLabels[i].text = rows[i];
            }
        }

        public void SetMountainsVisible(bool visible)
        {
            if (_slotButtons == null) return;
            for (int i = 0; i < _slotButtons.Length; i++)
                if (_slotButtons[i] != null) _slotButtons[i].gameObject.SetActive(visible);
        }

        /// <summary>
        /// The one affordance that has to exist on every screen. Placed top-left, away from the
        /// thumb: the whole game is played with one thumb at the bottom of the screen, and a
        /// destructive-ish control under it would be pressed by accident during a run.
        ///
        /// End game sits on the mountain, not on the main screen: it means "I am done with this
        /// session, take me back", which is meaningless when you are already there. It ends any run
        /// in flight, saves, and returns home. It does NOT close the application.
        /// </summary>
        void BuildBack(Transform root)
        {
            // Hard into the top-left corner, as a square glyph rather than a labelled slab.
            //
            // It used to be a 220x84 button reading "‹ Back" floating at (150, -160) — which is not
            // a corner, it is the middle of the sky above the mountain, and at that size it was a
            // piece of furniture sitting on the picture. A chrome control should be findable and
            // otherwise invisible: the game is the mountain, and every pixel this takes is one the
            // mountain does not get.
            _backButton = UIFactory.MakeIconButton(root, "Back", "‹", 52);
            UIFactory.Place(_backButton.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(Edge + IconSize * 0.5f, -(Edge + IconSize * 0.5f)),
                            new Vector2(IconSize, IconSize));
            _backGroup = UIFactory.Group(_backButton.gameObject);
            _backButton.onClick.AddListener(() => { if (BackRequested != null) BackRequested(); });
            SetBackVisible(false);
        }

        /// <summary>Corner inset, and the size of a corner control. One thumb-width, no smaller.</summary>
        const float Edge = 34f;
        const float IconSize = 92f;

        public void SetBackVisible(bool visible)
        {
            if (_backGroup == null) return;
            _backGroup.alpha = visible ? 1f : 0f;
            _backGroup.interactable = visible;
            _backGroup.blocksRaycasts = visible;
        }

        /// <summary>
        /// Shows or hides the main screen. Driven from the run loop's state every frame rather than
        /// called once at the moment of leaving it.
        ///
        /// Reported as "after Begin is pressed, the main screen doesn't go away". Hiding it used to
        /// be the responsibility of exactly one call site — whoever handled the Begin button — so
        /// any other route into play left the title sitting at full opacity on top of a live run.
        /// A screen that is visible because somebody remembered to hide the other one is a screen
        /// that will eventually be visible when they did not.
        /// </summary>
        public void SetTitleShown(bool visible)
        {
            if (_titleHolder == null || _titleGroup == null) return;

            // Guard on REALITY, never on a cached flag. The previous version returned early when
            // _titleShown already matched, which meant that if the flag and the pixels ever
            // disagreed — for any reason, in any order, once — nothing could ever put them back.
            // That is precisely how a title screen survives on top of a running game: something
            // said "already hidden" while it was plainly on screen.
            //
            // Hiding also DISABLES the object rather than only zeroing a CanvasGroup. A disabled
            // GameObject cannot draw under any circumstance, whatever else has been done to alphas
            // or sibling order further down the tree.
            if (_titleHolder.activeSelf != visible)
            {
                _titleHolder.SetActive(visible);
                if (visible) _titleFade = 0f;
            }
            _titleShown = visible;

            float target = visible ? _titleGroup.alpha : 0f;
            if (_titleGroup.alpha != target) _titleGroup.alpha = target;
            if (_titleGroup.blocksRaycasts != visible) _titleGroup.blocksRaycasts = visible;
            if (_titleGroup.interactable != visible) _titleGroup.interactable = visible;
        }

        /// <summary>Sets the main screen's content. Visibility is owned by SetTitleShown.</summary>
        public void SetTitle(bool visible, string record, System.Action onStart)
        {
            if (_titleGroup == null) return;
            SetTitleShown(visible);
            // Fade up from nothing rather than appearing on frame one, so the mountain is on screen
            // a beat before the words land on it.
            if (visible) { _titleFade = 0f; _titleGroup.alpha = 0f; }
            if (_titleRecord != null) _titleRecord.text = record ?? "";
            if (_startButton != null)
            {
                _startButton.onClick.RemoveAllListeners();
                if (onStart != null) _startButton.onClick.AddListener(() => onStart());
            }
        }

        /// <summary>The next change in the weather, shown under the tagline. Empty hides it.</summary>
        public void SetTitleForecast(string line)
        {
            if (_titleForecast == null) return;
            _titleForecast.text = line ?? "";
            _titleForecast.gameObject.SetActive(!string.IsNullOrEmpty(line));
        }

        public bool TitleVisible => _titleGroup != null && _titleGroup.blocksRaycasts;

        /// <summary>
        /// Completes the title's fade immediately. Only the capture tool uses it: the fade is driven
        /// by Update, which does not run in an editor script, so without this the main screen
        /// photographs as a blank sky and the one screen nobody could check stays uncheckable.
        /// </summary>
        public void SettleTitle()
        {
            if (_titleGroup == null) return;
            _titleFade = 1f;
            _titleGroup.alpha = _titleShown ? 1f : 0f;
        }

        /// <summary>
        /// What is actually on screen, read from the objects rather than from a flag — so a caller
        /// checking this cannot be told the same lie the flag was telling.
        /// </summary>
        public bool TitleOnScreen => _titleHolder != null && _titleHolder.activeSelf
                                     && _titleGroup != null && _titleGroup.alpha > 0.001f;

        void Update()
        {
            if (_titleGroup == null || !_titleShown) return;

            if (_titleFade < 1f)
            {
                _titleFade = Mathf.Min(1f, _titleFade + Time.deltaTime / TitleFadeSeconds);
                // Ease out: quick to become visible, slow to settle.
                _titleGroup.alpha = 1f - (1f - _titleFade) * (1f - _titleFade);
            }

            // The button breathes. Nothing else on this screen moves except the mountain, and a
            // still button on a drifting landscape reads as a screenshot rather than a game.
            if (_startButton != null)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 2.1f) * 0.022f;
                _startButton.transform.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        void BuildSpeedMeter(Transform root)
        {
            // Square: an 8 px bar with a 28 px corner radius is a lozenge with nothing left of it.
            var back = UIFactory.MakePanel(root, "SpeedTrack", new Color(1f, 1f, 1f, 0.10f), false);
            UIFactory.Place(back.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(560f, 8f));
            back.raycastTarget = false;

            _speedFill = UIFactory.MakePanel(back.transform, "SpeedFill", UIFactory.Accent, false);
            var rt = UIFactory.Rect(_speedFill.gameObject);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, 0f);
            _speedFill.raycastTarget = false;

            _speedGroup = UIFactory.Group(back.gameObject);
            _speedGroup.alpha = 0f;
        }

        void BuildButtons(Transform root)
        {
            var holder = new GameObject("Buttons", typeof(RectTransform));
            holder.transform.SetParent(root, false);
            _buttons = UIFactory.Rect(holder);
            _buttons.anchorMin = new Vector2(0.5f, 0f);
            _buttons.anchorMax = new Vector2(0.5f, 0f);
            _buttons.pivot = new Vector2(0.5f, 0f);
            _buttons.anchoredPosition = new Vector2(0f, 48f);
            _buttons.sizeDelta = new Vector2(1000f, 110f);
            _buttonsGroup = UIFactory.Group(holder);

            // End game belongs in this row, not floating in the corner of the play area. The row
            // only exists while the mountain is idle, which is exactly when leaving it makes sense,
            // and a control that ends the session should sit with the other deliberate choices
            // rather than hovering over the water.
            string[] labels = { "Almanac", "Time-lapse", "Boat", "Daily Rill", "Share", "End game" };
            Action[] actions =
            {
                () => { if (AlmanacRequested != null) AlmanacRequested(); },
                () => { if (TimeLapseRequested != null) TimeLapseRequested(); },
                () => { if (BoatRequested != null) BoatRequested(); },
                () => { if (DailyRequested != null) DailyRequested(); },
                () => { if (ShareRequested != null) ShareRequested(); },
                () => { if (EndGameRequested != null) EndGameRequested(); }
            };

            // Six buttons in the same 1000-wide row the five used: 6×160 + 5×8 = 1000 exactly.
            float w = 160f, gap = 8f;
            float total = labels.Length * w + (labels.Length - 1) * gap;
            for (int i = 0; i < labels.Length; i++)
            {
                // End game sits in the row's last cell but NOT under the row's CanvasGroup: the
                // row hides during a run, and that made "end the game mid-run" — the entire
                // request behind L-052 — structurally impossible to press. EndGame() itself
                // always handled the mid-run case; only the button vanished.
                Transform parent = holder.transform;
                if (labels[i] == "End game")
                {
                    var solo = new GameObject("EndGameHolder", typeof(RectTransform));
                    solo.transform.SetParent(holder.transform, false);
                    var rt = UIFactory.Rect(solo);
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = _buttons.sizeDelta;
                    _endGameGroup = UIFactory.Group(solo);
                    // The row's group zeroes its alpha during a run; without this the child
                    // group multiplies against that zero and the button stays invisible however
                    // hard SetEndGameVisible tries.
                    _endGameGroup.ignoreParentGroups = true;
                    parent = solo.transform;
                }
                // 25 rather than 28: "Time-lapse" has to fit the narrower six-across cell.
                var btn = UIFactory.MakeButton(parent, "Btn" + labels[i], labels[i], 25);
                float x = -total * 0.5f + w * 0.5f + i * (w + gap);
                UIFactory.Place(btn.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(w, 88f));
                var a = actions[i];
                btn.onClick.AddListener(() => a());
            }
        }

        /// <summary>
        /// End game alone, for the states where the rest of the row is hidden — a live run needs
        /// a way out on every platform, not only where a hardware back key exists.
        /// </summary>
        public void SetEndGameVisible(bool visible)
        {
            if (_endGameGroup == null) return;
            _endGameGroup.alpha = visible ? 1f : 0f;
            _endGameGroup.interactable = visible;
            _endGameGroup.blocksRaycasts = visible;
        }

        void BuildReportCard(Transform root)
        {
            // More opaque than the standard panel on purpose. The card is the run's reward and the
            // one piece of UI that must be readable over anything — noon sea or midnight rock — and
            // at the panel's 0.72 it was neither: photographed washed-out and reported as such.
            _reportCard = UIFactory.MakePanel(root, "CarveReport", new Color(0.07f, 0.085f, 0.11f, 0.93f));
            UIFactory.Place(_reportCard.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 620f));
            _reportGroup = UIFactory.Group(_reportCard.gameObject);

            _reportTitle = UIFactory.MakeText(_reportCard.transform, "Title", "", 44, TextAnchor.MiddleCenter, UIFactory.Ink);
            UIFactory.Place(_reportTitle.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(820f, 96f));

            _reportSub = UIFactory.MakeText(_reportCard.transform, "Sub", "", 27, TextAnchor.MiddleCenter, UIFactory.InkDim);
            UIFactory.Place(_reportSub.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(820f, 44f));

            // A short accent rule between the headline and the numbers. It is the only colour on
            // the card, which is what makes the headline read as a headline.
            var rule = UIFactory.MakePanel(_reportCard.transform, "Rule", new Color(0.55f, 0.84f, 0.93f, 0.85f), false);
            UIFactory.Place(rule.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -182f), new Vector2(220f, 3f));
            rule.raycastTarget = false;

            var rows = new GameObject("Rows", typeof(RectTransform));
            rows.transform.SetParent(_reportCard.transform, false);
            _reportRows = UIFactory.Rect(rows);
            _reportRows.anchorMin = _reportRows.anchorMax = new Vector2(0.5f, 1f);
            _reportRows.pivot = new Vector2(0.5f, 1f);
            _reportRows.anchoredPosition = new Vector2(0f, -ReportHeader);
            _reportRows.sizeDelta = new Vector2(790f, 10f);

            _reportFooter = UIFactory.MakeText(_reportCard.transform, "Footer", "Tap to continue", 26, TextAnchor.MiddleCenter, UIFactory.InkDim);
            UIFactory.Place(_reportFooter.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(500f, 44f));

            var dismiss = _reportCard.gameObject.AddComponent<Button>();
            dismiss.onClick.AddListener(() =>
            {
                SetReportVisible(false);
                if (ReportDismissed != null) ReportDismissed();
            });
        }

        // Vertical space above the first row (title, subtitle, rule) and below the last (footer).
        const float ReportHeader = 212f;
        const float ReportFooter = 92f;
        const float ReportRowHeight = 52f;

        RectTransform _reportRows;
        Text _reportSub, _reportFooter;
        readonly List<GameObject> _reportRowItems = new List<GameObject>();
        float _reportRowY;

        void ClearReportRows()
        {
            for (int i = 0; i < _reportRowItems.Count; i++)
            {
                if (_reportRowItems[i] == null) continue;
                // The capture tool shows this card outside play mode, where Destroy is an error.
                if (Application.isPlaying) Destroy(_reportRowItems[i]);
                else DestroyImmediate(_reportRowItems[i]);
            }
            _reportRowItems.Clear();
            _reportRowY = 0f;
        }

        /// <summary>A measurement: dim label on the left, bright value on the right, one line each.
        /// The old card packed these into one Text with run-together spaces, which read as a log
        /// dump — columns are most of the difference between a card and a printout.</summary>
        void AddReportStat(string label, string value)
        {
            var l = UIFactory.MakeText(_reportRows, "L", label, 28, TextAnchor.MiddleLeft, UIFactory.InkDim);
            UIFactory.Place(l.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -_reportRowY), new Vector2(440f, ReportRowHeight));
            var v = UIFactory.MakeText(_reportRows, "V", value, 28, TextAnchor.MiddleRight, UIFactory.Ink);
            UIFactory.Place(v.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -_reportRowY), new Vector2(340f, ReportRowHeight));
            _reportRowItems.Add(l.gameObject);
            _reportRowItems.Add(v.gameObject);
            _reportRowY += ReportRowHeight;
        }

        /// <summary>Something that happened, as opposed to something measured: centred, in the
        /// accent, because these lines — a dam break, reeds arriving, a fossil — are the reasons
        /// to read the card at all.</summary>
        void AddReportEvent(string text)
        {
            var t = UIFactory.MakeText(_reportRows, "E", text, 28, TextAnchor.MiddleCenter,
                                       new Color(0.62f, 0.87f, 0.95f, 0.95f));
            UIFactory.Place(t.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_reportRowY), new Vector2(790f, ReportRowHeight));
            _reportRowItems.Add(t.gameObject);
            _reportRowY += ReportRowHeight;
        }

        void AddReportGap() { _reportRowY += 20f; }

        void BuildPanel(Transform root)
        {
            _panel = UIFactory.MakePanel(root, "Panel", new Color(0.05f, 0.06f, 0.08f, 0.94f));
            UIFactory.Anchor(_panel.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _panelGroup = UIFactory.Group(_panel.gameObject);

            _panelTitle = UIFactory.MakeText(_panel.transform, "PanelTitle", "", 48, TextAnchor.UpperLeft, UIFactory.Ink);
            UIFactory.Place(_panelTitle.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -70f), new Vector2(900f, 90f));

            _panelBody = UIFactory.MakeText(_panel.transform, "PanelBody", "", 28, TextAnchor.UpperLeft, UIFactory.InkDim);
            UIFactory.Place(_panelBody.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -190f), new Vector2(960f, 1400f));

            var close = UIFactory.MakeButton(_panel.transform, "Close", "Close", 30);
            UIFactory.Place(close.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(300f, 90f));
            close.onClick.AddListener(() =>
            {
                SetPanelVisible(false);
                if (PanelClosed != null) PanelClosed();
            });

            // The choice row, for the one panel that is a question rather than a page: keeping an
            // expedition. Hidden unless ShowChoice asked for it.
            _choiceA = UIFactory.MakeButton(_panel.transform, "ChoiceA", "", 30, out _choiceALabel);
            UIFactory.Place(_choiceA.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-240f, 190f), new Vector2(420f, 100f));
            _choiceA.onClick.AddListener(() =>
            {
                var act = _onChoiceA;
                SetPanelVisible(false);
                if (act != null) act();
            });
            _choiceB = UIFactory.MakeButton(_panel.transform, "ChoiceB", "", 30, out _choiceBLabel);
            UIFactory.Place(_choiceB.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(240f, 190f), new Vector2(420f, 100f));
            _choiceB.onClick.AddListener(() =>
            {
                var act = _onChoiceB;
                SetPanelVisible(false);
                if (act != null) act();
            });
            _choiceA.gameObject.SetActive(false);
            _choiceB.gameObject.SetActive(false);
        }

        Button _choiceA, _choiceB;
        Text _choiceALabel, _choiceBLabel;
        Action _onChoiceA, _onChoiceB;

        /// <summary>
        /// A panel with a question in it. Option A may be null (a choice with one honest option
        /// is still a choice — "no free slot" leaves only walking away). Close remains available
        /// and means "not yet".
        /// </summary>
        public void ShowChoice(string title, string body, string aLabel, Action onA, string bLabel, Action onB)
        {
            ShowPanel(title, body);
            _onChoiceA = onA;
            _onChoiceB = onB;
            bool hasA = !string.IsNullOrEmpty(aLabel);
            _choiceA.gameObject.SetActive(hasA);
            if (hasA) _choiceALabel.text = aLabel;
            _choiceB.gameObject.SetActive(true);
            _choiceBLabel.text = bLabel;
        }

        // ------------------------------------------------------------------ state

        public void SetTopLine(string left, string right)
        {
            if (_topLeft != null) _topLeft.text = left;
            if (_topRight != null) _topRight.text = right;
        }

        public void SetHint(string text)
        {
            if (_hint != null) _hint.text = text;
        }

        public void SetIdleUI(bool visible)
        {
            if (_buttonsGroup == null) return;
            _buttonsGroup.alpha = visible ? 1f : 0f;
            _buttonsGroup.interactable = visible;
            _buttonsGroup.blocksRaycasts = visible;
        }

        public void SetSpeed(float speed01, bool visible)
        {
            if (_speedGroup == null) return;
            _speedGroup.alpha = Mathf.MoveTowards(_speedGroup.alpha, visible ? 1f : 0f, Time.deltaTime * 4f);
            if (_speedFill != null)
            {
                var rt = UIFactory.Rect(_speedFill.gameObject);
                float w = 560f * Mathf.Clamp01(speed01);
                rt.sizeDelta = new Vector2(w, 0f);
            }
        }

        public void ShowReport(CarveReport rep, int secretsFound, int secretsTotal)
        {
            if (_reportCard == null) return;
            _reportTitle.text = rep.Summary();
            _reportSub.text = rep.EndingLine + "   ·   run " + rep.RunNumber.ToString("n0");

            ClearReportRows();

            // What happened, before what was measured. The events are why the card is worth
            // reading; the numbers are the receipt.
            int events = 0;
            for (int i = 0; i < rep.BasinChanges.Count && i < 3; i++)
            {
                var b = rep.BasinChanges[i];
                AddReportEvent(string.Format("{0}   {1:0}% → {2:0}%", b.Name, b.Before01 * 100f, b.After01 * 100f));
                events++;
            }
            for (int i = 0; i < rep.Headlines.Count; i++) { AddReportEvent(rep.Headlines[i]); events++; }
            for (int i = 0; i < rep.LifeArrivals.Count; i++) { AddReportEvent(rep.LifeArrivals[i]); events++; }
            if (events > 0) AddReportGap();

            AddReportStat("Sediment moved", string.Format("{0:n1} m³", rep.SedimentMoved));
            if (rep.DeepestCarve > 0.001f) AddReportStat("Deepest cut", string.Format("{0:0.00} m", rep.DeepestCarve));
            if (rep.NewChannelMetres > 0.5f) AddReportStat("Channel worked", string.Format("{0:0} m", rep.NewChannelMetres));
            AddReportStat("Distance", string.Format("{0:0} m", rep.DistanceTravelled));
            AddReportStat("Top speed", string.Format("{0:0.0} m/s", rep.TopSpeed));
            if (rep.WaterToSea > 0.01f) AddReportStat("Delivered to sea", string.Format("{0:0} m³", rep.WaterToSea));

            if (rep.GatesThreaded > 0 || rep.SeedsCaught > 0 || rep.FlowersSplashed > 0)
            {
                AddReportGap();
                if (rep.GatesThreaded > 0) AddReportStat("Gates threaded", rep.GatesThreaded.ToString());
                if (rep.SeedsCaught > 0) AddReportStat("Seeds carried", rep.SeedsCaught.ToString());
                if (rep.FlowersSplashed > 0) AddReportStat("Dye splashed", rep.FlowersSplashed.ToString());
            }

            AddReportGap();
            AddReportStat("Uncovered", secretsFound + " of " + secretsTotal);

            // The last thing read is the reason to run again. One line, world-derived, never a
            // demand — the card states what is close and lets the player decide it matters.
            if (!string.IsNullOrEmpty(rep.NextLine))
            {
                AddReportGap();
                AddReportEvent("▸ " + rep.NextLine);
            }

            // The card is exactly as tall as what it has to say.
            var rt = UIFactory.Rect(_reportCard.gameObject);
            rt.sizeDelta = new Vector2(900f, Mathf.Clamp(ReportHeader + _reportRowY + ReportFooter, 480f, 1400f));

            SetReportVisible(true);
        }

        public void ShowPanel(string title, string body)
        {
            _panelTitle.text = title;
            _panelBody.text = body;
            // A plain page never inherits the previous question's buttons.
            if (_choiceA != null) _choiceA.gameObject.SetActive(false);
            if (_choiceB != null) _choiceB.gameObject.SetActive(false);
            _onChoiceA = null;
            _onChoiceB = null;
            SetPanelVisible(true);
        }

        void SetReportVisible(bool v)
        {
            ReportVisible = v;
            if (_reportGroup == null) return;
            _reportGroup.alpha = v ? 1f : 0f;
            _reportGroup.interactable = v;
            _reportGroup.blocksRaycasts = v;
        }

        void SetPanelVisible(bool v)
        {
            PanelVisible = v;
            if (_panelGroup == null) return;
            _panelGroup.alpha = v ? 1f : 0f;
            _panelGroup.interactable = v;
            _panelGroup.blocksRaycasts = v;
        }

        public void HideAllPanels()
        {
            SetReportVisible(false);
            SetPanelVisible(false);
        }

        /// <summary>Formats the Almanac for the panel: a place's biography, newest first.</summary>
        public static string FormatAlmanac(Almanac almanac, int max = 40)
        {
            var sb = new StringBuilder();
            var entries = almanac.Entries;
            if (entries.Count == 0) return "Nothing has happened here yet.\n\nRelease the water.";
            int start = Mathf.Max(0, entries.Count - max);
            for (int i = entries.Count - 1; i >= start; i--)
            {
                var e = entries[i];
                sb.AppendFormat("run {0,-5}  {1,-14}  {2}\n", e.Run, e.DateLabel, e.Text);
            }
            return sb.ToString();
        }
    }
}
