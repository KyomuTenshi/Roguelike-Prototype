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
    public class RoomManager : MonoBehaviour
    {
        public static RoomManager I { get; private set; }

        [Header("Rooms")]
        public List<RoomDef> rooms = new List<RoomDef>();
        public RoomDef bossRoom;
        [Tooltip("Floors in a run. Each ends with a boss; the last one with the Lawgiver.")]
        [Range(1, 5)] public int floors = 3;
        [Tooltip("Rooms on a floor before its boss (Прихожая counts on the first floor)")]
        [Range(2, 8)] public int roomsPerFloor = 4;

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
            secretFloor = Endless ? RunRandom.Range(0, 2) : RunRandom.Range(0, floors);
            secretFrom = RunRandom.Range(1, Mathf.Max(2, roomsPerFloor - 1));
            secretDone = !(GameAssets.I && GameAssets.I.HasModern);
            memoryThisRun = false;
            completedMemories = false;
            ending = -1;
            savedChoices = null;

            if (player) Destroy(player.gameObject);
            player = PlayerController.Create(transform, cfg.hero);
            if (cfg.Has(Curse.GlassHeart)) player.maxHalves = player.halves = 2;
            if (CameraFollow.I) CameraFollow.I.target = player.transform;
            if (Hud.I) { Hud.I.SetScore(0, cfg.ScoreMul); Hud.I.SetCoins(0); }

            StartCoroutine(EnterRoom(start, false));
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
            if (current.objective == Objective.Boss)
                return LastFloor ? new RoomDef[0] : Draw(2, current);   // down to the next floor
            int next = inFloor + 1;
            if (next >= roomsPerFloor) return new[] { BossFor(floor) };
            var picks = Draw(2, current);
            if (picks.Length == 2)
            {
                // one shop per floor, sometimes a shrine or a fountain
                if (next == 2) picks[RunRandom.Range(0, 2)] = Special(Objective.Shop);
                else if (next == roomsPerFloor - 1 && RunRandom.Value < 0.6f)
                    picks[RunRandom.Range(0, 2)] = Special(RunRandom.Value < 0.5f ? Objective.Shrine : Objective.Fountain);
            }
            return picks;
        }

        RoomDef BossFor(int f)
        {
            bool lawgiver = Endless ? f % 3 == 2 : f >= floors - 1;
            if (lawgiver) return bossRoom;
            return new RoomDef
            {
                id = "floorboss", objective = Objective.Boss, floorBoss = true,
                bossKind = f % 2 == 0 ? FloorBossKind.Ogre : FloorBossKind.Zombie,
                law = RunRandom.Pick(FloorBossLaws), layout = 6, theme = Theme.Dungeon,
                inPool = false, usableInBoss = false
            };
        }

        static RoomDef SecretRoom(int memory) => new RoomDef
        {
            id = "secret", objective = Objective.Secret, law = Law.None, layout = 10, theme = Theme.Modern,
            inPool = false, usableInBoss = false, memory = Mathf.Clamp(memory, 0, MemoryCount - 1)
        };

        static RoomDef Special(Objective o) => new RoomDef
        {
            id = o.ToString().ToLowerInvariant(), objective = o, law = Law.None,
            layout = o == Objective.Shop ? 7 : o == Objective.Shrine ? 8 : 9,
            inPool = false, usableInBoss = false
        };

        Theme FloorTheme(RoomDef d) => d.theme == Theme.Modern ? Theme.Modern : floor % 3 == 1 ? Theme.Classic : d.theme;

        RoomDef[] Draw(int n, RoomDef current)
        {
            if (unused.Count < n + 1)
            {
                var refill = rooms.FindAll(r => r.inPool && r.objective != Objective.Boss && r.law != Law.None && !unused.Contains(r));
                RunRandom.Shuffle(refill);
                unused.AddRange(refill);
            }
            var pick = new List<RoomDef>();
            foreach (var r in unused)
            {
                if (r == current || pick.Contains(r)) continue;
                pick.Add(r);
                if (pick.Count == n) break;
            }
            return pick.ToArray();
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

        /// <summary>Alt-Tab or a click outside the window pauses the run.</summary>
        void OnApplicationFocus(bool focus)
        {
            if (!focus && State == GameState.Playing && !busy) Pause();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && State == GameState.Playing && player != null) KonamiTick(kb);
            var pad = Gamepad.current;
            bool esc = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) ||
                       (pad != null && pad.startButton.wasPressedThisFrame);
            if (State == GameState.Playing)
            {
                if (esc && !busy) Pause();
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
            return boss ? Loc.F("hud.floorBoss", floor + 1) : Loc.F("hud.floorRoom", floor + 1, inFloor + 1, roomsPerFloor);
        }

        // ---------------- rooms ----------------

        void ClearRoom()
        {
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
            d.law == Law.Upside || d.law == Law.Neon || d.law == Law.Lava;

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
            objTimer = d.time;
            spawnTimer = 1.5f;
            lavaRow = 0;
            lavaTimer = 2.8f;
            bubbleTimer = 3.5f;
            chaosTimer = ChaosInterval;
            bool isBoss = d.objective == Objective.Boss;

            if (!restart)
            {
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
            Difficulty.EnemyHpBonus = baseHpBonus + floor;
            Difficulty.CountMul = 1f + 0.2f * floor;
            Difficulty.BossHpMul = 1f + 0.5f * loop;
            eliteChance = d.law == Law.None || d.Special ? 0f : 0.06f + 0.06f * floor;

            ApplyLaws(d.law);
            if (extraLaw != Law.None) SaveSystem.Discover(extraLaw);
            if (isBoss && !d.floorBoss && !bossSeen) { bossSeen = true; SaveSystem.Data.bossReached++; SaveSystem.Discover(Law.Boss); }

            // combat rooms are generated anew every run (the same for a given seed)
            bool generated = (d.objective == Objective.KillAll || d.objective == Objective.Survive) && d.law != Law.None;
            var layout = generated ? LayoutGen.Generate(new System.Random(cfg.seed ^ (step * 7919 + 104729))) : Layouts.Get(d.layout);
            string modernStyle = d.objective == Objective.Secret ? MemoryStyles[Mathf.Clamp(d.memory, 0, MemoryCount - 1)] : "";
            room = Room.Build(layout, FloorTheme(d), d.law, transform, Mathf.Max(1, nextChoices.Length), modernStyle);
            for (int i = 0; i < nextChoices.Length; i++)
            {
                var n = nextChoices[i];
                room.SetDoorLabel(i, n.objective == Objective.Boss && !n.floorBoss ? Loc.T("hud.boss") : n.Title, n.SignColor);
            }
            Fx.Root = room.Content;
            secretHits = 0;
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

            player.frozen = false;
            busy = false;
            if (Hud.I && !intoMemory)
            {
                string extra = extraLaw != Law.None
                    ? Loc.F("hud.doubleLaw", Rules.Info(extraLaw).name, Rules.Info(extraLaw).rule) : "";
                if (d.floorBoss) extra = Loc.F("hud.law", Rules.Info(d.law).name) + " — " + Rules.Info(d.law).rule;
                string label = Loc.T(isBoss ? (d.floorBoss ? "hud.floorBossCard" : "hud.finalBoss")
                                     : d.objective == Objective.Secret ? "hud.secretCard" : "hud.lawCard");
                bool newFloor = inFloor == 0 && floor > 0 && !restart;
                if (newFloor) label = Loc.F("hud.floorLabel", floor + 1) + Loc.Dot + FloorName(floor);
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
                case Objective.Buttons:
                    foreach (var p in room.Buttons)
                    {
                        var sr = room.Prop("button_red_up", p - new Vector2(0f, 0.5f), -60);
                        buttons.Add(new Button { pos = p, sr = sr });
                    }
                    if (buttons.Count == 0) Clear(true);
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
            int runners = Scaled(def.runners), shooters = Scaled(def.shooters), kamikazes = Scaled(def.kamikazes);
            for (int i = 0; i < runners; i++) SpawnOne(EnemyKind.Runner, def.runnerSkin, i);
            for (int i = 0; i < shooters; i++) SpawnOne(EnemyKind.Shooter, def.shooterSkin, runners + i);
            for (int i = 0; i < kamikazes; i++) SpawnOne(EnemyKind.Kamikaze, def.kamikazeSkin, runners + shooters + i);
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
                p = points[Random.Range(0, points.Count)];
                for (int t = 0; t < 6; t++)
                {
                    var c = points[Random.Range(0, points.Count)];
                    if (Vector2.Distance(c, player.Feet) > 5f) { p = c; break; }
                }
            }
            if (n >= points.Count) p += Random.insideUnitCircle * 0.4f;
            bool elite = Random.value < eliteChance;
            Enemy.Spawn(kind, skin, p, room.Content, Rules.Stealth, elite);
            if (elite) Hud.Hint("elite");
        }

        void SpawnRandom()
        {
            var kinds = new List<EnemyKind>();
            if (def.runners > 0) { kinds.Add(EnemyKind.Runner); kinds.Add(EnemyKind.Runner); }
            if (def.shooters > 0) kinds.Add(EnemyKind.Shooter);
            if (def.kamikazes > 0) kinds.Add(EnemyKind.Kamikaze);
            if (kinds.Count == 0) kinds.Add(EnemyKind.Runner);
            var k = kinds[Random.Range(0, kinds.Count)];
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

            string text = "";
            switch (def.objective)
            {
                case Objective.KillAll:
                    if (spawned && !cleared && Enemy.All.Count == 0) Clear(true);
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
                            Clear(true);
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
                        if (LastFloor)
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
                AddScore(100 + 25 * step);
                Pickup.Coins(player.Position + Vector2.up * 0.5f, 1 + floor);
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

        void OfferUpgrade() => OfferUpgrade(false);

        void OfferUpgrade(bool guaranteeRelic)
        {
            if (cfg.Has(Curse.Ascetic)) return;
            var options = Upgrades.Pick(player.luck ? 3 : 2, player, 0.4f, guaranteeRelic);
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

        // ---------------- special rooms ----------------

        static string FloorName(int f) => Loc.T("floor." + (f % 3 + 1));

        Sprite Spr(string name) => GameAssets.I ? GameAssets.I.Any(name) : null;

        void SetupShop()
        {
            var spots = room.ItemSpots;
            if (spots.Count == 0) return;
            var keeper = new GameObject("Merchant").AddComponent<SpriteRenderer>();
            keeper.transform.SetParent(room.Content, false);
            keeper.transform.position = spots[spots.Count / 2] + new Vector2(0f, 2.2f);
            keeper.sprite = Spr("c:npc_merchant");
            keeper.spriteSortPoint = SpriteSortPoint.Pivot;
            Gfx.Lit(keeper);

            for (int i = 0; i < spots.Count; i++)
            {
                Vector2 at = spots[i];
                switch (i % 3)
                {
                    case 0:
                        var r = Upgrades.RandomRelic(player);
                        if (r == null) goto case 2;
                        ShopItem.Create(room, at, Spr(r.sprite), ShopItem.Cost.Coins, 18 + 4 * floor, p =>
                        {
                            p.AddRelic(r.id);
                            Hud.Toast(r.Name + "!", r.color);
                            Hud.Hint("relic");
                            if (r.id == RelicId.Dash) Hud.Hint("dash");
                        });
                        break;
                    case 1:
                        var u = Upgrades.RandomFlask(player);
                        if (u == null) goto case 2;
                        ShopItem.Create(room, at, Spr(u.SpriteName), ShopItem.Cost.Coins, 8 + 2 * floor, p =>
                        {
                            bool unknown = !u.Known;
                            u.apply(p);
                            Upgrades.Identify(u);
                            Hud.Toast(unknown ? Loc.F("toast.identified", u.title) : u.title + "!", new Color(1f, 0.9f, 0.5f));
                        });
                        break;
                    case 2:
                        ShopItem.Create(room, at, Spr("ui_heart_full"), ShopItem.Cost.Coins, 6, p =>
                        {
                            p.Heal(2);
                            Hud.Toast(Loc.T("shop.healItem") + " +1", new Color(1f, 0.45f, 0.5f));
                        });
                        break;
                }
            }
        }

        void SetupShrine()
        {
            var spots = room.ItemSpots;
            var r = Upgrades.RandomRelic(player);
            if (spots.Count == 0 || r == null) return;
            room.Prop("skull", spots[0] + new Vector2(-1.2f, -0.2f), -60);
            room.Prop("skull", spots[0] + new Vector2(1.2f, -0.2f), -60);
            LightUtil.Point(room.Content, spots[0] + new Vector2(0f, 0.6f), new Color(1f, 0.3f, 0.35f), 3f, 1f);
            ShopItem.Create(room, spots[0], Spr(r.sprite), ShopItem.Cost.Heart, 0, p =>
            {
                p.AddRelic(r.id);
                Hud.Toast(r.Name + "!", r.color);
                Hud.Hint("relic");
                if (r.id == RelicId.Dash) Hud.Hint("dash");
            });
        }

        void SetupFountain()
        {
            var spots = room.ItemSpots;
            if (spots.Count == 0) return;
            room.Fountain(6, "blue");
            room.Fountain(room.W - 7, "blue");
            ShopItem.Create(room, spots[0] - new Vector2(0f, 0.3f), Spr("wall_fountain_basin_blue_anim_f0"), ShopItem.Cost.Free, 0, p =>
            {
                p.Heal(99);
                Hud.Toast(Loc.T("toast.healed"), new Color(0.6f, 0.9f, 1f));
                Sfx.Play("heal");
            }, false);
        }

        static void LawHints(Law law)
        {
            string key = "law." + Rules.Key(law);
            if (Loc.Has("hint." + key)) Hud.Hint(key);
        }

        void ButtonsTick(float dt)
        {
            if (!cleared && spawned)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0f && Enemy.All.Count < 6) { SpawnRandom(); spawnTimer = 4.5f; }
            }
            foreach (var b in buttons)
            {
                if (b.pressed || Vector2.Distance(player.Feet, b.pos) > 0.7f) continue;
                b.pressed = true;
                if (b.sr && GameAssets.I) b.sr.sprite = GameAssets.I.D("button_red_down");
                Sfx.Play("button");
                if (buttons.TrueForAll(x => x.pressed)) Clear(true);
                else
                {
                    Law next = Rules.Current;
                    for (int t = 0; t < 10 && next == Rules.Current; t++)
                        next = ConsoleLaws[Random.Range(0, ConsoleLaws.Length)];
                    SetLaw(next, true);
                }
            }
        }

        void LavaTick(float dt)
        {
            // the floor turns to lava row by row from the bottom, plus random bubbles ahead
            lavaTimer -= dt;
            if (lavaTimer <= 0f && lavaRow < room.H - 2)
            {
                lavaTimer = 1.35f;
                for (int x = 0; x < room.W; x++) QueueLava(new Vector2Int(x, lavaRow), 1.0f);
                lavaRow++;
            }
            bubbleTimer -= dt;
            if (bubbleTimer <= 0f)
            {
                bubbleTimer = 1.1f;
                for (int i = 0; i < 2; i++)
                {
                    int y = Mathf.Clamp(lavaRow + Random.Range(2, 7), 0, room.H - 3);
                    QueueLava(new Vector2Int(Random.Range(0, room.W), y), 1.2f);
                }
            }
            for (int i = pendingLava.Count - 1; i >= 0; i--)
            {
                var kv = pendingLava[i];
                float t = kv.Value - dt;
                if (t <= 0f)
                {
                    room.MakeLava(kv.Key);
                    pendingLava.RemoveAt(i);
                }
                else pendingLava[i] = new KeyValuePair<Vector2Int, float>(kv.Key, t);
            }

            lavaHurtTimer -= dt;
            if (room.IsLava(player.Feet + new Vector2(0f, 0.15f)) && lavaHurtTimer <= 0f)
            {
                lavaHurtTimer = 0.5f;
                if (player.Hit(1, Vector2.up))
                {
                    Sfx.Play("lava");
                    Fx.Burst(player.Feet, new Color(1f, 0.5f, 0.1f), 10, 3f, 0.4f, 1.5f);
                }
            }
        }

        void QueueLava(Vector2Int c, float delay)
        {
            if (!room.IsFree(c.x, c.y) || room.IsLavaCell(c)) return;
            foreach (var kv in pendingLava) if (kv.Key == c) return;
            room.Warn(c);
            pendingLava.Add(new KeyValuePair<Vector2Int, float>(c, delay));
        }

        /// <summary>Switch the law inside the current room (boss, console buttons, Chaos mode).</summary>
        public void SetLaw(Law law, bool announce)
        {
            ApplyLaws(law);
            var info = Rules.Info(law);
            if (StyleController.I) StyleController.I.Apply(law);
            if (MusicPlayer.I && def != null)
            {
                // Neon always gets a track with a measured beat, even in a boss fight
                if (def.objective != Objective.Boss) MusicPlayer.I.Play(LawMusic(law));
                else MusicPlayer.I.Play(law == Law.Neon ? NeonMusic() : BossMusic());
            }
            if (announce)
            {
                if (Hud.I) Hud.I.ShowLawChange(info.name, info.rule, info.color);
                Sfx.Play("law");
                if (CameraFollow.I) CameraFollow.I.Shake(0.2f);
                LawHints(law);
            }
            if (player) player.OnLawChanged();
        }

        // ---------------- the secret room ----------------

        /// <summary>The crack appears in a combat room from a random point of a random floor on (until found).</summary>
        bool SecretCanBeHere(RoomDef d)
        {
            if (d.Special || d.law == Law.None || d.objective == Objective.Boss || d.objective == Objective.ReachDoor) return false;
            return floor > secretFloor || (floor == secretFloor && inFloor >= secretFrom);
        }

        /// <summary>A shot, an explosion or a sword swing hit a wall here. Three hits open the crack once the room is clear.</summary>
        public void OnWallHit(Vector2 p, float radius = 0f)
        {
            if (!secretHere || room == null || room.SecretOpen || busy || State != GameState.Playing) return;
            Vector2 c = room.SecretPos;
            if (Vector2.Distance(p, c) > 1.4f + radius) return;
            if (Time.time - secretHitTime < 0.2f) return;
            secretHitTime = Time.time;
            Fx.Anim("dust", c + new Vector2(Random.Range(-0.6f, 0.6f), -0.5f), 0.4f);
            if (!cleared) return;   // it sounds hollow, but the fight comes first
            secretHits++;
            Sfx.Play("hit", 0.7f, 0.6f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.08f);
            if (secretHits < 3) return;

            room.OpenSecret();
            Fx.Anim("bigboom", c, 0.6f);
            Fx.Burst(c, new Color(0.6f, 0.55f, 0.5f), 18, 4f, 0.6f, 1.6f);
            Sfx.Play("boom", 0.6f);
            Sfx.Play("door");
            if (CameraFollow.I) CameraFollow.I.Shake(0.25f);
            Hud.Toast(Loc.T("toast.secret"), new Color(0.75f, 0.95f, 0.75f));
        }

        // memories, oldest first; the first one found is always the one about the work
        public const int MemoryCount = 3;
        static readonly int[] MemoryOrder = { 1, 0, 2 };
        static readonly string[] MemoryStyles = { "_mint", "", "_gray" };   // Modern tiles wall + floor set

        public static bool AllMemories => (SaveSystem.Data.memories & ((1 << MemoryCount) - 1)) == (1 << MemoryCount) - 1;

        void GoSecret()
        {
            if (busy) return;
            if (def.usableInBoss && def.law != Law.None && !passedLaws.Contains(def.law)) passedLaws.Add(def.law);
            savedChoices = nextChoices;
            secretDone = true;
            secretHere = false;
            memoryThisRun = true;
            AddScore(250);

            // the next memory not seen yet; once all are known, any of them
            var save = SaveSystem.Data;
            memoryVariant = -1;
            foreach (int m in MemoryOrder) if ((save.memories & (1 << m)) == 0) { memoryVariant = m; break; }
            if (memoryVariant < 0) memoryVariant = Random.Range(0, MemoryCount);
            bool wasAll = AllMemories;
            save.memories |= 1 << memoryVariant;
            save.memoryFound = true;
            completedMemories = !wasAll && AllMemories;
            SaveSystem.Save();

            StartCoroutine(EnterRoom(SecretRoom(memoryVariant), false));
        }

        int memoryVariant;
        bool completedMemories;

        /// <summary>A room from another world: the Maker's flat at one moment of his life.</summary>
        void SetupSecret()
        {
            int v = def.memory;
            switch (v)
            {
                case 0: SetupDream(); break;
                case 2: SetupSilence(); break;
                default: SetupWork(); break;
            }
            if (completedMemories) LastPage();
            Hud.Hint("memory");
        }

        /// <summary>Wall spots (x) away from the doors.</summary>
        List<float> WallSpots()
        {
            int W = room.W;
            var spots = new List<float>();
            foreach (float x in new[] { 2.5f, 6.5f, W / 2f, W - 6.5f, W - 2.5f })
            {
                bool nearDoor = false;
                for (int i = 0; i < room.DoorCount; i++) if (Mathf.Abs(room.DoorPos(i).x - x) < 2.5f) nearDoor = true;
                if (!nearDoor) spots.Add(x);
            }
            return spots;
        }

        /// <summary>Hangs things on the wall; "window" gives light, the rest are notes (lore.prefix+name; prefix null = no notes).</summary>
        void WallThings(string prefix, params string[] things)
        {
            int H = room.H;
            var spots = WallSpots();
            for (int i = 0; i < spots.Count && i < things.Length; i++)
            {
                room.Prop(things[i], new Vector2(spots[i], H + 0.35f), -39);
                if (things[i] == "window") LightUtil.Point(room.Content, new Vector2(spots[i], H - 1f), new Color(1f, 0.9f, 0.7f), 4f, 0.7f);
                else if (prefix != null) Lore(prefix + things[i], new Vector2(spots[i], H - 0.6f), 1.6f);
            }
        }

        SpriteRenderer Person(string name, Sprite[] frames, Vector2 at)
        {
            if (frames == null || frames.Length == 0) return null;
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(room.Content, false);
            sr.transform.position = at;
            sr.sprite = frames[0];
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            Gfx.Lit(sr);
            var anim = sr.gameObject.AddComponent<SpriteAnimator>();
            anim.target = sr;
            anim.Play(frames, 6f);
            room.BlockCell(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y));
            return sr;
        }

        void HealHere()
        {
            if (player.halves >= player.maxHalves) return;
            player.Heal(99);
            Fx.Anim("heal", player.Position, 0.5f);
            Sfx.Play("heal");
        }

        /// <summary>The work: the room as he left it while making the game (the first memory found).</summary>
        void SetupWork()
        {
            int W = room.W, H = room.H;
            WallThings("", "board", "photo", "calendar", "window");
            room.Prop("rug", new Vector2(W / 2f, 3f), -60);
            Furniture("bed", new Vector2(2.3f, 5f), 1, 5, 3, 4);
            Furniture("desk", new Vector2(W - 4f, 7.2f), W - 5, 7, 2, 2);
            Furniture("computer", new Vector2(W - 2.4f, 7.4f), W - 3, 7, 1, 1);
            room.Prop("chair", new Vector2(W - 4f, 6.3f), 0);
            Lore("computer", new Vector2(W - 3.2f, 6.6f), 1.8f);
            Furniture("boxes", new Vector2(W - 2.5f, 1f), W - 4, 1, 2, 1);
            Furniture("tree", new Vector2(0.7f, 0.2f), 0, 0, 1, 1);
            Furniture("plant", new Vector2(W - 0.7f, H - 1.8f), W - 1, H - 2, 1, 1);
            LightUtil.Point(room.Content, new Vector2(W - 3f, 8f), new Color(0.6f, 0.8f, 1f), 3f, 0.8f);   // the monitor glow

            Vector2 at = new Vector2(W / 2f, 4.2f);
            Person("Maker", GameAssets.I ? GameAssets.I.AdamIdle() : null, at);
            Lore("maker", at, 1.8f, HealHere);
        }

        /// <summary>The dream: years earlier, a bright flat, the game is only an idea. Lina is there.</summary>
        void SetupDream(bool notes = true, bool people = true)
        {
            int W = room.W, H = room.H;
            WallThings(notes ? "dream." : null, "calendar", "tv", "window", "photo");
            room.Prop("rug", new Vector2(W / 2f, 3.5f), -60);
            Furniture("bed", new Vector2(2.3f, 5f), 1, 5, 3, 4);
            Furniture("desk", new Vector2(W - 4f, 7.2f), W - 5, 7, 2, 2);
            room.Prop("chair", new Vector2(W - 4f, 6.3f), 0);
            Furniture("palm", new Vector2(0.8f, 0.2f), 0, 0, 1, 1);
            Furniture("plant", new Vector2(W - 0.7f, H - 1.8f), W - 1, H - 2, 1, 1);
            Furniture("tree", new Vector2(W - 0.7f, 0.2f), W - 1, 0, 1, 1);
            Furniture("bookshelf", new Vector2(8f, H - 2f), 7, H - 2, 2, 2);
            LightUtil.Point(room.Content, new Vector2(W / 2f, 4f), new Color(1f, 0.9f, 0.7f), 6f, 0.35f);

            if (!people) return;
            var adam = new Vector2(W / 2f - 0.9f, 4.2f);
            var lina = new Vector2(W / 2f + 0.9f, 4.2f);
            Person("Maker", GameAssets.I ? GameAssets.I.AdamIdle() : null, adam);
            Person("Lina", GameAssets.I ? GameAssets.I.AmeliaIdle() : null, lina);
            if (!notes) return;
            Lore("dream.maker", adam - new Vector2(0.4f, 0f), 1.4f, HealHere);
            Lore("dream.lina", lina + new Vector2(0.4f, 0f), 1.4f);
            Lore("dream.desk", new Vector2(W - 4f, 6.6f), 1.6f);
        }

        /// <summary>The silence: the last memory. He is gone; the computer still runs. Lina came back.</summary>
        void SetupSilence()
        {
            int W = room.W, H = room.H;
            if (StyleController.I) StyleController.I.Apply(Law.Noir, true);   // a grey memory (looks only)
            WallThings("silence.", "calendar", "window", "photo");
            Furniture("bed", new Vector2(2.3f, 5f), 1, 5, 3, 4);
            Lore("silence.bed", new Vector2(4.1f, 6.5f), 1.4f);
            Furniture("desk", new Vector2(W - 4f, 7.2f), W - 5, 7, 2, 2);
            Furniture("computer", new Vector2(W - 2.4f, 7.4f), W - 3, 7, 1, 1);
            room.Prop("chair", new Vector2(W - 4f, 6.3f), 0);
            Lore("silence.computer", new Vector2(W - 3.2f, 6.6f), 1.8f);
            LightUtil.Point(room.Content, new Vector2(W - 3f, 8f), new Color(0.6f, 0.8f, 1f), 3.5f, 1.1f);
            Furniture("boxes", new Vector2(W - 2.5f, 1f), W - 4, 1, 2, 1);
            Furniture("boxes", new Vector2(3f, 1f), 1, 1, 3, 1);
            Furniture("boxes", new Vector2(6.5f, 2.2f), 5, 2, 2, 1);
            Lore("silence.boxes", new Vector2(4.5f, 2.6f), 1.5f);
            Furniture("fridge", new Vector2(W - 0.5f, 4f), W - 1, 4, 1, 2);
            Lore("silence.fridge", new Vector2(W - 1.6f, 4.6f), 1.5f);

            Vector2 at = new Vector2(W / 2f + 1f, 5.2f);
            Person("Lina", GameAssets.I ? GameAssets.I.AmeliaIdle() : null, at);
            Lore("silence.lina", at, 1.8f, HealHere);
        }

        /// <summary>All memories are found: a last page appears in this room.</summary>
        void LastPage()
        {
            Vector2 at = new Vector2(room.W / 2f, 2.2f);
            Fx.Loop("sparkle", room.Content, at + new Vector2(0f, 0.5f), 0.4f, 5);
            LightUtil.Point(room.Content, at, UiKit.Gold, 3f, 1.2f);
            Lore("page", at, 1.5f, () =>
            {
                Hud.Toast(Loc.T("toast.allMemories"), UiKit.Gold);
                Fx.Anim("firework", at + Vector2.up, 0.6f);
                Sfx.Play("win", 0.6f);
            });
        }

        void Furniture(string sprite, Vector2 pos, int x, int y, int w, int h)
        {
            if (!room.Prop(sprite, pos, 0)) return;
            for (int i = 0; i < w; i++)
            for (int j = 0; j < h; j++) room.BlockCell(x + i, y + j);
        }

        void Lore(string id, Vector2 pos, float radius, System.Action onRead = null) =>
            LoreNote.Create(room.Content, pos, radius, "lore." + id, "lore." + id + ".text", onRead);

        // ---------------- endings ----------------

        /// <summary>0 = one more fix (plain win), 1 = let it go (a memory this run), 2 = version 1.0 (all memories).</summary>
        int EndingFor() => AllMemories ? 2 : memoryThisRun ? 1 : 0;

        /// <summary>The true ending happens in the first memory: the Maker and Lina, years ago.</summary>
        void ShowEndingRoom(bool people = true)
        {
            ClearRoom();
            room = Room.Build(Layouts.Get(10), Theme.Modern, Law.None, transform, 1, MemoryStyles[0]);
            Fx.Root = room.Content;
            def = SecretRoom(0);
            SetupDream(false, people);
            if (StyleController.I) StyleController.I.Apply(Law.None, true);   // the bright flat needs no extra glow
            if (player) player.Teleport(room.PlayerSpawn);
            if (CameraFollow.I)
            {
                CameraFollow.I.bounds = room.Bounds;
                if (!Cutscene.Playing) CameraFollow.I.target = null;   // a cutscene keeps steering the camera
                CameraFollow.I.Snap();
            }
            if (Hud.I) Hud.I.ShowBossBar(false);
        }

        /// <summary>
        /// Shows an ending without a win (the codex "Story" tab):
        /// nothing is saved; afterwards back to the menu (or the codex).
        /// </summary>
        public void PreviewEnding(int which, bool backToCodex)
        {
            which = Mathf.Clamp(which, 0, EndingCount - 1);
            StopAllCoroutines();
            Time.timeScale = 1f;
            if (MenuUI.I) MenuUI.I.Hide();
            StartCoroutine(PreviewRoutine(which, backToCodex));
        }

        IEnumerator PreviewRoutine(int which, bool backToCodex)
        {
            bool inRun = player != null && State != GameState.Menu;
            State = GameState.Won;
            busy = true;
            if (Hud.I) yield return Hud.I.FadeTo(1f, 0.4f);

            // the throne room, the hero in front of where the Lawgiver fell
            ClearRoom();
            def = bossRoom;
            room = Room.Build(Layouts.Get(bossRoom.layout), Theme.Dungeon, Law.None, transform, 1);
            Fx.Root = room.Content;
            Rules.Apply(Law.None);
            if (StyleController.I) StyleController.I.Apply(Law.None, true);
            if (!player) player = PlayerController.Create(transform, cfg.hero ?? Heroes.All[0]);
            player.gameObject.SetActive(true);
            Vector2 at = room.BossSpawn ?? (room.Center + Vector2.up * 3f);
            player.Teleport(at + Vector2.down * 4f);
            if (CameraFollow.I) { CameraFollow.I.bounds = room.Bounds; CameraFollow.I.target = player.transform; CameraFollow.I.Snap(); }
            if (Hud.I) { Hud.I.ShowBossBar(false); yield return Hud.I.FadeTo(0f, 0.4f); }

            yield return EndingScene(which, at);

            // a run interrupted by the preview is left without saving a win
            if (inRun) finished = true;
            busy = false;
            ShowBackdrop();
            if (MenuUI.I)
            {
                if (backToCodex) MenuUI.I.ShowStory();
                else MenuUI.I.ShowMain();
            }
            yield return LiftEndingBlack();
        }

        // ---------------- cutscenes ----------------

        static readonly Color LawgiverColor = new Color(1f, 0.5f, 0.45f);
        static readonly Color MakerColor = new Color(0.75f, 0.95f, 0.75f);
        static readonly Color LinaColor = new Color(1f, 0.75f, 0.85f);
        static string LawgiverName => Rules.Info(Law.Boss).name;
        static string MakerName => Loc.T("lore.maker");
        static string LinaName => Loc.T("lore.dream.lina");

        CutActor Lawgiver(Vector2 at, bool faceHero = true)
        {
            var a = GameAssets.I;
            var actor = CutActor.Create(room.Content, "LawgiverActor", at,
                a ? a.Anim("big_demon_idle_anim") : new Sprite[0], a ? a.Anim("big_demon_run_anim") : new Sprite[0]);
            if (player && faceHero) actor.Face(player.Feet.x < at.x);
            LightUtil.Point(actor.transform, new Vector3(0f, 1.1f, 0f), new Color(1f, 0.3f, 0.3f), 3.5f, 0.8f);
            return actor;
        }

        CutActor Person(bool maker, Vector2 at)
        {
            var a = GameAssets.I;
            if (!a || a.PersonDirs(maker, false) == null) return null;
            return CutActor.Create(room.Content, maker ? "MakerActor" : "LinaActor", at, a.PersonDirs(maker, false), a.PersonDirs(maker, true) ?? a.PersonDirs(maker, false));
        }

        /// <summary>A white flash, the memory shows itself: its name and how it feels.</summary>
        IEnumerator MemoryIntro(RoomDef d)
        {
            Cutscene.Begin();
            Vector2 focus = d.memory == 2 ? new Vector2(room.W - 3f, 7f) : new Vector2(room.W / 2f, 4.5f);
            Cutscene.Look(focus);
            if (CameraFollow.I) CameraFollow.I.Snap();
            yield return Cutscene.Flash(0f, 1.4f);
            var card = StartCoroutine(Cutscene.UI.Card(Loc.T("hud.secretCard"), d.Title, 1.6f));
            yield return Cutscene.Wait(2.2f);
            yield return Cutscene.Narrate(Loc.T("cs.mem." + d.memory));
            Cutscene.Look(player.Feet);
            yield return Cutscene.Wait(0.6f);
            StopCoroutine(card);
            Cutscene.UI.Clear();
            Cutscene.End(false);
        }

        /// <summary>The Lawgiver on his throne before the fight.</summary>
        IEnumerator MeetLawgiver(Vector2 at)
        {
            Cutscene.Begin();
            var lawgiver = Lawgiver(at);
            Cutscene.Look(at + Vector2.up * 0.6f);
            yield return Cutscene.Wait(1.2f);
            Fx.Anim("lightning", at + Vector2.up * 1.1f, 2f);
            Sfx.Play("law", 0.7f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.3f);
            yield return Cutscene.Say(LawgiverName, Loc.T("cs.meet.1"), LawgiverColor);
            yield return Cutscene.Say(LawgiverName, Loc.T("cs.meet.2"), LawgiverColor);
            if (AllMemories) yield return Cutscene.Say(LawgiverName, Loc.T("cs.meet.all"), LawgiverColor);
            else if (memoryThisRun || SaveSystem.Data.memories != 0) yield return Cutscene.Say(LawgiverName, Loc.T("cs.meet.mem"), LawgiverColor);
            Cutscene.Look((at + player.Feet) / 2f);
            yield return Cutscene.Say(LawgiverName, Loc.T("cs.meet.3"), LawgiverColor);
            Fx.Anim("lightning", at + Vector2.up * 1.1f, 2.5f);
            Sfx.Play("boom", 0.6f, 0.7f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.5f);
            Destroy(lawgiver.gameObject);
            Cutscene.End(false);
        }

        /// <summary>The ending as a scene. 0: one more fix, 1: let it go, 2: version 1.0.</summary>
        IEnumerator EndingScene(int which, Vector2 at)
        {
            Cutscene.Begin();
            if (MusicPlayer.I) MusicPlayer.I.Play(which == 2 ? "secret" : which == 1 ? "pacifist" : "entry");

            // the beaten Lawgiver is still lying there: the scene takes over the same body, in place
            var fallen = Boss.I;
            CutActor lawgiver;
            if (fallen && fallen.BodyRenderer)
            {
                at = fallen.transform.position;
                lawgiver = Lawgiver(at, false);
                lawgiver.Body.flipX = fallen.BodyRenderer.flipX;
                lawgiver.Body.color = fallen.BodyRenderer.color;
                Destroy(fallen.gameObject);
            }
            else
            {
                // no fight before it (a preview): he rises out of smoke
                lawgiver = Lawgiver(at);
                lawgiver.Body.color = new Color(1f, 1f, 1f, 0f);
                Fx.Anim("smoke", at + Vector2.up * 0.8f, 1.2f);
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.8f)
                {
                    lawgiver.Body.color = new Color(1f, 1f, 1f, t);
                    yield return null;
                }
                lawgiver.Body.color = Color.white;
            }
            Cutscene.Look(at + Vector2.up * 0.5f);
            yield return Cutscene.Wait(1f);

            switch (which)
            {
                case 0:
                {
                    lawgiver.Body.color = new Color(0.7f, 0.7f, 0.75f, 0.9f);
                    yield return Cutscene.Say(LawgiverName, Loc.T("cs.e0.1"), LawgiverColor);
                    Fx.Anim("death", at + Vector2.up, 1.2f);
                    Fx.Burst(at + Vector2.up, new Color(0.8f, 0.3f, 0.3f), 30, 5f, 0.9f, 2f);
                    Sfx.Play("kill", 0.8f, 0.6f);
                    yield return lawgiver.FadeOut(1.2f);
                    yield return Cutscene.Narrate(Loc.T("ending.0.1"));
                    Cutscene.Look(new Vector2(room.W / 2f, room.H));
                    yield return Cutscene.Wait(1f);
                    yield return Cutscene.Narrate(Loc.T("ending.0.2"));
                    yield return Cutscene.Say("???", Loc.T("cs.e0.2"), LawgiverColor);
                    // the dungeon rewrites itself
                    foreach (var law in new[] { Law.Noir, Law.Neon, Law.Ice, Law.Crypt, Law.Lava, Law.None })
                    {
                        if (StyleController.I) StyleController.I.Apply(law, true);
                        Sfx.Play("law", 0.4f, Random.Range(0.8f, 1.3f));
                        if (CameraFollow.I) CameraFollow.I.Shake(0.25f);
                        yield return Cutscene.Wait(0.3f);
                    }
                    yield return Cutscene.Narrate(Loc.T("ending.0.3"));
                    break;
                }
                case 1:
                {
                    lawgiver.Body.color = new Color(0.85f, 0.8f, 0.8f);
                    yield return Cutscene.WalkPlayer(at + new Vector2(0f, -2.3f));
                    yield return Cutscene.Say(LawgiverName, Loc.T("cs.e1.1"), LawgiverColor);
                    yield return Cutscene.Narrate(Loc.T("ending.1.1"));
                    yield return Cutscene.Say(LawgiverName, Loc.T("cs.e1.2"), LawgiverColor);
                    yield return Cutscene.Wait(1.2f);
                    yield return Cutscene.Say(LawgiverName, Loc.T("cs.e1.3"), LawgiverColor);
                    Fx.Loop("sparkle", room.Content, at + Vector2.up, 0.5f, 40);
                    Sfx.Play("heal");
                    Fx.Burst(at + Vector2.up, UiKit.Gold, 30, 3f, 1.2f, 2f);
                    yield return lawgiver.FadeOut(2.2f);
                    yield return Cutscene.Narrate(Loc.T("ending.1.2"));
                    yield return Cutscene.Narrate(Loc.T("ending.1.3"));
                    break;
                }
                default:
                {
                    yield return Cutscene.WalkPlayer(at + new Vector2(0f, -2.3f));
                    yield return Cutscene.Say(LawgiverName, Loc.T("cs.e2.1"), LawgiverColor);
                    yield return Cutscene.Narrate(Loc.T("ending.2.1"));
                    // the crown of rules falls away
                    Sfx.Play("blink");
                    yield return Cutscene.Flash(1f, 0.6f);
                    Destroy(lawgiver.gameObject);
                    var maker = Person(true, at);
                    yield return Cutscene.Flash(0f, 1f);
                    yield return Cutscene.Narrate(Loc.T("ending.2.2"));
                    yield return Cutscene.Say(MakerName, Loc.T("cs.e2.2"), MakerColor);

                    // years earlier: the bright flat, the desk, the door
                    yield return Cutscene.Flash(1f, 0.9f);
                    ShowEndingRoom(false);
                    if (player) player.gameObject.SetActive(false);
                    int W = room.W, H = room.H;
                    Vector2 desk = new Vector2(W - 4f, 6.2f), door = room.DoorPos(0) + new Vector2(0f, -0.5f);
                    maker = Person(true, desk);
                    if (maker) maker.Idle(1);
                    Cutscene.Look(new Vector2(W / 2f + 2f, 5f));
                    if (CameraFollow.I) CameraFollow.I.Snap();
                    yield return Cutscene.Flash(0f, 1.4f);
                    yield return Cutscene.Narrate(Loc.T("ending.2.3"));
                    Fx.Anim("firework", desk + new Vector2(0f, 1.8f), 0.6f);
                    Sfx.Play("win", 0.5f);
                    yield return Cutscene.Wait(0.8f);

                    room.OpenDoor();
                    Sfx.Play("door");
                    var lina = Person(false, door);
                    if (lina) yield return lina.WalkTo(new Vector2(W / 2f + 0.8f, 5f));
                    if (maker) yield return maker.WalkTo(new Vector2(W / 2f - 0.8f, 5f));
                    if (maker) maker.Idle(0);
                    if (lina) lina.Idle(2);
                    yield return Cutscene.Say(LinaName, Loc.T("cs.e2.3"), LinaColor);
                    yield return Cutscene.Say(MakerName, Loc.T("cs.e2.4"), MakerColor);
                    if (lina) StartCoroutine(lina.WalkTo(door, 2.2f));
                    if (maker) StartCoroutine(maker.WalkTo(door + new Vector2(-0.4f, 0f), 2.2f));
                    yield return Cutscene.Narrate(Loc.T("ending.2.4"));
                    if (lina) StartCoroutine(lina.FadeOut(0.6f));
                    if (maker) yield return maker.FadeOut(0.6f);
                    yield return Cutscene.FadeBlack(1f, 1.2f);
                    yield return Cutscene.Narrate(Loc.T("ending.2.5"));
                    break;
                }
            }

            yield return Cutscene.FadeBlack(1f, 1f);
            Cutscene.UI.HideLine();
            yield return Cutscene.UI.Card(Loc.T("ending.label"), Loc.T("ending." + which + ".name"), 3f);
            // the screen stays black: whoever called this opens the next screen and then lifts the dark slowly
            Cutscene.End(false);
        }

        /// <summary>Lifts the black left by an ending, under the menu that just opened.</summary>
        IEnumerator LiftEndingBlack()
        {
            yield return Cutscene.FadeBlack(0f, 1.6f);
            Cutscene.UI.Clear();
        }

        // ---------------- music ----------------

        /// <summary>Track for a room: bosses and special rooms have their own, combat rooms follow the law.</summary>
        string MusicFor(RoomDef d)
        {
            switch (d.objective)
            {
                case Objective.Boss: return d.law == Law.Neon ? NeonMusic() : BossMusic();
                case Objective.Shop: return "shop";
                case Objective.Shrine: return "shrine";
                case Objective.Fountain: return "fountain";
                case Objective.Secret: return "secret";
            }
            return d.law == Law.None ? "entry" : LawMusic(d.law);
        }

        string LawMusic(Law law)
        {
            switch (law)
            {
                case Law.Ice: return "ice";
                case Law.Pacifist: return "pacifist";
                case Law.Stealth: return "treasury";
                case Law.Lava: return "lava";
                case Law.Neon: return NeonMusic();   // the beat is measured for each track
                case Law.Retro: return "retro";   // procedural chiptune
                case Law.None: return "entry";
            }
            return floor >= 3 ? "deep" : "floor" + (floor + 1);
        }

        int neonPick = -1;

        /// <summary>One of the five Neon tracks, a different one each time.</summary>
        string NeonMusic()
        {
            var keys = MusicTracks.NeonKeys;
            neonPick = neonPick < 0 ? Random.Range(0, keys.Length) : (neonPick + Random.Range(1, keys.Length)) % keys.Length;
            return keys[neonPick];
        }

        bool lawgiverEnraged;

        string BossMusic() => def != null && def.floorBoss ? "floorboss" : lawgiverEnraged ? "lawgiver2" : "lawgiver";

        /// <summary>The Lawgiver's second half gets the final theme.</summary>
        public void OnLawgiverEnraged()
        {
            lawgiverEnraged = true;
            if (MusicPlayer.I && def != null && def.objective == Objective.Boss && !def.floorBoss && Rules.Current != Law.Neon)
                MusicPlayer.I.Play("lawgiver2");
        }

        void GoThrough(int door)
        {
            if (busy) return;
            if (def.usableInBoss && def.law != Law.None && !passedLaws.Contains(def.law)) passedLaws.Add(def.law);
            var next = nextChoices[door];
            if (def.objective == Objective.Boss) { floor++; inFloor = 0; }
            else inFloor++;
            // the room you didn't pick goes to the back of the queue
            foreach (var other in nextChoices)
                if (other != next && unused.Remove(other)) unused.Add(other);
            unused.Remove(next);
            step++;
            StartCoroutine(EnterRoom(next, false));
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
            SaveSystem.Data.endings |= 1 << ending;
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
            SaveSystem.Save();

            int t = Mathf.FloorToInt(runTime);
            string depth = Endless ? Loc.F("end.floorEndless", floor + 1) : Loc.F("end.floors", Mathf.Min(floor + 1, floors), floors);
            string curses = CurseList();
            if (win && ending >= 0)
            {
                int seen = 0;
                for (int i = 0; i < EndingCount; i++) if ((d.endings & (1 << i)) != 0) seen++;
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
