using UnityEngine;

namespace RulesShift
{
    /// <summary>Active items: one slot, used with Q (gamepad: right bumper), recharged by cleared rooms.</summary>
    public enum ActiveId { None, Bomb, Freeze, Teleport, Potion, Barrier, Storm, Overclock }

    public class ActiveDef
    {
        public ActiveId id;
        public string key;
        public string sprite;   // "r:123" = Raven icon
        public int charge;      // rooms to clear before the next use
        public Color color;

        public string Name => Loc.T("active." + key + ".name");
        public string Desc => Loc.F("active." + key + ".desc", charge);
    }

    public static class Actives
    {
        public static readonly ActiveDef[] All =
        {
            A(ActiveId.Bomb, "bomb", "r:778", 1, new Color(1f, 0.6f, 0.3f)),
            A(ActiveId.Freeze, "freeze", "r:787", 3, new Color(0.6f, 0.9f, 1f)),
            A(ActiveId.Teleport, "teleport", "r:301", 2, new Color(0.8f, 0.6f, 1f)),
            A(ActiveId.Potion, "potion", "r:14", 4, new Color(1f, 0.45f, 0.5f)),
            A(ActiveId.Barrier, "barrier", "r:2148", 2, new Color(0.55f, 0.8f, 1f)),
            A(ActiveId.Storm, "storm", "r:776", 3, new Color(0.85f, 0.6f, 1f)),
            A(ActiveId.Overclock, "overclock", "r:766", 3, new Color(1f, 0.7f, 0.3f)),
        };

        static ActiveDef A(ActiveId id, string key, string sprite, int charge, Color color) =>
            new ActiveDef { id = id, key = key, sprite = sprite, charge = charge, color = color };

        public static ActiveDef Get(ActiveId id)
        {
            foreach (var a in All) if (a.id == id) return a;
            return null;
        }

        /// <summary>A random active item other than the one the hero holds.</summary>
        public static ActiveDef Random(PlayerController p)
        {
            var list = new System.Collections.Generic.List<ActiveDef>();
            foreach (var a in All) if (p == null || a.id != p.ActiveItem) list.Add(a);
            return list.Count > 0 ? RunRandom.Pick(list) : null;
        }
    }
}
