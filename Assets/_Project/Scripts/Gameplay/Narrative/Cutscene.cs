using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RulesShift
{
    /// <summary>
    /// In-engine cutscenes: letterbox bars, subtitles, a camera dolly and actors that walk.
    /// Any key (or click) moves the dialogue on; Esc skips the whole scene — every wait then ends at once.
    /// </summary>
    public static class Cutscene
    {
        public static bool Playing { get; private set; }
        public static bool Skipped { get; private set; }

        static CutsceneUI ui;
        static Transform dolly;
        static Transform cameraTarget;

        public static CutsceneUI UI
        {
            get
            {
                if (!ui) ui = new GameObject("Cutscene").AddComponent<CutsceneUI>();
                return ui;
            }
        }

        // ---------------- start / end ----------------

        public static void Begin()
        {
            Playing = true;
            Skipped = false;
            UI.Bars(true);
            Projectile.ClearAll();
            if (PlayerController.I) { PlayerController.I.frozen = true; PlayerController.I.scriptedMove = Vector2.zero; }
            var cam = CameraFollow.I;
            if (cam)
            {
                cameraTarget = cam.target;
                if (!dolly) dolly = new GameObject("CutsceneDolly").transform;
                dolly.position = cam.target ? cam.target.position : (Vector3)cam.bounds.center;
                cam.target = dolly;
            }
        }

        public static void End(bool giveControl = true)
        {
            Playing = false;
            Skipped = false;
            UI.Bars(false);
            UI.HideLine();
            var cam = CameraFollow.I;
            if (cam && cam.target == dolly) cam.target = cameraTarget;
            if (PlayerController.I)
            {
                PlayerController.I.scriptedMove = Vector2.zero;
                if (giveControl) PlayerController.I.frozen = false;
            }
        }

        // ---------------- input ----------------

        static bool SkipPressed()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame) || TapSkip();
        }

        /// <summary>A tap on the "skip" hint in the top right corner (phones).</summary>
        static bool TapSkip()
        {
            var ts = Touchscreen.current;
            if (ts == null || !ts.primaryTouch.press.wasPressedThisFrame) return false;
            Vector2 p = ts.primaryTouch.position.ReadValue();
            return p.x > Screen.width * 0.7f && p.y > Screen.height * 0.82f;
        }

        static bool TapNext()
        {
            var ts = Touchscreen.current;
            return ts != null && ts.primaryTouch.press.wasPressedThisFrame && !TapSkip();
        }

        static bool NextPressed()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            return (kb != null && kb.anyKey.wasPressedThisFrame && !kb.escapeKey.wasPressedThisFrame) ||
                   (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                   (pad != null && pad.buttonSouth.wasPressedThisFrame) || TapNext();
        }

        /// <summary>Checks Esc; call every frame of a scene's own loops.</summary>
        static bool CheckSkip()
        {
            if (!Skipped && Playing && SkipPressed()) Skipped = true;
            return Skipped;
        }

        // ---------------- steps ----------------

        public static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                if (CheckSkip()) yield break;
                yield return null;
            }
        }

        /// <summary>A line of dialogue (speaker null = narration). Typed out; a key finishes it, the next key moves on.</summary>
        public static IEnumerator Say(string speaker, string text, Color color)
        {
            if (Skipped) yield break;
            UI.ShowLine(speaker, text, color);
            float typed = 0f;
            int total = text.Length;
            while (UI.Shown < total)
            {
                if (CheckSkip()) { UI.HideLine(); yield break; }
                typed += Time.unscaledDeltaTime * 45f;
                UI.Shown = Mathf.Min(total, Mathf.FloorToInt(typed));
                if (NextPressed() && typed > 2f) UI.Shown = total;
                yield return null;
            }
            yield return null;
            float wait = Mathf.Max(2.5f, total * 0.05f);
            for (float t = 0f; t < wait; t += Time.unscaledDeltaTime)
            {
                if (CheckSkip()) break;
                if (NextPressed()) break;
                yield return null;
            }
            UI.HideLine();
        }

        public static IEnumerator Narrate(string text) => Say(null, text, UiKit.Cream);

        /// <summary>Moves the camera to a point (it glides there with the usual smoothing).</summary>
        public static void Look(Vector2 at)
        {
            if (dolly) dolly.position = at;
        }

        /// <summary>The camera follows a moving thing (an actor) until Look() is called again.</summary>
        public static IEnumerator LookFollow(Transform t, float seconds)
        {
            for (float s = 0f; s < seconds && t; s += Time.unscaledDeltaTime)
            {
                if (dolly) dolly.position = t.position;
                if (CheckSkip()) yield break;
                yield return null;
            }
        }

        /// <summary>The hero walks to a point by himself.</summary>
        public static IEnumerator WalkPlayer(Vector2 to, float speed = 3.2f)
        {
            var p = PlayerController.I;
            if (!p) yield break;
            for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
            {
                Vector2 d = to - p.Feet;
                if (d.magnitude < 0.12f || CheckSkip()) break;
                p.scriptedMove = d.normalized * speed;
                yield return null;
            }
            p.scriptedMove = Vector2.zero;
            if (Skipped) p.Teleport(to);
        }

        public static IEnumerator FadeBlack(float to, float time) => UI.FadeTo(UI.black, to, Skipped ? 0.05f : time);
        public static IEnumerator Flash(float to, float time) => UI.FadeTo(UI.white, to, Skipped ? 0.05f : time);
    }

    /// <summary>Someone who acts in a cutscene: idle and walking frames, or four directions (LimeZu characters).</summary>
    public class CutActor : MonoBehaviour
    {
        public SpriteRenderer Body { get; private set; }
        SpriteAnimator anim;
        Sprite[] idle, run;
        Sprite[][] idleDirs, runDirs;   // right, up, left, down

        public static CutActor Create(Transform parent, string name, Vector2 feet, Sprite[] idle, Sprite[] run)
        {
            var a = Make(parent, name, feet);
            a.idle = idle;
            a.run = run != null && run.Length > 0 ? run : idle;
            a.Idle();
            return a;
        }

        public static CutActor Create(Transform parent, string name, Vector2 feet, Sprite[][] idleDirs, Sprite[][] runDirs)
        {
            var a = Make(parent, name, feet);
            a.idleDirs = idleDirs;
            a.runDirs = runDirs;
            a.Idle(3);
            return a;
        }

        static CutActor Make(Transform parent, string name, Vector2 feet)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            var a = go.AddComponent<CutActor>();
            a.Body = go.AddComponent<SpriteRenderer>();
            a.Body.spriteSortPoint = SpriteSortPoint.Pivot;
            Gfx.Lit(a.Body);
            a.anim = go.AddComponent<SpriteAnimator>();
            a.anim.target = a.Body;
            var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(go.transform, false);
            shadow.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            shadow.sprite = Gfx.Shadow;
            shadow.sortingOrder = -10;
            return a;
        }

        public Vector2 Feet => transform.position;

        /// <summary>Stands still; dir for four-direction characters (0 right, 1 up, 2 left, 3 down).</summary>
        public void Idle(int dir = 3)
        {
            var f = idleDirs != null ? idleDirs[Mathf.Clamp(dir, 0, 3)] : idle;
            if (f != null && f.Length > 0) anim.Play(f, 6f);
        }

        public void Face(bool left)
        {
            if (idleDirs != null) Idle(left ? 2 : 0);
            else Body.flipX = left;
        }

        static int Dir(Vector2 d) =>
            Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0f ? 0 : 2) : (d.y > 0f ? 1 : 3);

        public IEnumerator WalkTo(Vector2 to, float speed = 2.6f)
        {
            Vector2 d = to - Feet;
            int dir = Dir(d);
            if (runDirs != null) anim.Play(runDirs[dir], 10f);
            else { anim.Play(run, 10f); if (Mathf.Abs(d.x) > 0.01f) Body.flipX = d.x < 0f; }
            while (((Vector2)transform.position - to).sqrMagnitude > 0.0004f)
            {
                if (Cutscene.Skipped) break;
                transform.position = Vector2.MoveTowards(transform.position, to, speed * Time.unscaledDeltaTime);
                yield return null;
            }
            transform.position = to;
            Idle(dir);
        }

        public IEnumerator FadeOut(float time)
        {
            var c = Body.color;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, Cutscene.Skipped ? 0.01f : time))
            {
                Body.color = new Color(c.r, c.g, c.b, c.a * (1f - t));
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }

    /// <summary>Letterbox bars, the subtitle, a title card and full-screen fades.</summary>
    public class CutsceneUI : MonoBehaviour
    {
        RectTransform root, top, bottom;
        Text speakerText, lineText, skipHint, cardLabel, cardTitle;
        CanvasGroup lineGroup, cardGroup;
        public Image black, white;
        string full = "";
        int shown;
        float barTarget, bar;
        const float BarHeight = 92f;

        public int Shown
        {
            get => shown;
            set
            {
                // the hidden rest stays in the text (transparent) so the layout doesn't jump while typing
                shown = Mathf.Clamp(value, 0, full.Length);
                lineText.text = shown >= full.Length ? full : full.Substring(0, shown) + "<color=#00000000>" + full.Substring(shown) + "</color>";
            }
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            UiKit.Init();
            root = UiKit.Canvas(transform, "CutsceneCanvas", 15);
            // fades lie under the bars and the text, so a line can be read on black
            black = Cover("Black", Color.black);
            white = Cover("White", Color.white);

            top = Bar("Top", new Vector2(0.5f, 1f));
            bottom = Bar("Bottom", new Vector2(0.5f, 0f));

            // the subtitle sits above the bottom bar on a dark band, well inside the screen
            var line = UiKit.Rect(root, "Line", new Vector2(0.5f, 0f), new Vector2(0f, BarHeight + 70f), new Vector2(1140f, 104f));
            lineGroup = line.gameObject.AddComponent<CanvasGroup>();
            var back = UiKit.Image(line, "Back", Gfx.White, new Color(0f, 0f, 0f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1140f, 104f));
            back.raycastTarget = false;
            speakerText = UiKit.Text(line, "", 16, UiKit.Gold, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(0f, 34f), new Vector2(1080f, 22f), true);
            lineText = UiKit.Text(line, "", 28, UiKit.Cream, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(1080f, 62f));
            lineText.supportRichText = true;
            lineGroup.alpha = 0f;

            skipHint = UiKit.Text(root, Loc.T(Binds.UsingTouch ? "cs.skipTouch" : "cs.skip"), 16, UiKit.Dim, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-150f, -24f), new Vector2(280f, 24f));

            var card = UiKit.Rect(root, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 160f));
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            cardLabel = UiKit.Title(card, "", 16, UiKit.Dim, new Vector2(0f, 36f), new Vector2(900f, 28f));
            cardTitle = UiKit.Title(card, "", 40, UiKit.Gold, new Vector2(0f, -14f), new Vector2(1100f, 64f));
            cardGroup.alpha = 0f;
            Apply();
        }

        RectTransform Bar(string name, Vector2 anchor)
        {
            var img = UiKit.Image(root, name, Gfx.White, Color.black, anchor, Vector2.zero, new Vector2(4000f, 0f));
            img.rectTransform.pivot = anchor;
            img.raycastTarget = false;
            return img.rectTransform;
        }

        Image Cover(string name, Color c)
        {
            var img = UiKit.Image(root, name, Gfx.White, new Color(c.r, c.g, c.b, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UiKit.Stretch(img.rectTransform);
            img.raycastTarget = false;
            return img;
        }

        public void Bars(bool on)
        {
            barTarget = on ? BarHeight : 0f;
            if (skipHint) skipHint.text = Loc.T(Binds.UsingTouch ? "cs.skipTouch" : "cs.skip");
        }

        /// <summary>The language may have changed since the texts were made (Chinese uses its own font).</summary>
        void Fonts()
        {
            speakerText.font = cardLabel.font = cardTitle.font = UiKit.TitleFont ? UiKit.TitleFont : speakerText.font;
            lineText.font = skipHint.font = UiKit.Font ? UiKit.Font : lineText.font;
        }

        public void ShowLine(string speaker, string text, Color color)
        {
            Fonts();
            speakerText.text = speaker ?? "";
            speakerText.color = color;
            lineText.alignment = speaker == null ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
            lineText.color = speaker == null ? new Color(0.85f, 0.82f, 0.75f) : UiKit.Cream;
            lineText.fontStyle = speaker == null ? FontStyle.Italic : FontStyle.Normal;
            full = text ?? "";
            Shown = 0;
            lineGroup.alpha = 1f;
        }

        public void HideLine()
        {
            if (lineGroup) lineGroup.alpha = 0f;
        }

        /// <summary>"ENDING" + its name in the middle of the screen.</summary>
        public IEnumerator Card(string label, string title, float hold)
        {
            Fonts();
            cardLabel.text = label;
            cardTitle.text = title;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.8f) { cardGroup.alpha = t; yield return null; }
            cardGroup.alpha = 1f;
            for (float t = 0f; t < hold; t += Time.unscaledDeltaTime)
            {
                if (t > 0.8f && Binds.AnyPress()) break;
                yield return null;
            }
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.8f) { cardGroup.alpha = t; yield return null; }
            cardGroup.alpha = 0f;
        }

        public IEnumerator FadeTo(Image img, float to, float time)
        {
            float from = img.color.a;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, time))
            {
                var c = img.color;
                img.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, t));
                yield return null;
            }
            var e = img.color;
            img.color = new Color(e.r, e.g, e.b, to);
        }

        /// <summary>Everything off at once (leaving a scene early).</summary>
        public void Clear()
        {
            barTarget = bar = 0f;
            HideLine();
            cardGroup.alpha = 0f;
            black.color = new Color(0f, 0f, 0f, 0f);
            white.color = new Color(1f, 1f, 1f, 0f);
            Apply();
        }

        void Update()
        {
            bar = Mathf.MoveTowards(bar, barTarget, Time.unscaledDeltaTime * BarHeight * 3f);
            Apply();
        }

        void Apply()
        {
            top.sizeDelta = new Vector2(4000f, bar);
            bottom.sizeDelta = new Vector2(4000f, bar);
            skipHint.enabled = bar > BarHeight * 0.9f && Cutscene.Playing;
        }
    }
}
