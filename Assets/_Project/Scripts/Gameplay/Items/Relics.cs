using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>Permanent items for the run. Two matching relics form a combo (like synergies in Isaac).</summary>
    public enum RelicId
    {
        Fire, Frost, Venom, Ricochet, Homing, Split, Blast, Crit,
        Thorns, Orbit, Blood, Dash, Berserk, Soul, Lawkeeper, Codex
    }

    public enum ComboId { Napalm, Plague, Hail, Smart, Blades, Souls, Judge, Ram, Frenzy }

    public class RelicDef
    {
        public RelicId id;
        public string key;      // relic.<key>.name / .desc
        public string sprite;   // "r:123" = Raven icon, "c:" = classic tileset
        public Color color;

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

        static RelicDef R(RelicId id, string key, string sprite, Color color) =>
            new RelicDef { id = id, key = key, sprite = sprite, color = color };

        static ComboDef C(ComboId id, string key, RelicId a, RelicId b) =>
            new ComboDef { id = id, key = key, a = a, b = b };
    }
}
