using UnityEngine;
using UnityEngine.InputSystem;

namespace RulesShift
{
    /// <summary>Which button art the prompts use.</summary>
    public enum PadKind { Xbox, PlayStation, Switch }

    /// <summary>
    /// Rebindable keys and gamepad buttons (settings → more): dash, active item, use/talk. 0 / "" in the save = the default.
    /// Every action also listens to the touch controls on phones. The prompts follow the device used last.
    /// </summary>
    public static class Binds
    {
        public const Key DefaultDash = Key.LeftShift, DefaultActive = Key.Q, DefaultUse = Key.E;

        public static Key Dash => K(SaveSystem.Data.dashKey, DefaultDash);
        public static Key Active => K(SaveSystem.Data.activeKey, DefaultActive);
        public static Key Use => K(SaveSystem.Data.useKey, DefaultUse);

        static Key K(int saved, Key def) => saved > 0 && System.Enum.IsDefined(typeof(Key), saved) ? (Key)saved : def;

        /// <summary>The key went down this frame (the default dash also listens to the right Shift).</summary>
        public static bool Down(Key k)
        {
            var kb = Keyboard.current;
            if (kb == null || k == Key.None) return false;
            if (kb[k].wasPressedThisFrame) return true;
            return k == Key.LeftShift && kb.rightShiftKey.wasPressedThisFrame;
        }

        /// <summary>A short name for the key ("Shift", "Q", "F").</summary>
        public static string Name(Key k)
        {
            var kb = Keyboard.current;
            string n = kb != null ? kb[k].displayName : k.ToString();
            if (k == Key.LeftShift || k == Key.RightShift) n = "Shift";
            return string.IsNullOrEmpty(n) ? k.ToString() : n;
        }

        /// <summary>The key sheet icon name for a key, when there is one (lowercase letter keys and a few others).</summary>
        public static string IconName(Key k)
        {
            if (k == Key.LeftShift || k == Key.RightShift) return "shift";
            if (k == Key.Space) return "space";
            if (k == Key.Escape) return "esc";
            string s = k.ToString();
            return s.Length == 1 ? s.ToLowerInvariant() : null;
        }
    
        // ---------------- gamepad buttons ----------------

        public const string DefaultPadDash = "south", DefaultPadActive = "rb", DefaultPadUse = "north";

        public static string PadDash => P(SaveSystem.Data.padDash, DefaultPadDash);
        public static string PadActive => P(SaveSystem.Data.padActive, DefaultPadActive);
        public static string PadUse => P(SaveSystem.Data.padUse, DefaultPadUse);

        static string P(string saved, string def) => string.IsNullOrEmpty(saved) ? def : saved;

        /// <summary>The buttons that can be bound (the sticks move and aim, Start pauses).</summary>
        public static readonly string[] PadButtons = { "south", "east", "west", "north", "lb", "rb", "lt", "rt", "ls", "rs", "select" };

        public static UnityEngine.InputSystem.Controls.ButtonControl PadControl(Gamepad pad, string button)
        {
            if (pad == null) return null;
            switch (button)
            {
                case "south": return pad.buttonSouth;
                case "east": return pad.buttonEast;
                case "west": return pad.buttonWest;
                case "north": return pad.buttonNorth;
                case "lb": return pad.leftShoulder;
                case "rb": return pad.rightShoulder;
                case "lt": return pad.leftTrigger;
                case "rt": return pad.rightTrigger;
                case "ls": return pad.leftStickButton;
                case "rs": return pad.rightStickButton;
                case "select": return pad.selectButton;
                case "start": return pad.startButton;
            }
            return null;
        }

        public static bool PadDown(string button)
        {
            var c = PadControl(Gamepad.current, button);
            return c != null && c.wasPressedThisFrame;
        }

        /// <summary>The pad button pressed this frame (for rebinding), or null.</summary>
        public static string PadPressed()
        {
            var pad = Gamepad.current;
            if (pad == null) return null;
            foreach (var b in PadButtons)
            {
                var c = PadControl(pad, b);
                if (c != null && c.wasPressedThisFrame) return b;
            }
            return null;
        }

        /// <summary>The button as printed on the pad in hand: "A", "✕", "RB", "R1", "ZR"...</summary>
        public static string PadName(string button)
        {
            var t = PadType;
            bool ps = t == PadKind.PlayStation, sw = t == PadKind.Switch;
            switch (button)
            {
                case "south": return ps ? "✕" : sw ? "B" : "A";
                case "east": return ps ? "○" : sw ? "A" : "B";
                case "west": return ps ? "□" : sw ? "Y" : "X";
                case "north": return ps ? "△" : sw ? "X" : "Y";
                case "lb": return ps ? "L1" : sw ? "L" : "LB";
                case "rb": return ps ? "R1" : sw ? "R" : "RB";
                case "lt": return ps ? "L2" : sw ? "ZL" : "LT";
                case "rt": return ps ? "R2" : sw ? "ZR" : "RT";
                case "ls": return ps ? "L3" : "LS";
                case "rs": return ps ? "R3" : "RS";
                case "select": return ps ? "Share" : sw ? "−" : "View";
                case "start": return ps ? "Options" : sw ? "+" : "Menu";
            }
            return button;
        }

        // ---------------- actions (keyboard, gamepad, touch) ----------------

        /// <summary>Dash: the key, the pad button (the default also listens to the left trigger), the touch button.</summary>
        public static bool DashDown() =>
            Down(Dash) || PadDown(PadDash) || (PadDash == DefaultPadDash && PadDown("lt")) || TouchInput.DashDown;

        public static bool ActiveDown() => Down(Active) || PadDown(PadActive) || TouchInput.ActiveDown;

        public static bool UseDown() => Down(Use) || PadDown(PadUse) || TouchInput.UseDown;

        public static bool MapDown()
        {
            var kb = Keyboard.current;
            return (kb != null && (kb.tabKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame)) || PadDown("select") || TouchInput.MapDown;
        }

        public static bool PauseDown()
        {
            var kb = Keyboard.current;
            return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) || PadDown("start") || TouchInput.PauseDown;
        }

        /// <summary>Any key, click, pad button or tap this frame (splash, cutscenes).</summary>
        public static bool AnyPress()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            var touch = Touchscreen.current;
            return (kb != null && kb.anyKey.wasPressedThisFrame) ||
                   (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                   (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)) ||
                   (touch != null && touch.primaryTouch.press.wasPressedThisFrame);
        }

        // ---------------- which device the prompts show ----------------

        /// <summary>The last input came from a gamepad: prompts show its buttons instead of keys.</summary>
        public static bool UsingPad { get; private set; }
        /// <summary>The last input was a finger on the screen (the touch controls are shown).</summary>
        public static bool UsingTouch { get; private set; }
        /// <summary>Raised when the prompts switch between keyboard, gamepad and touch.</summary>
        public static event System.Action PadChanged;

        /// <summary>The look of the gamepad in hand (by its name: DualShock/DualSense, Switch Pro, otherwise Xbox).</summary>
        public static PadKind PadType
        {
            get
            {
                var pad = Gamepad.current;
                if (pad == null) return PadKind.Xbox;
                string n = (pad.layout + " " + pad.description.product + " " + pad.description.manufacturer).ToLowerInvariant();
                if (n.Contains("dualshock") || n.Contains("dualsense") || n.Contains("playstation") || n.Contains("sony")) return PadKind.PlayStation;
                if (n.Contains("switch") || n.Contains("nintendo") || n.Contains("joy-con") || n.Contains("pro controller")) return PadKind.Switch;
                return PadKind.Xbox;
            }
        }

        /// <summary>
        /// Called every frame (RoomManager): any button or stick of a pad → pad prompts; keys or the mouse → keyboard prompts;
        /// a finger on the screen → touch controls. Phones start in touch mode.
        /// </summary>
        public static void Track()
        {
            bool pad = UsingPad, touch = UsingTouch;
            if (!trackedOnce)
            {
                trackedOnce = true;
                touch = Application.isMobilePlatform;
            }
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonNorth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame ||
                               gp.buttonWest.wasPressedThisFrame || gp.startButton.wasPressedThisFrame || gp.rightTrigger.wasPressedThisFrame ||
                               gp.leftStick.ReadValue().sqrMagnitude > 0.25f || gp.rightStick.ReadValue().sqrMagnitude > 0.25f))
            { pad = true; touch = false; }
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed) lastTouch = Time.unscaledTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            // browsers turn taps into mouse events too (iPads are not reported as phones): a "mouse" right after a
            // finger is that same finger
            bool realMouse = mouse != null && !Application.isMobilePlatform && Time.unscaledTime - lastTouch > 1f;
            if ((kb != null && kb.anyKey.wasPressedThisFrame) ||
                (realMouse && (mouse.leftButton.wasPressedThisFrame || mouse.delta.ReadValue().sqrMagnitude > 16f)))
            { pad = false; touch = false; }
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) { pad = false; touch = true; }
            if (pad == UsingPad && touch == UsingTouch) return;
            UsingPad = pad;
            UsingTouch = touch;
            PadChanged?.Invoke();
        }

        static bool trackedOnce;
        static float lastTouch = -10f;

        /// <summary>
        /// The prompt icon of an action for the device in use: "dash", "use", "active", "pause", "map", "attack", "move", "aim".
        /// Keyboard actions without art return null (show the key name then).
        /// </summary>
        public static string Icon(string action)
        {
            if (UsingPad)
                switch (action)
                {
                    case "dash": return "pad:" + PadDash;
                    case "use": return "pad:" + PadUse;
                    case "active": return "pad:" + PadActive;
                    case "pause": return "pad:start";
                    case "map": return "pad:select";
                    case "attack": return "pad:rt";
                    case "move": return "pad:ls";
                    case "aim": return "pad:rs";
                }
            switch (action)
            {
                case "dash": return IconName(Dash) ?? "shift";
                case "use": return IconName(Use);
                case "active": return IconName(Active);
                case "pause": return "esc";
                case "attack": return "lmb";
            }
            return null;
        }

        /// <summary>Text for an action's button (world prompts): the key name, or the pad button's name.</summary>
        public static string Label(string action)
        {
            if (UsingPad) return PadName(action == "dash" ? PadDash : action == "active" ? PadActive : PadUse);
            return Name(action == "dash" ? Dash : action == "active" ? Active : Use);
        }
    }
}
