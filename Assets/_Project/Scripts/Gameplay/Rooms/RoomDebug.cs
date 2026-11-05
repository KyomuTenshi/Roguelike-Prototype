#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace RulesShift
{
    /// <summary>Shortcuts for the debug menu (F1). Only in the editor and development builds.</summary>
    public partial class RoomManager
    {
        public bool DebugReady => State == GameState.Playing && !busy && room != null && player != null;
        public PlayerController DebugPlayer => player;

        /// <summary>Every enemy and the boss die at once (the room clears by itself).</summary>
        public void DebugKillAll()
        {
            if (!DebugReady) return;
            foreach (var e in Enemy.All.ToArray())
            {
                if (!e || !e.Alive) continue;
                if (Rules.ShotsHeal) e.Vanish();
                else e.Hit(e.hp + 9999f, Vector2.up);
            }
            var b = Bosses.Current;
            if (b != null && !Rules.ShotsHeal) b.Hit(99999f, Vector2.up);
        }

        /// <summary>The room counts as cleared: doors open (no chest).</summary>
        public void DebugClear()
        {
            if (!DebugReady) return;
            Enemy.VanishAll();
            Clear(false);
        }

        /// <summary>Through the first door.</summary>
        public void DebugNext()
        {
            if (!DebugReady || nextChoices == null || nextChoices.Length == 0) return;
            GoThrough(0);
        }

        /// <summary>Straight into the first room of a world.</summary>
        public void DebugWorld(World w)
        {
            if (!DebugReady) return;
            if (w == World.Retro) retroDone = true; else cavesDone = true;
            step++;
            StartCoroutine(EnterRoom(WorldRoom(w, 0), false));
        }

        /// <summary>Straight into a room of the given type (combat variants take a random law room).</summary>
        public void DebugRoom(Objective o)
        {
            if (!DebugReady) return;
            RoomDef d;
            if (o == Objective.Challenge || o == Objective.Ambush)
            {
                var pool = rooms.FindAll(r => r.inPool && r.law != Law.None && r.objective != Objective.Boss);
                d = CombatVariant(pool.Count > 0 ? RunRandom.Pick(pool) : rooms[0], o);
            }
            else if (o == Objective.Boss) d = BossFor(floor);
            else d = Special(o);
            step++;
            StartCoroutine(EnterRoom(d, false));
        }

        // ---------------- the story (F1 → Сюжет) ----------------

        /// <summary>A save file of Amelia: the memory room with its intro and scenes (nothing is saved).</summary>
        public void DebugMemory(int memory)
        {
            if (!DebugReady) return;
            savedChoices = nextChoices;
            step++;
            StartCoroutine(EnterRoom(SecretRoom(memory), false));
        }

        /// <summary>The throne room: the meeting with the Administrator, then the fight.</summary>
        public void DebugLawgiver()
        {
            if (!DebugReady) return;
            step++;
            StartCoroutine(EnterRoom(bossRoom, false));
        }

        /// <summary>Pixie introduces herself again, right here.</summary>
        public void DebugPixieIntro()
        {
            if (!DebugReady) return;
            StartCoroutine(DebugScene(PixieIntro()));
        }

        /// <summary>The System and Pixie at the gate of a world (only the words, the room stays).</summary>
        public void DebugWorldIntro(World w)
        {
            if (!DebugReady) return;
            StartCoroutine(DebugScene(WorldIntro(w)));
        }

        System.Collections.IEnumerator DebugScene(System.Collections.IEnumerator scene)
        {
            busy = true;
            player.frozen = true;
            yield return scene;
            player.frozen = false;
            busy = false;
        }

        /// <summary>A stuck player right next to the hero (E to talk; each new meeting tells the next part).</summary>
        public void DebugNpc(string id)
        {
            if (!DebugReady) return;
            string skin = id == "mio" ? "elf_f" : id == "doc" ? "doc" : id == "beta" ? "pumpkin_dude" : id == "goro" ? "dwarf_m" : "angel";
            Color c = id == "mio" ? new Color(0.7f, 1f, 0.75f) : id == "doc" ? new Color(0.75f, 0.9f, 1f)
                    : id == "beta" ? new Color(1f, 0.75f, 0.4f) : id == "goro" ? new Color(0.95f, 0.8f, 0.55f) : new Color(0.85f, 0.95f, 1f);
            Vector2 p = room.NearestFree(player.Feet + Vector2.right * 1.5f);
            Npc.Create(room, id, skin, new Vector2(p.x, p.y - 0.3f), c, null);
        }

        public void DebugSpawn(EnemyKind kind, bool elite)
        {
            if (!DebugReady) return;
            Vector2 p = room.NearestFree(player.Feet + Random.insideUnitCircle.normalized * 4f);
            Enemy.Spawn(kind, SkinFor(kind, KindSkin(kind, kind == EnemyKind.Kamikaze ? "tiny_zombie" : Enemy.BaseKind(kind) == EnemyKind.Shooter ? "orc_shaman" : "goblin")),
                p, room.Content, false, elite);
        }
    }
}
#endif
