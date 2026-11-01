using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>Katanas circling the hero (Orbiting Katana relic, Blade Dance combo).</summary>
    public class OrbitBlades : MonoBehaviour
    {
        const float Radius = 1.35f, Speed = 240f, HitRadius = 0.55f, HitCooldown = 0.45f;

        PlayerController owner;
        readonly List<SpriteRenderer> blades = new List<SpriteRenderer>();
        readonly Dictionary<IDamageable, float> lastHit = new Dictionary<IDamageable, float>();
        float angle;

        public static OrbitBlades Ensure(PlayerController p)
        {
            var o = p.GetComponentInChildren<OrbitBlades>();
            if (o) return o;
            var go = new GameObject("OrbitBlades");
            go.transform.SetParent(p.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            o = go.AddComponent<OrbitBlades>();
            o.owner = p;
            return o;
        }

        public void SetCount(int n)
        {
            while (blades.Count < n)
            {
                var go = new GameObject("Blade");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GameAssets.I ? GameAssets.I.D("weapon_katana") : Gfx.White;
                sr.sortingOrder = 2;
                Gfx.Unlit(sr);
                blades.Add(sr);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || owner == null) return;
            bool active = owner.Alive && !owner.frozen;
            angle = Mathf.Repeat(angle + Speed * dt, 360f);
            for (int i = 0; i < blades.Count; i++)
            {
                float a = (angle + i * 360f / blades.Count) * Mathf.Deg2Rad;
                Vector2 off = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;
                var t = blades[i].transform;
                t.localPosition = off;
                // the katana is drawn pointing up: lay it along the circle
                t.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                blades[i].enabled = active;
                if (active && !Rules.ShotsHeal) HitAround((Vector2)transform.position + off, off.normalized);
            }
        }

        void HitAround(Vector2 p, Vector2 dir)
        {
            float now = Time.time;
            foreach (var e in Enemy.All.ToArray())
                if (e && e.Alive && Vector2.Distance(e.Position, p) < HitRadius) Strike(e, dir, now);
            var b = Bosses.Current;
            if (b != null && Vector2.Distance(b.Position, p) < HitRadius + 0.6f) Strike(b, dir, now);
        }

        void Strike(IDamageable target, Vector2 dir, float now)
        {
            if (lastHit.TryGetValue(target, out float t) && now - t < HitCooldown) return;
            lastHit[target] = now;
            float dmg = owner.Damage * 0.8f;
            bool crit = owner.HasCombo(ComboId.Blades) && Random.value < 0.25f;
            if (crit)
            {
                dmg *= 3f;
                Fx.Text(target.Position + Vector2.up * 0.8f, Loc.T("hud.crit"), new Color(1f, 0.85f, 0.3f), 0.7f, 0.6f);
            }
            target.Hit(dmg, dir);
        }
    }
}
