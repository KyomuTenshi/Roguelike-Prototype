using UnityEngine;

namespace RulesShift
{
    /// <summary>What the boss bar needs to know about any boss.</summary>
    public interface IBoss : IDamageable
    {
        string BossName { get; }
        float Health01 { get; }
        string StatusLine { get; }
        Color StatusColor { get; }
    }

    /// <summary>The boss of the current room: the Lawgiver or a floor boss.</summary>
    public static class Bosses
    {
        /// <summary>The room's boss died (reset when a room is built).</summary>
        public static bool Defeated;

        /// <summary>A boss is alive in the room.</summary>
        public static bool Fighting => Current != null;

        public static IBoss Current
        {
            get
            {
                if (Boss.I && Boss.I.Alive) return Boss.I;
                if (FloorBoss.I && FloorBoss.I.Alive) return FloorBoss.I;
                return null;
            }
        }
    }
}
