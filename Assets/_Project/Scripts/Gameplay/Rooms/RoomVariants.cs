using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// The extra room types behind the doors: the Challenge (three waves, a relic at the end), the Ambush
    /// (quiet until you step into the middle), the hidden Cache (three chests, one of them bites)
    /// and the System's gacha terminal (coins for a random reward).
    /// </summary>
    public partial class RoomManager
    {
        const int ChallengeWaves = 3;
        int wave;
        bool ambushArmed;
        float ambushTimer, waveDelay;
        int gambleCost, gambleUses;

        /// <summary>Laws that work with a fight to the last enemy (no Pacifist: nobody can die there).</summary>
        static readonly Law[] VariantLaws = { Law.Crypt, Law.Noir, Law.Ice, Law.Mirror, Law.Retro, Law.Lava, Law.Upside, Law.Neon, Law.Stealth, Law.Gravity, Law.Bounce, Law.Inflation };

        void ResetRoomVariant()
        {
            wave = 0;
            ambushArmed = false;
            ambushTimer = 7f;
            waveDelay = 0f;
            gambleUses = 0;
            gambleCost = 5 + floor * 2;
        }

        /// <summary>Puts one of the new room types behind one of the doors (never over a shop, a shrine or a portal).</summary>
        void AddRoomVariant(RoomDef[] picks)
        {
            int slot = -1;
            for (int i = 0; i < picks.Length; i++)
                if (!picks[i].Special && !picks[i].worldEntry && picks[i].objective != Objective.Boss) { slot = slot < 0 || RunRandom.Value < 0.5f ? i : slot; }
            if (slot < 0) return;
            float r = RunRandom.Value;
            if (r < 0.3f) picks[slot] = CombatVariant(picks[slot], Objective.Challenge);
            else if (r < 0.6f) picks[slot] = CombatVariant(picks[slot], Objective.Ambush);
            else if (cfg.Has(Curse.Ascetic)) picks[slot] = CombatVariant(picks[slot], Objective.Challenge);   // no chests for the Ascetic
            else if (r < 0.82f) picks[slot] = Special(Objective.Treasure);
            else picks[slot] = Special(Objective.Gamble);
        }

        RoomDef CombatVariant(RoomDef from, Objective o)
        {
            var d = from.Clone();
            d.objective = o;
            d.inPool = false;
            if (System.Array.IndexOf(VariantLaws, d.law) < 0) d.law = RunRandom.Pick(VariantLaws);
            d.id = from.id + "_" + o.ToString().ToLowerInvariant();
            if (d.runners + d.shooters + d.kamikazes < 4)
            {
                d.runners = Mathf.Max(d.runners, 3);
                d.shooters = Mathf.Max(d.shooters, 1);
                d.kamikazes = Mathf.Max(d.kamikazes, 1);
            }
            return d;
        }

        string VariantTick(float dt)
        {
            switch (def.objective)
            {
                case Objective.Challenge:
                    if (!spawned || cleared) return cleared ? Loc.T("obj.chooseDoor") : "";
                    if (Enemy.All.Count == 0)
                    {
                        if (wave >= ChallengeWaves)
                        {
                            Hud.Toast(Loc.T("toast.challenge"), new Color(1f, 0.7f, 0.3f));
                            if (player) Fx.Anim("complete", player.Position + Vector2.up * 2.2f, 0.3f, 0f, 40);
                            Achievements.Unlock("challenge");
                            Clear(true, true);
                        }
                        else if ((waveDelay += dt) > 1.2f) { waveDelay = 0f; NextWave(); }
                    }
                    return cleared ? Loc.T("obj.chooseDoor") : Loc.F("obj.wave", Mathf.Max(1, wave), ChallengeWaves, Enemy.All.Count);

                case Objective.Ambush:
                    if (!spawned || cleared) return cleared ? Loc.T("obj.chooseDoor") : "";
                    if (ambushArmed)
                    {
                        ambushTimer -= dt;
                        if (Vector2.Distance(player.Feet, room.Center) < 3.2f || ambushTimer <= 0f) SpringAmbush();
                        return Loc.T("obj.quiet");
                    }
                    if (Enemy.All.Count == 0) Clear(true);
                    return cleared ? Loc.T("obj.chooseDoor") : Loc.F("obj.kill", Enemy.All.Count);

                case Objective.Treasure:
                    return Loc.T("obj.treasure");

                case Objective.Gamble:
                    return Loc.F("obj.gamble", gambleCost, coins);
            }
            return "";
        }

        // ---------------- Challenge ----------------

        void NextWave()
        {
            wave++;
            Hud.Toast(Loc.F("toast.wave", wave, ChallengeWaves), new Color(1f, 0.7f, 0.3f));
            Sfx.Play("law", 0.5f, 1.2f);
            float mul = 0.8f + 0.2f * wave;
            int r = Mathf.Max(1, Mathf.RoundToInt(Scaled(def.runners) * mul));
            int s = Mathf.RoundToInt(Scaled(def.shooters) * mul);
            int k = Mathf.RoundToInt(Scaled(def.kamikazes) * mul);
            var points = new List<Vector2>(room.EnemySpawns);
            RunRandom.Shuffle(points);
            int n = 0;
            for (int i = 0; i < r; i++) SpawnAt(EnemyKind.Runner, def.runnerSkin, points[n++ % points.Count]);
            for (int i = 0; i < s; i++) SpawnAt(EnemyKind.Shooter, def.shooterSkin, points[n++ % points.Count]);
            for (int i = 0; i < k; i++) SpawnAt(EnemyKind.Kamikaze, def.kamikazeSkin, points[n++ % points.Count]);
        }

        Enemy SpawnAt(EnemyKind kind, string skin, Vector2 p)
        {
            bool elite = RunRandom.Value < eliteChance;
            kind = Mutate(kind);
            var e = Enemy.Spawn(kind, SkinFor(kind, KindSkin(kind, skin)), room.NearestFree(p + Random.insideUnitCircle * 0.3f), room.Content, false, elite);
            if (elite) Hud.Hint("elite");
            return e;
        }

        /// <summary>
        /// Some runners become Chargers or Guards, some shooters Summoners or Blinkers — more often deeper down
        /// and in the other worlds.
        /// </summary>
        EnemyKind Mutate(EnemyKind kind)
        {
            if (def == null || def.objective == Objective.Boss) return kind;
            float chance = Mathf.Min(0.45f, 0.14f + 0.1f * floor + (def.world != World.None ? 0.08f : 0f));
            if (kind == EnemyKind.Runner && RunRandom.Value < chance) return RunRandom.Value < 0.5f ? EnemyKind.Charger : EnemyKind.Guard;
            if (kind == EnemyKind.Shooter && RunRandom.Value < chance) return RunRandom.Value < 0.5f ? EnemyKind.Summoner : EnemyKind.Blinker;
            return kind;
        }

        /// <summary>The new kinds have their own monsters on the main path; the worlds keep their own skins.</summary>
        string KindSkin(EnemyKind kind, string skin)
        {
            if (RetroRoom || (def != null && def.world == World.Caves)) return skin;
            switch (kind)
            {
                case EnemyKind.Charger: return "masked_orc";
                case EnemyKind.Guard: return "orc_warrior";
                case EnemyKind.Summoner: return "necromancer";
                case EnemyKind.Blinker: return "imp";
                default: return skin;
            }
        }

        // ---------------- Ambush ----------------

        void SpringAmbush()
        {
            ambushArmed = false;
            int total = Mathf.Clamp(2 * (Scaled(def.runners) + Scaled(def.shooters) + Scaled(def.kamikazes)), 8, 16);
            if (CameraFollow.I) CameraFollow.I.Shake(0.3f);
            Sfx.Play("law", 0.7f, 0.8f);
            Hud.Toast(Loc.T("toast.ambush"), new Color(1f, 0.4f, 0.35f));
            Vector2 c = player.Feet;
            float start = RunRandom.Range(0f, 360f);
            for (int i = 0; i < total; i++)
            {
                float a = (start + i * 360f / total) * Mathf.Deg2Rad;
                float rad = i % 2 == 0 ? 4.8f : 6.2f;
                Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                p.x = Mathf.Clamp(p.x, 1f, room.W - 1f);
                p.y = Mathf.Clamp(p.y, 1f, room.H - 1f);
                var kind = i % 5 == 4 && def.kamikazes > 0 ? EnemyKind.Kamikaze : i % 3 == 2 && def.shooters > 0 ? EnemyKind.Shooter : EnemyKind.Runner;
                string skin = kind == EnemyKind.Runner ? def.runnerSkin : kind == EnemyKind.Shooter ? def.shooterSkin : def.kamikazeSkin;
                SpawnAt(kind, skin, p);
            }
        }

        // ---------------- the Cache ----------------

        void SetupTreasure()
        {
            int mimic = RunRandom.Range(0, 3);
            int coinsChest = (mimic + 1 + RunRandom.Range(0, 2)) % 3;
            Vector2 c = room.Center + Vector2.up * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = room.NearestFree(c + new Vector2((i - 1) * 4f, 0f));
                int which = i;
                TreasureChest chest = null;
                chest = TreasureChest.Spawn(room, p, false, () =>
                {
                    if (which == mimic) { if (chest) Destroy(chest.gameObject); SpawnMimic(p); }
                    else if (which == coinsChest) Pickup.Coins(p + Vector2.up * 0.5f, 6 + floor * 2);
                    else OfferUpgrade(false);
                });
            }
            Hud.Hint("treasure");
        }

        void SpawnMimic(Vector2 p)
        {
            var a = GameAssets.I;
            string skin = a && a.HasAnim("chest_mimic_open_anim") ? "chest_mimic_open" : "big_zombie";
            var e = Enemy.Spawn(EnemyKind.Runner, skin, p, room.Content, false, true);
            e.maxHp *= 1.5f;
            e.hp = e.maxHp;
            e.speed *= 1.3f;
            Hud.Toast(Loc.T("toast.mimic"), new Color(1f, 0.45f, 0.35f));
            Achievements.Unlock("mimic");
            Sfx.Play("wake", 1f, 0.7f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.2f);
        }

        // ---------------- the gacha terminal ----------------

        void SetupGamble()
        {
            Vector2 spot = room.ItemSpots.Count > 0 ? room.ItemSpots[0] : room.Center;
            GachaTerminal.Create(room, spot - new Vector2(0f, 0.3f), TryGamble);
            Hud.Hint("gamble");
        }

        /// <summary>One pull: pay, then a random outcome (nothing, coins, a heart, a flask, a relic, or a bug).</summary>
        bool TryGamble(Vector2 at)
        {
            if (coins < gambleCost)
            {
                Hud.Toast(Loc.T("toast.noCoins"), new Color(1f, 0.6f, 0.5f));
                Sfx.Play("button", 0.5f, 0.6f);
                return false;
            }
            AddCoins(-gambleCost);
            gambleUses++;
            gambleCost += 2 + floor;
            Sfx.Play("coin", 0.7f, 0.8f);
            float r = RunRandom.Value;
            Color c;
            string key;
            if (r < 0.3f) { key = "gacha.nothing"; c = UiKit.Dim; Fx.Anim("question", at + Vector2.up * 2.6f, 0.4f, 0f, 40); }
            else if (r < 0.55f) { key = "gacha.coins"; c = UiKit.Gold; Pickup.Coins(at + Vector2.up * 0.6f, RunRandom.Range(3, 10 + floor * 3)); }
            else if (r < 0.7f) { key = "gacha.heart"; c = new Color(1f, 0.45f, 0.5f); Pickup.Heart(at + Vector2.down * 0.6f); Pickup.Heart(at + Vector2.down * 0.6f); }
            else if (r < 0.82f) { key = "gacha.flask"; c = new Color(0.6f, 0.9f, 1f); OfferUpgrade(false); }
            else if (r < 0.9f) { key = "gacha.relic"; c = new Color(1f, 0.85f, 0.3f); OfferUpgrade(true); Sfx.Play("win", 0.5f); Achievements.Unlock("jackpot"); Fx.Anim("bonus", at + Vector2.up * 2.8f, 0.3f, 0f, 40); }
            else
            {
                key = "gacha.bug"; c = new Color(0.4f, 1f, 0.5f);
                for (int i = 0; i < 2 + floor; i++)
                    SpawnAt(EnemyKind.Runner, RetroRoom ? "sgq_slime" : "goblin", room.NearestFree(at + Random.insideUnitCircle.normalized * 3f));
            }
            Hud.Toast(Loc.T(key), c);
            Fx.Burst(at + Vector2.up, c, 14, 3f, 0.5f, 1.4f);
            return true;
        }
    }
}
