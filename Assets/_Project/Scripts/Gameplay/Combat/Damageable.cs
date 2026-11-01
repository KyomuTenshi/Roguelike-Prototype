using UnityEngine;

namespace RulesShift
{
    /// <summary>Who a hit belongs to: arrows hurt enemies, orbs hurt the hero.</summary>
    public enum Team { Player, Enemy }

    public interface IDamageable
    {
        Team Team { get; }
        bool Alive { get; }
        Vector2 Position { get; }
        /// <summary>Returns true if the hit landed.</summary>
        bool Hit(float damage, Vector2 direction);
    }

    /// <summary>Trigger collider over the body; projectiles look for it.</summary>
    public class Hurtbox : MonoBehaviour
    {
        public IDamageable owner;
    }
}
