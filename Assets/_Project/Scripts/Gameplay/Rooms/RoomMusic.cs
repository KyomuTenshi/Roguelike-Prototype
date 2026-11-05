using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RulesShift
{
    /// <summary>Music: the track of each room, law, boss and world.</summary>
    public partial class RoomManager
    {
        // ---------------- music ----------------

        /// <summary>Track for a room: bosses and special rooms have their own, combat rooms follow the law.</summary>
        string MusicFor(RoomDef d)
        {
            // Neon moves on the beat: only the fastpaced pack (measured tempo), also as the second law of the curse
            if (d.law == Law.Neon || extraLaw == Law.Neon) return NeonMusic();
            // other worlds have their own sound: 8-bit in Version 1.0, ice and fire in the caves; every room its own track
            if (d.world != World.None && d.objective != Objective.Boss && d.law != Law.Neon)
            {
                string[] list = d.world == World.Retro ? RetroTracks : d.fire ? FireTracks : IceTracks;
                return list[(d.worldIndex + worldMusicOffset) % list.Length];
            }
            switch (d.objective)
            {
                case Objective.Boss: return d.law == Law.Neon ? NeonMusic() : BossMusic();
                case Objective.Shop: return "shop";
                case Objective.Shrine: return "shrine";
                case Objective.Fountain: return "fountain";
                case Objective.Secret: return "secret";
                case Objective.Treasure: return "fountain";
                case Objective.Gamble: return "gamble";
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
                case Law.Retro: return "retro";   // the 8-bit pack
                case Law.None: return "entry";
                // laws with a theme of their own share the room with the floor's battle music
                case Law.Noir: case Law.Crypt: case Law.Mirror: case Law.Upside: case Law.Console:
                case Law.Gravity: case Law.Time: case Law.Inflation: case Law.Bounce:
                    if (Random.value < 0.5f) return Rules.Key(law);
                    break;
            }
            return floor >= 3 ? "deep" : "floor" + (floor + 1);
        }

        static readonly string[] RetroTracks = { "retro1", "retro2", "retro3", "retro4", "retro6" };
        static readonly string[] IceTracks = { "ice1", "ice2", "ice3" };
        static readonly string[] FireTracks = { "lava1", "lava2" };
        int worldMusicOffset;

        int neonPick = -1;

        static bool NeonPlaying => MusicPlayer.I && System.Array.IndexOf(MusicTracks.NeonKeys, MusicPlayer.I.CurrentKey) >= 0;

        /// <summary>One of the five Neon tracks (fastpaced-music-pack), a different one each time.</summary>
        string NeonMusic()
        {
            var keys = MusicTracks.NeonKeys;
            neonPick = neonPick < 0 ? Random.Range(0, keys.Length) : (neonPick + Random.Range(1, keys.Length)) % keys.Length;
            return keys[neonPick];
        }

        bool lawgiverEnraged;

        string BossMusic()
        {
            if (def != null && def.floorBoss)
                switch (def.bossKind)
                {
                    case FloorBossKind.SlimeKing: return "floorboss_slime";
                    case FloorBossKind.Zombie: return "floorboss_zombie";
                    case FloorBossKind.DemonSlime: return "floorboss_demon";
                    case FloorBossKind.FrostGuardian: return "floorboss_frost";
                    default: return "floorboss";
                }
            return lawgiverEnraged ? "lawgiver2" : "lawgiver";
        }

        /// <summary>The Lawgiver's second half gets the final theme.</summary>
        public void OnLawgiverEnraged()
        {
            lawgiverEnraged = true;
            if (MusicPlayer.I && def != null && def.objective == Objective.Boss && !def.floorBoss && !Rules.BeatMove)
                MusicPlayer.I.Play("lawgiver2");
        }

        void GoThrough(int door)
        {
            if (busy) return;
            if (def.usableInBoss && def.law != Law.None && !passedLaws.Contains(def.law)) passedLaws.Add(def.law);
            var next = nextChoices[door];
            if (def.world != World.None) { }   // rooms of another world don't count on the floor
            else if (def.objective == Objective.Boss) { floor++; inFloor = 0; }
            else inFloor++;
            if (next.world == World.Retro) retroDone = true;
            if (next.world == World.Caves) cavesDone = true;
            // the room you didn't pick goes to the back of the queue
            foreach (var other in nextChoices)
                if (other != next && unused.Remove(other)) unused.Add(other);
            unused.Remove(next);
            step++;
            StartCoroutine(EnterRoom(next, false));
        }
    }
}
