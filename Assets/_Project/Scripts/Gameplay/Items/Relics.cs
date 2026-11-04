using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>Permanent items for the run. Two matching relics form a combo (like synergies in Isaac).</summary>
    public enum RelicId
    {
        Fire, Frost, Venom, Ricochet, Homing, Split, Blast, Crit,
        Thorns, Orbit, Blood, Dash, Berserk, Soul, Lawkeeper, Codex,
        // shots and stats
        Whetstone, Quiver, Metronome, Feather, Longbow, Giant, Glass, HeavyHeart, IronSkin, LuckyCoin,
        Scope, Brawler, Momentum, Focus, FullHeart, LastStand, Execute, Piercer, Mallet, Chain,
        Fifth, Mark, Stun, Ghost, Echo, Rear, Wings, Burst, Nova, BattlePixie,
        // defence and healing
        Regen, Bandage, OneUp, Armor, Shell, MirrorShield, Mantle, Fang, Container, Rage, Adrenaline, Smoke,
        // coins
        Magnet, Piggy, Midas, Coupon, Greed, Map, Clover, Investor,
        // laws, time and the System
        Lawbreaker, NeonHeart, NightVision, Skates, Compass, Stopwatch, Glitch,
        BlinkBoots, Phase, Shockwave, CursedEye, BloodOath, Hourglass, BombBag, Thunder, Cartridge, CrystalHeart, Ember
    }

    public enum ComboId
    {
        Napalm, Plague, Hail, Smart, Blades, Souls, Judge, Ram, Frenzy,
        Sharpshooter, Overcharge, Storm, Inferno, Shatter, BulletHell, Starfall, FairyTale, Juggernaut, GlassPhoenix,
        Bloodlust, Fortress, BerserkerRage, CashFlow, GoldenTouch, Bargain, TreasureHunter, Lawyer, NightHunter, SpeedDemon,
        GhostShip, PhantomDash, Earthquake, Edge, TimeStop, Demolition, PixelPerfect, EchoChamber, FifthElement, Healer,
        SpikedFortress, VenomCloud, NeonDream
    }

    public class RelicDef
    {
        public RelicId id;
        public string key;      // relic.<key>.name / .desc
        public string sprite;   // "r:123" = Raven icon, "c:" = classic tileset
        public Color color;
        public bool ranged;     // only for heroes who shoot (Elf, Lizard)
        public bool dash;       // only useful with a dash or a blink

        public string Name => Loc.T("relic." + key + ".name");
        public string Desc => Loc.T("relic." + key + ".desc");
    }

    public class ComboDef
    {
        public ComboId id;
        public string key;      // combo.<key>.name / .desc
        public RelicId a, b;

        public string Name => Loc.T("combo." + key + ".name");
        public string Desc => Loc.T("combo." + key + ".desc");
    }

    public static class Relics
    {
        public static readonly RelicDef[] All =
        {
            R(RelicId.Fire, "fire", "r:993", new Color(1f, 0.55f, 0.2f)),
            R(RelicId.Frost, "frost", "r:1009", new Color(0.55f, 0.85f, 1f)),
            R(RelicId.Venom, "venom", "r:121", new Color(0.55f, 1f, 0.35f)),
            R(RelicId.Ricochet, "ricochet", "r:1501", new Color(0.9f, 0.85f, 0.7f)),
            R(RelicId.Homing, "homing", "r:2155", new Color(0.7f, 0.9f, 1f)),
            R(RelicId.Split, "split", "r:1462", new Color(0.85f, 0.85f, 0.9f)),
            R(RelicId.Blast, "blast", "r:771", new Color(1f, 0.6f, 0.25f)),
            R(RelicId.Crit, "crit", "r:1521", new Color(1f, 0.85f, 0.3f)),
            R(RelicId.Thorns, "thorns", "r:1506", new Color(0.85f, 0.8f, 0.75f)),
            R(RelicId.Orbit, "orbit", "r:1463", new Color(0.8f, 0.9f, 1f)),
            R(RelicId.Blood, "blood", "r:659", new Color(1f, 0.3f, 0.35f)),
            R(RelicId.Dash, "dash", "r:2161", new Color(0.6f, 1f, 0.9f)),
            R(RelicId.Berserk, "berserk", "r:1466", new Color(1f, 0.4f, 0.3f)),
            R(RelicId.Soul, "soul", "r:862", new Color(0.6f, 1f, 1f)),
            R(RelicId.Lawkeeper, "lawkeeper", "r:124", new Color(1f, 0.85f, 0.55f)),
            R(RelicId.Codex, "codex", "r:43", new Color(1f, 0.9f, 0.4f)),

            R(RelicId.Whetstone, "whetstone", "r:218", new Color(0.8f, 0.8f, 0.85f)),
            R(RelicId.Quiver, "quiver", "r:1514", new Color(0.9f, 0.7f, 0.45f), ranged: true),
            R(RelicId.Metronome, "metronome", "r:945", new Color(0.55f, 0.75f, 1f)),
            R(RelicId.Feather, "feather", "r:1793", new Color(1f, 0.75f, 0.5f)),
            R(RelicId.Longbow, "longbow", "r:1515", new Color(0.85f, 0.65f, 0.4f), ranged: true),
            R(RelicId.Giant, "giant", "r:216", new Color(0.85f, 0.85f, 0.95f), ranged: true),
            R(RelicId.Glass, "glass", "r:214", new Color(0.75f, 0.9f, 1f)),
            R(RelicId.HeavyHeart, "heavyheart", "r:1042", new Color(1f, 0.6f, 0.4f)),
            R(RelicId.IronSkin, "ironskin", "r:1921", new Color(0.75f, 0.8f, 0.8f)),
            R(RelicId.LuckyCoin, "luckycoin", "r:131", new Color(1f, 0.85f, 0.3f)),
            R(RelicId.Scope, "scope", "r:719", new Color(0.7f, 0.9f, 1f), ranged: true),
            R(RelicId.Brawler, "brawler", "r:663", new Color(1f, 0.7f, 0.5f)),
            R(RelicId.Momentum, "momentum", "r:1029", new Color(1f, 0.9f, 0.4f)),
            R(RelicId.Focus, "focus", "r:990", new Color(1f, 0.85f, 0.5f)),
            R(RelicId.FullHeart, "fullheart", "r:762", new Color(1f, 0.5f, 0.45f)),
            R(RelicId.LastStand, "laststand", "r:864", new Color(1f, 0.35f, 0.35f)),
            R(RelicId.Execute, "execute", "r:730", new Color(1f, 0.4f, 0.4f)),
            R(RelicId.Piercer, "piercer", "r:2153", new Color(0.6f, 0.85f, 1f), ranged: true),
            R(RelicId.Mallet, "mallet", "r:126", new Color(0.8f, 0.75f, 0.7f)),
            R(RelicId.Chain, "chain", "r:1028", new Color(1f, 0.95f, 0.4f)),
            R(RelicId.Fifth, "fifth", "r:986", new Color(1f, 0.85f, 0.35f), ranged: true),
            R(RelicId.Mark, "mark", "r:989", new Color(1f, 0.75f, 0.35f)),
            R(RelicId.Stun, "stun", "r:656", new Color(1f, 0.9f, 0.5f)),
            R(RelicId.Ghost, "ghost", "r:750", new Color(0.7f, 0.7f, 0.85f), ranged: true),
            R(RelicId.Echo, "echo", "r:793", new Color(0.85f, 0.9f, 1f), ranged: true),
            R(RelicId.Rear, "rear", "r:980", new Color(1f, 0.85f, 0.45f), ranged: true),
            R(RelicId.Wings, "wings", "r:669", new Color(0.55f, 0.75f, 1f), ranged: true),
            R(RelicId.Burst, "burst", "r:982", new Color(1f, 0.85f, 0.4f), ranged: true),
            R(RelicId.Nova, "nova", "r:769", new Color(1f, 0.7f, 0.35f)),
            R(RelicId.BattlePixie, "battlepixie", "r:1074", new Color(1f, 0.6f, 0.95f)),
            R(RelicId.Regen, "regen", "r:865", new Color(0.55f, 1f, 0.55f)),
            R(RelicId.Bandage, "bandage", "r:670", new Color(1f, 0.95f, 0.85f)),
            R(RelicId.OneUp, "oneup", "r:11", new Color(0.6f, 1f, 0.6f)),
            R(RelicId.Armor, "armor", "r:1905", new Color(0.8f, 0.8f, 0.75f)),
            R(RelicId.Shell, "shell", "r:860", new Color(1f, 0.7f, 0.4f)),
            R(RelicId.MirrorShield, "mirrorshield", "r:856", new Color(0.55f, 0.8f, 1f)),
            R(RelicId.Mantle, "mantle", "r:665", new Color(0.9f, 0.95f, 1f)),
            R(RelicId.Fang, "fang", "r:1057", new Color(1f, 0.35f, 0.4f)),
            R(RelicId.Container, "container", "r:661", new Color(1f, 0.5f, 0.7f)),
            R(RelicId.Rage, "rage", "r:1059", new Color(1f, 0.35f, 0.3f)),
            R(RelicId.Adrenaline, "adrenaline", "r:757", new Color(1f, 0.7f, 0.35f)),
            R(RelicId.Smoke, "smoke", "r:861", new Color(0.75f, 0.75f, 0.8f)),
            R(RelicId.Magnet, "magnet", "r:116", new Color(0.8f, 0.8f, 0.9f)),
            R(RelicId.Piggy, "piggy", "r:158", new Color(1f, 0.75f, 0.6f)),
            R(RelicId.Midas, "midas", "r:984", new Color(1f, 0.85f, 0.3f)),
            R(RelicId.Coupon, "coupon", "r:983", new Color(1f, 0.9f, 0.5f)),
            R(RelicId.Greed, "greed", "r:160", new Color(1f, 0.8f, 0.3f)),
            R(RelicId.Map, "map", "r:106", new Color(0.95f, 0.85f, 0.6f)),
            R(RelicId.Clover, "clover", "r:668", new Color(0.5f, 1f, 0.5f)),
            R(RelicId.Investor, "investor", "r:130", new Color(0.85f, 0.85f, 0.9f)),
            R(RelicId.Lawbreaker, "lawbreaker", "r:662", new Color(1f, 0.55f, 0.45f)),
            R(RelicId.NeonHeart, "neonheart", "r:741", new Color(1f, 0.35f, 0.9f)),
            R(RelicId.NightVision, "nightvision", "r:85", new Color(0.6f, 0.75f, 1f)),
            R(RelicId.Skates, "skates", "r:667", new Color(0.6f, 0.85f, 1f)),
            R(RelicId.Compass, "compass", "r:293", new Color(0.75f, 0.6f, 1f)),
            R(RelicId.Stopwatch, "stopwatch", "r:971", new Color(0.6f, 1f, 0.7f)),
            R(RelicId.Glitch, "glitch", "r:851", new Color(0.45f, 1f, 0.55f)),
            R(RelicId.BlinkBoots, "blinkboots", "r:2164", new Color(0.55f, 0.75f, 1f), dash: true),
            R(RelicId.Phase, "phase", "r:739", new Color(0.75f, 0.55f, 1f), dash: true),
            R(RelicId.Shockwave, "shockwave", "r:1032", new Color(1f, 0.9f, 0.45f), dash: true),
            R(RelicId.CursedEye, "cursedeye", "r:1049", new Color(0.8f, 0.45f, 1f)),
            R(RelicId.BloodOath, "bloodoath", "r:748", new Color(1f, 0.3f, 0.35f)),
            R(RelicId.Hourglass, "hourglass", "r:1039", new Color(1f, 0.9f, 0.5f)),
            R(RelicId.BombBag, "bombbag", "r:778", new Color(1f, 0.6f, 0.3f), ranged: true),
            R(RelicId.Thunder, "thunder", "r:1031", new Color(1f, 0.95f, 0.45f)),
            R(RelicId.Cartridge, "cartridge", "r:9", new Color(0.7f, 1f, 0.55f)),
            R(RelicId.CrystalHeart, "crystalheart", "r:1014", new Color(0.6f, 0.9f, 1f)),
            R(RelicId.Ember, "ember", "r:994", new Color(1f, 0.6f, 0.3f)),
        };

        public static readonly ComboDef[] Combos =
        {
            C(ComboId.Napalm, "napalm", RelicId.Fire, RelicId.Blast),
            C(ComboId.Plague, "plague", RelicId.Fire, RelicId.Venom),
            C(ComboId.Hail, "hail", RelicId.Frost, RelicId.Split),
            C(ComboId.Smart, "smart", RelicId.Ricochet, RelicId.Homing),
            C(ComboId.Blades, "blades", RelicId.Orbit, RelicId.Crit),
            C(ComboId.Souls, "souls", RelicId.Soul, RelicId.Blast),
            C(ComboId.Judge, "judge", RelicId.Lawkeeper, RelicId.Codex),
            C(ComboId.Ram, "ram", RelicId.Dash, RelicId.Thorns),
            C(ComboId.Frenzy, "frenzy", RelicId.Blood, RelicId.Berserk),
            C(ComboId.Sharpshooter, "sharpshooter", RelicId.Scope, RelicId.Longbow),
            C(ComboId.Overcharge, "overcharge", RelicId.Metronome, RelicId.Burst),
            C(ComboId.Storm, "storm", RelicId.Chain, RelicId.Thunder),
            C(ComboId.Inferno, "inferno", RelicId.Fire, RelicId.Ember),
            C(ComboId.Shatter, "shatter", RelicId.Frost, RelicId.CrystalHeart),
            C(ComboId.BulletHell, "bullethell", RelicId.Rear, RelicId.Wings),
            C(ComboId.Starfall, "starfall", RelicId.Nova, RelicId.Split),
            C(ComboId.FairyTale, "fairytale", RelicId.BattlePixie, RelicId.Homing),
            C(ComboId.Juggernaut, "juggernaut", RelicId.HeavyHeart, RelicId.Armor),
            C(ComboId.GlassPhoenix, "glassphoenix", RelicId.Glass, RelicId.OneUp),
            C(ComboId.Bloodlust, "bloodlust", RelicId.Fang, RelicId.BloodOath),
            C(ComboId.Fortress, "fortress", RelicId.IronSkin, RelicId.MirrorShield),
            C(ComboId.BerserkerRage, "berserkerrage", RelicId.Rage, RelicId.Berserk),
            C(ComboId.CashFlow, "cashflow", RelicId.Piggy, RelicId.Investor),
            C(ComboId.GoldenTouch, "goldentouch", RelicId.Midas, RelicId.Magnet),
            C(ComboId.Bargain, "bargain", RelicId.Coupon, RelicId.Greed),
            C(ComboId.TreasureHunter, "treasurehunter", RelicId.Map, RelicId.Clover),
            C(ComboId.Lawyer, "lawyer", RelicId.Lawbreaker, RelicId.Lawkeeper),
            C(ComboId.NightHunter, "nighthunter", RelicId.NightVision, RelicId.Mark),
            C(ComboId.SpeedDemon, "speeddemon", RelicId.Feather, RelicId.Adrenaline),
            C(ComboId.GhostShip, "ghostship", RelicId.Ghost, RelicId.Ricochet),
            C(ComboId.PhantomDash, "phantomdash", RelicId.Phase, RelicId.BlinkBoots),
            C(ComboId.Earthquake, "earthquake", RelicId.Shockwave, RelicId.Mallet),
            C(ComboId.Edge, "edge", RelicId.Execute, RelicId.Crit),
            C(ComboId.TimeStop, "timestop", RelicId.Stopwatch, RelicId.Hourglass),
            C(ComboId.Demolition, "demolition", RelicId.BombBag, RelicId.Blast),
            C(ComboId.PixelPerfect, "pixelperfect", RelicId.Cartridge, RelicId.Glitch),
            C(ComboId.EchoChamber, "echochamber", RelicId.Echo, RelicId.Quiver),
            C(ComboId.FifthElement, "fifthelement", RelicId.Fifth, RelicId.Giant),
            C(ComboId.Healer, "healer", RelicId.Regen, RelicId.Bandage),
            C(ComboId.SpikedFortress, "spikedfortress", RelicId.Shell, RelicId.Thorns),
            C(ComboId.VenomCloud, "venomcloud", RelicId.Smoke, RelicId.Venom),
            C(ComboId.NeonDream, "neondream", RelicId.NeonHeart, RelicId.Metronome),
        };

        public static RelicDef Get(RelicId id) => All[(int)id];
        public static ComboDef Get(ComboId id) => Combos[(int)id];

        /// <summary>Owned relic that would form a combo with this one (null if none).</summary>
        public static RelicDef Partner(RelicId id, ICollection<RelicId> owned)
        {
            foreach (var c in Combos)
            {
                if (c.a == id && owned.Contains(c.b)) return Get(c.b);
                if (c.b == id && owned.Contains(c.a)) return Get(c.a);
            }
            return null;
        }

        static RelicDef R(RelicId id, string key, string sprite, Color color, bool ranged = false, bool dash = false) =>
            new RelicDef { id = id, key = key, sprite = sprite, color = color, ranged = ranged, dash = dash };

        /// <summary>The relic is worth offering to this hero (shooting relics for shooters, dash relics for dashers).</summary>
        public static bool Fits(RelicDef r, PlayerController p) =>
            (int)r.id < Achievements.UnlockedRelics && (p == null || ((!r.ranged || p.CanShootExtras) && (!r.dash || p.CanDash)));

        static ComboDef C(ComboId id, string key, RelicId a, RelicId b) =>
            new ComboDef { id = id, key = key, a = a, b = b };
    }
}
