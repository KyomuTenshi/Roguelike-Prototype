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

        /// <summary>The mode just asked for (the real switch lands a frame or two later).</summary>
        public static bool IsFull =>
            requested.HasValue && Time.frameCount - requestedFrame < 30 ? requested.Value : Screen.fullScreen;

        public static void Toggle() => Set(!IsFull);

        public static void Set(bool full)
        {
            requested = full;
            requestedFrame = Time.frameCount;
#if UNITY_WEBGL && !UNITY_EDITOR
            // the browser only allows it from a click or a key press, which is where this is called from
            Screen.fullScreen = full;
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
                Screen.SetResolution(Mathf.Max(640, w), Mathf.Max(360, h), FullScreenMode.Windowed);
            }
#endif
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
#endif
        }
    }
}
