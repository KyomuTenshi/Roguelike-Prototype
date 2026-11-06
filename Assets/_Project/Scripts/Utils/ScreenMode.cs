using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Fullscreen on and off the same way everywhere: borderless fullscreen at the display's own resolution
    /// on desktop, a 16:9 window that fits the display when windowed, and the browser's fullscreen on the web.
    /// </summary>
    public static class ScreenMode
    {
        static bool? requested;
        static int requestedFrame;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int RulecrawlFullscreen();
#endif
#pragma warning disable 0169, 0414   // used by the web build only
        static bool webWasFull, restoreFull;
#pragma warning restore 0169, 0414

        /// <summary>The mode just asked for (the real switch lands a frame or two later).</summary>
        public static bool IsFull =>
            requested.HasValue && Time.frameCount - requestedFrame < 30 ? requested.Value : Screen.fullScreen;

        public static void Toggle() => Set(!IsFull);

        public static void Set(bool full)
        {
            requested = full;
            requestedFrame = Time.frameCount;
            restoreFull = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            // the browser only allows it from a click or a key press, which is where this is called from;
            // the page's helper does it at once, Unity's own way waits for the next click
            if (!full || RulecrawlFullscreen() == 0) Screen.fullScreen = full;
#else
            var d = Display.main;
            int sw = d != null && d.systemWidth > 0 ? d.systemWidth : Screen.currentResolution.width;
            int sh = d != null && d.systemHeight > 0 ? d.systemHeight : Screen.currentResolution.height;
            if (full)
            {
                Screen.SetResolution(sw, sh, FullScreenMode.FullScreenWindow);
            }
            else
            {
                // the biggest 16:9 window that takes about 80% of the display
                int h = Mathf.RoundToInt(Mathf.Min(sh * 0.8f, sw * 0.8f * 9f / 16f));
                int w = Mathf.RoundToInt(h * 16f / 9f);
                // even sizes: the Pixel Perfect Camera warns (and blurs) at odd ones
                Screen.SetResolution(Mathf.Max(640, w) & ~1, Mathf.Max(360, h) & ~1, FullScreenMode.Windowed);
            }
#endif
        }

        /// <summary>
        /// Web: the browser left fullscreen by itself (Esc in Safari and Firefox, or holding Esc in Chrome).
        /// True once, on that frame: the run pauses, and "Continue" (or starting a run) brings fullscreen back.
        /// </summary>
        public static bool WebFullscreenLost()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Application.isMobilePlatform) return false;   // phones: no fullscreen of our own, and no pause loops
            bool full = Screen.fullScreen;
            bool asked = requested.HasValue && !requested.Value && Time.frameCount - requestedFrame < 60;
            bool lost = webWasFull && !full && !asked;
            webWasFull = full;
            if (lost) restoreFull = true;
            return lost;
#else
            return false;
#endif
        }

        /// <summary>Back to fullscreen if the browser dropped it (call from a click or a key press).</summary>
        public static void RestoreFullscreen()
        {
            if (restoreFull) Set(true);
        }

        /// <summary>
        /// F11 on desktop, plus Option + Enter on macOS. Windows handles Alt + Enter by itself
        /// ("Allow Fullscreen Switch"), and in a browser F11 belongs to the browser.
        /// </summary>
        public static void HotkeyTick()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            bool toggle = kb.f11Key.wasPressedThisFrame;
#if !UNITY_STANDALONE_WIN
            bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            toggle |= alt && kb.enterKey.wasPressedThisFrame;
#endif
            if (toggle) Toggle();
            KeepEven();
#endif
        }

        static float evenCheck;

        /// <summary>
        /// A window dragged to an odd width or height makes the Pixel Perfect Camera complain: snap it back to an even
        /// size (desktop builds, windowed only; the editor's Game view is left alone).
        /// </summary>
        static void KeepEven()
        {
#if !UNITY_EDITOR && !UNITY_WEBGL
            evenCheck -= Time.unscaledDeltaTime;
            if (evenCheck > 0f) return;
            evenCheck = 0.5f;
            if (Screen.fullScreenMode != FullScreenMode.Windowed) return;
            int w = Screen.width, h = Screen.height;
            if ((w & 1) == 0 && (h & 1) == 0) return;
            Screen.SetResolution(w & ~1, h & ~1, FullScreenMode.Windowed);
#endif
        }
    }
}
