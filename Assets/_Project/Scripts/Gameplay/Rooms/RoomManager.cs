using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RulesShift
{
    public enum GameState { Menu, Playing, Choosing, Paused, Dead, Won }

    public class RunResult
    {
        public bool win;
        public GameMode mode;
        public int score;
        public bool record;
        public string stats;
        public string ending;       // which ending (wins only)
        public List<string> unlocks = new List<string>();
    }

    /// <summary>
    /// The run: Прихожая → rooms picked through doors → Законодатель (or forever in Endless).
    /// Each room = layout + law + objective. Leaving a room just applies the next law.
    /// </summary>
    public partial class RoomManager : MonoBehaviour
    {
        public static RoomManager I { get; private set; }

        [Header("Rooms")]
        public List<RoomDef> rooms = new List<RoomDef>();
        public RoomDef bossRoom;
        [Tooltip("Floors in a run. Each ends with a boss; the last one with the Lawgiver.")]
        [Range(1, 5)] public int floors = 3;
        [Tooltip("Rooms on a floor before its boss (Прихожая counts on the first floor)")]
        [Range(2, 8)] public int roomsPerFloor = 6;

        public GameState State { get; private set; }
        public Room CurrentRoom => room;
        public RoomDef Current => def;
        public RunConfig Config => cfg;
        public int Score => score;
        public int Depth => step + 1;
        public int Floor => floor;
        public int Coins => coins;
        /// <summary>Rooms passed in this run (Codex Banner relic).</summary>
        public int RoomsPassed => step;
        public IReadOnlyList<Law> PassedLaws => passedLaws;

        [HideInInspector] public int kills;
        [HideInInspector] public float runTime;

        const float ChaosInterval = 12f;
        static readonly Law[] ConsoleLaws = { Law.Ice, Law.Noir, Law.Mirror, Law.Upside, Law.Crypt, Law.Neon, Law.Pacifist };
        static readonly Law[] ChaosKill = { Law.Crypt, Law.Noir, Law.Ice, Law.Mirror, Law.Upside, Law.Neon, Law.Retro };
        static readonly Law[] ChaosSurvive = { Law.Pacifist, Law.Crypt, Law.Ice, Law.Mirror, Law.Upside, Law.Neon };
        static readonly Law[] ChaosRun = { Law.Ice, Law.Mirror, Law.Upside, Law.Noir, Law.Crypt, Law.Neon };
        static readonly Law[] FloorBossLaws = { Law.Ice, Law.Noir, Law.Crypt, Law.Mirror, Law.Upside, Law.Neon };
        static readonly Law[] DemonLaws = { Law.Noir, Law.Crypt, Law.Mirror, Law.Upside, Law.Neon };

        readonly List<Law> passedLaws = new List<Law>();
        RunConfig cfg = new RunConfig();
        List<RoomDef> unused = new List<RoomDef>();
        RoomDef[] nextChoices = new RoomDef[0];
        Law extraLaw;
        int step, score, hpSnapshot, lavaRow, baseHpBonus, floor, inFloor, coins, coinsSnapshot;
        float eliteChance;
        RoomDef def;
        Room room;
        PlayerController player;
        bool busy, cleared, spawned, keyTaken, finished, bossSeen;
        float spawnTimer, objTimer, lavaTimer, bubbleTimer, lavaHurtTimer, chaosTimer;
        readonly List<KeyValuePair<Vector2Int, float>> pendingLava = new List<KeyValuePair<Vector2Int, float>>();

        // other worlds: a portal door on the first floors, a chain of rooms and their own bosses
        bool retroDone, cavesDone;
        int portalFloor = -1;                     // one portal offer per floor at most
        // an escapee from another world in this room, and the hidden rift it came through
        bool riftHere;
        World riftWorld;
        int riftHits, escapeeFloor = -1;
        float riftHitTime;
        readonly HashSet<string> npcsMet = new HashSet<string>();

        // the secret room: a cracked wall in one combat room per run
        int secretFloor, secretFrom, secretHits;
        bool secretDone, secretHere, memoryThisRun;
        float secretHitTime;
        RoomDef[] savedChoices;

        class Button { public Vector2 pos; public SpriteRenderer sr; public bool pressed; }
        readonly List<Button> buttons = new List<Button>();

        void Awake()
        {
            I = this;
            Time.timeScale = 1f;
            Rules.Reset();
            Difficulty.Reset();
            Enemy.All.Clear();
            Boss.Defeated = false;
            Fx.Root = null;
            if (rooms == null || rooms.Count == 0) rooms = RoomCatalog.Pool();
            if (bossRoom == null || bossRoom.objective != Objective.Boss) bossRoom = RoomCatalog.Boss();
            foreach (var extra in RoomCatalog.Extra())
                if (!rooms.Exists(r => r.law == extra.law)) rooms.Add(extra);
            SaveSystem.Load();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }

        void Start()
        {
            ApplySettings();
            ShowBackdrop();
            if (MenuUI.I) MenuUI.I.ShowMain();
        }

        public void ApplySettings()
        {
            var d = SaveSystem.Data;
            if (MusicPlayer.I) MusicPlayer.I.volume = d.music;
            if (Sfx.I) Sfx.I.volume = d.sfx;
            CameraFollow.ShakeEnabled = d.shake;
            QualitySettings.vSyncCount = 1;
        }

        // ---------------- menu backdrop ----------------

        void ShowBackdrop()
        {
            State = GameState.Menu;
            ClearRoom();
            if (player) { Destroy(player.gameObject); player = null; }
            if (Pixie.I) Destroy(Pixie.I.gameObject);
            Rules.Apply(Law.None);
            room = Room.Build(Layouts.Get(0), Theme.Dungeon, Law.None, transform);
            Fx.Root = room.Content;
            if (StyleController.I) StyleController.I.Apply(Law.None, true);
            if (CameraFollow.I) { CameraFollow.I.bounds = room.Bounds; CameraFollow.I.target = null; CameraFollow.I.Snap(); }
            if (MusicPlayer.I) MusicPlayer.I.Play("menu");
            if (Hud.I)
            {
                Hud.I.ShowBossBar(false);
                StartCoroutine(Hud.I.FadeTo(0f, 0.6f));
            }
            PopulateBackdrop();
            StartCoroutine(BackdropCycle());
        }

        static readonly string[] BackdropSkins =
            { "goblin", "skelet", "orc_shaman", "chort", "ice_zombie", "necromancer", "masked_orc", "swampy", "imp", "wogol" };
        static readonly Law[] BackdropLaws = { Law.None, Law.Noir, Law.Ice, Law.Pacifist, Law.Neon, Law.Retro, Law.Mirror, Law.Stealth };

        /// <summary>Idle monsters standing around the menu hall.</summary>
        void PopulateBackdrop()
        {
            if (!room || !GameAssets.I) return;
            var a = GameAssets.I;
            int n = 0;
            foreach (var p in room.EnemySpawns)
            {
                string skin = BackdropSkins[(n++ * 3 + Random.Range(0, 3)) % BackdropSkins.Length];
                var frames = a.Anim(skin + "_idle_anim");
                if (frames.Length == 0) frames = a.Anim(skin + "_anim");
                if (frames.Length == 0) continue;
                var sr = room.Prop(frames[0].name, p, 0);
                if (!sr) continue;
                sr.flipX = p.x > room.W / 2f;
                var anim = sr.gameObject.AddComponent<SpriteAnimator>();
                anim.target = sr;
                anim.Play(frames, Random.Range(5f, 8f));
            }
        }

        /// <summary>The menu hall keeps shifting its law: Noir, Ice, Neon… (visual only).</summary>
        IEnumerator BackdropCycle()
        {
            int i = 0;
            while (true)
            {
                yield return new WaitForSecondsRealtime(5f);
                if (State != GameState.Menu) yield break;
                i = (i + 1) % BackdropLaws.Length;
                if (StyleController.I) StyleController.I.Apply(BackdropLaws[i]);
            }
        }

        /// <summary>Leave the run (pause menu → В меню).</summary>
        public void ReturnToMenu()
        {
            StopAllCoroutines();
            Cutscene.UI.Clear();   // a black left by an ending must not stay
            Time.timeScale = 1f;
            if (!finished && State != GameState.Menu)
            {
                SaveSystem.Data.totalKills += kills;
                SaveSystem.Save();
                finished = true;
            }
            ShowBackdrop();
            if (MenuUI.I) MenuUI.I.ShowMain();
        }

        // ---------------- run ----------------

        public void StartRun(RunConfig config)
        {
            StopAllCoroutines();
            Cutscene.UI.Clear();
            Time.timeScale = 1f;
            cfg = config.Copy();
            if (cfg.mode == GameMode.Daily)
            {
                cfg.seed = Daily.Seed;
                cfg.curses = 1 << (int)Daily.TodayCurse;
            }
            if (cfg.seed == 0) cfg.seed = Random.Range(1, int.MaxValue);
            RunRandom.Init(cfg.seed);
            Upgrades.NewRun();

            Difficulty.Reset();
            if (cfg.Has(Curse.Fury)) { Difficulty.EnemySpeed = 1.3f; Difficulty.EnemyHpBonus = 1; }
            baseHpBonus = Difficulty.EnemyHpBonus;

            kills = 0;
            runTime = 0f;
            score = 0;
            step = 0;
            floor = 0;
            inFloor = 0;
            coins = 0;
            finished = false;
            bossSeen = false;
            passedLaws.Clear();
            unused = rooms.FindAll(r => r.inPool && r.objective != Objective.Boss && r.law != Law.None);
            RunRandom.Shuffle(unused);

            var save = SaveSystem.Data;
            save.runs++;
            save.hero = cfg.hero.id;
            if (cfg.mode != GameMode.Daily) { save.mode = (int)cfg.mode; save.curses = cfg.curses; }
            SaveSystem.Save();

            RoomDef start = rooms.Find(r => r.law == Law.None) ?? rooms[0];
            konamiUsed = false;
            konamiIndex = 0;
            worldMusicOffset = RunRandom.Range(0, 30);
            retroDone = !(GameAssets.I && GameAssets.I.HasRetro && FloorBoss.Available(FloorBossKind.SlimeKing));
            cavesDone = !(GameAssets.I && GameAssets.I.HasCave && FloorBoss.Available(FloorBossKind.FrostGuardian));
            secretFloor = Endless ? RunRandom.Range(0, 2) : RunRandom.Range(0, floors);
            secretFrom = RunRandom.Range(1, Mathf.Max(2, roomsPerFloor - 1));
            // the crack doesn't show up in every run
            secretDone = !(GameAssets.I && GameAssets.I.HasModern) || RunRandom.Value > 0.55f;
            portalFloor = -1;
            escapeeFloor = -1;
            planFloor = -1;
            npcsMet.Clear();
            memoryThisRun = false;
            completedMemories = false;
            ending = -1;
            savedChoices = null;

            if (player) Destroy(player.gameObject);
            player = PlayerController.Create(transform, cfg.hero);
            Pixie.Spawn(transform, player.transform);
            player.SetActive(ActiveId.Bomb);   // every run starts with a bomb in the active slot (Q)
            if (cfg.Has(Curse.GlassHeart)) player.maxHalves = player.halves = 2;
            if (CameraFollow.I) CameraFollow.I.target = player.transform;
            if (Hud.I) { Hud.I.SetScore(0, cfg.ScoreMul); Hud.I.SetCoins(0); }

            StartCoroutine(BeginRun(start));
        }

        /// <summary>The very first run starts with the night the console turned on.</summary>
        IEnumerator BeginRun(RoomDef start)
        {
            if (!SaveSystem.Data.prologueSeen)
            {
                yield return Prologue();
                SaveSystem.Data.prologueSeen = true;
                SaveSystem.Save();
                if (Hud.I) yield return Hud.I.FadeTo(1f, 0.01f);
                Cutscene.UI.Clear();
            }
            yield return EnterRoom(start, false);
        }

        /// <summary>Same mode, hero and curses; a new seed (except Daily).</summary>
        public void Restart()
        {
            var c = cfg.Copy();
            if (c.mode != GameMode.Daily) c.seed = 0;
            StartRun(c);
        }

        bool Endless => cfg.mode == GameMode.Endless;
        bool LastFloor => !Endless && floor >= floors - 1;

        /// <summary>What waits behind the doors of this room: two rooms, the floor boss, or nothing (the end).</summary>
        RoomDef[] DecideNext(RoomDef current)
        {
            if (current.objective == Objective.Secret)   // the memory leads back to the doors you found it behind
                return savedChoices != null && savedChoices.Length > 0 ? savedChoices : Draw(2, current);
            if (current.world != World.None)
            {
                // the next room of the world, or back to the main path where the portal was
                var w = WorldRoom(current.world, current.worldIndex + 1);
                return w != null ? new[] { w } : MainNext(inFloor + 1, current);
            }
            if (current.objective == Objective.Boss)
                return LastFloor ? new RoomDef[0] : Draw(2, current);   // down to the next floor
            return MainNext(inFloor + 1, current);
        }

        /// <summary>One layer of the floor plan (the two doors of room `next`): see RoomMap.cs.</summary>
        RoomDef[] PlanLayer(int next)
        {
            var picks = Draw(2, null);
            // the rooms drawn for this layer go to the back of the queue: the next layers get other ones
            foreach (var r in picks) { unused.Remove(r); unused.Add(r); }
            if (picks.Length == 2)
            {
                // one shop per floor, sometimes a shrine or a fountain
                if (next == 2) picks[RunRandom.Range(0, 2)] = Special(Objective.Shop);
                else if ((next == roomsPerFloor - 1 || next == 4) && RunRandom.Value < 0.5f)
                    picks[RunRandom.Range(0, 2)] = Special(RunRandom.Value < 0.5f ? Objective.Shrine : Objective.Fountain);
                // a challenge, an ambush, a hidden cache or the System's gacha terminal
                if (next >= 1 && RunRandom.Value < 0.35f) AddRoomVariant(picks);
                // sometimes a portal to another world takes the place of a door (once a floor, each world once a run)
                if (next >= 1 && next <= roomsPerFloor - 2 && portalFloor != floor && RunRandom.Value < 0.22f)
                {
                    var w = PickWorld();
                    if (w != World.None)
                    {
                        portalFloor = floor;
                        int slot = picks[0].Special ? 1 : picks[1].Special ? 0 : RunRandom.Range(0, 2);
                        picks[slot] = WorldRoom(w, 0);
                    }
                }
            }
            return picks;
        }

        // ---------------- the classic code: ↑ ↑ ↓ ↓ ← → ← → B A ----------------

        static readonly Key[] Konami =
            { Key.UpArrow, Key.UpArrow, Key.DownArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.LeftArrow, Key.RightArrow, Key.B, Key.A };
        int konamiIndex;
        bool konamiUsed;

        /// <summary>Once per run: +1 heart and 30 coins.</summary>
        void KonamiTick(Keyboard kb)
        {
            if (konamiUsed || !kb.anyKey.wasPressedThisFrame) return;
            if (kb[Konami[konamiIndex]].wasPressedThisFrame)
            {
                if (++konamiIndex < Konami.Length) return;
                konamiIndex = 0;
                konamiUsed = true;
                player.maxHalves = Mathf.Min(12, player.maxHalves + 2);
                player.Heal(2);
                AddCoins(30);
                Hud.Toast(Loc.T("toast.konami"), UiKit.Gold);
                Sfx.Play("win", 0.6f);
                Fx.Burst(player.Position, UiKit.Gold, 24, 5f, 0.7f, 2f);
            }
            else konamiIndex = kb[Konami[0]].wasPressedThisFrame ? 1 : 0;
        }

        bool pendingFocusPause;

        /// <summary>
        /// Alt-Tab or a click outside the window pauses the run ("Run In Background" keeps the game going otherwise).
        /// During a room transition the pause waits for the transition to end instead of being lost
        /// (and is dropped if the player is back by then).
        /// </summary>
        void OnApplicationFocus(bool focus)
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer && Application.isMobilePlatform) return;   // see MobilePlatform
            pendingFocusPause = !focus && State == GameState.Playing;   // back before it applied: nothing to pause
        }

        void Update()
        {
            Binds.Track();   // keyboard or gamepad prompts
            var kb = Keyboard.current;
            if (kb != null && State == GameState.Playing && player != null) KonamiTick(kb);
            // web: Esc can drop the browser out of fullscreen without the game seeing the key — pause then too
            bool lostFull = ScreenMode.WebFullscreenLost();
            // the window lost focus (Alt+Tab, Cmd+Tab, a click elsewhere): the run waits in the pause menu
            bool focusPause = pendingFocusPause && State == GameState.Playing && !busy;
            if (focusPause || State != GameState.Playing) pendingFocusPause = false;
            bool esc = Binds.PauseDown() || lostFull || focusPause;
            if (State == GameState.Playing)
            {
                if (esc && !busy) Pause();
                else if (MapKey()) ToggleMap();
                else Tick(Time.deltaTime);
            }
        }

        int resumedFrame = -1;

        public void Pause()
        {
            if (State != GameState.Playing || Time.frameCount == resumedFrame) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
            if (MenuUI.I) MenuUI.I.ShowPause(PauseInfo());
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
            resumedFrame = Time.frameCount;   // the same Esc press must not pause again
        }

        string PauseInfo()
        {
            var m = Modes.Info(cfg.mode).name;
            string curses = CurseList();
            return $"{m}{Loc.Dot}{cfg.hero.name}{Loc.Dot}{CounterText()}\n" +
                   Loc.F("hud.law", Rules.Label) + "\n" +
                   (curses.Length > 0 ? Loc.F("pause.curses", curses) + "\n" : "") +
                   Loc.F("pause.coins", coins) + Loc.Dot +
                   Loc.F("pause.score", score, cfg.ScoreMul.ToString("0.0"), cfg.seed);
        }

        string CurseList()
        {
            var list = new List<string>();
            for (int i = 0; i < Curses.Count; i++) if (cfg.Has((Curse)i)) list.Add(Curses.Info((Curse)i).name);
            return string.Join(", ", list);
        }

        string CounterText()
        {
            bool boss = def != null && def.objective == Objective.Boss;
            if (def != null && def.world != World.None)
            {
                string name = Loc.T(def.world == World.Retro ? "world.retro" : "world.caves");
                return boss ? Loc.F("hud.worldBoss", name) : Loc.F("hud.worldRoom", name, def.worldIndex + 1);
            }
            return boss ? Loc.F("hud.floorBoss", floor + 1) : Loc.F("hud.floorRoom", floor + 1, inFloor + 1, roomsPerFloor);
        }

        // ---------------- rooms ----------------

        void ClearRoom()
        {
            GameAssets.RetroTheme = false;
            foreach (var e in Enemy.All.ToArray()) if (e) Destroy(e.gameObject);
            Enemy.All.Clear();
            Bosses.Defeated = false;
            if (room) Destroy(room.gameObject);
            room = null;
            buttons.Clear();
            pendingLava.Clear();
        }

        static bool CanStack(RoomDef d) =>
            d.law == Law.Crypt || d.law == Law.Noir || d.law == Law.Ice || d.law == Law.Mirror ||
            d.law == Law.Upside || d.law == Law.Neon || d.law == Law.Lava || d.law == Law.Gravity || d.law == Law.Bounce;

        void ApplyLaws(Law law)
        {
            Rules.Apply(law);
            if (extraLaw != Law.None && extraLaw != law) Rules.AddExtra(extraLaw);
            SaveSystem.Discover(law);
        }

        IEnumerator EnterRoom(RoomDef d, bool restart)
        {
            busy = true;
            State = GameState.Playing;
            player.frozen = true;
            bool intoMemory = d.objective == Objective.Secret && !restart;
            if (intoMemory)
            {
                Sfx.Play("blink");
                if (CameraFollow.I) CameraFollow.I.Shake(0.2f);
                yield return Cutscene.UI.FadeTo(Cutscene.UI.white, 1f, 0.35f);   // a flash instead of the dark
            }
            else if (Hud.I) yield return Hud.I.FadeTo(1f, restart ? 0.5f : 0.3f);

            ClearRoom();
            def = d;
            cleared = false;
            spawned = false;
            keyTaken = false;
            objTimer = player ? player.SurviveTime(d.time) : d.time;
            ResetRoomVariant();
            spawnTimer = 1.5f;
            lavaRow = 0;
            lavaTimer = 2.8f;
            bubbleTimer = 3.5f;
            chaosTimer = ChaosInterval;
            bool isBoss = d.objective == Objective.Boss;

            if (!restart)
            {
                RunRandom.ForRoom(step, d.id + "/" + d.law + "/" + floor);
                nextChoices = DecideNext(d);
                extraLaw = Law.None;
                if (cfg.Has(Curse.DoubleLaw) && CanStack(d))
                {
                    var options = new List<Law>(Rules.Stackable);
                    options.Remove(d.law);
                    extraLaw = RunRandom.Pick(options);
                }
            }

            // deeper floors: tougher, more and elite enemies
            int loop = Endless ? floor / 3 : 0;
            // the deeper and the stronger the hero, the tougher everything gets
            int relics = player ? player.RelicList.Count : 0;
            float power = 1f + Mathf.Min(0.6f, 0.04f * relics);
            Difficulty.EnemyHpBonus = baseHpBonus + floor;
            Difficulty.EnemyHpMul = (1f + 0.3f * floor) * power * (1f + 0.5f * loop);
            Difficulty.EnemyDamage = floor >= 2 || loop > 0 ? 2 : 1;
            Difficulty.CountMul = 1.15f + 0.2f * floor;   // the rooms are bigger now
            Difficulty.BossHpMul = 1.35f * (1f + 0.5f * loop) * (1f + Mathf.Min(0.5f, 0.03f * relics));
            eliteChance = d.law == Law.None || d.Special ? 0f : 0.06f + 0.06f * floor;

            ApplyLaws(d.law);
            if (extraLaw != Law.None) SaveSystem.Discover(extraLaw);
            if (isBoss && !d.floorBoss && !bossSeen) { bossSeen = true; SaveSystem.Data.bossReached++; SaveSystem.Discover(Law.Boss); }

            // combat rooms are generated anew every run (the same for a given seed)
            bool generated = (d.objective == Objective.KillAll || d.objective == Objective.Survive
                              || d.objective == Objective.Challenge || d.objective == Objective.Ambush) && d.law != Law.None;
            var layout = generated ? LayoutGen.Generate(new System.Random(cfg.seed ^ (step * 7919 + 104729))) : Layouts.Get(d.layout);
            var theme = FloorTheme(d);
            string modernStyle = d.objective == Objective.Secret ? MemoryStyles[Mathf.Clamp(d.memory, 0, MemoryCount - 1)]
                               : theme == Theme.Cave ? CaveStyle(d) : "";
            GameAssets.RetroTheme = theme == Theme.Retro;
            room = Room.Build(layout, theme, d.law, transform, Mathf.Max(1, nextChoices.Length), modernStyle);
            for (int i = 0; i < nextChoices.Length; i++)
            {
                var n = nextChoices[i];
                room.SetDoorLabel(i, n.objective == Objective.Boss && !n.floorBoss ? Loc.T("hud.boss") : n.Title, n.SignColor);
                if (n.worldEntry) room.Portal(i, RoomDef.WorldColor(n.world));
            }
            Fx.Root = room.Content;
            secretHits = 0;
            riftHere = false;
            lawgiverEnraged = false;
            lastBossPos = null;
            secretHere = !secretDone && SecretCanBeHere(d) && room.TryAddSecret(RunRandom.Value < 0.5f);
            if (StyleController.I) StyleController.I.Apply(d.law, true);
            if (MusicPlayer.I) MusicPlayer.I.Play(MusicFor(d));

            player.Teleport(room.PlayerSpawn);
            if (restart) { player.Revive(hpSnapshot); coins = coinsSnapshot; if (Hud.I) Hud.I.SetCoins(coins); }
            else player.ResetForRoom();
            hpSnapshot = player.halves;
            coinsSnapshot = coins;
            if (StyleController.I) StyleController.I.ApplyHeroLight();

            if (CameraFollow.I)
            {
                CameraFollow.I.bounds = room.Bounds;
                CameraFollow.I.target = player.transform;
                CameraFollow.I.Snap();
            }

            SetupObjective();
            Populate(d);
            if (d.law == Law.Gravity) GravityWell();
            if (!restart) NotePath(d);
            if (floor >= 1) Achievements.Unlock("floor2");
            if (floor >= 2) Achievements.Unlock("floor3");
            if (Hud.I)
            {
                Hud.I.SetRoomCounter(CounterText());
                Hud.I.ShowBossBar(false);
                yield return Hud.I.FadeTo(0f, 0.3f);
            }

            // cutscenes: stepping into a memory, meeting the Lawgiver
            if (intoMemory) yield return MemoryIntro(d);
            bool meeting = isBoss && !d.floorBoss && !restart;
            if (meeting) yield return MeetLawgiver(room.BossSpawn ?? (room.Center + Vector2.up * 3f));
            if (step == 0 && !restart && !SaveSystem.Data.pixieMet) yield return PixieIntro();
            if (d.worldEntry && !restart) yield return WorldIntro(d.world);

            player.frozen = false;
            busy = false;
            if (Hud.I && !intoMemory)
            {
                string extra = extraLaw != Law.None
                    ? Loc.F("hud.doubleLaw", Rules.Info(extraLaw).name, Rules.Info(extraLaw).rule) : "";
                if (d.floorBoss) extra = Loc.F("hud.law", Rules.Info(d.law).name) + " — " + Rules.Info(d.law).rule;
                string label = Loc.T(isBoss ? (d.floorBoss ? "hud.floorBossCard" : "hud.finalBoss")
                                     : d.objective == Objective.Secret ? "hud.secretCard" : "hud.lawCard");
                bool newFloor = inFloor == 0 && floor > 0 && !restart && d.world == World.None;
                if (newFloor) label = Loc.F("hud.floorLabel", floor + 1) + Loc.Dot + FloorName(floor);
                if (d.worldEntry)
                {
                    label = Loc.T("hud.worldCard");
                    extra = Loc.F("hud.law", Rules.Info(d.law).name) + " — " + Rules.Info(d.law).rule;
                }
                Hud.I.ShowCard(d.Title, d.Rule, d.SignColor, label, extra);
            }
            Sfx.Play("card");
            if (!restart)
            {
                if (d.law != Law.None) player.OnLawChanged();
                if (step == 0) Hud.Hint("hero." + cfg.hero.id);
                if (isBoss && !d.floorBoss) Hud.Hint("boss");
                if (inFloor == 0 && floor > 0) Hud.Hint("floor");
                LawHints(d.law);
                if (extraLaw != Law.None) LawHints(extraLaw);
            }

            var thisRoom = room;
            yield return new WaitForSeconds(meeting ? 0.3f : isBoss ? 1.6f : 0.9f);
            if (room == thisRoom) SpawnInitial();
        }

        void SetupObjective()
        {
            switch (def.objective)
            {
                case Objective.ReachDoor:
                    room.OpenDoor();
                    break;
                case Objective.FindKey:
                    if (room.KeyChest.HasValue)
                        TreasureChest.Spawn(room, room.KeyChest.Value, true, () =>
                        {
                            keyTaken = true;
                            Hud.Toast(Loc.T("toast.keyFound"), new Color(1f, 0.85f, 0.4f));
                            Clear(false);
                            OfferUpgrade(true);
                        });
                    else Clear(false);
                    break;
                case Objective.Shop:
                    Clear(false);
                    SetupShop();
                    Hud.Hint("shop");
                    break;
                case Objective.Shrine:
                    Clear(false);
                    SetupShrine();
                    Hud.Hint("shrine");
                    break;
                case Objective.Fountain:
                    Clear(false);
                    SetupFountain();
                    break;
                case Objective.Secret:
                    Clear(false);
                    SetupSecret();
                    break;
                case Objective.Treasure:
                    Clear(false);
                    SetupTreasure();
                    break;
                case Objective.Gamble:
                    Clear(false);
                    SetupGamble();
                    break;
                case Objective.Buttons:
                    foreach (var p in room.Buttons)
                    {
                        var sr = room.Prop("button_red_up", p - new Vector2(0f, 0.5f), -60);
                        buttons.Add(new Button { pos = p, sr = sr });
                    }
                    if (buttons.Count == 0) Clear(RollChest());
                    break;
            }
        }

        void SpawnInitial()
        {
            spawned = true;
            if (def.objective == Objective.Boss)
            {
                Vector2 p = room.BossSpawn ?? (room.Center + Vector2.up * 3f);
                if (def.floorBoss) FloorBoss.Spawn(def.bossKind, p, room.Content, floor);
                else Boss.Spawn(p, room.Content, passedLaws).final = LastFloor;
                if (Hud.I) Hud.I.ShowBossBar(true);
                return;
            }
            if (def.Special) return;
            if (def.objective == Objective.Ambush) { ambushArmed = true; return; }
            if (def.objective == Objective.Challenge) wave = 1;
            int runners = Scaled(def.runners), shooters = Scaled(def.shooters), kamikazes = Scaled(def.kamikazes);
            for (int i = 0; i < runners; i++) SpawnOne(EnemyKind.Runner, def.runnerSkin, i);
            for (int i = 0; i < shooters; i++) SpawnOne(EnemyKind.Shooter, def.shooterSkin, runners + i);
            for (int i = 0; i < kamikazes; i++) SpawnOne(EnemyKind.Kamikaze, def.kamikazeSkin, runners + shooters + i);
            if (RetroRoom && def.law == Law.Retro && def.objective == Objective.KillAll) SpawnSlimeKing();
            MaybeSpawnEscapee();
            MaybeSpawnChampion();
        }

        /// <summary>From the second floor on, a champion sometimes leads the monsters of a fight: a big demon that rams,
        /// an ogre with a shield or a rotting giant.</summary>
        void MaybeSpawnChampion()
        {
            if (riftHere || floor < 1 || def.world != World.None || (def.objective != Objective.KillAll && def.objective != Objective.Challenge)) return;
            if (RunRandom.Value > 0.2f + 0.05f * floor) return;
            var points = room.EnemySpawns;
            Vector2 p = points[0];
            foreach (var c in points) if (Vector2.Distance(c, player.Feet) > Vector2.Distance(p, player.Feet)) p = c;
            int pick = RunRandom.Range(0, 3);
            var kind = pick == 0 ? EnemyKind.Charger : pick == 1 ? EnemyKind.Guard : EnemyKind.Runner;
            string skin = pick == 0 ? "big_demon" : pick == 1 ? "ogre" : "big_zombie";
            var e = Enemy.Spawn(kind, skin, p, room.Content, Rules.Stealth);
            e.MakeChampion(4f + floor);
        }

        bool RetroRoom => room && room.Theme == Theme.Retro;

        /// <summary>The Retro law's skins: the SGQ Game Boy monsters, mixed in every room.</summary>
        string SkinFor(EnemyKind kind, string skin)
        {
            if (!RetroRoom) return skin;
            var baseKind = Enemy.BaseKind(kind);
            var pool = baseKind == EnemyKind.Runner ? GameAssets.RetroRunners
                     : baseKind == EnemyKind.Shooter ? GameAssets.RetroShooters : GameAssets.RetroKamikazes;
            return pool[RunRandom.Range(0, pool.Length)];
        }

        /// <summary>A Retro room has a big slime: one hit splits it in two, and each of them in two again.</summary>
        void SpawnSlimeKing()
        {
            if (!GameAssets.I.HasAnim("sgq_medium_slime_idle_anim")) return;
            var points = room.EnemySpawns;
            Vector2 p = room.NearestFree(room.Center + Vector2.up * 2f);
            foreach (var c in points)
                if (Vector2.Distance(c, player.Feet) > Vector2.Distance(p, player.Feet)) p = c;
            var big = Enemy.Spawn(EnemyKind.Runner, "sgq_medium_slime", p, room.Content, Rules.Stealth);
            big.splits = 2;
            big.speed *= 0.8f;
            Hud.Toast(Loc.T("toast.slimes"), new Color(0.75f, 1f, 0.7f));
        }

        static int Scaled(int n) => n <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(n * Difficulty.CountMul));

        void SpawnOne(EnemyKind kind, string skin, int n)
        {
            var points = room.EnemySpawns;
            Vector2 p;
            if (n >= 0 && n < points.Count) p = points[n];
            else
            {
                // pick a spawn point away from the hero
                p = points[RunRandom.Range(0, points.Count)];
                for (int t = 0; t < 6; t++)
                {
                    var c = points[RunRandom.Range(0, points.Count)];
                    if (Vector2.Distance(c, player.Feet) > 5f) { p = c; break; }
                }
            }
            if (n >= points.Count) p += Random.insideUnitCircle * 0.4f;
            bool elite = RunRandom.Value < eliteChance;
            kind = Mutate(kind);
            Enemy.Spawn(kind, SkinFor(kind, KindSkin(kind, skin)), p, room.Content, Rules.Stealth, elite);
            if (elite) Hud.Hint("elite");
        }

        void SpawnRandom()
        {
            var kinds = new List<EnemyKind>();
            if (def.runners > 0) { kinds.Add(EnemyKind.Runner); kinds.Add(EnemyKind.Runner); }
            if (def.shooters > 0) kinds.Add(EnemyKind.Shooter);
            if (def.kamikazes > 0) kinds.Add(EnemyKind.Kamikaze);
            if (kinds.Count == 0) kinds.Add(EnemyKind.Runner);
            var k = kinds[RunRandom.Range(0, kinds.Count)];
            string skin = k == EnemyKind.Runner ? def.runnerSkin : k == EnemyKind.Shooter ? def.shooterSkin : def.kamikazeSkin;
            SpawnOne(k, skin, -1);
        }

        // ---------------- objectives ----------------

        void Tick(float dt)
        {
            runTime += dt;
            if (busy || room == null || player == null) return;

            int door = room.DoorAt(player.Feet);
            if (door >= 0 && door < nextChoices.Length) { GoThrough(door); return; }
            if (secretHere && room.SecretAt(player.Feet)) { GoSecret(); return; }
            if (riftHere && room.RiftAt(player.Feet)) { GoRift(); return; }

            string text = "";
            switch (def.objective)
            {
                case Objective.KillAll:
                    if (spawned && !cleared && Enemy.All.Count == 0) Clear(RollChest());
                    text = cleared ? Loc.T("obj.chooseDoor") : spawned ? Loc.F("obj.kill", Enemy.All.Count) : "";
                    break;

                case Objective.Survive:
                    if (spawned && !cleared)
                    {
                        objTimer -= dt;
                        spawnTimer -= dt;
                        if (spawnTimer <= 0f && Enemy.All.Count < 7) { SpawnRandom(); spawnTimer = 2.4f; }
                        if (objTimer <= 0f)
                        {
                            Enemy.VanishAll();
                            Hud.Toast(Loc.T("toast.garden"), new Color(0.6f, 1f, 0.6f));
                            Clear(RollChest());
                        }
                    }
                    text = cleared ? Loc.T("obj.chooseDoor") : Loc.F("obj.survive", Mathf.CeilToInt(Mathf.Max(0f, objTimer)));
                    break;

                case Objective.ReachDoor:
                    LavaTick(dt);
                    text = Loc.T("obj.run");
                    break;

                case Objective.FindKey:
                    text = Loc.T(keyTaken ? "obj.keyHave" : "obj.key");
                    break;

                case Objective.Buttons:
                    ButtonsTick(dt);
                    int pressed = buttons.FindAll(b => b.pressed).Count;
                    text = cleared ? Loc.T("obj.chooseDoor") : Loc.F("obj.buttons", pressed, buttons.Count);
                    break;

                case Objective.Boss:
                    if (Boss.I) lastBossPos = Boss.I.transform.position;
                    if (spawned && !cleared && Bosses.Defeated)
                    {
                        AddScore(def.floorBoss ? 600 : 1000);
                        if (LastFloor && !def.floorBoss)
                        {
                            cleared = true;
                            StartCoroutine(Victory());
                        }
                        else
                        {
                            Hud.Toast(def.floorBoss ? Loc.F("toast.bossDefeated", def.Title) : Loc.T("toast.bossDown"), new Color(1f, 0.85f, 0.4f));
                            Clear(true, true);
                        }
                    }
                    break;

                case Objective.Shop:
                    text = Loc.F("obj.shop", coins);
                    break;
                case Objective.Shrine:
                    text = Loc.T("obj.shrine");
                    break;
                case Objective.Fountain:
                    text = Loc.T("obj.fountain");
                    break;
                case Objective.Secret:
                    text = Loc.T("obj.secret");
                    break;
                case Objective.Challenge:
                case Objective.Ambush:
                case Objective.Treasure:
                case Objective.Gamble:
                    text = VariantTick(dt);
                    break;
            }

            ChaosTick(dt, ref text);

            // what waits behind the closest open door
            if (room.DoorOpen && nextChoices.Length > 0)
            {
                int near = room.NearestDoor(player.Feet, out float dist);
                if (near >= 0 && near < nextChoices.Length && dist < 4f)
                {
                    var n = nextChoices[near];
                    text = n.objective == Objective.Boss ? Loc.T("obj.behindBoss") : Loc.F("obj.behind", n.Title, n.Rule);
                }
            }
            if (Hud.I) Hud.I.SetObjective(text);
        }

        void ChaosTick(float dt, ref string text)
        {
            if (cfg.mode != GameMode.Chaos || !spawned || cleared) return;
            Law[] pool = def.objective == Objective.KillAll ? ChaosKill
                       : def.objective == Objective.Survive ? ChaosSurvive
                       : def.objective == Objective.ReachDoor ? ChaosRun : null;
            if (pool == null) return;
            chaosTimer -= dt;
            if (chaosTimer <= 0f)
            {
                chaosTimer = ChaosInterval;
                Law next = Rules.Current;
                for (int t = 0; t < 10 && next == Rules.Current; t++) next = pool[Random.Range(0, pool.Length)];
                SetLaw(next, true);
            }
            if (text.Length > 0) text += Loc.Dot + Loc.F("obj.chaos", Mathf.CeilToInt(chaosTimer));
        }

        void Clear(bool chest, bool relicChest = false)
        {
            if (cleared) return;
            cleared = true;
            if (!def.Special)
            {
                if (def.Combat) Juice.Slow(0.3f, 0.45f);   // the last enemy falls in slow motion
                AddScore(100 + 25 * step);
                Pickup.Coins(player.Position + Vector2.up * 0.5f, 1 + floor / 2);
                player.OnRoomClear(coins);
                player.ChargeActive();
                Pickup.MagnetAll();
                if (player.Hero.cleanRoomHeal && !player.HurtThisRoom && player.halves < player.maxHalves && def.law != Law.None)
                {
                    player.Heal(1);
                    Fx.Text(player.Position + Vector2.up * 1.1f, Loc.T("toast.regen"), new Color(0.6f, 1f, 0.5f), 0.9f, 1.2f);
                    Sfx.Play("heal", 0.7f);
                }
            }
            if (nextChoices.Length > 0)
            {
                room.OpenDoor();
                Sfx.Play("door");
                Hud.Toast(Loc.T(nextChoices.Length > 1 ? "toast.doors" : "toast.door"), new Color(1f, 0.95f, 0.8f));
                if (nextChoices.Length > 1) Hud.Hint("doors");
            }
            if (chest && !cfg.Has(Curse.Ascetic))
            {
                TreasureChest.Spawn(room, room.NearestFree(room.Center), false, () => OfferUpgrade(relicChest));
                Hud.Hint("chest");
            }
            if (secretHere) Hud.Hint("secret");
        }

        /// <summary>A cleared room leaves a chest only sometimes (relics and the map help).</summary>
        bool RollChest()
        {
            float chance = 0.4f;
            if (player.Has(RelicId.Map)) chance += 0.2f;
            if (player.HasCombo(ComboId.TreasureHunter)) chance += 0.2f;
            return RunRandom.Value < chance;
        }

        /// <summary>Shop price with the hero's discounts.</summary>
        int Price(int coinsBase)
        {
            float m = player.HasCombo(ComboId.Bargain) ? 0.6f : player.Has(RelicId.Coupon) ? 0.75f : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(coinsBase * m));
        }

        void OfferUpgrade() => OfferUpgrade(false);

        void OfferUpgrade(bool guaranteeRelic)
        {
            if (cfg.Has(Curse.Ascetic)) return;
            int cards = 2 + (player.luck ? 1 : 0) + (player.Has(RelicId.Clover) ? 1 : 0);
            var options = Upgrades.Pick(cards, player, 0.35f, guaranteeRelic || player.HasCombo(ComboId.TreasureHunter));
            if (options.Length == 0 || Hud.I == null) return;
            State = GameState.Choosing;
            Time.timeScale = 0f;
            player.frozen = true;
            foreach (var o in options) if (!o.Known) { Hud.Hint("flask"); break; }
            Hud.I.ShowUpgrades(options, u =>
            {
                u.apply(player);
                Sfx.Play("pickup");
                bool wasUnknown = !u.Known;
                Upgrades.Identify(u);
                Hud.Toast(wasUnknown ? Loc.F("toast.identified", u.title) : u.title + "!", u.IsRelic ? u.relic.color : new Color(1f, 0.9f, 0.5f));
                Time.timeScale = 1f;
                player.frozen = false;
                State = GameState.Playing;
                if (u.IsRelic)
                {
                    Hud.Hint("relic");
                    if (u.relic.id == RelicId.Dash) Hud.Hint("dash");
                }
            });
        }

        // ---------------- score & end ----------------

        public void OnEnemyKilled(bool boss)
        {
            kills++;
            AddScore(boss ? 500 : 10);
            if (player) player.OnKill();
        }

        public void AddCoins(int n)
        {
            coins = Mathf.Max(0, coins + n);
            if (coins >= 100) Achievements.Unlock("rich");
            if (Hud.I) Hud.I.SetCoins(coins);
        }

        void AddScore(int value)
        {
            score += Mathf.RoundToInt(value * cfg.ScoreMul);
            if (Hud.I) Hud.I.SetScore(score, cfg.ScoreMul);
        }

        public void OnPlayerDied()
        {
            if (State != GameState.Playing) return;
            if (Rules.OneHit) StartCoroutine(RestartRoom());
            else StartCoroutine(GameOver());
        }

        IEnumerator RestartRoom()
        {
            busy = true;
            Hud.Toast(Loc.T("toast.retro"), new Color(0.75f, 1f, 0.7f));
            yield return new WaitForSecondsRealtime(1.2f);
            StartCoroutine(EnterRoom(def, true));
        }

        IEnumerator GameOver()
        {
            State = GameState.Dead;
            busy = true;
            yield return new WaitForSecondsRealtime(1.2f);
            Sfx.Play("lose");
            if (MusicPlayer.I) MusicPlayer.I.Play("defeat");
            FinishRun(false);
        }

        IEnumerator Victory()
        {
            State = GameState.Won;
            busy = true;
            AddScore(1500);
            Vector2 at = lastBossPos ?? room.BossSpawn ?? room.Center;
            Rules.Apply(Law.None);
            if (StyleController.I) StyleController.I.Apply(Law.None);
            yield return new WaitForSecondsRealtime(2f);

            ending = EndingFor();
            yield return EndingScene(ending, at);
            SaveSystem.Data.finales |= 1 << ending;
            FinishRun(true);   // the ending's music keeps playing under the results
            yield return LiftEndingBlack();
        }

        Vector2? lastBossPos;

        int ending = -1;
        public const int EndingCount = 3;

        void FinishRun(bool win)
        {
            if (finished) return;
            finished = true;
            var d = SaveSystem.Data;

            var heroesBefore = new List<HeroDef>();
            foreach (var h in Heroes.All) if (SaveSystem.HeroUnlocked(h)) heroesBefore.Add(h);
            bool endlessBefore = SaveSystem.ModeUnlocked(GameMode.Endless);
            bool chaosBefore = SaveSystem.ModeUnlocked(GameMode.Chaos);

            d.totalKills += kills;
            if (win) d.wins++;
            d.bestDepth = Mathf.Max(d.bestDepth, step + 1);
            d.bestFloor = Mathf.Max(d.bestFloor, floor + 1);
            if (win)
            {
                int n = 0;
                for (int i = 0; i < Curses.Count; i++) if (cfg.Has((Curse)i)) n++;
                d.maxCursesWin = Mathf.Max(d.maxCursesWin, n);
            }

            bool record = false;
            switch (cfg.mode)
            {
                case GameMode.Standard:
                    if (score > d.bestStandard) { d.bestStandard = score; record = true; }
                    break;
                case GameMode.Chaos:
                    if (score > d.bestChaos) { d.bestChaos = score; record = true; }
                    break;
                case GameMode.Endless:
                    if (score > d.bestEndless) { d.bestEndless = score; record = true; }
                    d.bestEndlessDepth = Mathf.Max(d.bestEndlessDepth, step + 1);
                    break;
                case GameMode.Daily:
                    if (d.dailyDate != Daily.Date) { d.dailyDate = Daily.Date; d.dailyBest = 0; }
                    if (score > d.dailyBest) { d.dailyBest = score; record = true; }
                    break;
            }

            var result = new RunResult { win = win, mode = cfg.mode, score = score, record = record && score > 0 };
            foreach (var h in Heroes.All)
                if (!heroesBefore.Contains(h) && SaveSystem.HeroUnlocked(h)) result.unlocks.Add(Loc.F("unlock.hero", h.name));
            if (!chaosBefore && SaveSystem.ModeUnlocked(GameMode.Chaos)) result.unlocks.Add(Loc.T("unlock.chaos"));
            if (!endlessBefore && SaveSystem.ModeUnlocked(GameMode.Endless)) result.unlocks.Add(Loc.T("unlock.endless"));

            // meta progress: achievements, new relics in the pool, the history of runs
            if (win) Achievements.Unlock("win");
            Achievements.CheckSave();
            int relicsBefore = Achievements.UnlockedRelics;
            d.relicPoints += 3 + 2 * floor + (win ? 6 : 0);
            int gained = Achievements.UnlockedRelics - relicsBefore;
            if (gained > 0) result.unlocks.Add(Loc.F("unlock.relics", gained));
            d.history.Insert(0, new RunRecord
            {
                date = System.DateTime.Now.ToString("dd.MM HH:mm"), mode = (int)cfg.mode, hero = cfg.hero.id,
                floor = floor + 1, score = score, win = win, kills = kills, seconds = Mathf.FloorToInt(runTime),
                relics = player ? player.RelicList.Count : 0, combos = player ? player.ComboList.Count : 0
            });
            while (d.history.Count > 12) d.history.RemoveAt(d.history.Count - 1);
            SaveSystem.Save();

            int t = Mathf.FloorToInt(runTime);
            string depth = Endless ? Loc.F("end.floorEndless", floor + 1) : Loc.F("end.floors", Mathf.Min(floor + 1, floors), floors);
            string curses = CurseList();
            if (win && ending >= 0)
            {
                int seen = 0;
                for (int i = 0; i < EndingCount; i++) if ((d.finales & (1 << i)) != 0) seen++;
                result.ending = Loc.F("end.ending", Loc.T("ending." + ending + ".name"), seen, EndingCount);
            }
            result.stats = $"{Modes.Info(cfg.mode).name}{Loc.Dot}{cfg.hero.name}\n" +
                           Loc.F("end.line", depth, kills, $"{t / 60}:{t % 60:00}") + "\n" +
                           (curses.Length > 0 ? Loc.F("end.curses", curses, cfg.ScoreMul.ToString("0.0")) + "\n" : "") +
                           Loc.F("end.seed", cfg.seed);

            if (MenuUI.I) MenuUI.I.ShowEnd(result, win && ending >= 0 ? 1.2f : 0.18f);
        }
    }
}
