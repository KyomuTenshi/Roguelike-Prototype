using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RulesShift
{
    /// <summary>
    /// All menus in the dungeon style: splash, main menu, run setup (mode / hero / curses),
    /// codex (laws, relics, combos), settings (incl. language and hints), about, pause and the end screen.
    /// Every string comes from Loc, so the language can be switched in the settings.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class MenuUI : MonoBehaviour
    {
        public static MenuUI I { get; private set; }

        enum Page { None, Splash, Main, Setup, Codex, Settings, About, Pause, End }

        static readonly Law[] CodexLaws =
        {
            Law.Crypt, Law.Noir, Law.Ice, Law.Mirror, Law.Pacifist, Law.Retro,
            Law.Lava, Law.Stealth, Law.Upside, Law.Neon, Law.Console, Law.Boss
        };

        static readonly Color Locked = new Color(0.55f, 0.53f, 0.58f);
        static bool splashShown;

        RectTransform root;
        GameObject panel;
        Page page = Page.None;
        Page settingsReturn = Page.Main;
        Text blink;
        RunConfig pending;
        string pauseInfo = "";
        RunResult lastResult;
        bool confirmReset;
        float splashTimer;
        int codexTab;

        public bool IsOpen => page != Page.None;

        GameAssets A => GameAssets.I;

        void Awake()
        {
            I = this;
            UiKit.Init();
            root = UiKit.Canvas(transform, "Menus", 20);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void Update()
        {
            ScreenMode.HotkeyTick();
            float t = Time.unscaledTime;
            if (blink) blink.color = new Color(blink.color.r, blink.color.g, blink.color.b, Mathf.Repeat(t * 2f, 1f) < 0.65f ? 1f : 0.25f);

            if (page == Page.Splash)
            {
                splashTimer -= Time.unscaledDeltaTime;
                bool skip = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
                if (splashTimer <= 0f || skip) Open(Page.Main);
                return;
            }

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) ||
                        (pad != null && pad.buttonEast.wasPressedThisFrame) ||
                        (pad != null && page == Page.Pause && pad.startButton.wasPressedThisFrame);
            if (!back) return;
            switch (page)
            {
                case Page.Pause: ResumeGame(); break;
                case Page.Settings: Open(settingsReturn); break;
                case Page.Setup:
                case Page.Codex:
                case Page.About: Open(Page.Main); break;
            }
        }

        // ---------------- public API ----------------

        public void ShowMain()
        {
            if (!splashShown) { splashShown = true; Open(Page.Splash); }
            else Open(Page.Main);
        }

        /// <summary>The codex on its Story tab (back from an ending preview).</summary>
        public void ShowStory()
        {
            codexTab = 3;
            Open(Page.Codex);
        }

        public void ShowPause(string info)
        {
            pauseInfo = info;
            Open(Page.Pause);
        }

        /// <param name="fade">After an ending the results come in slowly.</param>
        public void ShowEnd(RunResult r, float fade = 0.18f)
        {
            lastResult = r;
            openFade = fade;
            Open(Page.End);
            openFade = 0.18f;
        }

        float openFade = 0.18f;

        public void Hide()
        {
            if (panel) Destroy(panel);
            panel = null;
            blink = null;
            page = Page.None;
        }

        void Open(Page p)
        {
            Hide();
            page = p;
            confirmReset = false;
            switch (p)
            {
                case Page.Splash: BuildSplash(); break;
                case Page.Main: BuildMain(); break;
                case Page.Setup: BuildSetup(); break;
                case Page.Codex: BuildCodex(); break;
                case Page.Settings: BuildSettings(); break;
                case Page.About: BuildAbout(); break;
                case Page.Pause: BuildPause(); break;
                case Page.End: BuildEnd(); break;
            }
            if (panel && !instant) UiAnim.FadeIn(panel.GetComponent<CanvasGroup>(), openFade);
            instant = false;
        }

        bool instant;   // rebuild in place (setup choices, tabs, language) without entrance animations

        /// <summary>Rebuilds a page in place and keeps the focus on the button at the same position.</summary>
        void Rebuild(Page p)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            var cur = es ? es.currentSelectedGameObject : null;
            Vector2 pos = cur ? ((RectTransform)cur.transform).anchoredPosition : new Vector2(float.MaxValue, 0f);
            string parent = cur && cur.transform.parent ? cur.transform.parent.name : "";
            instant = true;
            Open(p);
            if (!panel) return;
            foreach (var sel in panel.GetComponentsInChildren<Selectable>())
                if (((RectTransform)sel.transform).anchoredPosition == pos && sel.transform.parent && sel.transform.parent.name == parent)
                { UiKit.Select(sel); break; }
        }

        void ResumeGame()
        {
            Hide();
            if (RoomManager.I) RoomManager.I.Resume();
        }

        // ---------------- helpers ----------------

        /// <summary>Stone board in the middle of the screen with a title and two hanging banners.</summary>
        RectTransform Board(string title, Vector2 size, Color titleColor, float y = 0f)
        {
            var board = UiKit.Panel(panel.transform, "Board", new Vector2(0.5f, 0.5f), new Vector2(0f, y), size).rectTransform;
            if (!instant) UiAnim.Pop(board, 0.94f, 0.22f);
            if (A && A.DarkAges)
            {
                // the title sits on a crowned plate across the top of the frame
                float w = Mathf.Clamp(title.Length * (Loc.Cjk ? 40f : 26f) + 110f, 340f, size.x - 160f);
                UiKit.TitlePlate(board, title, new Vector2(0f, size.y / 2f - 34f), w, titleColor);
            }
            else
            {
                var bl = UiKit.Icon(board, A ? A.D("wall_banner_red") : null, new Vector2(-size.x / 2f + 70f, size.y / 2f - 8f), 4f);
                var br = UiKit.Icon(board, A ? A.D("wall_banner_blue") : null, new Vector2(size.x / 2f - 70f, size.y / 2f - 8f), 4f);
                bl.rectTransform.pivot = br.rectTransform.pivot = new Vector2(0.5f, 1f);
                UiKit.Title(board, title, 24, titleColor, new Vector2(0f, size.y / 2f - 44f), new Vector2(size.x - 220f, 44f));
            }
            return board;
        }

        Image AnimatedSprite(Transform parent, string anim, Vector2 pos, float scale, float fps = 8f, bool bob = false)
        {
            var frames = A ? A.Anim(anim) : new Sprite[0];
            if (frames.Length == 0) return null;
            var img = UiKit.Icon(parent, frames[0], pos, scale);
            img.rectTransform.pivot = new Vector2(0.5f, 0f);
            UiSpriteAnim.Add(img, frames, fps);
            if (bob) UiBob.Add(img, 3f, 2f);
            return img;
        }

        Image Torch(Transform parent, Vector2 pos, float scale)
        {
            if (!A) return null;
            var frames = new List<Sprite>();
            for (int i = 1; i <= 8; i++) { var s = A.C("torch_" + i); if (s) frames.Add(s); }
            if (frames.Count == 0) return null;
            var img = UiKit.Icon(parent, frames[0], pos, scale);
            UiSpriteAnim.Add(img, frames.ToArray(), 10f);
            return img;
        }

        /// <summary>Pack sprite scaled to fit a square of the given size (whole-pixel scale).</summary>
        Image FitIcon(Transform parent, string sprite, Vector2 pos, float box)
        {
            var s = A ? A.Any(sprite) : null;
            float scale = s ? Mathf.Max(1f, Mathf.Floor(box / Mathf.Max(s.rect.width, s.rect.height))) : 3f;
            return UiKit.Icon(parent, s, pos, scale);
        }

        static void Stagger(List<UnityEngine.UI.Button> buttons, Vector2 from, float start = 0.05f, float step = 0.05f)
        {
            for (int i = 0; i < buttons.Count; i++)
                UiAnim.Appear((RectTransform)buttons[i].transform, start + step * i, from, 0.28f);
        }

        // ---------------- splash ----------------

        void BuildSplash()
        {
            panel = UiKit.Screen(root, "Splash", 1f);
            splashTimer = 2.2f;
            var t = UiKit.Title(panel.transform, "PIPERITE GAMES", 32, UiKit.Gold, new Vector2(0f, 24f), new Vector2(900f, 52f));
            if (UiKit.LatinTitleFont) { t.font = UiKit.LatinTitleFont; t.fontSize = t.resizeTextMaxSize = 32; }
            UiAnim.Pop(t.transform, 0.6f, 0.35f);
            var s = UiKit.Label(panel.transform, Loc.T("splash.presents"), 32, UiKit.Dim, new Vector2(0f, -30f), new Vector2(600f, 40f));
            UiAnim.Appear(s.rectTransform, 0.3f, new Vector2(0f, -20f), 0.35f);
            var flask = AnimatedSprite(panel.transform, "wall_fountain_mid_red_anim", new Vector2(0f, 70f), 4f);
            if (flask) UiAnim.Pop(flask.transform, 0.3f, 0.3f, 0.15f);
        }

        // ---------------- main ----------------

        void BuildMain()
        {
            panel = UiKit.Screen(root, "Main", 0.45f);
            var tr = panel.transform;
            var d = SaveSystem.Data;

            TitleGlitch.Create(tr, "RULECRAWL", 64, new Vector2(0f, 212f));
            var sub = UiKit.Label(tr, Loc.T("menu.subtitle"), 40, UiKit.Cream, new Vector2(0f, 140f), new Vector2(860f, 50f));
            UiAnim.Appear(sub.rectTransform, 0.6f, new Vector2(0f, -16f), 0.3f);

            Torch(tr, new Vector2(-470f, 205f), 5f);
            Torch(tr, new Vector2(470f, 205f), 5f);
            var bl = UiKit.Icon(tr, A ? A.D("wall_banner_red") : null, new Vector2(-560f, 250f), 5f);
            var br = UiKit.Icon(tr, A ? A.D("wall_banner_yellow") : null, new Vector2(560f, 250f), 5f);
            if (bl) UiBob.Add(bl, 3f, 1.4f);
            if (br) UiBob.Add(br, 3f, 1.6f);

            string records = d.runs == 0 ? Loc.T("menu.firstRun")
                : Loc.F("menu.records", d.bestStandard, d.wins, d.runs, Mathf.Min(d.laws.Count, CodexLaws.Length), CodexLaws.Length);
            UiKit.Label(tr, records, 28, UiKit.Dim, new Vector2(0f, 92f), new Vector2(820f, 32f));

            var list = new List<UnityEngine.UI.Button>
            {
                UiKit.Button(tr, Loc.T("menu.play"), new Vector2(0f, 20f), new Vector2(420f, 66f), () => { InitPending(); Open(Page.Setup); }, 24),
                UiKit.Button(tr, Loc.T("menu.codex"), new Vector2(0f, -54f), new Vector2(420f, 58f), () => { codexTab = 0; Open(Page.Codex); }),
                UiKit.Button(tr, Loc.T("menu.settings"), new Vector2(0f, -120f), new Vector2(420f, 58f), () => { settingsReturn = Page.Main; Open(Page.Settings); }),
                UiKit.Button(tr, Loc.T("menu.about"), new Vector2(0f, -186f), new Vector2(420f, 58f), () => Open(Page.About)),
            };
            if (Application.platform != RuntimePlatform.WebGLPlayer)   // a browser tab can't be closed by the game
                list.Add(UiKit.Button(tr, Loc.T("menu.quit"), new Vector2(0f, -252f), new Vector2(420f, 58f), Quit));
            Stagger(list, new Vector2(-60f, 0f), 0.35f, 0.06f);

            // the hero and the Lawgiver face each other at the bottom
            AnimatedSprite(tr, SaveSystem.HeroUnlocked(Heroes.Get(d.hero)) ? Heroes.Get(d.hero).skin + "_idle_anim" : "elf_m_idle_anim", new Vector2(-470f, -300f), 5f);
            var boss = AnimatedSprite(tr, "big_demon_idle_anim", new Vector2(470f, -300f), 5f, 7f);
            if (boss) boss.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            UiKit.Select(list[0]);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------- run setup ----------------

        void InitPending()
        {
            var d = SaveSystem.Data;
            pending = new RunConfig
            {
                mode = (GameMode)Mathf.Clamp(d.mode, 0, Modes.Count - 1),
                hero = Heroes.Get(d.hero),
                curses = d.curses
            };
            if (!SaveSystem.ModeUnlocked(pending.mode)) pending.mode = GameMode.Standard;
            if (!SaveSystem.HeroUnlocked(pending.hero)) pending.hero = Heroes.All[0];
        }

        void BuildSetup()
        {
            if (pending == null || pending.hero == null) InitPending();
            panel = UiKit.Screen(root, "Setup", 0.55f);
            var board = Board(Loc.T("setup.title"), new Vector2(1240f, 690f), UiKit.Gold);

            // modes
            UiKit.Title(board, Loc.T("setup.mode"), 16, UiKit.Dim, new Vector2(0f, 262f), new Vector2(400f, 26f));
            for (int i = 0; i < Modes.Count; i++)
            {
                var m = (GameMode)i;
                var info = Modes.Info(m);
                bool open = SaveSystem.ModeUnlocked(m);
                var b = UiKit.Button(board, open ? info.name.ToUpper() : "",
                    new Vector2(-427.5f + i * 285f, 220f), new Vector2(270f, 56f), () => { pending.mode = m; Rebuild(Page.Setup); });
                b.interactable = open;
                UiKit.SetChosen(b, pending.mode == m);
                if (!open) UiKit.Label((RectTransform)b.transform, info.lockHint, 24, Locked, new Vector2(0f, 2f), new Vector2(244f, 46f));
            }
            var mi = Modes.Info(pending.mode);
            string modeDesc = mi.desc + "   " + (pending.mode == GameMode.Daily
                ? Loc.F("setup.dailyInfo", Curses.Info(Daily.TodayCurse).name, SaveSystem.Best(GameMode.Daily))
                : Loc.F("setup.best", SaveSystem.Best(pending.mode)));
            UiKit.Paragraph(board, modeDesc, 26, UiKit.Cream, new Vector2(0f, 160f), new Vector2(1100f, 56f));

            // heroes
            UiKit.Title(board, Loc.T("setup.hero"), 16, UiKit.Dim, new Vector2(0f, 112f), new Vector2(400f, 26f));
            for (int i = 0; i < Heroes.All.Length; i++)
            {
                var h = Heroes.All[i];
                bool open = SaveSystem.HeroUnlocked(h);
                var b = UiKit.Button(board, "", new Vector2(-427.5f + i * 285f, 24f), new Vector2(270f, 150f), () => { pending.hero = h; Rebuild(Page.Setup); });
                b.interactable = open;
                UiKit.SetChosen(b, pending.hero == h);
                var rt = (RectTransform)b.transform;
                var frames = A ? A.Anim(h.skin + (pending.hero == h ? "_run_anim" : "_idle_anim")) : new Sprite[0];
                if (frames.Length > 0)
                {
                    var portrait = UiKit.Icon(rt, frames[0], new Vector2(-88f, -6f), 4f);
                    portrait.color = open ? Color.white : Color.black;
                    if (open) UiSpriteAnim.Add(portrait, frames, pending.hero == h ? 10f : 6f);
                }
                if (open)
                {
                    UiKit.Text(rt, h.name.ToUpper(), 16, UiKit.Gold, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(44f, 44f), new Vector2(170f, 28f), true);
                    UiKit.Text(rt, Loc.F("setup.stats", h.halves / 2, h.damage, h.speed.ToString("0.#")), 24, UiKit.Cream,
                        TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(44f, -18f), new Vector2(170f, 84f));
                }
                else
                {
                    UiKit.Text(rt, "???", 16, Locked, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(44f, 44f), new Vector2(170f, 28f), true);
                    UiKit.Text(rt, h.unlockHint, 22, Locked, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(44f, -14f), new Vector2(166f, 84f));
                }
            }
            UiKit.Label(board, pending.hero.desc, 26, UiKit.Cream, new Vector2(0f, -68f), new Vector2(1100f, 30f));

            // curses
            bool daily = pending.mode == GameMode.Daily;
            UiKit.Title(board, Loc.T(daily ? "setup.dailyCurse" : "setup.curses"), 16, UiKit.Dim, new Vector2(0f, -108f), new Vector2(600f, 26f));
            if (!daily) UiKit.Label(board, Loc.T("setup.cursesHint"), 22, UiKit.Dim, new Vector2(0f, -132f), new Vector2(600f, 24f));
            for (int i = 0; i < Curses.Count; i++)
            {
                var c = (Curse)i;
                var info = Curses.Info(c);
                bool on = daily ? c == Daily.TodayCurse : pending.Has(c);
                var b = UiKit.Button(board, "", new Vector2(-427.5f + i * 285f, -184f), new Vector2(270f, 74f), () => { pending.curses ^= 1 << (int)c; Rebuild(Page.Setup); });
                b.interactable = !daily;
                UiKit.SetOn(b, on);
                var rt = (RectTransform)b.transform;
                var skull = UiKit.Icon(rt, A ? A.D(on ? "skull" : "flask_blue") : null, new Vector2(-112f, 0f), 3f);
                skull.color = on ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                UiKit.Text(rt, info.name, 28, on ? UiKit.Gold : UiKit.Cream, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(26f, 14f), new Vector2(196f, 30f));
                UiKit.Text(rt, $"{info.desc}  +{Mathf.RoundToInt(info.bonus * 100)}%", 22, UiKit.Dim, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(26f, -16f), new Vector2(196f, 26f));
            }

            // bottom
            var preview = pending.Copy();
            if (daily) preview.curses = 1 << (int)Daily.TodayCurse;
            UiKit.Text(board, Loc.F("setup.mult", preview.ScoreMul.ToString("0.0")), 24, UiKit.Gold, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 0.5f), new Vector2(-300f, -270f), new Vector2(440f, 40f), true);
            UiKit.Button(board, Loc.T("menu.back"), new Vector2(140f, -270f), new Vector2(220f, 58f), () => Open(Page.Main));
            var start = UiKit.Button(board, Loc.T("setup.start"), new Vector2(410f, -270f), new Vector2(260f, 62f), StartRun, 24);
            UiKit.Select(start);
        }

        void StartRun()
        {
            Hide();
            if (RoomManager.I) RoomManager.I.StartRun(pending);
        }

        // ---------------- codex ----------------

        void BuildCodex()
        {
            panel = UiKit.Screen(root, "Codex", 0.6f);
            var board = Board(Loc.T("codex.title"), new Vector2(1240f, 690f), UiKit.Gold);

            string[] tabs = { "codex.laws", "codex.relics", "codex.combos", "codex.story" };
            UnityEngine.UI.Button current = null;
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                var b = UiKit.Button(board, Loc.T(tabs[i]), new Vector2(-441f + i * 294f, 238f), new Vector2(276f, 50f), () => { codexTab = idx; Rebuild(Page.Codex); });
                UiKit.SetChosen(b, codexTab == i);
                if (codexTab == i) current = b;
            }

            switch (codexTab)
            {
                case 1: CodexRelics(board); break;
                case 2: CodexCombos(board); break;
                case 3: CodexStory(board); break;
                default: CodexLawsTab(board); break;
            }

            var back = UiKit.Button(board, Loc.T("menu.back"), new Vector2(0f, -278f), new Vector2(260f, 52f), () => Open(Page.Main));
            UiKit.Select(instant && current ? (Selectable)current : back);
        }

        void CodexLawsTab(RectTransform board)
        {
            var d = SaveSystem.Data;
            int known = 0;
            foreach (var l in CodexLaws) if (SaveSystem.Knows(l)) known++;
            UiKit.Label(board, Loc.F("codex.opened", known, CodexLaws.Length), 26, UiKit.Dim, new Vector2(0f, 196f), new Vector2(600f, 30f));

            for (int i = 0; i < CodexLaws.Length; i++)
            {
                var law = CodexLaws[i];
                var info = Rules.Info(law);
                bool knows = SaveSystem.Knows(law);
                int col = i % 4, row = i / 4;
                var card = UiKit.Plate(board, "Law", new Vector2(0.5f, 0.5f), new Vector2(-426f + col * 284f, 128f - row * 118f), new Vector2(272f, 110f)).rectTransform;
                var icon = UiKit.Icon(card, knows ? UiKit.BannerFor(info.color) : (A ? A.D("skull") : null), new Vector2(-92f, 0f), 2f);
                if (!knows) icon.color = new Color(1f, 1f, 1f, 0.5f);
                UiKit.Text(card, knows ? info.name : "???", 28, knows ? info.color : Locked, TextAnchor.MiddleLeft,
                    new Vector2(0.5f, 0.5f), new Vector2(22f, 16f), new Vector2(170f, 30f));
                UiKit.Text(card, knows ? info.rule : Loc.T("codex.unknownLaw"), 20, knows ? UiKit.Cream : Locked,
                    TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(22f, -14f), new Vector2(170f, 34f));
                if (!instant) UiAnim.Appear(card, 0.03f * i, new Vector2(0f, -24f), 0.25f);
            }

            string endless = d.bestEndlessDepth > 0 ? Loc.F("codex.depthFmt", d.bestEndless, d.bestEndlessDepth) : "-";
            string stats = Loc.F("codex.stats1", d.runs, d.wins, d.totalKills, d.bossReached) + "\n" +
                           Loc.F("codex.stats2", d.bestStandard, d.bestChaos, endless, SaveSystem.Best(GameMode.Daily));
            UiKit.Label(board, stats, 26, UiKit.Cream, new Vector2(0f, -210f), new Vector2(1100f, 60f));
        }

        /// <summary>Memories found and endings seen; a seen ending can be watched again.</summary>
        void CodexStory(RectTransform board)
        {
            var d = SaveSystem.Data;
            int mems = 0, ends = 0;
            for (int i = 0; i < RoomManager.MemoryCount; i++) if ((d.memories & (1 << i)) != 0) mems++;
            for (int i = 0; i < RoomManager.EndingCount; i++) if ((d.endings & (1 << i)) != 0) ends++;

            UiKit.Label(board, Loc.F("codex.memories", mems, RoomManager.MemoryCount), 26, UiKit.Dim, new Vector2(0f, 190f), new Vector2(600f, 30f));
            for (int i = 0; i < RoomManager.MemoryCount; i++)
            {
                bool known = (d.memories & (1 << i)) != 0;
                var card = UiKit.Plate(board, "Memory", new Vector2(0.5f, 0.5f), new Vector2(-380f + i * 380f, 112f), new Vector2(360f, 104f)).rectTransform;
                UiKit.Text(card, known ? Loc.T("room.secret." + i) : "???", 24, known ? new Color(0.75f, 0.95f, 0.75f) : Locked, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(320f, 30f));
                UiKit.Text(card, known ? Loc.T("room.secret." + i + ".rule") : Loc.T("codex.unknownMemory"), 18, known ? UiKit.Cream : Locked,
                    TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(320f, 44f));
                if (!instant) UiAnim.Appear(card, 0.04f * i, new Vector2(0f, -24f), 0.25f);
            }

            UiKit.Label(board, Loc.F("codex.endings", ends, RoomManager.EndingCount), 26, UiKit.Dim, new Vector2(0f, 26f), new Vector2(600f, 30f));
            for (int i = 0; i < RoomManager.EndingCount; i++)
            {
                int which = i;
                bool seen = (d.endings & (1 << i)) != 0;
                var card = UiKit.Plate(board, "Ending", new Vector2(0.5f, 0.5f), new Vector2(-380f + i * 380f, -84f), new Vector2(360f, 160f)).rectTransform;
                UiKit.Text(card, seen ? Loc.T("ending." + i + ".name") : "???", 26, seen ? UiKit.Gold : Locked, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 48f), new Vector2(320f, 32f));
                UiKit.Text(card, Loc.T("ending." + i + ".how"), 18, seen ? UiKit.Cream : Locked, TextAnchor.UpperCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(320f, 44f));
                if (seen)
                    UiKit.Button(card, Loc.T("codex.watch"), new Vector2(0f, -48f), new Vector2(220f, 44f), () =>
                    {
                        if (RoomManager.I) RoomManager.I.PreviewEnding(which, true);
                    });
                if (!instant) UiAnim.Appear(card, 0.1f + 0.04f * i, new Vector2(0f, -24f), 0.25f);
            }
        }

        void CodexRelics(RectTransform board)
        {
            int known = 0;
            foreach (var r in Relics.All) if (SaveSystem.KnowsRelic(r.key)) known++;
            UiKit.Label(board, Loc.F("codex.opened", known, Relics.All.Length), 26, UiKit.Dim, new Vector2(0f, 196f), new Vector2(600f, 30f));
            for (int i = 0; i < Relics.All.Length; i++)
            {
                var r = Relics.All[i];
                bool knows = SaveSystem.KnowsRelic(r.key);
                int col = i % 4, row = i / 4;
                var card = UiKit.Plate(board, "Relic", new Vector2(0.5f, 0.5f), new Vector2(-426f + col * 284f, 134f - row * 92f), new Vector2(272f, 86f)).rectTransform;
                var icon = knows ? FitIcon(card, r.sprite, new Vector2(-88f, 0f), 32f) : UiKit.Icon(card, A ? A.D("skull") : null, new Vector2(-88f, 0f), 2f);
                if (!knows) icon.color = new Color(1f, 1f, 1f, 0.45f);
                UiKit.Text(card, knows ? r.Name : "???", 24, knows ? r.color : Locked, TextAnchor.MiddleLeft,
                    new Vector2(0.5f, 0.5f), new Vector2(22f, 12f), new Vector2(170f, 26f));
                UiKit.Text(card, knows ? r.Desc : Loc.T("codex.unknownRelic"), 18, knows ? UiKit.Cream : Locked,
                    TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(22f, -12f), new Vector2(170f, 24f));
                if (!instant) UiAnim.Appear(card, 0.02f * i, new Vector2(0f, -24f), 0.25f);
            }
        }

        void CodexCombos(RectTransform board)
        {
            int known = 0;
            foreach (var c in Relics.Combos) if (SaveSystem.KnowsCombo(c.key)) known++;
            UiKit.Label(board, Loc.F("codex.opened", known, Relics.Combos.Length), 26, UiKit.Dim, new Vector2(0f, 196f), new Vector2(600f, 30f));
            for (int i = 0; i < Relics.Combos.Length; i++)
            {
                var c = Relics.Combos[i];
                bool knows = SaveSystem.KnowsCombo(c.key);
                int col = i % 3, row = i / 3;
                var card = UiKit.Plate(board, "Combo", new Vector2(0.5f, 0.5f), new Vector2(-376f + col * 376f, 122f - row * 126f), new Vector2(360f, 118f)).rectTransform;
                var a = Relics.Get(c.a);
                var b = Relics.Get(c.b);
                var ia = FitIcon(card, a.sprite, new Vector2(-128f, 20f), 32f);
                var ib = FitIcon(card, b.sprite, new Vector2(-128f, -22f), 32f);
                if (!knows) ia.color = ib.color = new Color(0f, 0f, 0f, 0.7f);
                UiKit.Text(card, knows ? c.Name : "???", 16, knows ? UiKit.Gold : Locked, TextAnchor.MiddleLeft,
                    new Vector2(0.5f, 0.5f), new Vector2(24f, 30f), new Vector2(240f, 28f), true);
                UiKit.Text(card, knows ? c.Desc : Loc.T("codex.unknownCombo"), 22, knows ? UiKit.Cream : Locked,
                    TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(24f, -16f), new Vector2(240f, 56f));
                if (!instant) UiAnim.Appear(card, 0.03f * i, new Vector2(0f, -24f), 0.25f);
            }
        }

        // ---------------- settings ----------------

        void BuildSettings()
        {
            panel = UiKit.Screen(root, "Settings", 0.6f);
            var d = SaveSystem.Data;
            var board = Board(Loc.T("menu.settings"), new Vector2(780f, 690f), UiKit.Gold);
            bool fromMenu = settingsReturn == Page.Main;

            var items = new List<Selectable>();
            items.Add(Stepper(board, Loc.T("set.music"), 222f, () => d.music, v => { d.music = v; Apply(); }));
            items.Add(Stepper(board, Loc.T("set.sfx"), 162f, () => d.sfx, v => { d.sfx = v; Apply(); Sfx.Play("button", 0.8f); }));
            items.Add(Toggle(board, Loc.T("set.shake"), 102f, () => d.shake, v => { d.shake = v; Apply(); }));
            items.Add(Toggle(board, Loc.T("set.full"), 42f, () => ScreenMode.IsFull, ScreenMode.Set));
            items.Add(Toggle(board, Loc.T("set.hints"), -18f, () => d.hints, v => { d.hints = v; SaveSystem.Save(); }));
            // the language is switched from the main menu only (the HUD is rebuilt)
            if (fromMenu) items.Add(LanguageRow(board, -78f));

            UnityEngine.UI.Button reset = null;
            reset = UiKit.Button(board, Loc.T("set.reset"), new Vector2(0f, -160f), new Vector2(520f, 54f), () =>
            {
                if (!confirmReset)
                {
                    confirmReset = true;
                    UiKit.SetCaption(reset, Loc.T("set.resetConfirm"), UiKit.Red);
                    return;
                }
                SaveSystem.ResetProgress();
                confirmReset = false;
                UiKit.SetCaption(reset, Loc.T("set.resetDone"), UiKit.Dim);
            });
            UiKit.SetCaption(reset, Loc.T("set.reset"), new Color(1f, 0.6f, 0.55f));

            UiKit.Button(board, Loc.T("menu.back"), new Vector2(0f, -260f), new Vector2(260f, 58f), () => Open(settingsReturn));
            if (!instant)
                for (int i = 0; i < items.Count; i++) UiAnim.Appear((RectTransform)items[i].transform.parent, 0.04f * i, new Vector2(-30f, 0f), 0.25f);
            UiKit.Select(items[0]);
        }

        void Apply()
        {
            SaveSystem.Save();
            if (RoomManager.I) RoomManager.I.ApplySettings();
        }

        /// <summary>"Музыка  [-] 40% [+]" row. Returns the minus button.</summary>
        Selectable Stepper(Transform tr, string label, float y, System.Func<float> get, System.Action<float> set)
        {
            var row = UiKit.Group(tr, label, new Vector2(0f, y), new Vector2(560f, 60f));
            UiKit.Text(row, label, 34, UiKit.Cream, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f), new Vector2(260f, 44f));
            Text value = UiKit.Text(row, "", 16, UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(150f, 0f), new Vector2(110f, 30f), true);
            System.Action refresh = () => value.text = $"{Mathf.RoundToInt(get() * 100f)}%";
            refresh();
            var minus = UiKit.Button(row, "-", new Vector2(60f, 0f), new Vector2(60f, 52f), () => { set(Mathf.Clamp01(Mathf.Round((get() - 0.1f) * 10f) / 10f)); refresh(); UiAnim.Punch(value.transform, 0.2f, 0.15f); }, 24);
            UiKit.Button(row, "+", new Vector2(240f, 0f), new Vector2(60f, 52f), () => { set(Mathf.Clamp01(Mathf.Round((get() + 0.1f) * 10f) / 10f)); refresh(); UiAnim.Punch(value.transform, 0.2f, 0.15f); }, 24);
            return minus;
        }

        Selectable Toggle(Transform tr, string label, float y, System.Func<bool> get, System.Action<bool> set)
        {
            var row = UiKit.Group(tr, label, new Vector2(0f, y), new Vector2(520f, 54f));
            UnityEngine.UI.Button b = null;
            System.Func<string> caption = () => $"{label}: {Loc.T(get() ? "set.on" : "set.off")}";
            b = UiKit.Button(row, caption(), Vector2.zero, new Vector2(520f, 54f), () =>
            {
                set(!get());
                UiKit.SetCaption(b, caption());
                UiAnim.Punch(b.transform, 0.06f, 0.15f);
            });
            return b;
        }

        /// <summary>"ЯЗЫК  [&lt;] Русский [&gt;]" — switches the whole game at once.</summary>
        Selectable LanguageRow(Transform tr, float y)
        {
            var row = UiKit.Group(tr, "Language", new Vector2(0f, y), new Vector2(560f, 60f));
            UiKit.Text(row, Loc.T("set.lang"), 16, UiKit.Cream, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(-160f, 0f), new Vector2(240f, 32f), true);
            UiKit.Text(row, Loc.Name(Loc.Current), 30, UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(150f, 0f), new Vector2(120f, 40f));
            var prev = UiKit.Button(row, "<", new Vector2(60f, 0f), new Vector2(60f, 52f), () => SwitchLanguage(-1), 24);
            UiKit.Button(row, ">", new Vector2(240f, 0f), new Vector2(60f, 52f), () => SwitchLanguage(1), 24);
            return prev;
        }

        void SwitchLanguage(int dir)
        {
            var next = (Lang)(((int)Loc.Current + dir + Loc.Count) % Loc.Count);
            Loc.Set(next);
            if (Hud.I) Hud.I.Rebuild();
            Rebuild(Page.Settings);
        }

        // ---------------- about ----------------

        void BuildAbout()
        {
            panel = UiKit.Screen(root, "About", 0.6f);
            var board = Board(Loc.T("menu.about"), new Vector2(1100f, 670f), UiKit.Gold);
            var body = UiKit.Paragraph(board, Loc.T("about.text"), 28, UiKit.Cream, new Vector2(0f, 36f), new Vector2(980f, 420f));
            UiAnim.Appear(body.rectTransform, 0.1f, new Vector2(0f, -20f), 0.3f);

            string[] cast = { "elf_m_idle_anim", "knight_m_idle_anim", "wizzard_f_idle_anim", "lizard_m_idle_anim", "goblin_idle_anim", "skelet_idle_anim", "necromancer_anim", "chort_idle_anim", "tiny_zombie_idle_anim" };
            for (int i = 0; i < cast.Length; i++)
            {
                var img = AnimatedSprite(board, cast[i], new Vector2(-400f + i * 100f, -214f), 3f, 7f);
                if (img) UiAnim.Appear(img.rectTransform, 0.2f + 0.05f * i, new Vector2(0f, -30f), 0.3f);
            }
            var back = UiKit.Button(board, Loc.T("menu.back"), new Vector2(0f, -264f), new Vector2(260f, 54f), () => Open(Page.Main));
            UiKit.Select(back);
        }

        // ---------------- pause ----------------

        void BuildPause()
        {
            panel = UiKit.Screen(root, "Pause", 0.55f);
            var board = Board(Loc.T("pause.title"), new Vector2(840f, 650f), Color.white);
            UiKit.Label(board, pauseInfo, 26, UiKit.Cream, new Vector2(0f, 194f), new Vector2(780f, 116f));

            // the run's relics and combos
            var p = PlayerController.I;
            if (p != null && p.RelicList.Count > 0)
            {
                var list = p.RelicList;
                float x0 = -(list.Count - 1) * 42f / 2f;
                for (int i = 0; i < list.Count; i++)
                    FitIcon(board, Relics.Get(list[i]).sprite, new Vector2(x0 + i * 42f, 110f), 36f);
                var names = new List<string>();
                foreach (var c in p.ComboList) names.Add(Relics.Get(c).Name);
                string line = names.Count > 0 ? Loc.T("hud.combo") + " " + string.Join(", ", names) : "";
                if (line.Length > 0) UiKit.Label(board, line, 24, UiKit.Gold, new Vector2(0f, 72f), new Vector2(780f, 30f));
            }
            else UiKit.Label(board, Loc.T("pause.noRelics"), 24, UiKit.Dim, new Vector2(0f, 96f), new Vector2(780f, 30f));
            UiKit.KeyRow(board, new Vector2(0f, -262f), new[]
            {
                (new[] { "w", "a", "s", "d" }, Loc.T("ctl.move")),
                (new[] { "lmb" }, Loc.T("ctl.attack")),
                (new[] { "shift", "rmb" }, Loc.T("ctl.dash")),
                (new[] { "esc" }, Loc.T("ctl.pause")),
            }, 22, Loc.T("pause.controls"));

            var buttons = new List<UnityEngine.UI.Button>
            {
                UiKit.Button(board, Loc.T("pause.resume"), new Vector2(0f, 4f), new Vector2(460f, 60f), ResumeGame, 24),
                UiKit.Button(board, Loc.T("menu.settings"), new Vector2(0f, -64f), new Vector2(460f, 56f), () => { settingsReturn = Page.Pause; Open(Page.Settings); }),
                UiKit.Button(board, Loc.T("pause.restart"), new Vector2(0f, -130f), new Vector2(460f, 56f), () => { Hide(); RoomManager.I.Restart(); }),
                UiKit.Button(board, Loc.T("pause.menu"), new Vector2(0f, -196f), new Vector2(460f, 56f), () => { Hide(); RoomManager.I.ReturnToMenu(); }),
            };
            Stagger(buttons, new Vector2(0f, -24f), 0.04f, 0.04f);
            UiKit.Select(buttons[0]);
        }

        // ---------------- end of run ----------------

        void BuildEnd()
        {
            var r = lastResult;
            panel = UiKit.Screen(root, "End", 0.6f);
            var board = Board(Loc.T(r.win ? "end.win" : "end.lose"), new Vector2(980f, 620f),
                r.win ? UiKit.Gold : UiKit.Red);

            var icon = r.win ? AnimatedSprite(board, "chest_full_open_anim", new Vector2(0f, 128f), 4f, 4f)
                             : AnimatedSprite(board, "skelet_idle_anim", new Vector2(0f, 128f), 4f, 6f);
            if (icon) UiAnim.Pop(icon.transform, 0.3f, 0.35f, 0.1f);

            string fmt = Loc.T("end.score");
            var score = UiKit.Title(board, string.Format(fmt, 0), 32, Color.white, new Vector2(0f, 98f), new Vector2(800f, 52f));
            UiAnim.CountUp(score, r.score, Mathf.Clamp(r.score / 1500f, 0.6f, 1.6f), fmt, 0.3f);
            if (r.record) blink = UiKit.Title(board, Loc.T("end.record"), 16, UiKit.Gold, new Vector2(0f, 60f), new Vector2(600f, 26f));

            var lines = r.stats.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var t = UiKit.Label(board, lines[i], 28, UiKit.Cream, new Vector2(0f, 18f - i * 34f), new Vector2(860f, 32f));
                UiAnim.Appear(t.rectTransform, 0.5f + 0.12f * i, new Vector2(0f, -14f), 0.25f);
            }
            if (!string.IsNullOrEmpty(r.ending))
            {
                var e = UiKit.Title(board, r.ending.ToUpper(), 16, UiKit.Gold, new Vector2(0f, -114f), new Vector2(860f, 28f));
                UiAnim.Pop(e.transform, 0.5f, 0.3f, 1f);
            }
            if (r.unlocks.Count > 0)
            {
                var u = UiKit.Title(board, Loc.F("end.unlocked", string.Join(Loc.Dot, r.unlocks)).ToUpper(), 16, UiKit.Gold, new Vector2(0f, -150f), new Vector2(860f, 30f));
                UiAnim.Pop(u.transform, 0.5f, 0.3f, 1.1f);
            }
            var again = UiKit.Button(board, Loc.T("end.again"), new Vector2(-160f, -216f), new Vector2(280f, 60f), () => { Hide(); RoomManager.I.Restart(); }, 24);
            var menu = UiKit.Button(board, Loc.T("end.menu"), new Vector2(160f, -216f), new Vector2(280f, 60f), () => { Hide(); RoomManager.I.ReturnToMenu(); }, 24);
            Stagger(new List<UnityEngine.UI.Button> { again, menu }, new Vector2(0f, -24f), 0.9f, 0.08f);
            UiKit.Select(again);
        }
    }
}
