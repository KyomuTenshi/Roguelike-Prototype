using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RulesShift
{
    /// <summary>Other worlds: Version 1.0 and the Deep Caves (portals, rooms, escapees and rifts).</summary>
    public partial class RoomManager
    {
        // ---------------- other worlds ----------------

        static readonly Law[] RetroWorldLaws = { Law.Noir, Law.Crypt, Law.Mirror, Law.Upside, Law.Pacifist, Law.Neon };
        static readonly Law[] IceCaveLaws = { Law.Crypt, Law.Noir, Law.Mirror, Law.Upside };
        static readonly Law[] FireCaveLaws = { Law.Noir, Law.Crypt, Law.Upside, Law.Neon };

        /// <summary>
        /// Rooms of the worlds, in order. Version 1.0: three rooms (the second plays by the Retro law) and the Slime King.
        /// The Deep Caves: two ice rooms and the Frost Guardian, then two fire rooms and the Demon Slime.
        /// null = the world is over.
        /// </summary>
        RoomDef WorldRoom(World w, int i)
        {
            RoomDef d;
            if (w == World.Retro)
            {
                if (i > 3) return null;
                if (i == 3) d = WorldBoss(FloorBossKind.SlimeKing, RunRandom.Pick(new[] { Law.Noir, Law.Mirror, Law.Upside, Law.Crypt }));
                else d = WorldCombat(i == 1 ? Law.Retro : RunRandom.Pick(RetroWorldLaws));
                d.theme = Theme.Retro;
            }
            else
            {
                if (i > 5) return null;
                bool fire = i >= 3;
                if (i == 2) d = WorldBoss(FloorBossKind.FrostGuardian, Law.Ice);
                else if (i == 5) d = WorldBoss(FloorBossKind.DemonSlime, RunRandom.Pick(DemonLaws));
                else
                {
                    d = WorldCombat(i == 0 ? Law.Ice : i == 3 ? Law.Lava : RunRandom.Pick(fire ? FireCaveLaws : IceCaveLaws));
                    d.runnerSkin = fire ? "chort" : "ice_zombie";
                    d.shooterSkin = fire ? "imp" : "necromancer";
                    d.kamikazeSkin = fire ? "tiny_zombie" : "tiny_slug";
                }
                d.theme = Theme.Cave;
                d.fire = fire;
            }
            d.world = w;
            d.worldIndex = i;
            d.worldEntry = i == 0;
            d.inPool = false;
            d.usableInBoss = d.objective != Objective.Boss;
            return d;
        }

        /// <summary>A world not visited yet in this run (random if both are free), or None.</summary>
        World PickWorld()
        {
            bool r = !retroDone, c = !cavesDone;
            if (r && c) return RunRandom.Value < 0.5f ? World.Retro : World.Caves;
            return r ? World.Retro : c ? World.Caves : World.None;
        }

        /// <summary>
        /// Now and then a monster escapes from a world not visited yet: big, tough and glowing in its world's colour.
        /// A faint rift in a side wall is where it came from — three hits once the room is clear open the way there.
        /// </summary>
        void MaybeSpawnEscapee()
        {
            riftHere = false;
            if (def.world != World.None || def.objective != Objective.KillAll || escapeeFloor == floor || RunRandom.Value > 0.14f) return;
            var w = PickWorld();
            if (w == World.None) return;
            escapeeFloor = floor;
            riftWorld = w;
            riftHits = 0;
            Vector2 p = room.EnemySpawns[RunRandom.Range(0, room.EnemySpawns.Count)];
            Color c = RoomDef.WorldColor(w);
            Enemy e;
            if (w == World.Retro)
            {
                e = Enemy.Spawn(EnemyKind.Runner, "sgq_medium_slime", p, room.Content, Rules.Stealth);
                e.splits = 2;
                e.MakeMiniBoss(4f, 1.6f, c);
            }
            else
            {
                bool fire = RunRandom.Value < 0.5f;
                if (fire) c = new Color(1f, 0.55f, 0.25f);
                e = Enemy.Spawn(EnemyKind.Runner, fire ? "chort" : "ice_zombie", p, room.Content, Rules.Stealth);
                e.MakeMiniBoss(5f, 1.8f, c);
            }
            room.AddRift(RunRandom.Value < 0.5f, RoomDef.WorldColor(w));
            riftHere = true;
            Hud.Hint("escapee");
        }

        /// <summary>The rift is open and the hero stepped in: into the world the escapee came from.</summary>
        void GoRift()
        {
            if (busy) return;
            if (def.usableInBoss && def.law != Law.None && !passedLaws.Contains(def.law)) passedLaws.Add(def.law);
            riftHere = false;
            Achievements.Unlock("rift");
            if (def.world == World.None) inFloor++;
            if (riftWorld == World.Retro) retroDone = true; else cavesDone = true;
            step++;
            Sfx.Play("law", 0.7f, 0.8f);
            StartCoroutine(EnterRoom(WorldRoom(riftWorld, 0), false));
        }

        /// <summary>A copy of the pool's room for this law (or the first combat room).</summary>
        RoomDef WorldCombat(Law law)
        {
            var src = rooms.Find(r => r.law == law && r.inPool) ?? rooms.Find(r => r.law == law)
                      ?? rooms.Find(r => r.objective == Objective.KillAll && r.law != Law.None) ?? rooms[0];
            var d = src.Clone();
            d.law = law;
            return d;
        }

        static RoomDef WorldBoss(FloorBossKind kind, Law law) => new RoomDef
        {
            id = "worldboss", objective = Objective.Boss, floorBoss = true, worldBoss = true,
            bossKind = kind, law = law, layout = 6, inPool = false, usableInBoss = false
        };

        /// <summary>Two bosses per floor: the Ogre or the Chieftain on the first, the Rotting Giant or the Archdemon on the second.</summary>
        static FloorBossKind FloorBossFor(int f)
        {
            bool alt = RunRandom.Value < 0.5f;
            FloorBossKind k = f % 2 == 0 ? (alt ? FloorBossKind.Chieftain : FloorBossKind.Ogre)
                                         : (alt ? FloorBossKind.Archdemon : FloorBossKind.Zombie);
            if (!FloorBoss.Available(k)) k = f % 2 == 0 ? FloorBossKind.Ogre : FloorBossKind.Zombie;
            return k;
        }

        RoomDef BossFor(int f)
        {
            bool lawgiver = Endless ? f % 3 == 2 : f >= floors - 1;
            if (lawgiver) return bossRoom;
            return new RoomDef
            {
                id = "floorboss", objective = Objective.Boss, floorBoss = true,
                bossKind = FloorBossFor(f),
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
            layout = o == Objective.Shop ? 7 : o == Objective.Shrine || o == Objective.Gamble ? 8 : 9,
            inPool = false, usableInBoss = false
        };

        /// <summary>
        /// The tileset of a room: Retro rooms are drawn in the SGQ Game Boy set, only the Deep Caves world in the RPG Worlds caves
        /// (ice and lava rooms of the main path keep the dungeon tiles), the second floor in the classic 16x16 set.
        /// </summary>
        Theme FloorTheme(RoomDef d)
        {
            var a = GameAssets.I;
            if (d.theme == Theme.Modern) return Theme.Modern;
            if ((d.theme == Theme.Retro || d.law == Law.Retro) && a && a.HasRetro) return Theme.Retro;
            if (d.theme == Theme.Cave && a && a.HasCave) return Theme.Cave;   // the Deep Caves only: a world of its own
            if (d.theme == Theme.Retro || d.theme == Theme.Cave) return floor % 3 == 1 ? Theme.Classic : Theme.Dungeon;
            return floor % 3 == 1 ? Theme.Classic : d.theme;
        }

        /// <summary>Glowing red rocks for lava and the Demon, blue crystals for everything else.</summary>
        static string CaveStyle(RoomDef d) =>
            d.fire || d.law == Law.Lava || (d.floorBoss && d.bossKind == FloorBossKind.DemonSlime) ? "fire" : "ice";

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
    }
}
