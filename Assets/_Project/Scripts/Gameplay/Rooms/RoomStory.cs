using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RulesShift
{
    /// <summary>The story: Pixie, the prologue, the intros of the worlds, the secret rooms (save files of Amelia), the endings and the cutscenes.</summary>
    public partial class RoomManager
    {
        // ---------------- the story: Pixie, the prologue, other worlds ----------------

        /// <summary>The first run: the fairy navigator of the System introduces herself.</summary>
        IEnumerator PixieIntro()
        {
            SaveSystem.Data.pixieMet = true;
            SaveSystem.Save();
            Cutscene.Begin();
            Cutscene.Look(player.Feet + Vector2.up);
            yield return Cutscene.Wait(0.4f);
            for (int i = 1; i <= 4; i++)
            {
                if (Pixie.I) Pixie.I.Ping();
                yield return Cutscene.Say(Pixie.Name, Loc.T("pixie.intro." + i), Pixie.Color);
            }
            Cutscene.End(false);
        }

        /// <summary>Stepping through a portal: the System warns, Pixie comments.</summary>
        IEnumerator WorldIntro(World w)
        {
            string key = w == World.Retro ? "retro" : "caves";
            Cutscene.Begin();
            Cutscene.Look(player.Feet + Vector2.up);
            yield return Cutscene.Wait(0.3f);
            yield return Cutscene.Say(Loc.T("name.system"), Loc.T("world." + key + ".system"), SystemColor);
            if (Pixie.I) Pixie.I.Ping();
            yield return Cutscene.Say(Pixie.Name, Loc.T("world." + key + ".pixie"), Pixie.Color);
            Cutscene.End(false);
        }

        static readonly Color SystemColor = new Color(0.45f, 0.95f, 1f);

        /// <summary>The codex: watch the prologue again.</summary>
        public void PreviewPrologue()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            if (MenuUI.I) MenuUI.I.Hide();
            StartCoroutine(PrologueRoutine());
        }

        IEnumerator PrologueRoutine()
        {
            bool inRun = player != null && State != GameState.Menu;
            State = GameState.Won;
            yield return Prologue();
            if (inRun) finished = true;
            busy = false;
            ShowBackdrop();
            if (MenuUI.I) MenuUI.I.ShowStory();
            yield return LiftEndingBlack();
        }

        /// <summary>
        /// The night it began: Amelia's room a year after she vanished, her old console turns on by itself
        /// and pulls Adam into the game. Ends on black.
        /// </summary>
        IEnumerator Prologue()
        {
            busy = true;
            if (Hud.I) yield return Hud.I.FadeTo(1f, 0.3f);
            ClearRoom();
            room = Room.Build(Layouts.Get(10), Theme.Modern, Law.None, transform, 1, MemoryStyles[2]);
            Fx.Root = room.Content;
            Rules.Apply(Law.None);
            if (StyleController.I) StyleController.I.Apply(Law.Console, true);   // night, the screen's blue light
            if (player) player.gameObject.SetActive(false);
            if (Pixie.I) Pixie.I.gameObject.SetActive(false);
            if (MusicPlayer.I) MusicPlayer.I.Play("prologue");

            int W = room.W, H = room.H;
            WallThings(null, "calendar", "window", "photo");
            Furniture("bed", new Vector2(2.3f, 5f), 1, 5, 3, 4);
            Furniture("desk", new Vector2(W - 4f, 7.2f), W - 5, 7, 2, 2);
            Furniture("computer", new Vector2(W - 2.4f, 7.4f), W - 3, 7, 1, 1);
            room.Prop("chair", new Vector2(W - 4f, 6.3f), 0);
            Furniture("boxes", new Vector2(3f, 1f), 1, 1, 3, 1);
            var screen = LightUtil.Point(room.Content, new Vector2(W - 3f, 8f), new Color(0.5f, 0.8f, 1f), 3.5f, 0.4f);
            var adam = Person(true, new Vector2(W / 2f - 2f, 3f));

            Cutscene.Begin();
            Cutscene.Look(new Vector2(W / 2f, 5f));
            if (CameraFollow.I) { CameraFollow.I.bounds = room.Bounds; CameraFollow.I.Snap(); }
            if (Hud.I) { Hud.I.ShowBossBar(false); yield return Hud.I.FadeTo(0f, 1.2f); }

            yield return Cutscene.Narrate(Loc.T("prologue.1"));
            yield return Cutscene.Narrate(Loc.T("prologue.2"));
            // the console turns itself on
            for (int i = 0; i < 4; i++)
            {
                if (screen) screen.intensity = i % 2 == 0 ? 1.6f : 0.3f;
                Sfx.Play("blink", 0.4f, 1.2f + i * 0.1f);
                yield return Cutscene.Wait(0.18f);
            }
            if (screen) screen.intensity = 1.4f;
            Fx.Loop("sparkle", room.Content, new Vector2(W - 2.4f, 8.2f), 0.35f, 40);
            if (adam) yield return adam.WalkTo(new Vector2(W - 4f, 5.6f));
            if (adam) adam.Idle(1);
            yield return Cutscene.Say(AdamName, Loc.T("prologue.3"), AdamColor);
            yield return Cutscene.UI.Card(Loc.T("name.system"), Loc.T("prologue.card"), 1.4f);
            yield return Cutscene.Say(Loc.T("name.system"), Loc.T("prologue.4"), SystemColor);
            yield return Cutscene.Say(AdamName, Loc.T("prologue.5"), AdamColor);
            // pulled in
            Sfx.Play("law", 0.8f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.5f);
            if (StyleController.I) StyleController.I.Apply(Law.Neon, true);
            Fx.Anim("warp", new Vector2(W - 3f, 6.5f), 2f);
            yield return Cutscene.Flash(1f, 0.8f);
            if (adam) Destroy(adam.gameObject);
            yield return Cutscene.Narrate(Loc.T("prologue.6"));
            yield return Cutscene.FadeBlack(1f, 0.6f);
            yield return Cutscene.UI.FadeTo(Cutscene.UI.white, 0f, 0.01f);
            Cutscene.UI.HideLine();
            Cutscene.End(false);
            if (player) player.gameObject.SetActive(true);
            if (Pixie.I) Pixie.I.gameObject.SetActive(true);
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
            if (room && State == GameState.Playing) room.HitTorch(p, radius);
            if (riftHere && room && !room.RiftOpen && !busy && State == GameState.Playing && cleared &&
                Vector2.Distance(p, room.RiftPos) < 1.3f + radius && Time.time - riftHitTime > 0.2f)
            {
                riftHitTime = Time.time;
                riftHits++;
                Fx.Burst(room.RiftPos, RoomDef.WorldColor(riftWorld), 6, 2f, 0.3f, 1.2f);
                Sfx.Play("blink", 0.6f, 0.8f + riftHits * 0.2f);
                if (riftHits >= 3)
                {
                    room.OpenRift(RoomDef.WorldColor(riftWorld));
                    Sfx.Play("law", 0.7f, 1.2f);
                    if (CameraFollow.I) CameraFollow.I.Shake(0.2f);
                    Hud.Toast(Loc.T("toast.rift"), RoomDef.WorldColor(riftWorld));
                }
            }
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

        public static bool AllMemories => (SaveSystem.Data.saveFiles & ((1 << MemoryCount) - 1)) == (1 << MemoryCount) - 1;

        void GoSecret()
        {
            if (busy) return;
            if (def.usableInBoss && def.law != Law.None && !passedLaws.Contains(def.law)) passedLaws.Add(def.law);
            savedChoices = nextChoices;
            secretDone = true;
            secretHere = false;
            memoryThisRun = true;
            Achievements.Unlock("save");
            AddScore(250);

            // the next memory not seen yet; once all are known, any of them
            var save = SaveSystem.Data;
            memoryVariant = -1;
            foreach (int m in MemoryOrder) if ((save.saveFiles & (1 << m)) == 0) { memoryVariant = m; break; }
            if (memoryVariant < 0) memoryVariant = RunRandom.Range(0, MemoryCount);
            bool wasAll = AllMemories;
            save.saveFiles |= 1 << memoryVariant;
            save.saveFound = true;
            completedMemories = !wasAll && AllMemories;
            SaveSystem.Save();

            StartCoroutine(EnterRoom(SecretRoom(memoryVariant), false));
        }

        int memoryVariant;
        bool completedMemories;

        /// <summary>A save file of Amelia that the System broke: a room of the real world at one moment.</summary>
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

        SpriteRenderer Person(string name, Sprite[] frames, Vector2 at, float fps = 6f)
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
            anim.Play(frames, fps);
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

        /// <summary>The first night: Amelia at her desk the night she put the cartridge in (the first save found).</summary>
        void SetupWork()
        {
            int W = room.W, H = room.H;
            WallThings("work.", "board", "photo", "calendar", "window");
            room.Prop("rug", new Vector2(W / 2f, 3f), -60);
            Furniture("bed", new Vector2(2.3f, 5f), 1, 5, 3, 4);
            Furniture("desk", new Vector2(W - 4f, 7.2f), W - 5, 7, 2, 2);
            Furniture("computer", new Vector2(W - 2.4f, 7.4f), W - 3, 7, 1, 1);
            room.Prop("chair", new Vector2(W - 4f, 6.3f), 0);
            Lore("work.computer", new Vector2(W - 3.2f, 6.6f), 1.8f);
            Furniture("boxes", new Vector2(W - 2.5f, 1f), W - 4, 1, 2, 1);
            Furniture("tree", new Vector2(0.7f, 0.2f), 0, 0, 1, 1);
            Furniture("plant", new Vector2(W - 0.7f, H - 1.8f), W - 1, H - 2, 1, 1);
            LightUtil.Point(room.Content, new Vector2(W - 3f, 8f), new Color(0.6f, 0.8f, 1f), 3f, 0.8f);   // the monitor glow

            // Amelia on the chair, facing the monitor (standing in the middle of the room when the sit strip is missing)
            var a = GameAssets.I;
            var sit = a ? a.Sitting(false, false) : null;
            Vector2 at = sit != null ? new Vector2(W - 4f, 6.25f) : new Vector2(W / 2f, 4.2f);
            var amelia = Person("Amelia", sit ?? (a ? a.AmeliaIdle() : null), at);
            if (amelia && sit != null) amelia.sortingOrder = 1;   // in front of the chair
            Lore("work.yuki", sit != null ? at + new Vector2(0f, -1f) : at, 1.8f, HealHere);
        }

        /// <summary>Before the game: a bright afternoon, Adam and Amelia at home, the cartridge still in its box.</summary>
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
            var ameliaAt = new Vector2(W / 2f + 0.9f, 4.2f);
            Person("Adam", GameAssets.I ? GameAssets.I.AdamIdle() : null, adam);
            Person("Amelia", GameAssets.I ? GameAssets.I.AmeliaIdle() : null, ameliaAt);
            // Alex and Bob dropped by: the friends from the real world (LimeZu)
            var g = GameAssets.I;
            var alexDirs = g ? g.FriendDirs(true, false) : null;
            var bobDirs = g ? g.FriendDirs(false, false) : null;
            Vector2 alex = new Vector2(W / 2f - 3.2f, 2.6f), bob = new Vector2(W / 2f - 4.4f, 3.4f);
            if (alexDirs != null) Person("Alex", alexDirs[0], alex);   // facing right, towards the siblings
            if (bobDirs != null) Person("Bob", bobDirs[0], bob);
            if (!notes) return;
            if (alexDirs != null || bobDirs != null) Lore("dream.friends", alex + new Vector2(-0.5f, 0f), 1.5f);
            Lore("dream.ren", adam - new Vector2(0.4f, 0f), 1.4f, HealHere);
            Lore("dream.yuki", ameliaAt + new Vector2(0.4f, 0f), 1.4f);
            Lore("dream.desk", new Vector2(W - 4f, 6.6f), 1.6f);
        }

        /// <summary>The empty room: a year after Amelia was gone. Adam alone; the old console turns itself on at night.</summary>
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

            // Adam staring at his phone: her number, again
            Vector2 at = new Vector2(W / 2f + 1f, 5.2f);
            var g = GameAssets.I;
            Person("Adam", g ? (g.OnPhone(true) ?? g.AdamIdle()) : null, at, 4f);
            Lore("silence.ren", at, 1.8f, HealHere);
        }

        /// <summary>All three saves are found: Amelia's last note appears in this room.</summary>
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

        /// <summary>0 = log out (plain win), 1 = the new Admin (a save found this run), 2 = home together (all three saves).</summary>
        int EndingFor() => AllMemories ? 2 : memoryThisRun ? 1 : 0;

        /// <summary>The true ending wakes up in the bright room: Adam at the desk, Amelia at the door.</summary>
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
        static readonly Color AdamColor = new Color(0.65f, 0.85f, 1f);
        static readonly Color AmeliaColor = new Color(1f, 0.75f, 0.85f);
        static string LawgiverName => Rules.Info(Law.Boss).name;
        static string AdamName => Loc.T("name.ren");
        static string AmeliaName => Loc.T("name.yuki");

        CutActor Lawgiver(Vector2 at, bool faceHero = true)
        {
            var a = GameAssets.I;
            var actor = CutActor.Create(room.Content, "LawgiverActor", at,
                a ? a.Anim("big_demon_idle_anim") : new Sprite[0], a ? a.Anim("big_demon_run_anim") : new Sprite[0]);
            if (player && faceHero) actor.Face(player.Feet.x < at.x);
            LightUtil.Point(actor.transform, new Vector3(0f, 1.1f, 0f), new Color(1f, 0.3f, 0.3f), 3.5f, 0.8f);
            return actor;
        }

        /// <summary>Adam (true) or Amelia (false) as a walking actor.</summary>
        CutActor Person(bool adam, Vector2 at)
        {
            var a = GameAssets.I;
            if (!a || a.PersonDirs(adam, false) == null) return null;
            return CutActor.Create(room.Content, adam ? "AdamActor" : "AmeliaActor", at, a.PersonDirs(adam, false), a.PersonDirs(adam, true) ?? a.PersonDirs(adam, false));
        }

        /// <summary>A white flash, the memory shows itself: its name and how it feels.</summary>
        IEnumerator MemoryIntro(RoomDef d)
        {
            Cutscene.Begin();
            if (MusicPlayer.I) MusicPlayer.I.Jingle("jingle_night");   // Peaceful Night over the memory's own music
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
            else if (memoryThisRun || SaveSystem.Data.saveFiles != 0) yield return Cutscene.Say(LawgiverName, Loc.T("cs.meet.mem"), LawgiverColor);
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
                    // the crown of rules breaks: under it is Amelia
                    Sfx.Play("blink");
                    yield return Cutscene.Flash(1f, 0.6f);
                    Destroy(lawgiver.gameObject);
                    var sister = Person(false, at);
                    yield return Cutscene.Flash(0f, 1f);
                    yield return Cutscene.Narrate(Loc.T("ending.2.2"));
                    yield return Cutscene.Say(AmeliaName, Loc.T("cs.e2.2"), AmeliaColor);
                    if (sister) Destroy(sister.gameObject);

                    // home: Adam wakes up at the old console, Amelia comes in
                    yield return Cutscene.Flash(1f, 0.9f);
                    ShowEndingRoom(false);
                    if (player) player.gameObject.SetActive(false);
                    int W = room.W, H = room.H;
                    Vector2 desk = new Vector2(W - 4f, 6.2f), door = room.DoorPos(0) + new Vector2(0f, -0.5f);
                    var adam = Person(true, desk);
                    if (adam) adam.Idle(1);
                    Cutscene.Look(new Vector2(W / 2f + 2f, 5f));
                    if (CameraFollow.I) CameraFollow.I.Snap();
                    yield return Cutscene.Flash(0f, 1.4f);
                    yield return Cutscene.Narrate(Loc.T("ending.2.3"));
                    Fx.Anim("firework", desk + new Vector2(0f, 1.8f), 0.6f);
                    Sfx.Play("win", 0.5f);
                    yield return Cutscene.Wait(0.8f);

                    room.OpenDoor();
                    Sfx.Play("door");
                    var amelia = Person(false, door);
                    if (amelia) yield return amelia.WalkTo(new Vector2(W / 2f + 0.8f, 5f));
                    if (adam) yield return adam.WalkTo(new Vector2(W / 2f - 0.8f, 5f));
                    if (adam) adam.Idle(0);
                    if (amelia) amelia.Idle(2);
                    yield return Cutscene.Say(AmeliaName, Loc.T("cs.e2.3"), AmeliaColor);
                    yield return Cutscene.Say(AdamName, Loc.T("cs.e2.4"), AdamColor);
                    if (amelia) StartCoroutine(amelia.WalkTo(door, 2.2f));
                    if (adam) StartCoroutine(adam.WalkTo(door + new Vector2(-0.4f, 0f), 2.2f));
                    yield return Cutscene.Narrate(Loc.T("ending.2.4"));
                    if (amelia) StartCoroutine(amelia.FadeOut(0.6f));
                    if (adam) yield return adam.FadeOut(0.6f);
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
    }
}
