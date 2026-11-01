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

        [Header("Effects (Super Pixel Effects Gigapack, free)")]
        public FxClip[] effects;

        [Header("Music (xDeviruchi 16-bit Fantasy & Adventure + Assets/_Project/Audio/Music)")]
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
                    if (TrackNumber(m.name, out int n) && !trackMap.ContainsKey(n)) trackMap[n] = m;
                }
            if (effects != null)
                foreach (var e in effects)
                    if (e != null && e.frames != null && e.frames.Length > 0) fxMap[e.key] = e.frames;
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!titleFont) titleFont = font;
            BuildDarkAgesUi();
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

        static bool TrackNumber(string name, out int n)
        {
            n = 0;
            int i = 0;
            while (i < name.Length && char.IsDigit(name[i])) i++;
            return i > 0 && int.TryParse(name.Substring(0, i), out n);
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
            { "w", new RectInt(80, 48, 16, 16) }, { "a", new RectInt(64, 64, 16, 16) },
            { "s", new RectInt(80, 64, 16, 16) }, { "d", new RectInt(96, 64, 16, 16) },
            { "shift", new RectInt(36, 80, 28, 16) }, { "space", new RectInt(100, 96, 104, 16) },
            { "lmb", new RectInt(503, 34, 18, 29) }, { "rmb", new RectInt(535, 2, 18, 29) },
        };

        /// <summary>Keyboard / mouse prompt icon: esc, 1, 2, 3, w, a, s, d, shift, space, lmb, rmb.</summary>
        public Sprite Key(string key)
        {
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

        /// <summary>Idle frames of Adam (LimeZu), facing the camera: the Maker.</summary>
        public Sprite[] AdamIdle() => adamFrames ?? (adamFrames = IdleFrames(modernAdam));

        /// <summary>Idle frames of Amelia (LimeZu), facing the camera: Lina.</summary>
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

        /// <summary>The Maker (true) or Lina (false) for cutscenes: idle and walking in four directions.</summary>
        public Sprite[][] PersonDirs(bool maker, bool running) =>
            Directions(maker ? (running ? modernAdamRun : modernAdam) : (running ? modernAmeliaRun : modernAmelia));

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
    }
}
