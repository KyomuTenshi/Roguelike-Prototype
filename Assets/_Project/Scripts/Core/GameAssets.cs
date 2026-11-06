using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>One pixel effect from the Super Pixel Effects pack: frames played at 15 fps.</summary>
    [System.Serializable]
    public class FxClip
    {
        public string key;
        public Sprite[] frames;
    }

    /// <summary>
    /// Every asset the game uses (the references are kept in the scene);
    /// sprites are looked up by their file name at runtime (e.g. "goblin_run_anim_f0").
    /// Sheets (UI, icons, key prompts, modern interiors) are cut into sprites here at runtime.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameAssets : MonoBehaviour
    {
        public static GameAssets I { get; private set; }

        [Header("Sprites")]
        [Tooltip("0x72 DungeonTileset II v1.7 / frames")]
        public Sprite[] dungeon;
        [Tooltip("0x72 16x16 Dungeon Tileset v5 / items")]
        public Sprite[] classic;

        [Header("Rendering")]
        public Material invertMaterial;
        public Material unlitSprite;
        public Material litSprite;
        public GameObject pointLightPrefab;

        [Header("Text")]
        [Tooltip("Body text (Handjet)")]
        public Font font;
        [Tooltip("Titles and buttons (Press Start 2P)")]
        public Font titleFont;
        [Tooltip("Chinese (ZCOOL QingKe HuangYou, subset)")]
        public Font cjkFont;

        /// <summary>Body font for the current language (world labels, floating text).</summary>
        public Font BodyFont => Loc.Cjk && cjkFont ? cjkFont : font;

        [Header("UI (fallback: Assets/_Project/UI/Sprites)")]
        public Sprite uiPanel;
        public Sprite uiButton, uiButtonHover, uiButtonPressed, uiButtonDisabled, uiButtonOn;
        public Sprite uiBarBack, uiBarFill;
        public Texture2D cursorAim, cursorMenu;

        [Header("Third-party sheets (Assets/_ThirdParty)")]
        [Tooltip("Dark Ages UI by Hypnobius — 32x32-Tilesheet.png (Read/Write enabled)")]
        public Texture2D darkAgesUi;
        [Tooltip("Raven Fantasy Icons (free) — Full Spritesheet/16x16.png")]
        public Texture2D ravenIcons;
        [Tooltip("gdb gamepad UI — gdb-keyboard-2.png")]
        public Texture2D keyPrompts;
        [Tooltip("Modern tiles (free) by LimeZu — Room_Builder_free_16x16 / Interiors_free_16x16 / Adam_idle_anim_16x16")]
        public Texture2D modernRoom, modernInteriors, modernAdam, modernAmelia, modernAdamRun, modernAmeliaRun;
        [Tooltip("Modern tiles: Adam_phone / Amelia_sit (memories), Alex and Bob idle_anim and run (friends in the bright room)")]
        public Texture2D modernAdamPhone, modernAmeliaSit, modernAdamSit, modernAmeliaPhone, modernAlex, modernBob, modernAlexRun, modernBobRun;
        [Tooltip("gdb gamepad UI — gdb-xbox-2 / gdb-playstation-2 / gdb-switch-2 (prompts while a gamepad is used)")]
        public Texture2D padXbox, padPlaystation, padSwitch;

        [Header("Effects (Super Pixel Effects Gigapack, free)")]
        public FxClip[] effects;

        [Header("Floor bosses (chierit: Demon Slime, Frost Guardian)")]
        [Tooltip("boss_demon_slime_FREE_v1.0 / individual sprites (pivot at the feet)")]
        public Sprite[] demonSlime;
        [Tooltip("Frost_Guardian_FREE_v1.0 / PNG files (pivot at the feet)")]
        public Sprite[] frostGuardian;

        [Header("Retro law (SGQ Enemies + SGQ Dungeon, cut at runtime)")]
        [Tooltip("Sheets found by file name: goblin, skeleton, …, slime_king, elf, walls, grounds, props, animated_props…")]
        public Texture2D[] retroSheets;

        [Header("Caves (RPG Worlds: Caves v2, cut at runtime)")]
        [Tooltip("MainLev2.0.png")]
        public Texture2D caveTiles;
        [Tooltip("decorative.png")]
        public Texture2D caveDecor;
        [Tooltip("Anim/LedA…LedD.png")]
        public Texture2D[] caveLights;

        [Header("Music (xDeviruchi 16-bit and 8-bit packs, 16-bit RPG Music, Fast-Paced pack)")]
        public AudioClip[] music;

        /// <summary>Dark Ages UI pieces (null when the pack is missing → old UI sprites are used).</summary>
        [System.NonSerialized] public Sprite uiPlate, uiParchment, uiCrown, uiAlert, uiDivider;
        /// <summary>True when the UI comes from the Dark Ages sheet (2 UI units per art pixel).</summary>
        public bool DarkAges { get; private set; }

        Dictionary<string, Sprite> dungeonMap, classicMap;
        readonly Dictionary<string, Sprite[]> animCache = new Dictionary<string, Sprite[]>();
        readonly Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>();
        readonly Dictionary<string, Sprite> keyCache = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Sprite> modernCache = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Sprite[]> fxMap = new Dictionary<string, Sprite[]>();
        Dictionary<string, AudioClip> musicMap;
        Dictionary<int, AudioClip> trackMap;
        Sprite[] adamFrames, ameliaFrames;

#if UNITY_EDITOR
        /// <summary>
        /// The Modern tiles pack lost its links once (re-imported with new GUIDs, the secret rooms and Adam and Amelia vanished):
        /// in the editor any missing sheet of it is found again by file name and saved into the scene.
        /// </summary>
        void OnValidate()
        {
            // while Unity imports (a platform switch reimports every texture) the sheets only look missing for a moment
            if (UnityEditor.EditorApplication.isUpdating || UnityEditor.EditorApplication.isCompiling) return;
            bool changed = false;
            modernRoom = Relink(modernRoom, "Room_Builder_free_16x16", ref changed);
            modernInteriors = Relink(modernInteriors, "Interiors_free_16x16", ref changed);
            modernAdam = Relink(modernAdam, "Adam_idle_anim_16x16", ref changed);
            modernAmelia = Relink(modernAmelia, "Amelia_idle_anim_16x16", ref changed);
            modernAdamRun = Relink(modernAdamRun, "Adam_run_16x16", ref changed);
            modernAmeliaRun = Relink(modernAmeliaRun, "Amelia_run_16x16", ref changed);
            modernAdamPhone = Relink(modernAdamPhone, "Adam_phone_16x16", ref changed);
            modernAmeliaPhone = Relink(modernAmeliaPhone, "Amelia_phone_16x16", ref changed);
            modernAdamSit = Relink(modernAdamSit, "Adam_sit_16x16", ref changed);
            modernAmeliaSit = Relink(modernAmeliaSit, "Amelia_sit_16x16", ref changed);
            modernAlex = Relink(modernAlex, "Alex_idle_anim_16x16", ref changed);
            modernBob = Relink(modernBob, "Bob_idle_anim_16x16", ref changed);
            modernAlexRun = Relink(modernAlexRun, "Alex_run_16x16", ref changed);
            modernBobRun = Relink(modernBobRun, "Bob_run_16x16", ref changed);
            padXbox = Relink(padXbox, "gdb-xbox-2", ref changed);
            padPlaystation = Relink(padPlaystation, "gdb-playstation-2", ref changed);
            padSwitch = Relink(padSwitch, "gdb-switch-2", ref changed);
            if (changed) UnityEditor.EditorUtility.SetDirty(this);
        }

        static Texture2D Relink(Texture2D current, string fileName, ref bool changed)
        {
            if (current) return current;
            // the same texture, just not loaded right now: nothing to fix (and the scene must not turn "modified")
            if (!ReferenceEquals(current, null) && current.GetInstanceID() != 0 &&
                UnityEditor.AssetDatabase.Contains(current.GetInstanceID())) return current;
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets(fileName + " t:Texture2D"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != fileName) continue;
                var t = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (!t) continue;
                if (!ReferenceEquals(current, null) && current.GetInstanceID() == t.GetInstanceID()) return current;
                changed = true;
                return t;
            }
            return current;
        }
#endif

        void Awake()
        {
            I = this;
            dungeonMap = Map(dungeon);
            classicMap = Map(classic);
            musicMap = new Dictionary<string, AudioClip>();
            trackMap = new Dictionary<int, AudioClip>();
            if (music != null)
                foreach (var m in music)
                {
                    if (!m) continue;
                    musicMap[m.name.ToLowerInvariant()] = m;
                    musicMap[m.name.Trim().ToLowerInvariant()] = m;   // some 8-bit files end with a space
                    if (TrackNumber(m.name, out int n) && !trackMap.ContainsKey(n)) trackMap[n] = m;
                }
            if (effects != null)
                foreach (var e in effects)
                {
                    if (e == null || e.frames == null) continue;
                    // a frame can be missing until its texture is imported as a single sprite (ThirdPartyImportFix)
                    var ok = new List<Sprite>(e.frames.Length);
                    foreach (var f in e.frames) if (f) ok.Add(f);
                    if (ok.Count > 0) fxMap[e.key] = ok.ToArray();
                }
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!titleFont) titleFont = font;
            BuildDarkAgesUi();
            BuildBossAnims();
            BuildRetro();
            // keep the pixel font crisp: its glyph atlas must use point filtering
            Font.textureRebuilt += OnFontRebuilt;
            OnFontRebuilt(titleFont);
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= OnFontRebuilt;
            if (I == this) I = null;
        }

        void OnFontRebuilt(Font f)
        {
            if (f && f == titleFont && f.material && f.material.mainTexture)
                f.material.mainTexture.filterMode = FilterMode.Point;
        }

        static Dictionary<string, Sprite> Map(Sprite[] sprites)
        {
            var map = new Dictionary<string, Sprite>();
            if (sprites == null) return map;
            foreach (var s in sprites)
                if (s && !map.ContainsKey(s.name)) map[s.name] = s;
            return map;
        }

        /// <summary>"05 - Battle 1" → 5 (the xDeviruchi 16-bit naming; "05. Long Journey" of the RPG pack is not a track number).</summary>
        static bool TrackNumber(string name, out int n)
        {
            n = 0;
            int i = 0;
            while (i < name.Length && char.IsDigit(name[i])) i++;
            if (i == 0 || i + 2 >= name.Length || name[i] != ' ' || name[i + 1] != '-') return false;
            return int.TryParse(name.Substring(0, i), out n);
        }

        /// <summary>Sprite from a sheet; rect in pixels from the TOP-left corner (as seen in an image editor).</summary>
        static Sprite Cut(Texture2D tex, int x, int y, int w, int h, Vector2 pivot, float ppu, Vector4 border = default)
        {
            if (!tex) return null;
            var r = new Rect(x, tex.height - y - h, w, h);
            if (r.xMin < 0 || r.yMin < 0 || r.xMax > tex.width || r.yMax > tex.height) return null;
            var s = Sprite.Create(tex, r, pivot, ppu, 0, SpriteMeshType.FullRect, border);
            s.name = $"{tex.name}_{x}_{y}";
            return s;
        }

        // ---------------- Dark Ages UI ----------------

        void BuildDarkAgesUi()
        {
            var t = darkAgesUi;
            if (!t) return;
            t.filterMode = FilterMode.Point;
            var c = new Vector2(0.5f, 0.5f);
            // borders: (left, bottom, right, top)
            var panel = Cut(t, 0, 0, 96, 96, c, 100f, new Vector4(26, 26, 26, 26));
            if (!panel) return;
            DarkAges = true;
            uiPanel = panel;
            uiPlate = Cut(t, 210, 18, 60, 60, c, 100f, new Vector4(10, 10, 10, 10));
            uiParchment = Cut(t, 96, 0, 96, 96, c, 100f, new Vector4(12, 12, 12, 12));
            uiButton = Cut(t, 0, 104, 64, 19, c, 100f, new Vector4(10, 6, 10, 6));
            uiButtonHover = Cut(t, 64, 104, 64, 19, c, 100f, new Vector4(10, 6, 10, 6));
            uiButtonPressed = uiButton;
            uiButtonDisabled = uiButton;
            uiButtonOn = Decrowned(t);
            uiCrown = Cut(t, 144, 96, 32, 9, new Vector2(0.5f, 0f), 100f);
            uiBarBack = Cut(t, 102, 141, 84, 7, c, 100f, new Vector4(10, 2, 10, 2));
            uiBarFill = Cut(t, 199, 176, 82, 4, c, 100f);
            uiAlert = Cut(t, 105, 232, 14, 17, c, 100f);
            uiDivider = Cut(t, 294, 266, 85, 10, c, 100f);
        }

        /// <summary>The blue button without its crown (the crown is drawn separately so it never stretches).</summary>
        Sprite Decrowned(Texture2D t)
        {
            const int x0 = 128, y0 = 104, w = 64, h = 19;
            try
            {
                var src = t.GetPixels(x0, t.height - y0 - h, w, h);   // bottom-up rows
                for (int row = 0; row < 6; row++)                     // top 6 rows of the button
                {
                    int r = h - 1 - row;
                    for (int x = 16; x < 48; x++) src[r * w + x] = src[r * w + 12];
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "ui_button_on" };
                tex.SetPixels(src);
                tex.Apply(false, true);
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10, 6, 10, 6));
            }
            catch (UnityException)
            {
                // sheet not readable: use the gray button for "on"
                return uiButtonHover;
            }
        }

        // ---------------- sprites by name ----------------

        /// <summary>Sprite from DungeonTileset II.</summary>
        public Sprite D(string name) => name != null && dungeonMap.TryGetValue(name, out var s) ? s : null;

        /// <summary>Sprite from the classic 16x16 Dungeon Tileset.</summary>
        public Sprite C(string name) => name != null && classicMap.TryGetValue(name, out var s) ? s : null;

        /// <summary>Raven Fantasy icon number n (1-based, as in "Separated Files/16x16/fa{n}.png").</summary>
        public Sprite Icon(int n)
        {
            if (!ravenIcons || n < 1) return null;
            if (iconCache.TryGetValue(n, out var s)) return s;
            int i = n - 1, cols = ravenIcons.width / 16;
            s = Cut(ravenIcons, (i % cols) * 16, (i / cols) * 16, 16, 16, new Vector2(0.5f, 0f), 16f);
            iconCache[n] = s;
            return s;
        }

        /// <summary>
        /// "c:name" = classic tileset, "r:123" = Raven icon, "m:name" = modern interior,
        /// otherwise DungeonTileset II.
        /// </summary>
        public Sprite Any(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (name.StartsWith("c:")) return C(name.Substring(2));
            if (name.StartsWith("r:")) return int.TryParse(name.Substring(2), out int n) ? Icon(n) : null;
            if (name.StartsWith("m:")) return Modern(name.Substring(2));
            return D(name);
        }

        // ---------------- key prompts ----------------

        static readonly Dictionary<string, RectInt> Keys = new Dictionary<string, RectInt>
        {
            { "esc", new RectInt(32, 16, 16, 16) },
            { "1", new RectInt(64, 32, 16, 16) }, { "2", new RectInt(80, 32, 16, 16) }, { "3", new RectInt(96, 32, 16, 16) },
            { "q", new RectInt(64, 48, 16, 16) }, { "w", new RectInt(80, 48, 16, 16) }, { "e", new RectInt(96, 48, 16, 16) }, { "p", new RectInt(208, 48, 16, 16) },
            { "a", new RectInt(64, 64, 16, 16) },
            { "s", new RectInt(80, 64, 16, 16) }, { "d", new RectInt(96, 64, 16, 16) },
            { "shift", new RectInt(36, 80, 28, 16) }, { "space", new RectInt(100, 96, 104, 16) },
            { "lmb", new RectInt(503, 34, 18, 29) }, { "rmb", new RectInt(535, 2, 18, 29) },
        };

        // gamepad buttons on the gdb sheets (16x16 cells): south, east, west, north, lb, rb, lt, rt, start, select, ls, rs
        static readonly Dictionary<string, RectInt> XboxKeys = new Dictionary<string, RectInt>
        {
            { "west", new RectInt(16, 32, 16, 16) }, { "south", new RectInt(16, 48, 16, 16) },
            { "north", new RectInt(16, 64, 16, 16) }, { "east", new RectInt(16, 80, 16, 16) },
            { "select", new RectInt(16, 96, 16, 16) }, { "start", new RectInt(16, 112, 16, 16) },
            { "lt", new RectInt(336, 48, 16, 16) }, { "rt", new RectInt(336, 64, 16, 16) },
            { "lb", new RectInt(336, 80, 16, 16) }, { "rb", new RectInt(336, 96, 16, 16) },
            { "ls", new RectInt(216, 32, 16, 16) }, { "rs", new RectInt(280, 32, 16, 16) },
        };
        static readonly Dictionary<string, RectInt> PlaystationKeys = new Dictionary<string, RectInt>
        {
            { "north", new RectInt(16, 32, 16, 16) }, { "east", new RectInt(16, 48, 16, 16) },
            { "south", new RectInt(16, 64, 16, 16) }, { "west", new RectInt(16, 80, 16, 16) },
            { "start", new RectInt(336, 48, 16, 16) }, { "select", new RectInt(336, 64, 16, 16) },
            { "lt", new RectInt(336, 80, 16, 16) }, { "rt", new RectInt(336, 96, 16, 16) },
            { "lb", new RectInt(336, 112, 16, 16) }, { "rb", new RectInt(336, 128, 16, 16) },
            { "ls", new RectInt(216, 32, 16, 16) }, { "rs", new RectInt(280, 32, 16, 16) },
        };
        static readonly Dictionary<string, RectInt> SwitchKeys = new Dictionary<string, RectInt>
        {
            { "south", new RectInt(16, 32, 16, 16) }, { "east", new RectInt(16, 48, 16, 16) },
            { "west", new RectInt(16, 64, 16, 16) }, { "north", new RectInt(16, 80, 16, 16) },
            { "start", new RectInt(112, 128, 16, 16) }, { "select", new RectInt(112, 144, 16, 16) },
            { "lt", new RectInt(304, 48, 16, 16) }, { "rt", new RectInt(304, 64, 16, 16) },
            { "lb", new RectInt(304, 80, 16, 16) }, { "rb", new RectInt(304, 96, 16, 16) },
            { "ls", new RectInt(184, 32, 16, 16) }, { "rs", new RectInt(248, 32, 16, 16) },
        };

        /// <summary>A gamepad button of the pad in hand (Xbox, PlayStation or Switch look), null when its sheet is missing.</summary>
        public Sprite PadKey(string button)
        {
            var kind = Binds.PadType;
            Texture2D sheet = kind == PadKind.PlayStation ? padPlaystation : kind == PadKind.Switch ? padSwitch : padXbox;
            var rects = kind == PadKind.PlayStation ? PlaystationKeys : kind == PadKind.Switch ? SwitchKeys : XboxKeys;
            if (!sheet) { sheet = padXbox; rects = XboxKeys; }
            if (!sheet || button == null || !rects.TryGetValue(button, out var r)) return null;
            string cacheKey = "pad:" + kind + ":" + button;
            if (keyCache.TryGetValue(cacheKey, out var s)) return s;
            s = Cut(sheet, r.x, r.y, r.width, r.height, new Vector2(0.5f, 0.5f), 16f);
            keyCache[cacheKey] = s;
            return s;
        }

        /// <summary>Keyboard / mouse prompt icon: esc, 1, 2, 3, w, e, p, a, s, d, shift, space, lmb, rmb; "pad:south" etc. for the gamepad.</summary>
        public Sprite Key(string key)
        {
            if (key != null && key.StartsWith("pad:")) return PadKey(key.Substring(4));
            if (!keyPrompts || key == null || !Keys.TryGetValue(key, out var r)) return null;
            if (keyCache.TryGetValue(key, out var s)) return s;
            s = Cut(keyPrompts, r.x, r.y, r.width, r.height, new Vector2(0.5f, 0.5f), 16f);
            keyCache[key] = s;
            return s;
        }

        // ---------------- modern interiors (the secret room) ----------------

        // name → (sheet: 0 room builder / 1 interiors, x, y, w, h) in pixels from the top-left
        static readonly Dictionary<string, int[]> ModernRects = new Dictionary<string, int[]>
        {
            // room builder: wooden wall (2 tiles high), herringbone floor, dark ceiling
            { "wall_top_l", new[] { 0, 0, 176, 16, 16 } }, { "wall_top", new[] { 0, 16, 176, 16, 16 } }, { "wall_top_r", new[] { 0, 32, 176, 16, 16 } },
            { "wall_bottom_l", new[] { 0, 0, 192, 16, 16 } }, { "wall_bottom", new[] { 0, 16, 192, 16, 16 } }, { "wall_bottom_r", new[] { 0, 32, 192, 16, 16 } },
            { "void", new[] { 0, 192, 0, 16, 16 } },
            { "floor", new[] { 0, 192, 224, 16, 16 } },
            // the dream: mint wall, yellow tiles
            { "wall_top_l_mint", new[] { 0, 0, 144, 16, 16 } }, { "wall_top_mint", new[] { 0, 16, 144, 16, 16 } }, { "wall_top_r_mint", new[] { 0, 32, 144, 16, 16 } },
            { "wall_bottom_l_mint", new[] { 0, 0, 160, 16, 16 } }, { "wall_bottom_mint", new[] { 0, 16, 160, 16, 16 } }, { "wall_bottom_r_mint", new[] { 0, 32, 160, 16, 16 } },
            { "floor_mint", new[] { 0, 192, 128, 16, 16 } },
            // the silence: grey-blue wall, grey carpet
            { "wall_top_l_gray", new[] { 0, 0, 272, 16, 16 } }, { "wall_top_gray", new[] { 0, 16, 272, 16, 16 } }, { "wall_top_r_gray", new[] { 0, 32, 272, 16, 16 } },
            { "wall_bottom_l_gray", new[] { 0, 0, 288, 16, 16 } }, { "wall_bottom_gray", new[] { 0, 16, 288, 16, 16 } }, { "wall_bottom_r_gray", new[] { 0, 32, 288, 16, 16 } },
            { "floor_gray", new[] { 0, 240, 128, 16, 16 } },
            // interiors
            { "door_closed", new[] { 1, 113, 139, 14, 21 } },
            { "door_open", new[] { 1, 129, 139, 14, 21 } },
            { "bookshelf", new[] { 1, 80, 224, 32, 32 } },
            { "tv", new[] { 1, 3, 234, 26, 18 } },
            { "bed", new[] { 1, 92, 16, 41, 64 } },
            { "rug", new[] { 1, 114, 244, 60, 40 } },
            { "boxes", new[] { 1, 177, 170, 31, 20 } },
            { "window", new[] { 1, 147, 391, 25, 20 } },
            { "computer", new[] { 1, 48, 138, 16, 22 } },
            { "desk", new[] { 1, 80, 576, 32, 32 } },
            { "chair", new[] { 1, 1, 572, 14, 23 } },
            { "fridge", new[] { 1, 192, 640, 16, 32 } },
            { "board", new[] { 1, 210, 647, 28, 23 } },
            { "calendar", new[] { 1, 208, 616, 31, 21 } },
            { "photo", new[] { 1, 6, 394, 21, 14 } },
            { "tree", new[] { 1, 167, 713, 18, 31 } },
            { "palm", new[] { 1, 213, 704, 23, 31 } },
            { "plant", new[] { 1, 193, 722, 14, 23 } },
        };

        public Sprite Modern(string key)
        {
            if (key == null) return null;
            if (modernCache.TryGetValue(key, out var s)) return s;
            if (!ModernRects.TryGetValue(key, out var r)) return null;
            var tex = r[0] == 0 ? modernRoom : modernInteriors;
            s = Cut(tex, r[1], r[2], r[3], r[4], new Vector2(0.5f, 0f), 16f);
            if (s) s.name = "modern_" + key;
            modernCache[key] = s;
            return s;
        }

        /// <summary>Both Modern tiles sheets are there (the secret room can be built).</summary>
        public bool HasModern => modernRoom && modernInteriors;

        /// <summary>Idle frames of Adam (LimeZu), facing the camera: Adam.</summary>
        public Sprite[] AdamIdle() => adamFrames ?? (adamFrames = IdleFrames(modernAdam));

        /// <summary>Idle frames of Amelia (LimeZu), facing the camera: Amelia.</summary>
        public Sprite[] AmeliaIdle() => ameliaFrames ?? (ameliaFrames = IdleFrames(modernAmelia));

        readonly Dictionary<Texture2D, Sprite[][]> dirCache = new Dictionary<Texture2D, Sprite[][]>();

        /// <summary>A LimeZu 24-frame strip as four directions of 6 frames: right, up, left, down.</summary>
        public Sprite[][] Directions(Texture2D sheet)
        {
            if (!sheet) return null;
            if (dirCache.TryGetValue(sheet, out var d)) return d;
            int frames = sheet.width / 16;
            if (frames < 24) return null;
            d = new Sprite[4][];
            for (int dir = 0; dir < 4; dir++)
            {
                d[dir] = new Sprite[6];
                for (int i = 0; i < 6; i++)
                    d[dir][i] = Cut(sheet, (dir * 6 + i) * 16, 0, 16, sheet.height, new Vector2(0.5f, 0f), 16f);
            }
            dirCache[sheet] = d;
            return d;
        }

        /// <summary>Adam (true) or Amelia (false) for cutscenes: idle and walking in four directions.</summary>
        public Sprite[][] PersonDirs(bool adam, bool running) =>
            Directions(adam ? (running ? modernAdamRun : modernAdam) : (running ? modernAmeliaRun : modernAmelia));

        readonly Dictionary<string, Sprite[]> poseCache = new Dictionary<string, Sprite[]>();

        /// <summary>Sitting on a chair (LimeZu sit strip: 12 frames 32 px wide, 6 facing right then 6 facing left).</summary>
        public Sprite[] Sitting(bool adam, bool facingLeft)
        {
            var sheet = adam ? modernAdamSit : modernAmeliaSit;
            string key = (adam ? "adam" : "amelia") + "_sit" + (facingLeft ? "_l" : "_r");
            if (!sheet) return null;
            if (poseCache.TryGetValue(key, out var f)) return f;
            int count = Mathf.Min(6, sheet.width / 64);
            f = new Sprite[count];
            for (int i = 0; i < count; i++) f[i] = Cut(sheet, (facingLeft ? count + i : i) * 32, 0, 32, sheet.height, new Vector2(0.5f, 0f), 16f);
            poseCache[key] = f;
            return f;
        }

        /// <summary>Looking at the phone, facing the camera (LimeZu phone strip: 9 frames).</summary>
        public Sprite[] OnPhone(bool adam)
        {
            var sheet = adam ? modernAdamPhone : modernAmeliaPhone;
            string key = adam ? "adam_phone" : "amelia_phone";
            if (!sheet) return null;
            if (poseCache.TryGetValue(key, out var f)) return f;
            int count = sheet.width / 16;
            f = new Sprite[count];
            for (int i = 0; i < count; i++) f[i] = Cut(sheet, i * 16, 0, 16, sheet.height, new Vector2(0.5f, 0f), 16f);
            poseCache[key] = f;
            return f;
        }

        /// <summary>Alex (true) or Bob (false), the friends from the real world: four directions, idle or running.</summary>
        public Sprite[][] FriendDirs(bool alex, bool running) =>
            Directions(alex ? (running ? modernAlexRun : modernAlex) : (running ? modernBobRun : modernBob));

        static Sprite[] IdleFrames(Texture2D sheet)
        {
            if (!sheet) return new Sprite[0];
            var list = new List<Sprite>();
            int frames = sheet.width / 16;
            int start = frames >= 24 ? 18 : 0;   // 4 directions x 6 frames: right, up, left, down
            for (int i = start; i < Mathf.Min(frames, start + 6); i++)
            {
                var s = Cut(sheet, i * 16, 0, 16, sheet.height, new Vector2(0.5f, 0f), 16f);
                if (s) list.Add(s);
            }
            return list.ToArray();
        }

        // ---------------- animations, effects, music ----------------

        /// <summary>Frames "prefix_f0", "prefix_f1"… sorted by number. Falls back to a single sprite named prefix.</summary>
        public Sprite[] Anim(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return new Sprite[0];
            if (animCache.TryGetValue(prefix, out var cached)) return cached;

            var found = new List<KeyValuePair<int, Sprite>>();
            string p = prefix + "_f";
            foreach (var kv in dungeonMap)
                if (kv.Key.StartsWith(p) && int.TryParse(kv.Key.Substring(p.Length), out int n))
                    found.Add(new KeyValuePair<int, Sprite>(n, kv.Value));
            found.Sort((a, b) => a.Key.CompareTo(b.Key));

            Sprite[] result;
            if (found.Count > 0)
            {
                result = new Sprite[found.Count];
                for (int i = 0; i < found.Count; i++) result[i] = found[i].Value;
            }
            else
            {
                var single = D(prefix);
                result = single ? new[] { single } : new Sprite[0];
            }
            animCache[prefix] = result;
            return result;
        }

        public bool HasAnim(string prefix) => Anim(prefix).Length > 0;

        /// <summary>Frames of a pixel effect ("hit", "death", "boom"…), or null.</summary>
        public Sprite[] Effect(string key) => key != null && fxMap.TryGetValue(key, out var f) ? f : null;

        /// <summary>Music for a key: an own file named like the key, or the pack track from MusicTracks.</summary>
        public AudioClip Music(string key)
        {
            if (string.IsNullOrEmpty(key) || musicMap == null) return null;
            if (musicMap.TryGetValue(key.ToLowerInvariant(), out var c)) return c;
            var t = MusicTracks.Get(key);
            if (t == null) return null;
            if (!string.IsNullOrEmpty(t.file)) return musicMap.TryGetValue(t.file.ToLowerInvariant(), out c) ? c : null;
            return trackMap.TryGetValue(t.number, out c) ? c : null;
        }
    
        // ---------------- floor bosses (chierit) ----------------

        /// <summary>Registers frame sets as animations, so Anim("demon_slime_run_anim") works like any 0x72 animation.</summary>
        void Register(string key, Sprite[] frames)
        {
            if (frames != null && frames.Length > 0) animCache[key] = frames;
        }

        /// <summary>"demon_walk_1" … "demon_walk_12" from one set, sorted by number.</summary>
        static Sprite[] Numbered(Sprite[] set, string prefix)
        {
            var found = new List<KeyValuePair<int, Sprite>>();
            if (set != null)
                foreach (var s in set)
                    if (s && s.name.StartsWith(prefix) && int.TryParse(s.name.Substring(prefix.Length), out int n))
                        found.Add(new KeyValuePair<int, Sprite>(n, s));
            found.Sort((a, b) => a.Key.CompareTo(b.Key));
            var result = new Sprite[found.Count];
            for (int i = 0; i < found.Count; i++) result[i] = found[i].Value;
            return result;
        }

        void BuildBossAnims()
        {
            Register("demon_slime_idle_anim", Numbered(demonSlime, "demon_idle_"));
            Register("demon_slime_run_anim", Numbered(demonSlime, "demon_walk_"));
            Register("demon_slime_attack_anim", Numbered(demonSlime, "demon_cleave_"));
            Register("demon_slime_hit_anim", Numbered(demonSlime, "demon_take_hit_"));
            Register("demon_slime_death_anim", Numbered(demonSlime, "demon_death_"));
            Register("frost_guardian_idle_anim", Numbered(frostGuardian, "idle_"));
            Register("frost_guardian_run_anim", Numbered(frostGuardian, "walk_"));
            Register("frost_guardian_attack_anim", Numbered(frostGuardian, "1_atk_"));
            Register("frost_guardian_hit_anim", Numbered(frostGuardian, "take_hit_"));
            Register("frost_guardian_death_anim", Numbered(frostGuardian, "death_"));
        }

        // ---------------- the Retro law (SGQ, Game Boy palette) ----------------

        readonly Dictionary<string, Texture2D> retroMap = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, Sprite> retroCache = new Dictionary<string, Sprite>();

        Texture2D Sheet(string name) => name != null && retroMap.TryGetValue(name, out var t) ? t : null;

        /// <summary>The SGQ tileset is there: Retro rooms get their own walls, floor and monsters.</summary>
        public bool HasRetro => Sheet("walls") && Sheet("grounds");

        /// <summary>The current room is drawn in the SGQ set (Version 1.0, a Retro room). Set by RoomManager.</summary>
        public static bool RetroTheme;

        /// <summary>The current room plays by the Retro law or lies in Version 1.0, and the SGQ sprites are there.</summary>
        public bool RetroOn => (Rules.Current == Law.Retro || RetroTheme) && HasRetro;

        /// <summary>One animation strip of an SGQ sheet: count cells of w×h from (col, row).</summary>
        class Strip
        {
            public string key, sheet;
            public int w, h, row, count, col;
            public Vector2 pivot = new Vector2(0.5f, 0f);
            public Strip(string key, string sheet, int w, int h, int row, int count, int col = 0)
            { this.key = key; this.sheet = sheet; this.w = w; this.h = h; this.row = row; this.count = count; this.col = col; }
        }

        // monsters: the first row of each pair has the dark outline (the second is the same without it)
        static readonly Strip[] RetroStrips =
        {
            new Strip("sgq_bat_idle_anim", "bat", 16, 16, 0, 3), new Strip("sgq_bat_run_anim", "bat", 16, 16, 2, 2), new Strip("sgq_bat_death_anim", "bat", 16, 16, 4, 4),
            new Strip("sgq_fly_idle_anim", "fly", 16, 16, 0, 3), new Strip("sgq_fly_death_anim", "fly", 16, 16, 2, 4),
            new Strip("sgq_flying_skull_idle_anim", "flying_skull", 16, 16, 0, 4), new Strip("sgq_flying_skull_death_anim", "flying_skull", 16, 16, 2, 4),
            new Strip("sgq_ghost_idle_anim", "ghost", 16, 16, 0, 3), new Strip("sgq_ghost_death_anim", "ghost", 16, 16, 2, 4),
            new Strip("sgq_goblin_idle_anim", "goblin", 16, 16, 0, 3), new Strip("sgq_goblin_run_anim", "goblin", 16, 16, 2, 4), new Strip("sgq_goblin_death_anim", "goblin", 16, 16, 4, 4),
            new Strip("sgq_rat_idle_anim", "rat", 16, 16, 0, 2), new Strip("sgq_rat_run_anim", "rat", 16, 16, 2, 3), new Strip("sgq_rat_death_anim", "rat", 16, 16, 4, 4),
            new Strip("sgq_skeleton_idle_anim", "skeleton", 16, 16, 0, 3), new Strip("sgq_skeleton_run_anim", "skeleton", 16, 16, 2, 4), new Strip("sgq_skeleton_death_anim", "skeleton", 16, 16, 4, 4),
            new Strip("sgq_slime_idle_anim", "slime", 16, 16, 0, 2), new Strip("sgq_slime_run_anim", "slime", 16, 16, 2, 3), new Strip("sgq_slime_death_anim", "slime", 16, 16, 4, 4),
            new Strip("sgq_spider_idle_anim", "spider", 16, 16, 0, 2), new Strip("sgq_spider_run_anim", "spider", 16, 16, 2, 4), new Strip("sgq_spider_death_anim", "spider", 16, 16, 4, 4),
            new Strip("sgq_cube_slime_idle_anim", "cube_slime", 16, 32, 0, 2), new Strip("sgq_cube_slime_run_anim", "cube_slime", 16, 32, 2, 3), new Strip("sgq_cube_slime_death_anim", "cube_slime", 16, 32, 4, 4),
            new Strip("sgq_medium_slime_idle_anim", "medium_slime", 32, 32, 0, 2), new Strip("sgq_medium_slime_run_anim", "medium_slime", 32, 32, 2, 4), new Strip("sgq_medium_slime_death_anim", "medium_slime", 32, 32, 4, 4),
            new Strip("sgq_slime_king_idle_anim", "slime_king", 48, 48, 0, 2), new Strip("sgq_slime_king_run_anim", "slime_king", 48, 48, 2, 5), new Strip("sgq_slime_king_death_anim", "slime_king", 48, 48, 6, 4),
            // the elf (the Elf hero in Retro rooms)
            new Strip("sgq_elf_idle_anim", "elf", 16, 16, 0, 3), new Strip("sgq_elf_run_anim", "elf", 16, 16, 2, 4),
            new Strip("sgq_elf_hit_anim", "elf", 16, 16, 6, 3), new Strip("sgq_elf_death_anim", "elf", 16, 16, 8, 6),
            // props, pickups, projectiles
            new Strip("sgq_torch_anim", "animated_props", 16, 16, 0, 4),
            new Strip("sgq_brazier_anim", "animated_props", 16, 32, 2, 4),
            new Strip("sgq_altar_anim", "animated_props", 16, 32, 3, 4),
            new Strip("sgq_chest_anim", "animated_props", 16, 16, 0, 3, 4),
            new Strip("sgq_lever_anim", "animated_props", 16, 16, 1, 3, 4),
            new Strip("sgq_button_anim", "animated_props", 16, 16, 2, 3, 4),
            new Strip("sgq_gold_anim", "animated_props", 16, 16, 0, 4, 7),
            new Strip("sgq_crack_anim", "animated_props", 16, 16, 4, 3, 4),
            new Strip("sgq_coin_anim", "pickup_items_animated", 16, 16, 0, 4) { pivot = new Vector2(0.5f, 0.5f) },
            new Strip("sgq_key_anim", "pickup_items_animated", 16, 16, 1, 4),
            new Strip("sgq_potion_anim", "pickup_items_animated", 16, 16, 2, 4),
            new Strip("sgq_heart_anim", "pickup_items_animated", 16, 16, 4, 4),
            new Strip("sgq_ingot_anim", "pickup_items_animated", 16, 16, 6, 4),
            new Strip("sgq_nugget_anim", "pickup_items_animated", 16, 16, 7, 4),
            new Strip("sgq_arrow_anim", "projectiles_animated", 16, 16, 0, 2) { pivot = new Vector2(0.5f, 0.5f) },
            new Strip("sgq_bone_anim", "projectiles_animated", 16, 16, 1, 8) { pivot = new Vector2(0.5f, 0.5f) },
            new Strip("sgq_orb_anim", "projectiles_animated", 16, 16, 2, 3) { pivot = new Vector2(0.5f, 0.5f) },
            new Strip("sgq_pickaxe_anim", "projectiles_animated", 16, 16, 3, 8) { pivot = new Vector2(0.5f, 0.5f) },
            new Strip("sgq_whirl_anim", "projectiles_animated", 16, 16, 4, 3) { pivot = new Vector2(0.5f, 0.5f) },
            new Strip("sgq_spark_anim", "projectiles_animated", 16, 16, 5, 3) { pivot = new Vector2(0.5f, 0.5f) },
        };

        // single sprites: sheet, x, y, w, h in pixels from the top-left (bottom-centre pivot)
        static readonly Dictionary<string, object[]> RetroRects = new Dictionary<string, object[]>
        {
            // walls: a brick mass seen from above (walls.png, second block)
            { "wall_top", new object[] { "walls", 112, 0, 16, 16 } }, { "wall_top_2", new object[] { "walls", 96, 0, 16, 16 } },
            { "wall_fill", new object[] { "walls", 112, 16, 16, 16 } }, { "wall_fill_2", new object[] { "walls", 96, 32, 16, 16 } },
            { "wall_face", new object[] { "walls", 112, 48, 16, 16 } }, { "wall_face_2", new object[] { "walls", 96, 48, 16, 16 } },
            { "wall_left", new object[] { "walls", 80, 16, 16, 16 } }, { "wall_left_2", new object[] { "walls", 80, 32, 16, 16 } },
            { "wall_right", new object[] { "walls", 128, 16, 16, 16 } }, { "wall_right_2", new object[] { "walls", 128, 32, 16, 16 } },
            { "wall_corner_tl", new object[] { "walls", 80, 0, 16, 16 } }, { "wall_corner_tr", new object[] { "walls", 128, 0, 16, 16 } },
            { "wall_corner_bl", new object[] { "walls", 80, 48, 16, 16 } }, { "wall_corner_br", new object[] { "walls", 128, 48, 16, 16 } },
            { "door_closed", new object[] { "walls", 192, 32, 16, 16 } }, { "door_open", new object[] { "walls", 176, 0, 16, 16 } },
            // floor: plain stone, a few worn and grassy tiles
            { "floor", new object[] { "grounds", 144, 32, 16, 16 } },
            { "floor_v1", new object[] { "grounds", 80, 16, 16, 16 } }, { "floor_v2", new object[] { "grounds", 96, 16, 16, 16 } },
            { "floor_v3", new object[] { "grounds", 80, 32, 16, 16 } }, { "floor_v4", new object[] { "grounds", 96, 32, 16, 16 } },
            { "floor_v5", new object[] { "grounds", 80, 80, 16, 16 } }, { "floor_v6", new object[] { "grounds", 96, 96, 16, 16 } },
            { "floor_v7", new object[] { "grounds", 192, 192, 16, 16 } }, { "floor_v8", new object[] { "grounds", 144, 224, 16, 16 } },
            // props.png
            { "stairs", new object[] { "props", 0, 0, 16, 16 } }, { "books", new object[] { "props", 16, 0, 16, 16 } },
            { "barrel", new object[] { "props", 0, 16, 16, 16 } }, { "rope", new object[] { "props", 16, 16, 16, 16 } },
            { "tablet", new object[] { "props", 32, 16, 16, 16 } }, { "cauldron", new object[] { "props", 48, 16, 16, 16 } },
            { "boulder", new object[] { "props", 0, 32, 16, 16 } }, { "boulder_cracked", new object[] { "props", 16, 32, 16, 16 } },
            { "fern", new object[] { "props", 32, 32, 16, 16 } }, { "signpost", new object[] { "props", 48, 32, 16, 16 } },
            { "skull", new object[] { "props", 0, 48, 16, 16 } }, { "stool", new object[] { "props", 16, 48, 16, 16 } },
            { "crate", new object[] { "props", 32, 48, 16, 16 } }, { "mushrooms", new object[] { "props", 48, 48, 16, 16 } },
            { "rock", new object[] { "props", 0, 64, 16, 16 } }, { "ore", new object[] { "props", 16, 64, 16, 16 } },
            { "rocks", new object[] { "props", 32, 64, 16, 16 } }, { "ore_2", new object[] { "props", 48, 64, 16, 16 } },
            { "dead_bush", new object[] { "props", 0, 80, 16, 16 } }, { "ore_cart", new object[] { "props", 16, 80, 16, 16 } },
            { "pickaxe", new object[] { "props", 32, 80, 16, 16 } },
            { "table", new object[] { "props", 160, 0, 32, 16 } }, { "rack", new object[] { "props", 192, 0, 32, 16 } },
            { "bookshelf", new object[] { "props", 240, 0, 16, 32 } }, { "column", new object[] { "props", 256, 0, 16, 32 } },
            { "hole", new object[] { "animated_props", 112, 64, 16, 16 } },
            // mine: rails and carts
            { "rail", new object[] { "minecart_and_rails", 32, 0, 16, 16 } },
            { "cart", new object[] { "minecart_and_rails", 0, 64, 16, 16 } }, { "cart_side", new object[] { "minecart_and_rails", 16, 64, 16, 16 } },
            { "cart_tipped", new object[] { "minecart_and_rails", 32, 64, 16, 16 } }, { "cart_full", new object[] { "minecart_and_rails", 80, 32, 16, 32 } },
        };

        void BuildRetro()
        {
            if (retroSheets == null) return;
            foreach (var t in retroSheets)
            {
                if (!t) continue;
                t.filterMode = FilterMode.Point;
                retroMap[t.name.ToLowerInvariant()] = t;
            }
            foreach (var st in RetroStrips)
            {
                var tex = Sheet(st.sheet);
                if (!tex) continue;
                var frames = new List<Sprite>();
                for (int i = 0; i < st.count; i++)
                {
                    var s = Cut(tex, (st.col + i) * st.w, st.row * st.h, st.w, st.h, st.pivot, 16f);
                    if (s) { s.name = $"{st.key}_f{i}"; frames.Add(s); }
                }
                Register(st.key, frames.ToArray());
            }
        }

        /// <summary>A single SGQ sprite by name (see RetroRects), or the first frame of "sgq_*_anim".</summary>
        public Sprite Retro(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (retroCache.TryGetValue(name, out var s)) return s;
            if (RetroRects.TryGetValue(name, out var r))
                s = Cut(Sheet((string)r[0]), (int)r[1], (int)r[2], (int)r[3], (int)r[4], new Vector2(0.5f, 0f), 16f);
            if (s) s.name = "sgq_" + name;
            retroCache[name] = s;
            return s;
        }

        /// <summary>Monster skins of the Retro law, by behaviour.</summary>
        public static readonly string[] RetroRunners = { "sgq_goblin", "sgq_skeleton", "sgq_rat", "sgq_spider", "sgq_bat" };
        public static readonly string[] RetroShooters = { "sgq_ghost", "sgq_flying_skull", "sgq_cube_slime" };
        public static readonly string[] RetroKamikazes = { "sgq_slime", "sgq_fly" };

        // ---------------- caves (RPG Worlds: Caves v2) ----------------

        readonly Dictionary<string, Sprite> caveCache = new Dictionary<string, Sprite>();

        public bool HasCave => caveTiles;

        /// <summary>A 32×32 cave tile (two world units) by its column and row in MainLev2.0; covers the cell and the one to its right.</summary>
        public Sprite CaveTile(int tx, int ty)
        {
            if (!caveTiles) return null;
            string key = "t" + tx + "_" + ty;
            if (caveCache.TryGetValue(key, out var s)) return s;
            caveTiles.filterMode = FilterMode.Point;
            s = Cut(caveTiles, tx * 32, ty * 32, 32, 32, new Vector2(0.25f, 0f), 16f);
            if (s) s.name = "cave_" + key;
            caveCache[key] = s;
            return s;
        }

        // decorative.png: x, y, w, h (glowing rocks, dark spires, crystals, pebbles)
        static readonly RectInt[] CaveDecorRects =
        {
            new RectInt(2, 35, 88, 95), new RectInt(233, 75, 53, 55), new RectInt(301, 78, 44, 52), new RectInt(353, 101, 30, 29),   // 0-3 glowing red rocks
            new RectInt(223, 265, 100, 121), new RectInt(126, 299, 68, 87), new RectInt(65, 344, 31, 42), new RectInt(223, 472, 31, 42), // 4-7 dark spires
            new RectInt(66, 579, 29, 25), new RectInt(63, 608, 35, 31), new RectInt(31, 646, 29, 25), new RectInt(30, 675, 35, 31),     // 8-11 blue crystals
            new RectInt(190, 579, 29, 25), new RectInt(187, 608, 35, 31), new RectInt(227, 646, 29, 25), new RectInt(222, 675, 35, 31), // 12-15 green crystals
            new RectInt(44, 574, 18, 23), new RectInt(105, 582, 15, 16), new RectInt(14, 592, 16, 16), new RectInt(38, 606, 22, 26),    // 16-19 small blue crystals
            new RectInt(41, 734, 38, 18), new RectInt(14, 738, 16, 17), new RectInt(96, 748, 34, 48), new RectInt(39, 766, 27, 22),     // 20-23 rocks
            new RectInt(169, 734, 38, 18), new RectInt(142, 738, 16, 17), new RectInt(224, 748, 34, 48), new RectInt(167, 766, 27, 22), // 24-27 rocks (other tint)
        };

        public const int CaveRedRocks = 0, CaveSpires = 4, CaveBlue = 8, CaveGreen = 12, CaveBlueSmall = 16, CaveRocks = 20, CaveRocks2 = 24;

        public Sprite CaveDecor(int i)
        {
            if (!caveDecor || i < 0 || i >= CaveDecorRects.Length) return null;
            string key = "d" + i;
            if (caveCache.TryGetValue(key, out var s)) return s;
            caveDecor.filterMode = FilterMode.Point;
            var r = CaveDecorRects[i];
            s = Cut(caveDecor, r.x, r.y, r.width, r.height, new Vector2(0.5f, 0.05f), 16f);
            if (s) s.name = "cave_decor_" + i;
            caveCache[key] = s;
            return s;
        }

        readonly Dictionary<int, Sprite[]> caveLightCache = new Dictionary<int, Sprite[]>();

        /// <summary>Glowing crystals (LedA…D): sheet 0-3, variant 0-3; six frames of 32×32.</summary>
        public Sprite[] CaveLight(int sheet, int variant)
        {
            if (caveLights == null || caveLights.Length == 0) return new Sprite[0];
            sheet = Mathf.Abs(sheet) % caveLights.Length;
            variant = Mathf.Abs(variant) % 4;
            int key = sheet * 4 + variant;
            if (caveLightCache.TryGetValue(key, out var f)) return f;
            var tex = caveLights[sheet];
            var list = new List<Sprite>();
            if (tex)
            {
                tex.filterMode = FilterMode.Point;
                for (int i = 0; i < 6; i++)
                {
                    int row = variant * 2 + (i < 4 ? 0 : 1), col = i < 4 ? i : i - 4;
                    var s = Cut(tex, col * 32, row * 32, 32, 32, new Vector2(0.5f, 0.1f), 16f);
                    if (s) list.Add(s);
                }
            }
            f = list.ToArray();
            caveLightCache[key] = f;
            return f;
        }

    }
}
