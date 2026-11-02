using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RulesShift
{
    /// <summary>Short UI tweens (0.15–0.35 s) on unscaled time, so they also run in pause and flask choice.</summary>
    public static class UiAnim
    {
        class Runner : MonoBehaviour { }
        static Runner runner;

        static Runner R
        {
            get
            {
                if (!runner)
                {
                    var go = new GameObject("UiAnim");
                    runner = go.AddComponent<Runner>();
                }
                return runner;
            }
        }

        public static Coroutine Run(IEnumerator routine) => R.StartCoroutine(routine);

        public static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        static CanvasGroup Group(Component c)
        {
            var g = c.GetComponent<CanvasGroup>();
            return g ? g : c.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>Slides in from an offset while fading in, after a delay (for staggered lists).</summary>
        public static void Appear(RectTransform rt, float delay, Vector2 from, float time = 0.28f)
        {
            if (!rt) return;
            var g = Group(rt);
            g.alpha = 0f;
            Vector2 end = rt.anchoredPosition;
            rt.anchoredPosition = end + from;
            Run(AppearRoutine(rt, g, delay, end + from, end, time));
        }

        static IEnumerator AppearRoutine(RectTransform rt, CanvasGroup g, float delay, Vector2 a, Vector2 b, float time)
        {
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                if (!rt) yield break;
                float k = t / time;
                rt.anchoredPosition = Vector2.LerpUnclamped(a, b, EaseOutBack(k));
                g.alpha = EaseOut(k * 1.4f);
                yield return null;
            }
            if (!rt) yield break;
            rt.anchoredPosition = b;
            g.alpha = 1f;
        }

        public static void FadeIn(Component c, float time = 0.2f)
        {
            if (!c) return;
            var g = Group(c);
            g.alpha = 0f;
            Run(FadeRoutine(g, 0f, 1f, time));
        }

        static IEnumerator FadeRoutine(CanvasGroup g, float a, float b, float time)
        {
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                if (!g) yield break;
                g.alpha = Mathf.Lerp(a, b, t / time);
                yield return null;
            }
            if (g) g.alpha = b;
        }

        /// <summary>Scales up from "from" with a small overshoot.</summary>
        public static void Pop(Transform tr, float from = 0.85f, float time = 0.22f, float delay = 0f)
        {
            if (!tr) return;
            tr.localScale = Vector3.one * from;
            Run(PopRoutine(tr, from, time, delay));
        }

        static IEnumerator PopRoutine(Transform tr, float from, float time, float delay)
        {
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                if (!tr) yield break;
                tr.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, EaseOutBack(t / time));
                yield return null;
            }
            if (tr) tr.localScale = Vector3.one;
        }

        /// <summary>Quick scale punch (hearts, score).</summary>
        public static void Punch(Transform tr, float amount = 0.3f, float time = 0.25f)
        {
            if (!tr) return;
            Run(PunchRoutine(tr, amount, time));
        }

        static IEnumerator PunchRoutine(Transform tr, float amount, float time)
        {
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                if (!tr) yield break;
                float k = t / time;
                tr.localScale = Vector3.one * (1f + amount * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
                yield return null;
            }
            if (tr) tr.localScale = Vector3.one;
        }

        /// <summary>Counts a number up in a text: "{0}" in the format is replaced.</summary>
        public static void CountUp(Text text, int target, float time, string format, float delay = 0f)
        {
            if (!text) return;
            Run(CountRoutine(text, target, time, format, delay));
        }

        static IEnumerator CountRoutine(Text text, int target, float time, string format, float delay)
        {
            text.text = string.Format(format, 0);
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
            float last = 0f;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                if (!text) yield break;
                int v = Mathf.RoundToInt(target * EaseOut(t / time));
                text.text = string.Format(format, v);
                if (t - last > 0.06f) { last = t; Sfx.Play("select", 0.25f, 1.6f); }
                yield return null;
            }
            if (text) text.text = string.Format(format, target);
        }
    }

    /// <summary>
    /// Button feel: the mouse selects what it hovers (one highlight for mouse and keyboard),
    /// the selected button grows a little, its caption turns gold and a pack arrow points at it.
    /// </summary>
    public class UiButtonFx : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        public bool chosen;
        Selectable sel;
        Text caption;
        RectTransform arrow;
        bool selected;
        float scale = 1f;
        Color baseColor;

        void Awake()
        {
            sel = GetComponent<Selectable>();
            caption = GetComponentInChildren<Text>();
            if (caption) baseColor = caption.color;
            var rt = (RectTransform)transform;
            var s = GameAssets.I ? GameAssets.I.D("weapon_arrow") : null;
            if (s)
            {
                var img = UiKit.Image(rt, "Pointer", s, Color.white, new Vector2(0f, 0.5f), new Vector2(-30f, 0f),
                    new Vector2(s.rect.width, s.rect.height) * 2.5f);
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                arrow = img.rectTransform;
                arrow.gameObject.SetActive(false);
            }
        }

        public void OnSelect(BaseEventData e)
        {
            selected = true;
            if (arrow) arrow.gameObject.SetActive(true);
            Sfx.Play("select", 0.35f);
        }

        public void OnDeselect(BaseEventData e)
        {
            selected = false;
            if (arrow) arrow.gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (sel && sel.IsInteractable() && EventSystem.current && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool on = selected && sel && sel.IsInteractable();
            scale = Mathf.MoveTowards(scale, on ? 1.05f : 1f, dt * 0.8f);
            transform.localScale = Vector3.one * scale;
            if (caption)
            {
                Color target = !sel || sel.IsInteractable() ? (on || chosen ? UiKit.Gold : baseColor) : new Color(0.6f, 0.58f, 0.62f);
                caption.color = Color.Lerp(caption.color, target, dt * 14f);
            }
            if (arrow && arrow.gameObject.activeSelf)
                arrow.anchoredPosition = new Vector2(-30f - 6f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)), 0f);
        }
    }

    /// <summary>Plays sprite frames on a UI Image (animated portraits, torches, flasks).</summary>
    public class UiSpriteAnim : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 8f;
        Image img;
        float t;

        public static UiSpriteAnim Add(Image img, Sprite[] frames, float fps)
        {
            var a = img.gameObject.AddComponent<UiSpriteAnim>();
            a.frames = frames;
            a.fps = fps;
            a.t = Random.value * 4f;
            return a;
        }

        void Awake() => img = GetComponent<Image>();

        void Update()
        {
            if (frames == null || frames.Length == 0 || !img) return;
            t += Time.unscaledDeltaTime * fps;
            img.sprite = frames[(int)t % frames.Length];
        }
    }

    /// <summary>Floats a UI element up and down.</summary>
    public class UiBob : MonoBehaviour
    {
        public float amplitude = 6f;
        public float speed = 2.5f;
        RectTransform rt;
        Vector2 basePos;
        float phase;

        public static UiBob Add(Component c, float amplitude = 6f, float speed = 2.5f)
        {
            var b = c.gameObject.AddComponent<UiBob>();
            b.amplitude = amplitude;
            b.speed = speed;
            return b;
        }

        void Start()
        {
            rt = (RectTransform)transform;
            basePos = rt.anchoredPosition;
            phase = Random.value * 6f;
        }

        void Update()
        {
            if (!rt) return;
            // pixel steps: round to 3 UI units (one game pixel)
            float y = Mathf.Round(Mathf.Sin(Time.unscaledTime * speed + phase) * amplitude / 3f) * 3f;
            rt.anchoredPosition = basePos + new Vector2(0f, y);
        }
    }

    /// <summary>
    /// The title "RULES SHIFT" as separate letters: they drop in one by one, then the rules keep
    /// shifting — a letter jumps and takes a law's colour, sometimes two letters swap places.
    /// </summary>
    public class TitleGlitch : MonoBehaviour
    {
        static readonly Law[] Palette = { Law.Crypt, Law.Ice, Law.Mirror, Law.Pacifist, Law.Lava, Law.Neon, Law.Stealth, Law.Boss };

        readonly List<Text> letters = new List<Text>();
        readonly List<Vector2> homes = new List<Vector2>();
        Vector2[] offsets;
        float timer = 1.6f, swapTimer = 4f;
        Color baseColor = UiKit.Cream;

        public static TitleGlitch Create(Transform parent, string text, int size, Vector2 pos)
        {
            var rt = UiKit.Group(parent, "Title", pos, new Vector2(text.Length * size, size * 1.4f));
            var g = rt.gameObject.AddComponent<TitleGlitch>();
            float x0 = -(text.Length - 1) * size / 2f;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ') continue;
                var t = UiKit.Title(rt, text[i].ToString(), size, g.baseColor, new Vector2(x0 + i * size, 0f), new Vector2(size * 1.5f, size * 1.5f));
                UiKit.Free(t);
                if (UiKit.LatinTitleFont) t.font = UiKit.LatinTitleFont;   // the logo stays pixel art in every language
                t.fontSize = size;
                t.lineSpacing = 1.3f;
                g.letters.Add(t);
                g.homes.Add(t.rectTransform.anchoredPosition);
                UiAnim.Appear(t.rectTransform, 0.08f + 0.05f * g.letters.Count, new Vector2(0f, 60f), 0.35f);
            }
            g.offsets = new Vector2[g.letters.Count];
            return g;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            timer -= dt;
            swapTimer -= dt;
            if (timer <= 0f && letters.Count > 0)
            {
                timer = Random.Range(0.9f, 1.8f);
                int i = Random.Range(0, letters.Count);
                offsets[i] = new Vector2(0f, 12f);
                letters[i].color = Rules.Info(Palette[Random.Range(0, Palette.Length)]).color;
            }
            if (swapTimer <= 0f && letters.Count > 1)
            {
                swapTimer = Random.Range(3.5f, 6f);
                int a = Random.Range(0, letters.Count), b = Random.Range(0, letters.Count);
                if (a != b)
                {
                    (homes[a], homes[b]) = (homes[b], homes[a]);
                    Sfx.Play("law", 0.15f, 1.8f);
                    StartCoroutine(SwapBack(a, b));
                }
            }
            for (int i = 0; i < letters.Count; i++)
            {
                if (!letters[i]) continue;
                offsets[i] = Vector2.MoveTowards(offsets[i], Vector2.zero, dt * 40f);
                var rt = letters[i].rectTransform;
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg && cg.alpha < 0.99f) continue;   // still dropping in
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, homes[i] + offsets[i], dt * 12f);
                letters[i].color = Color.Lerp(letters[i].color, baseColor, dt * 0.8f);
            }
        }

        IEnumerator SwapBack(int a, int b)
        {
            for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime) yield return null;
            (homes[a], homes[b]) = (homes[b], homes[a]);
        }
    }
}
