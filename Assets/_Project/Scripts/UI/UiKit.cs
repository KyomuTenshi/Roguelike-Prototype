using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RulesShift
{
    /// <summary>
    /// Builds uGUI from code: frames, buttons, plates and parchment from the Dark Ages UI pack
    /// (1 art pixel = 2 UI units; falls back to the old stone sprites in Assets/_Project/UI/Sprites),
    /// Press Start 2P for titles and buttons, Handjet for body text.
    /// </summary>
    public static class UiKit
    {
        public static Font Font;        // body (Handjet, or the CJK font in Chinese)
        public static Font TitleFont;   // titles & buttons (Press Start 2P, or the CJK font in Chinese)
        public static Font LatinTitleFont;  // always Press Start 2P (game logo)
        static bool cjk;

        public const float PixelScale = 1f / 3f;   // Image.pixelsPerUnitMultiplier for the 9-slices

        public const float DarkAgesScale = 0.5f;   // Dark Ages UI: 1 art pixel = 2 UI units

        // Dark Ages palette: honey gold #dcc47c, tan #9f8873, charcoal brown #2E322A
        public static readonly Color Gold = new Color(0.94f, 0.82f, 0.50f);
        public static readonly Color Cream = new Color(0.93f, 0.89f, 0.80f);
        public static readonly Color Stone = new Color(0.83f, 0.75f, 0.66f);
        public static readonly Color Dim = new Color(0.72f, 0.64f, 0.56f, 0.9f);
        public static readonly Color Ink = new Color(0.18f, 0.20f, 0.16f, 1f);
        public static readonly Color Red = new Color(1f, 0.38f, 0.32f);

        static Font Body => Font ? Font : Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        static Font Head => TitleFont ? TitleFont : Body;

        static GameAssets A => GameAssets.I;
        static Sprite PanelSprite => A && A.uiPanel ? A.uiPanel : Gfx.Panel;
        static Sprite ButtonSprite => A && A.uiButton ? A.uiButton : Gfx.Panel;

        public static void Init()
        {
            Loc.Init();
            if (A)
            {
                cjk = Loc.Cjk && A.cjkFont;
                Font = cjk ? A.cjkFont : A.font;
                TitleFont = cjk ? A.cjkFont : A.titleFont;
                LatinTitleFont = A.titleFont;
            }
        }

        // ---------------- containers ----------------

        public static RectTransform Canvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            canvas.pixelPerfect = true;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Group(Transform parent, string name, Vector2 pos, Vector2 size) =>
            Rect(parent, name, new Vector2(0.5f, 0.5f), pos, size);

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A sprite from the pack shown in the UI, pixel-scaled (3 UI units per pixel by default).</summary>
        public static Image Icon(Transform parent, Sprite sprite, Vector2 pos, float scale = 3f)
        {
            Vector2 size = sprite ? new Vector2(sprite.rect.width, sprite.rect.height) * scale : Vector2.one * 16f * scale;
            var img = Image(parent, sprite ? sprite.name : "Icon", sprite, Color.white, new Vector2(0.5f, 0.5f), pos, size);
            img.preserveAspect = true;
            return img;
        }

        /// <summary>Stone-framed panel (tiled 9-slice).</summary>
        public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = Image(parent, name, PanelSprite, Color.white, anchor, pos, size);
            Tile(img);
            return img;
        }

        static bool IsFrame(Sprite s) =>
            A && s && (s == A.uiPanel || s == A.uiButton || s == A.uiButtonHover || s == A.uiButtonOn ||
                       s == A.uiPlate || s == A.uiParchment || s == A.uiBarBack);

        static void Tile(Image img)
        {
            bool real = IsFrame(img.sprite);
            img.type = real ? UnityEngine.UI.Image.Type.Tiled : UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = real ? (A.DarkAges ? DarkAgesScale : PixelScale) : 1.5f;
        }

        /// <summary>Quiet dark plate (HUD). Falls back to the panel.</summary>
        public static Image Plate(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = Image(parent, name, A && A.uiPlate ? A.uiPlate : PanelSprite, Color.white, anchor, pos, size);
            Tile(img);
            return img;
        }

        /// <summary>Parchment (hints, notes). Text on it should use Ink and NoShadow.</summary>
        public static Image Parchment(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            bool has = A && A.uiParchment;
            var img = Image(parent, name, has ? A.uiParchment : PanelSprite, Color.white, anchor, pos, size);
            Tile(img);
            return img;
        }

        public static bool HasParchment => A && A.uiParchment;

        /// <summary>Removes the drop shadow / outline (dark text on parchment).</summary>
        public static Text NoShadow(Text t)
        {
            foreach (var sh in t.GetComponents<Shadow>()) UnityEngine.Object.Destroy(sh);
            return t;
        }

        /// <summary>Blue plate with a gold crown and a title on it (top of the menu boards).</summary>
        public static RectTransform TitlePlate(Transform parent, string text, Vector2 pos, float width, Color color)
        {
            var sprite = A && A.uiButtonOn ? A.uiButtonOn : ButtonSprite;
            var img = Image(parent, "TitlePlate", sprite, Color.white, new Vector2(0.5f, 0.5f), pos, new Vector2(width, 60f));
            Tile(img);
            if (A && A.uiCrown)
            {
                var crown = Image(img.rectTransform, "Crown", A.uiCrown, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -2f),
                    new Vector2(A.uiCrown.rect.width * 2f, A.uiCrown.rect.height * 2f));
                crown.rectTransform.pivot = new Vector2(0.5f, 0f);
            }
            Title(img.rectTransform, text, 24, color, new Vector2(0f, 1f), new Vector2(width - 60f, 44f));
            return img.rectTransform;
        }

        /// <summary>Keyboard / mouse prompt icon (gdb keyboard sheet), 2 UI units per art pixel.</summary>
        public static Image KeyIcon(Transform parent, string key, Vector2 pos, float scale = 2f)
        {
            var s = A ? A.Key(key) : null;
            if (!s) return null;
            var img = Image(parent, "Key_" + key, s, Color.white, new Vector2(0.5f, 0.5f), pos,
                new Vector2(s.rect.width * scale, s.rect.height * scale));
            img.preserveAspect = true;
            return img;
        }

        /// <summary>
        /// A centered row "[W][A][S][D] move  [LMB] attack …": entries are (keys, label).
        /// Falls back to plain text when the key sheet is missing.
        /// </summary>
        public static RectTransform KeyRow(Transform parent, Vector2 pos, (string[] keys, string label)[] entries, int fontSize = 24, string fallback = "")
        {
            var row = Rect(parent, "Keys", new Vector2(0.5f, 0.5f), pos, new Vector2(1000f, 40f));
            if (!A || !A.keyPrompts)
            {
                Label(row, fallback, fontSize, Dim, Vector2.zero, new Vector2(1000f, 32f));
                return row;
            }
            // measure: key widths + label widths (approximate by character count)
            float gap = 10f, total = 0f;
            var widths = new float[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                float w = 0f;
                foreach (var k in entries[i].keys) { var s = A.Key(k); if (s) w += s.rect.width * 2f + 4f; }
                w += 8f + entries[i].label.Length * fontSize * 0.42f;
                widths[i] = w;
                total += w + (i > 0 ? gap * 3f : 0f);
            }
            float x = -total / 2f;
            for (int i = 0; i < entries.Length; i++)
            {
                float cx = x;
                foreach (var k in entries[i].keys)
                {
                    var s = A.Key(k);
                    if (!s) continue;
                    float kw = s.rect.width * 2f;
                    KeyIcon(row, k, new Vector2(cx + kw / 2f, 0f));
                    cx += kw + 4f;
                }
                var t = Text(row, entries[i].label, fontSize, Dim, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f),
                    new Vector2(cx + 8f, 0f), new Vector2(entries[i].label.Length * fontSize * 0.5f + 20f, 32f));
                t.rectTransform.pivot = new Vector2(0f, 0.5f);
                x += widths[i] + gap * 3f;
            }
            return row;
        }

        /// <summary>Full-screen dimmed overlay with a soft vignette.</summary>
        public static GameObject Screen(Transform parent, string name, float dim)
        {
            var img = Image(parent, name, Gfx.White, new Color(0.03f, 0.02f, 0.04f, dim), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(img.rectTransform);
            img.raycastTarget = true;
            var v = Image(img.rectTransform, "Vignette", Gfx.Vignette, new Color(0f, 0f, 0f, 0.85f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(v.rectTransform);
            img.gameObject.AddComponent<CanvasGroup>();
            return img.gameObject;
        }

        // ---------------- text ----------------

        /// <summary>
        /// Text that always stays inside its box: it wraps and shrinks (best fit) instead of spilling out.
        /// Pixel-font sizes should be multiples of 8; Chinese gets bigger titles and a slightly smaller body.
        /// </summary>
        public static Text Text(Transform parent, string text, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box, bool title = false)
        {
            var rt = Rect(parent, "Text", anchor, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = title ? Head : Body;
            t.text = text;
            int fs = size;
            if (cjk) fs = title ? Mathf.RoundToInt(size * 1.5f) : Mathf.Max(16, Mathf.RoundToInt(size * 0.85f));
            t.fontSize = fs;
            t.color = color;
            t.alignment = align;
            t.lineSpacing = title && !cjk ? 1.3f : 1f;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = fs;
            t.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(fs * (title ? 0.5f : 0.55f)));
            t.raycastTarget = false;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = title ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
            if (title)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.13f, 0.13f, 0.13f, 1f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        /// <summary>Text that may overflow its box (counters that never get long, animated letters).</summary>
        public static Text Free(Text t)
        {
            t.resizeTextForBestFit = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Centered body text.</summary>
        public static Text Label(Transform parent, string text, int size, Color color, Vector2 pos, Vector2 box) =>
            Text(parent, text, size, color, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), pos, box);

        /// <summary>Centered pixel-font text. Use sizes that are multiples of 8.</summary>
        public static Text Title(Transform parent, string text, int size, Color color, Vector2 pos, Vector2 box) =>
            Text(parent, text, size, color, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), pos, box, true);

        /// <summary>Body text that wraps inside its box.</summary>
        public static Text Paragraph(Transform parent, string text, int size, Color color, Vector2 pos, Vector2 box, TextAnchor align = TextAnchor.MiddleCenter)
        {
            return Text(parent, text, size, color, align, new Vector2(0.5f, 0.5f), pos, box);
        }

        // ---------------- buttons ----------------

        /// <summary>Stone button: hover/selection lights it gold, an arrow points at it. Mouse, keyboard and gamepad.</summary>
        public static UnityEngine.UI.Button Button(Transform parent, string caption, Vector2 pos, Vector2 size, Action onClick, int fontSize = 16)
        {
            var img = Image(parent, "Button", ButtonSprite, Color.white, new Vector2(0.5f, 0.5f), pos, size);
            img.raycastTarget = true;
            Tile(img);
            var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = img;
            if (A && A.uiButtonHover)
            {
                b.transition = Selectable.Transition.SpriteSwap;
                var st = new SpriteState
                {
                    highlightedSprite = A.uiButtonHover,
                    selectedSprite = A.uiButtonHover,
                    pressedSprite = A.uiButtonPressed ? A.uiButtonPressed : A.uiButtonHover,
                    disabledSprite = A.uiButtonDisabled ? A.uiButtonDisabled : A.uiButton
                };
                b.spriteState = st;
            }
            else
            {
                var c = b.colors;
                c.highlightedColor = new Color(1f, 0.88f, 0.6f);
                c.selectedColor = new Color(1f, 0.82f, 0.5f);
                c.pressedColor = new Color(0.8f, 0.68f, 0.45f);
                c.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.8f);
                b.colors = c;
            }
            if (onClick != null) b.onClick.AddListener(() => { Sfx.Play("button", 0.6f); onClick(); });
            if (!string.IsNullOrEmpty(caption))
            {
                var t = Title(img.rectTransform, caption, fontSize, Cream, new Vector2(0f, 2f), size - new Vector2(24f, 10f));
                t.supportRichText = true;
            }
            img.gameObject.AddComponent<UiButtonFx>();
            return b;
        }

        /// <summary>Makes a toggle-like button look "on" (red stone).</summary>
        public static void SetOn(UnityEngine.UI.Button b, bool on)
        {
            var img = b.targetGraphic as Image;
            if (!img) return;
            if (A && A.uiButtonOn) img.sprite = on ? A.uiButtonOn : A.uiButton;
            else img.color = on ? new Color(1f, 0.6f, 0.55f) : Color.white;
            Tile(img);
        }

        /// <summary>Marks a choice (mode, hero) as the current one: gold frame that stays.</summary>
        public static void SetChosen(UnityEngine.UI.Button b, bool chosen)
        {
            var img = b.targetGraphic as Image;
            if (!img) return;
            if (A && A.DarkAges && A.uiButtonOn) img.sprite = chosen ? A.uiButtonOn : A.uiButton;
            else if (A && A.uiButtonHover) img.sprite = chosen ? A.uiButtonHover : A.uiButton;
            else img.color = chosen ? new Color(1f, 0.78f, 0.42f) : Color.white;
            Tile(img);
            // a small crown over the chosen option
            var crown = img.transform.Find("Crown");
            if (chosen && !crown && A && A.uiCrown)
            {
                var c = Image(img.rectTransform, "Crown", A.uiCrown, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -2f),
                    new Vector2(A.uiCrown.rect.width * 2f, A.uiCrown.rect.height * 2f));
                c.rectTransform.pivot = new Vector2(0.5f, 0f);
            }
            else if (crown) crown.gameObject.SetActive(chosen);
            var fx = b.GetComponent<UiButtonFx>();
            if (fx) fx.chosen = chosen;
        }

        public static void SetCaption(Selectable b, string text, Color? color = null)
        {
            var t = b.GetComponentInChildren<Text>();
            if (!t) return;
            t.text = text;
            if (color.HasValue) t.color = color.Value;
        }

        public static void Select(Selectable s)
        {
            if (!s || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        /// <summary>Hanging banner from the pack in a law's colour (red / blue / green / yellow).</summary>
        public static Sprite BannerFor(Color c)
        {
            if (!A) return null;
            string n;
            if (c.b > c.r && c.b > c.g) n = "wall_banner_blue";
            else if (c.g > c.r * 0.95f && c.g > c.b) n = "wall_banner_green";
            else if (c.r > 0.9f && c.g > 0.65f) n = "wall_banner_yellow";
            else n = "wall_banner_red";
            return A.D(n);
        }
    }
}
