using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RulesShift
{
    /// <summary>Special rooms (shop, shrine, fountain), lava and buttons, and the interactive things of rooms (breakables, spikes, levers, NPCs).</summary>
    public partial class RoomManager
    {
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
            Npc.Attach(keeper, "merchant", UiKit.Gold, 1.6f);

            for (int i = 0; i < spots.Count; i++)
            {
                Vector2 at = spots[i];
                switch (i % 3)
                {
                    case 0:
                        var r = Upgrades.RandomRelic(player);
                        if (r == null) goto case 2;
                        ShopItem.Create(room, at, Spr(r.sprite), ShopItem.Cost.Coins, Price(24 + 8 * floor), p =>
                        {
                            p.AddRelic(r.id);
                            Hud.Toast(r.Name + "!", r.color);
                            Hud.Hint("relic");
                            if (r.id == RelicId.Dash) Hud.Hint("dash");
                        });
                        break;
                    case 1:
                        var act = RunRandom.Value < 0.3f ? Actives.Random(player) : null;
                        if (act != null)
                        {
                            ShopItem.Create(room, at, Spr(act.sprite), ShopItem.Cost.Coins, Price(16 + 5 * floor), p =>
                            {
                                p.SetActive(act.id);
                                Hud.Toast(act.Name + "!", act.color);
                            });
                            break;
                        }
                        var u = Upgrades.RandomFlask(player);
                        if (u == null) goto case 2;
                        ShopItem.Create(room, at, Spr(u.SpriteName), ShopItem.Cost.Coins, Price(10 + 3 * floor), p =>
                        {
                            bool unknown = !u.Known;
                            u.apply(p);
                            Upgrades.Identify(u);
                            Hud.Toast(unknown ? Loc.F("toast.identified", u.title) : u.title + "!", new Color(1f, 0.9f, 0.5f));
                        });
                        break;
                    case 2:
                        ShopItem.Create(room, at, Spr("ui_heart_full"), ShopItem.Cost.Coins, Price(8 + 2 * floor), p =>
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
                if (buttons.TrueForAll(x => x.pressed)) Clear(RollChest());
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
                // Neon always gets a track with a measured beat (the fastpaced pack), even in a boss fight
                bool neon = law == Law.Neon || extraLaw == Law.Neon;
                if (neon) { if (!NeonPlaying) MusicPlayer.I.Play(NeonMusic()); }
                else if (def.objective != Objective.Boss) MusicPlayer.I.Play(LawMusic(law));
                else MusicPlayer.I.Play(BossMusic());
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

        // ---------------- interactive rooms ----------------

        /// <summary>Things to break, spike traps with a lever, hidden caches and people to talk to.</summary>
        void Populate(RoomDef d)
        {
            if (room == null || room.Theme == Theme.Modern) return;
            var rnd = new System.Random(cfg.seed ^ (step * 4241 + 17) ^ ((int)d.law * 31));
            bool combat = d.Combat || d.objective == Objective.FindKey;
            if (combat)
            {
                PlaceBreakables(rnd, 2 + rnd.Next(3), false);
                if (room.Theme == Theme.Cave) PlaceBreakables(rnd, 2 + rnd.Next(2), true);
                bool spikes = d.law != Law.Lava && d.law != Law.Neon && d.law != Law.Retro && d.objective != Objective.FindKey && rnd.NextDouble() < 0.4;
                if (spikes) spikes = PlaceSpikes(rnd);
                if (spikes || rnd.NextDouble() < 0.3) PlaceLever(rnd, spikes);
            }

            // people stuck in the game
            if (d.objective == Objective.Fountain) { if (rnd.NextDouble() < 0.6) PlaceNpc("angel", "angel", new Color(0.85f, 0.95f, 1f), null); }
            else if (d.worldEntry && rnd.NextDouble() > 0.7) { }   // the world's local is not always home
            else if (d.worldEntry && d.world == World.Retro)
                PlaceNpc("beta", "pumpkin_dude", new Color(1f, 0.75f, 0.4f), () =>
                {
                    player.maxHalves = Mathf.Min(PlayerController.MaxHalvesCap, player.maxHalves + 2);
                    player.Heal(2);
                    Hud.Toast(Loc.T("toast.maxHeart"), new Color(1f, 0.45f, 0.5f));
                    Sfx.Play("heal");
                });
            else if (d.worldEntry && d.world == World.Caves)
                PlaceNpc("goro", "dwarf_m", new Color(0.95f, 0.8f, 0.55f), () =>
                {
                    player.Heal(2);
                    AddCoins(8);
                    Sfx.Play("coin");
                });
            else if (combat && d.world == World.None && step > 0 && rnd.NextDouble() < 0.2)
            {
                // a stuck player, now and then, each once a run
                bool mio = !npcsMet.Contains("mio"), doc = !npcsMet.Contains("doc");
                if (mio && (!doc || rnd.NextDouble() < 0.5))
                {
                    npcsMet.Add("mio");
                    PlaceNpc("mio", "elf_f", new Color(0.7f, 1f, 0.75f), () => { AddCoins(12); Sfx.Play("coin"); });
                }
                else if (doc)
                {
                    npcsMet.Add("doc");
                    PlaceNpc("doc", "doc", new Color(0.75f, 0.9f, 1f), () => { player.Heal(99); Fx.Anim("heal", player.Position, 0.5f); Sfx.Play("heal"); });
                }
            }
        }

        /// <summary>A free cell away from the hero's spawn, the monsters' spawns, the doors and the crack.</summary>
        bool GoodSpot(Vector2Int c)
        {
            if (!room.IsFree(c.x, c.y)) return false;
            Vector2 p = new Vector2(c.x + 0.5f, c.y + 0.3f);
            if (Vector2.Distance(p, room.PlayerSpawn) < 3f) return false;
            foreach (var e in room.EnemySpawns) if (Vector2.Distance(p, e) < 1.3f) return false;
            for (int i = 0; i < room.DoorCount; i++)
                if (Mathf.Abs(room.DoorPos(i).x - p.x) < 2.6f && c.y >= room.H - 3) return false;
            if (room.HasSecret && Vector2.Distance(p, room.SecretPos) < 2.6f) return false;
            if (room.KeyChest.HasValue && Vector2.Distance(p, room.KeyChest.Value) < 1.6f) return false;
            return true;
        }

        Vector2Int? FindSpot(System.Random rnd, bool nearWall)
        {
            for (int t = 0; t < 80; t++)
            {
                int x, y;
                if (nearWall)
                {
                    int side = rnd.Next(4);
                    x = side == 0 ? 0 : side == 1 ? room.W - 1 : rnd.Next(room.W);
                    y = side == 2 ? 0 : side == 3 ? room.H - 1 : rnd.Next(room.H);
                }
                else { x = 2 + rnd.Next(Mathf.Max(1, room.W - 4)); y = 2 + rnd.Next(Mathf.Max(1, room.H - 4)); }
                var c = new Vector2Int(x, y);
                if (GoodSpot(c)) return c;
            }
            return null;
        }

        void PlaceBreakables(System.Random rnd, int count, bool env)
        {
            for (int i = 0; i < count; i++)
            {
                var c = FindSpot(rnd, !env);
                if (c.HasValue) room.MakeBreakable(c.Value, rnd, env);
            }
        }

        /// <summary>A small field of floor spikes that rise one after another.</summary>
        bool PlaceSpikes(System.Random rnd)
        {
            var center = FindSpot(rnd, false);
            if (!center.HasValue) return false;
            bool across = rnd.NextDouble() < 0.5;
            int n = 3 + rnd.Next(3), made = 0;
            for (int i = 0; i < n; i++)
            {
                var c = center.Value + (across ? new Vector2Int(i - n / 2, 0) : new Vector2Int(0, i - n / 2));
                if (!GoodSpot(c)) continue;
                if (Spikes.Create(room, c, i * 0.18f)) made++;
            }
            return made > 0;
        }

        /// <summary>A lever by the wall: it switches the spikes off, or opens a hidden cache.</summary>
        void PlaceLever(System.Random rnd, bool spikes)
        {
            var c = FindSpot(rnd, true);
            if (!c.HasValue) return;
            var at = new Vector2(c.Value.x + 0.5f, c.Value.y);
            var thisRoom = room;
            Lever.Create(room, c.Value, () =>
            {
                if (room != thisRoom) return;
                if (spikes)
                {
                    foreach (var s in Spikes.All.ToArray()) if (s) s.Disable();
                    Hud.Toast(Loc.T("toast.spikesOff"), new Color(1f, 0.85f, 0.4f));
                }
                else
                {
                    Vector2 chest = room.NearestFree(at + (at.x < room.W / 2f ? Vector2.right : Vector2.left) * 2f);
                    TreasureChest.Spawn(room, chest, false, () => Pickup.Coins(chest + Vector2.up * 0.5f, 5 + floor * 2));
                    Hud.Toast(Loc.T("toast.cache"), new Color(1f, 0.85f, 0.4f));
                    Sfx.Play("door");
                }
            });
        }

        void PlaceNpc(string id, string skin, Color color, System.Action gift)
        {
            Vector2 spot = room.NearestFree(new Vector2(2.5f, room.H - 3.5f));
            Npc.Create(room, id, skin, new Vector2(spot.x, spot.y - 0.3f), color, gift);
        }
    }
}
