using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>One reward card in a chest: a flask (stat) or a relic (effect, can form a combo).</summary>
    public class Upgrade
    {
        public string id, key, sprite;
        public RelicDef relic;   // null = flask
        public ActiveDef active; // an active item (Q) instead
        public System.Func<PlayerController, bool> available;
        public System.Action<PlayerController> apply;

        public bool IsRelic => relic != null;
        public bool IsActive => active != null;
        /// <summary>Stat flask whose look is shuffled every run (unknown until drunk once).</summary>
        public bool IsPotion => relic == null && active == null && Upgrades.IsPotionKey(key);
        public bool Known => !IsPotion || Upgrades.IsKnown(key);
        public string title => relic != null ? relic.Name : active != null ? active.Name : Known ? Loc.T("up." + key + ".name") : Loc.T("up.unknown.name");
        public string desc => relic != null ? relic.Desc : active != null ? active.Desc : Known ? Loc.T("up." + key + ".desc") : Loc.T("up.unknown.desc");
        public string SpriteName => IsPotion ? Upgrades.SpriteFor(key) : sprite;

        public static Upgrade FromRelic(RelicDef r) => new Upgrade
        {
            id = "relic." + r.key, key = r.key, sprite = r.sprite, relic = r,
            available = p => !p.Has(r.id) && Relics.Fits(r, p),
            apply = p => p.AddRelic(r.id)
        };

        public static Upgrade FromActive(ActiveDef a) => new Upgrade
        {
            id = "active." + a.key, key = a.key, sprite = a.sprite, active = a,
            available = p => p.ActiveItem != a.id,
            apply = p => p.SetActive(a.id)
        };
    }

    /// <summary>Flasks from the pack + relics. After a cleared room the chest offers two of them (three with Luck).</summary>
    public static class Upgrades
    {
        public static readonly List<Upgrade> All = new List<Upgrade>
        {
            F("heart", "flask_red", p => p.maxHalves < 12, p => { p.maxHalves += 2; p.Heal(2); }),
            F("heal", "flask_big_red", p => p.halves < p.maxHalves, p => p.Heal(99)),
            F("speed", "flask_blue", p => p.moveSpeed < 7.5f, p => p.moveSpeed *= 1.1f),
            F("damage", "flask_green", p => p.damage < 4f, p => p.damage *= 1.2f),
            F("rate", "flask_yellow", p => p.fireRate < 6f, p => p.fireRate *= 1.12f),
            F("multi", "flask_big_green", p => p.shots < 3 && p.CanShootExtras, p => { p.shots += 1; p.damage *= 0.85f; }),
            F("pierce", "flask_big_blue", p => p.pierce < 2 && !p.Hero.Melee, p => p.pierce += 1),
            F("velocity", "flask_big_yellow", p => p.arrowSpeed < 22f && !p.Hero.Melee, p => p.arrowSpeed *= 1.2f),
            F("vampire", "r:742", p => p.vampireEvery == 0 || p.vampireEvery > 4, p => p.vampireEvery = p.vampireEvery == 0 ? 8 : 4),
            F("shield", "r:2113", p => !p.hasShield, p => p.hasShield = true),
            F("luck", "r:668", p => !p.luck, p => p.luck = true),
            F("bomb", "r:787", p => !p.bombOnHurt, p => p.bombOnHurt = true),
        };

        // ---------------- unknown flasks (shuffled every run) ----------------

        static readonly string[] PotionKeys = { "heart", "heal", "speed", "damage", "rate", "multi", "pierce", "velocity" };
        static readonly string[] PotionSprites =
            { "flask_red", "flask_big_red", "flask_blue", "flask_green", "flask_yellow", "flask_big_green", "flask_big_blue", "flask_big_yellow" };
        static readonly Dictionary<string, string> runSprite = new Dictionary<string, string>();
        static readonly HashSet<string> known = new HashSet<string>();

        /// <summary>New run: every stat flask gets a random look and is unknown again.</summary>
        public static void NewRun()
        {
            var looks = new List<string>(PotionSprites);
            RunRandom.Shuffle(looks);
            runSprite.Clear();
            for (int i = 0; i < PotionKeys.Length; i++) runSprite[PotionKeys[i]] = looks[i];
            known.Clear();
        }

        public static bool IsPotionKey(string key) => System.Array.IndexOf(PotionKeys, key) >= 0;
        public static bool IsKnown(string key) => known.Contains(key);
        public static void Identify(Upgrade u) { if (u != null && u.IsPotion) known.Add(u.key); }
        public static string SpriteFor(string key) => runSprite.TryGetValue(key, out var s) ? s : "flask_red";

        /// <summary>One random flask the player can use (shops).</summary>
        public static Upgrade RandomFlask(PlayerController p)
        {
            var list = new List<Upgrade>();
            foreach (var u in All) if (p == null || u.available(p)) list.Add(u);
            return list.Count > 0 ? RunRandom.Pick(list) : null;
        }

        /// <summary>One random relic the player doesn't have (shops, shrines).</summary>
        public static RelicDef RandomRelic(PlayerController p)
        {
            var pool = new List<RelicDef>();
            foreach (var r in Relics.All) if (p == null || (!p.Has(r.id) && Relics.Fits(r, p))) pool.Add(r);
            if (pool.Count == 0) return null;
            RunRandom.Shuffle(pool);
            return PickRelic(pool, p);
        }

        static Upgrade F(string key, string sprite, System.Func<PlayerController, bool> ok, System.Action<PlayerController> apply) =>
            new Upgrade { id = key, key = key, sprite = sprite, available = ok, apply = apply };

        /// <summary>
        /// Chest offer. Each card is a relic with chance relicChance (while relics are left), otherwise a flask.
        /// guaranteeRelic: at least one card is a relic. Relics that complete a combo come up a bit more often.
        /// </summary>
        public static Upgrade[] Pick(int count, PlayerController p, float relicChance = 0.4f, bool guaranteeRelic = false)
        {
            var flasks = new List<Upgrade>();
            foreach (var u in All) if (p == null || u.available(p)) flasks.Add(u);
            RunRandom.Shuffle(flasks);

            var relics = new List<RelicDef>();
            foreach (var r in Relics.All) if (p == null || (!p.Has(r.id) && Relics.Fits(r, p))) relics.Add(r);
            RunRandom.Shuffle(relics);

            var result = new List<Upgrade>();
            bool activeOffered = false;
            for (int i = 0; i < count; i++)
            {
                // now and then an active item instead (one per chest at most)
                if (!activeOffered && p != null && !(guaranteeRelic && i == 0) && RunRandom.Value < 0.12f)
                {
                    var a = Actives.Random(p);
                    if (a != null) { result.Add(Upgrade.FromActive(a)); activeOffered = true; continue; }
                }
                bool wantRelic = relics.Count > 0 && (RunRandom.Value < relicChance || (guaranteeRelic && i == 0) || flasks.Count == 0);
                if (wantRelic)
                {
                    var r = PickRelic(relics, p);
                    relics.Remove(r);
                    result.Add(Upgrade.FromRelic(r));
                }
                else if (flasks.Count > 0)
                {
                    result.Add(flasks[0]);
                    flasks.RemoveAt(0);
                }
            }
            RunRandom.Shuffle(result);
            return result.ToArray();
        }

        static RelicDef PickRelic(List<RelicDef> pool, PlayerController p)
        {
            if (p != null && RunRandom.Value < 0.35f)
                foreach (var r in pool)
                    if (Relics.Partner(r.id, p.RelicSet) != null) return r;
            return pool[0];
        }
    }
}
