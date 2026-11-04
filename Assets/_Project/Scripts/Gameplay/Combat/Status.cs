using UnityEngine;

namespace RulesShift
{
    /// <summary>Burn, poison, slow and freeze on an enemy or the boss (from relics).</summary>
    public class Status : MonoBehaviour
    {
        public System.Action<float> dot;          // silent damage over time
        public System.Func<Vector2> position;
        public bool resistFreeze;                 // the boss is only slowed

        float burn, poisonTime, slow, freeze, mark, tick = 0.5f, sparkTimer, poisonFxTimer;
        int poison;
        GameObject fireFx;

        public bool Burning => burn > 0f;
        public bool Poisoned => poison > 0 && poisonTime > 0f;
        public bool Frozen => freeze > 0f && !resistFreeze;
        public bool Chilled => slow > 0f || freeze > 0f;
        public bool Marked => mark > 0f;

        public float SpeedMul => Frozen ? 0f : (slow > 0f || freeze > 0f) ? 0.55f : 1f;

        /// <summary>Colour to multiply the body with (white = no effect).</summary>
        public Color Tint
        {
            get
            {
                if (Frozen) return new Color(0.55f, 0.85f, 1f);
                if (slow > 0f || freeze > 0f) return new Color(0.75f, 0.9f, 1f);
                if (Burning) return Mathf.Repeat(Time.time * 8f, 1f) > 0.5f ? new Color(1f, 0.7f, 0.45f) : Color.white;
                if (Poisoned) return new Color(0.7f, 1f, 0.6f);
                return Color.white;
            }
        }

        public static Status Of(IDamageable d) => d is Component c ? c.GetComponent<Status>() : null;

        public void Burn(float time) => burn = Mathf.Max(burn, time);
        public void Poison(int stacks = 1)
        {
            poison = Mathf.Min(6, poison + stacks);
            poisonTime = 4f;
            if (poisonFxTimer <= 0f)
            {
                poisonFxTimer = 1f;
                Fx.Anim("poison", position != null ? position() : (Vector2)transform.position, 0.35f);
            }
        }
        public void Slow(float time) => slow = Mathf.Max(slow, time);
        public void Freeze(float time) => freeze = Mathf.Max(freeze, time);
        /// <summary>Mark relic: the target takes more damage for a while.</summary>
        public void Mark(float time) => mark = Mathf.Max(mark, time);

        public void Clear() { burn = poisonTime = slow = freeze = mark = 0f; poison = 0; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            burn -= dt;
            slow -= dt;
            freeze -= dt;
            mark -= dt;
            poisonTime -= dt;
            poisonFxTimer -= dt;
            if (poisonTime <= 0f) poison = 0;

            // burning enemies carry a small looping flame (pixel effect), sparks otherwise
            if (Burning && !fireFx) fireFx = Fx.Loop("fire", transform, new Vector2(0f, 0.55f), 0.35f, 3);
            else if (!Burning && fireFx) { Destroy(fireFx); fireFx = null; }

            Vector2 p = position != null ? position() : (Vector2)transform.position;
            sparkTimer -= dt;
            if (sparkTimer <= 0f && Marked && !Burning && !Poisoned)
            {
                sparkTimer = 0.25f;
                Fx.Burst(p + Vector2.up * 0.7f, new Color(1f, 0.75f, 0.3f), 1, 0.6f, 0.3f, 1f);
            }
            if (sparkTimer <= 0f && ((Burning && !fireFx) || Poisoned))
            {
                sparkTimer = 0.18f;
                Fx.Burst(p + Random.insideUnitCircle * 0.25f, Burning ? new Color(1f, 0.55f, 0.15f) : new Color(0.5f, 1f, 0.35f), 2, 1.2f, 0.35f, 1f);
            }

            tick -= dt;
            if (tick > 0f) return;
            tick = 0.5f;
            if (Rules.ShotsHeal) return;   // the Garden: nothing can hurt them
            float dmg = (Burning ? 0.5f : 0f) + (Poisoned ? 0.15f * poison : 0f);
            if (dmg > 0f) dot?.Invoke(dmg);
        }
    }
}
