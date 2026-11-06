using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RulesShift
{
    /// <summary>What the touch controls want this frame (read by the player, the rooms and Binds).</summary>
    public static class TouchInput
    {
        /// <summary>The controls are on screen (phone in hand, a run going).</summary>
        public static bool Active { get; internal set; }
        public static Vector2 Move { get; internal set; }
        public static Vector2 Aim { get; internal set; }
        /// <summary>A finger is on the aim stick: aim there and shoot.</summary>
        public static bool AimHeld { get; internal set; }

        static int dashFrame = -1, activeFrame = -1, useFrame = -1, mapFrame = -1, pauseFrame = -1;

        public static bool DashDown => dashFrame == Time.frameCount;
        public static bool ActiveDown => activeFrame == Time.frameCount;
        public static bool UseDown => useFrame == Time.frameCount;
        public static bool MapDown => mapFrame == Time.frameCount;
        public static bool PauseDown => pauseFrame == Time.frameCount;

        internal static void Press(TouchControls.Action a)
        {
            int f = Time.frameCount;
            switch (a)
            {
                case TouchControls.Action.Dash: dashFrame = f; break;
                case TouchControls.Action.Active: activeFrame = f; break;
                case TouchControls.Action.Use: useFrame = f; break;
                case TouchControls.Action.Map: mapFrame = f; break;
                case TouchControls.Action.Pause: pauseFrame = f; break;
            }
        }

        internal static void Release()
        {
            Move = Aim = Vector2.zero;
            AimHeld = false;
        }
    }

    /// <summary>
    /// Phone controls: a floating move stick on the left half, an aim stick on the right half (holding it shoots),
    /// round buttons for the dash, the active item and talking, plus the map and pause in the corner.
    /// Shown only while a run is going and the last input was a finger. Runs before everything else,
    /// so the presses are seen in the same frame.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class TouchControls : MonoBehaviour
    {
        public enum Action { Dash, Active, Use, Map, Pause }

        const float StickRadius = 70f;      // canvas units the knob can travel
        const float DeadZone = 0.18f;

        class Stick
        {
            public RectTransform root, knob;
            public Image baseImg, knobImg;
            public Vector2 rest;            // anchored position when nobody holds it
            public int finger = -1;
            public Vector2 center;          // screen pixels
            public Vector2 value;
        }

        class Button
        {
            public Action action;
            public RectTransform rt;
            public Image ring, fill, icon;
            public Text label;
            public float pressed;
        }

        RectTransform root, safe;
        CanvasGroup group;
        Canvas canvas;
        Stick move, aim;
        Button[] buttons;
        Rect lastSafe;
        float lastSize = -1f;
        string activeShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("TouchControls");
            DontDestroyOnLoad(go);
            go.AddComponent<TouchControls>();
        }

        void Build()
        {
            root = UiKit.Canvas(transform, "Touch", 15);
            canvas = root.GetComponent<Canvas>();
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;   // the fingers are read directly: menus under it keep working
            safe = UiKit.Rect(root, "Safe", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            float k = Mathf.Clamp(SaveSystem.Data.touchSize, 0.8f, 1.3f);
            lastSize = k;

            move = MakeStick("Move", new Vector2(0f, 0f), new Vector2(190f, 170f) * k, k);
            aim = MakeStick("Aim", new Vector2(1f, 0f), new Vector2(-190f, 170f) * k, k);
            buttons = new[]
            {
                MakeButton(Action.Dash, new Vector2(1f, 0f), new Vector2(-380f, 92f) * k, 104f * k, new Color(0.55f, 1f, 0.85f)),
                MakeButton(Action.Active, new Vector2(1f, 0f), new Vector2(-386f, 236f) * k, 92f * k, UiKit.Gold),
                MakeButton(Action.Use, new Vector2(1f, 0f), new Vector2(-250f, 338f) * k, 88f * k, new Color(0.75f, 0.85f, 1f)),
                // under the right plate of the HUD (room, law, score)
                MakeButton(Action.Pause, new Vector2(1f, 1f), new Vector2(-56f, -214f), 72f, UiKit.Cream),
                MakeButton(Action.Map, new Vector2(1f, 1f), new Vector2(-56f, -300f), 72f, UiKit.Cream),
            };
            foreach (var b in buttons) Caption(b);
            ApplySafeArea();
        }

        Stick MakeStick(string name, Vector2 anchor, Vector2 rest, float k)
        {
            var s = new Stick { rest = rest };
            s.root = UiKit.Rect(safe, name, anchor, rest, new Vector2(190f, 190f) * k);
            s.baseImg = UiKit.Image(s.root, "Base", Gfx.Ring, new Color(1f, 1f, 1f, 0.35f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 190f) * k);
            s.knobImg = UiKit.Image(s.root, "Knob", Gfx.Dot, new Color(1f, 1f, 1f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f) * k);
            s.knob = s.knobImg.rectTransform;
            s.baseImg.raycastTarget = s.knobImg.raycastTarget = false;
            return s;
        }

        Button MakeButton(Action a, Vector2 anchor, Vector2 pos, float size, Color color)
        {
            var b = new Button { action = a };
            b.rt = UiKit.Rect(safe, a.ToString(), anchor, pos, new Vector2(size, size));
            b.fill = UiKit.Image(b.rt, "Fill", Gfx.Dot, new Color(0f, 0f, 0f, 0.35f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            b.ring = UiKit.Image(b.rt, "Ring", Gfx.Ring, new Color(color.r, color.g, color.b, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            b.icon = UiKit.Image(b.rt, "Icon", Gfx.White, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.55f, size * 0.55f));
            b.icon.preserveAspect = true;
            b.icon.enabled = false;
            b.label = UiKit.Text(b.rt, "", Mathf.RoundToInt(size * 0.17f), color, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.4f, size * 0.5f), true);
            b.fill.raycastTarget = b.ring.raycastTarget = b.icon.raycastTarget = b.label.raycastTarget = false;
            return b;
        }

        /// <summary>The words on the buttons (rebuilt with the language) and the pause bars.</summary>
        void Caption(Button b)
        {
            switch (b.action)
            {
                case Action.Dash: b.label.text = Loc.T("touch.dash"); break;
                case Action.Use: b.label.text = Loc.T("touch.use"); break;
                case Action.Map: b.label.text = Loc.T("touch.map"); break;
                case Action.Active: b.label.text = ""; break;
                case Action.Pause:
                    b.label.text = "";
                    if (b.rt.Find("BarL") == null)
                    {
                        float s = b.rt.sizeDelta.x;
                        var l = UiKit.Image(b.rt, "BarL", Gfx.White, UiKit.Cream, new Vector2(0.5f, 0.5f), new Vector2(-s * 0.1f, 0f), new Vector2(s * 0.11f, s * 0.36f));
                        var r = UiKit.Image(b.rt, "BarR", Gfx.White, UiKit.Cream, new Vector2(0.5f, 0.5f), new Vector2(s * 0.1f, 0f), new Vector2(s * 0.11f, s * 0.36f));
                        l.raycastTarget = r.raycastTarget = false;
                    }
                    break;
            }
        }

        /// <summary>Notches and rounded corners: everything sits inside Screen.safeArea.</summary>
        void ApplySafeArea()
        {
            Rect sa = Screen.safeArea;
            lastSafe = sa;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        bool ShouldShow()
        {
            if (!Binds.UsingTouch || Touchscreen.current == null) return false;
            var rm = RoomManager.I;
            if (rm == null || rm.State != GameState.Playing) return false;
            if (Cutscene.Playing || Time.timeScale <= 0f) return false;
            if (Hud.I && Hud.I.Choosing) return false;
            return true;
        }

        void Update()
        {
            bool show = ShouldShow();
            if (!show)
            {
                if (root) root.gameObject.SetActive(false);
                if (TouchInput.Active) { TouchInput.Active = false; TouchInput.Release(); }
                if (move != null) { Reset(move); Reset(aim); }
                return;
            }
            float k = Mathf.Clamp(SaveSystem.Data.touchSize, 0.8f, 1.3f);
            if (root && !Mathf.Approximately(k, lastSize)) { Destroy(root.gameObject); root = null; }   // the size changed in the settings
            if (!root) Build();
            if (!root.gameObject.activeSelf) { root.gameObject.SetActive(true); foreach (var b in buttons) Caption(b); }
            if (Screen.safeArea != lastSafe) ApplySafeArea();
            TouchInput.Active = true;

            ReadFingers();
            TouchInput.Move = move.value;
            TouchInput.AimHeld = aim.finger >= 0 && aim.value.sqrMagnitude > DeadZone * DeadZone;
            TouchInput.Aim = TouchInput.AimHeld ? aim.value.normalized : Vector2.zero;
            Refresh();
        }

        void ReadFingers()
        {
            var ts = Touchscreen.current;
            float scale = canvas ? canvas.scaleFactor : 1f;
            bool leftHeld = false, rightHeld = false;
            foreach (var t in ts.touches)
            {
                if (t == null) continue;
                int id = t.touchId.ReadValue();
                Vector2 pos = t.position.ReadValue();
                if (t.press.wasPressedThisFrame)
                {
                    var b = ButtonAt(pos, scale);
                    if (b != null)
                    {
                        b.pressed = 0.15f;
                        if (b.action != Action.Use || Interactable.AnyFocused) TouchInput.Press(b.action);
                        continue;
                    }
                    bool left = pos.x < Screen.width * 0.5f;
                    var s = left ? move : aim;
                    if (s.finger < 0)
                    {
                        s.finger = id;
                        s.center = ClampCenter(pos, left, scale);
                        s.root.position = s.center;   // overlay canvas: world position = screen pixels
                    }
                }
                if (id == move.finger) { if (t.press.isPressed) { Drag(move, pos, scale); leftHeld = true; } }
                else if (id == aim.finger) { if (t.press.isPressed) { Drag(aim, pos, scale); rightHeld = true; } }
            }
            if (!leftHeld && move.finger >= 0) Reset(move);
            if (!rightHeld && aim.finger >= 0) Reset(aim);
        }

        /// <summary>The stick appears under the thumb but never half off the screen.</summary>
        Vector2 ClampCenter(Vector2 pos, bool left, float scale)
        {
            float r = (StickRadius + 30f) * scale;
            Rect sa = Screen.safeArea;
            float minX = left ? sa.xMin + r : Screen.width * 0.5f + r * 0.5f;
            float maxX = left ? Screen.width * 0.5f - r * 0.5f : sa.xMax - r;
            return new Vector2(Mathf.Clamp(pos.x, minX, Mathf.Max(minX, maxX)), Mathf.Clamp(pos.y, sa.yMin + r, sa.yMax - r));
        }

        void Drag(Stick s, Vector2 pos, float scale)
        {
            float k = Mathf.Clamp(SaveSystem.Data.touchSize, 0.8f, 1.3f);
            float radius = StickRadius * k * scale;
            Vector2 d = Vector2.ClampMagnitude(pos - s.center, radius);
            s.knob.anchoredPosition = d / scale;
            Vector2 v = d / radius;
            s.value = v.magnitude < DeadZone ? Vector2.zero : v * Mathf.InverseLerp(DeadZone, 1f, v.magnitude) / v.magnitude;
            s.baseImg.color = new Color(1f, 1f, 1f, 0.55f);
            s.knobImg.color = new Color(1f, 1f, 1f, 0.75f);
        }

        void Reset(Stick s)
        {
            s.finger = -1;
            s.value = Vector2.zero;
            if (!s.root) return;
            s.root.anchoredPosition = s.rest;
            s.knob.anchoredPosition = Vector2.zero;
            s.baseImg.color = new Color(1f, 1f, 1f, 0.3f);
            s.knobImg.color = new Color(1f, 1f, 1f, 0.4f);
        }

        Button ButtonAt(Vector2 screen, float scale)
        {
            Button best = null;
            float bestD = float.MaxValue;
            foreach (var b in buttons)
            {
                if (!b.rt.gameObject.activeInHierarchy) continue;
                Vector2 c = b.rt.position;
                float r = b.rt.sizeDelta.x * 0.5f * scale * 1.25f;   // a bit bigger than drawn: thumbs are wide
                float d = Vector2.Distance(screen, c);
                if (d < r && d < bestD) { best = b; bestD = d; }
            }
            return best;
        }

        /// <summary>Button states: the active item's icon and charge, "talk" only near someone, the dash only for heroes who have it.</summary>
        void Refresh()
        {
            var p = PlayerController.I;
            foreach (var b in buttons)
            {
                b.pressed -= Time.unscaledDeltaTime;
                float press = b.pressed > 0f ? 0.85f : 1f;
                b.rt.localScale = Vector3.one * press;
                bool on = true;
                switch (b.action)
                {
                    case Action.Use: on = Interactable.AnyFocused; break;
                    case Action.Dash:
                        bool has = p != null && p.CanDash;
                        b.rt.gameObject.SetActive(has);
                        if (!has) continue;
                        on = p.DashReady;
                        break;
                    case Action.Active:
                        var a = p != null ? p.Active : null;
                        b.rt.gameObject.SetActive(a != null);
                        if (a == null) continue;
                        if (activeShown != a.key)
                        {
                            activeShown = a.key;
                            var sprite = GameAssets.I ? GameAssets.I.Any(a.sprite) : null;
                            b.icon.sprite = sprite;
                            b.icon.enabled = sprite != null;
                            b.label.text = sprite != null ? "" : a.Name;
                        }
                        on = p.ActiveReady;
                        break;
                }
                float alpha = on ? 1f : 0.35f;
                b.ring.color = new Color(b.ring.color.r, b.ring.color.g, b.ring.color.b, 0.8f * alpha);
                b.fill.color = new Color(0f, 0f, 0f, 0.35f * alpha);
                if (b.icon.enabled) b.icon.color = new Color(1f, 1f, 1f, alpha);
                b.label.color = new Color(b.label.color.r, b.label.color.g, b.label.color.b, alpha);
            }
        }
    }
}
