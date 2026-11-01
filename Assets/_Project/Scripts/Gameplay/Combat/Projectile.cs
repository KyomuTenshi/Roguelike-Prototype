using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>What relics add to a hero shot.</summary>
    public struct ShotMods
    {
        public float burn, slow, freeze;   // seconds
        public int poison;                 // stacks per hit
        public int bounces;                // ricochets off walls
        public float homing;               // turn speed, degrees per second
        public int split;                  // shards on the first hit (0 = none)
        public float blast;                // explosion radius on impact (0 = none)
        public bool napalm;                // explosions set enemies on fire
        public float crit;                 // chance of triple damage
    }

    /// <summary>Arrow, orb or soul. Moves itself and checks overlaps every frame (no physics bodies needed).</summary>
    public class Projectile : MonoBehaviour
    {
        static readonly List<Projectile> all = new List<Projectile>();
        static readonly List<Collider2D> results = new List<Collider2D>();
        static readonly RaycastHit2D[] rayHits = new RaycastHit2D[8];
        static ContactFilter2D filter, solidFilter;
        static bool filterReady;

        Team team;
        Vector2 velocity;
        float damage;
        int pierce;
        float radius;
        float life;
        bool wakes, shard, crit, soul;
        ShotMods mods;
        readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
        Color sparkColor;
        bool spin, turnVisual;
        Transform visual;

        /// <summary>The hero's shot: arrow, throwing axe, knife or magic orb (see HeroDef).</summary>
        public static Projectile Hero(Vector2 pos, Vector2 dir, HeroDef hero, float speed, float damage, int pierce, ShotMods mods = default, float size = 1f)
        {
            var p = Create("Shot", pos, dir, speed, damage, Team.Player, (hero.projectile == null ? 0.22f : 0.18f) * size, 1.8f);
            var s = hero.projectile != null && GameAssets.I ? GameAssets.I.D(hero.projectile) : null;
            var vis = p.Visual(s ? s : Gfx.Orb, s ? Color.white : hero.projColor, s != null);
            vis.transform.localScale = Vector3.one * (s ? 1f : 1.2f) * size;
            Gfx.Unlit(vis);
            p.pierce = pierce;
            p.wakes = true;
            p.spin = hero.spin;
            p.visual = vis.transform;
            p.mods = mods;
            p.sparkColor = s ? new Color(1f, 0.9f, 0.7f) : hero.projColor;
            if (mods.bounces > 0) p.life += 0.8f;

            // relic tints: the shot shows what it carries
            if (mods.crit > 0f && Random.value < mods.crit)
            {
                p.crit = true;
                p.damage *= 3f;
                vis.color = new Color(1f, 0.85f, 0.3f);
                vis.transform.localScale *= 1.25f;
            }
            else if (mods.freeze > 0f || mods.slow > 0f) vis.color = new Color(0.65f, 0.9f, 1f);
            else if (mods.burn > 0f) vis.color = new Color(1f, 0.7f, 0.45f);
            else if (mods.poison > 0) vis.color = new Color(0.7f, 1f, 0.55f);

            if (Rules.EnemyLights || hero.projGlow)
                LightUtil.Point(p.transform, Vector3.zero, hero.projGlow ? hero.projColor : new Color(0.4f, 1f, 1f), 1.4f, 1.2f);
            return p;
        }

        /// <summary>A soul leaving a fallen enemy: flies to the closest one (Soul Skull relic).</summary>
        public static Projectile Soul(Vector2 pos, float damage, bool explode)
        {
            Vector2 dir = Quaternion.Euler(0f, 0f, Random.Range(-40f, 40f)) * Vector2.up;
            var p = Create("Soul", pos, dir, 7f, damage, Team.Player, 0.24f, 3f);
            var sr = p.Visual(Gfx.Orb, new Color(0.6f, 1f, 1f), false);
            sr.transform.localScale = Vector3.one * 1.1f;
            Gfx.Unlit(sr);
            p.sparkColor = new Color(0.6f, 1f, 1f);
            p.mods = new ShotMods { homing = 540f, blast = explode ? 1.3f : 0f };
            p.soul = true;
            return p;
        }

        public static Projectile Orb(Vector2 pos, Vector2 dir, float speed, float damage, Color color, float size = 1f)
        {
            var p = Create("Orb", pos, dir, speed, damage, Team.Enemy, 0.2f * size, 4f);
            var sr = p.Visual(Gfx.Orb, color, false);
            sr.transform.localScale = Vector3.one * size;
            if (Fx.Unlit) sr.sharedMaterial = Fx.Unlit;   // visible in the dark
            p.sparkColor = color;
            if (Rules.EnemyLights || Rules.Current == Law.Crypt)
                LightUtil.Point(p.transform, Vector3.zero, color, Rules.EnemyLights ? 1.6f : 1f, 1.3f);
            return p;
        }

        static Projectile Create(string name, Vector2 pos, Vector2 dir, float speed, float damage, Team team, float radius, float life)
        {
            var go = new GameObject(name);
            if (Fx.Root) go.transform.SetParent(Fx.Root, false);
            go.transform.position = pos;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            var p = go.AddComponent<Projectile>();
            p.team = team;
            p.velocity = dir.normalized * speed;
            p.damage = damage;
            p.radius = radius;
            p.life = life;
            return p;
        }

        /// <summary>Child renderer centred on the projectile. pointsUp: sprite drawn pointing up (0x72 weapons).</summary>
        SpriteRenderer Visual(Sprite s, Color c, bool pointsUp)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.color = c;
            sr.sortingOrder = 20;
            if (s)
            {
                float pivotFrac = s.pivot.y / Mathf.Max(1f, s.rect.height);
                float len = s.rect.height / s.pixelsPerUnit;
                float shift = (0.5f - pivotFrac) * len;
                if (pointsUp)
                {
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                    go.transform.localPosition = new Vector3(-shift, 0f, 0f);
                }
                else go.transform.localPosition = new Vector3(0f, -shift, 0f);
            }
            return sr;
        }

        void OnDestroy() => all.Remove(this);

        void Awake()
        {
            all.Add(this);
            if (!filterReady)
            {
                filter = ContactFilter2D.noFilter;
                filter.useTriggers = true;
                solidFilter = ContactFilter2D.noFilter;
                solidFilter.useTriggers = false;
                filterReady = true;
            }
        }

        void Start()
        {
            if (team == Team.Player && wakes && !soul) Enemy.WakeNear(transform.position, 4.5f);
            turnVisual = mods.homing > 0f || mods.bounces > 0;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            life -= dt;
            if (life <= 0f)
            {
                if (soul && mods.blast > 0f) Explode(transform.position);
                Destroy(gameObject);
                return;
            }

            if (mods.homing > 0f) Steer(dt);
            if (turnVisual && !spin)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            if (spin && visual) visual.RotateAround(transform.position, Vector3.forward, -900f * dt);

            Vector2 from = transform.position;
            Vector2 step = velocity * dt;
            int sub = Mathf.Max(1, Mathf.CeilToInt(step.magnitude / 0.25f));
            Vector2 prev = from;
            for (int i = 1; i <= sub; i++)
            {
                Vector2 p = from + step * (i / (float)sub);
                transform.position = p;
                if (Check(p, prev)) return;
                prev = p;
            }
            var room = RoomManager.I ? RoomManager.I.CurrentRoom : null;
            if (room && !room.Contains(transform.position, 2f)) Destroy(gameObject);
        }

        void Steer(float dt)
        {
            Vector2 pos = transform.position;
            IDamageable best = null;
            float bestD = 7f;
            foreach (var e in Enemy.All)
            {
                if (!e || !e.Alive || alreadyHit.Contains(e)) continue;
                float d = Vector2.Distance(e.Position, pos);
                if (d < bestD) { bestD = d; best = e; }
            }
            var b = Bosses.Current;
            if (b != null && !alreadyHit.Contains(b) && Vector2.Distance(b.Position, pos) < bestD) best = b;
            if (best == null) return;
            Vector2 want = best.Position - pos;
            float speed = velocity.magnitude;
            Vector2 dir = Vector3.RotateTowards(velocity.normalized, want.normalized, mods.homing * Mathf.Deg2Rad * dt, 0f);
            velocity = dir * speed;
        }

        bool Check(Vector2 p, Vector2 prev)
        {
            results.Clear();
            Physics2D.OverlapCircle(p, radius, filter, results);
            foreach (var col in results)
            {
                if (!col) continue;
                var hb = col.GetComponent<Hurtbox>();
                if (hb != null)
                {
                    var target = hb.owner;
                    if (target == null || !target.Alive || target.Team == team || alreadyHit.Contains(target)) continue;
                    alreadyHit.Add(target);
                    HitTarget(target);
                    if (pierce-- <= 0) { Impact(p); return true; }
                    continue;
                }
                if (col.isTrigger) continue;
                var rb = col.attachedRigidbody;
                if (rb == null || rb.bodyType == RigidbodyType2D.Static)
                {
                    if (mods.bounces > 0 && team == Team.Player)
                    {
                        mods.bounces--;
                        velocity = Vector2.Reflect(velocity, WallNormal(prev, p));
                        transform.position = prev;
                        alreadyHit.Clear();
                        Fx.Burst(prev, sparkColor, 3, 2f, 0.2f, 1f);
                        return true;
                    }
                    Impact(p);
                    return true;
                }
            }
            return false;
        }

        void HitTarget(IDamageable target)
        {
            if (crit) Fx.Text(target.Position + Vector2.up * 0.8f, Loc.T("hud.crit"), new Color(1f, 0.85f, 0.3f), 0.7f, 0.6f);
            target.Hit(damage, velocity.normalized);
            if (team != Team.Player || Rules.ShotsHeal) return;

            ApplyStatus(target, mods);
            if (mods.split > 0 && !shard)
                SplitShards(target, velocity.normalized, velocity.magnitude * 0.8f, damage, mods, sparkColor);
        }

        // ---------------- shared relic effects (shots and the knight's sword) ----------------

        /// <summary>Burn / slow / freeze / poison from the hero's relics.</summary>
        /// <summary>Every shot in flight disappears (cutscenes).</summary>
        public static void ClearAll()
        {
            foreach (var p in all.ToArray()) if (p) Destroy(p.gameObject);
            all.Clear();
        }

        public static void ApplyStatus(IDamageable target, ShotMods mods)
        {
            if (Rules.ShotsHeal) return;
            var st = Status.Of(target);
            if (!st) return;
            if (mods.burn > 0f) st.Burn(mods.burn);
            if (mods.slow > 0f) st.Slow(mods.slow);
            if (mods.freeze > 0f) st.Freeze(mods.freeze);
            if (mods.poison > 0) st.Poison(mods.poison);
        }

        /// <summary>Splitter relic: shards fly on from the target.</summary>
        public static void SplitShards(IDamageable target, Vector2 dir, float speed, float damage, ShotMods mods, Color color)
        {
            if (Rules.ShotsHeal || mods.split <= 0) return;
            float fan = mods.split >= 3 ? 40f : 35f;
            for (int i = 0; i < mods.split; i++)
            {
                float off = mods.split == 1 ? 0f : Mathf.Lerp(-fan, fan, i / (float)(mods.split - 1));
                Vector2 d = Quaternion.Euler(0f, 0f, off) * dir;
                var p = Create("Shard", target.Position + d * 0.6f, d, Mathf.Max(6f, speed), Mathf.Max(0.5f, damage * 0.5f), Team.Player, 0.16f, 0.7f);
                bool ice = mods.freeze > 0f;
                var sr = p.Visual(Gfx.Orb, ice ? new Color(0.6f, 0.9f, 1f) : color, false);
                sr.transform.localScale = Vector3.one * 0.7f;
                Gfx.Unlit(sr);
                p.sparkColor = sr.color;
                p.shard = true;
                p.wakes = true;
                var m = mods;
                m.split = 0;
                m.blast = 0f;
                m.homing = 0f;
                m.bounces = 0;
                p.mods = m;
                p.alreadyHit.Add(target);
            }
        }

        /// <summary>
        /// Knight's swing: enemy shots inside the arc turn around and become the hero's,
        /// carrying the relic effects. Returns how many were reflected.
        /// </summary>
        public static int Reflect(Vector2 center, Vector2 aim, float range, float halfArc, float damage, ShotMods mods)
        {
            int n = 0;
            foreach (var p in all.ToArray())
            {
                if (!p || p.team != Team.Enemy) continue;
                Vector2 to = (Vector2)p.transform.position - center;
                if (to.magnitude > range || Vector2.Angle(aim, to) > halfArc) continue;
                p.team = Team.Player;
                float speed = Mathf.Max(9f, p.velocity.magnitude * 1.6f);
                p.velocity = (aim.normalized * 0.7f + to.normalized * 0.3f).normalized * speed;
                p.damage = Mathf.Max(p.damage, damage);
                var m = mods;
                m.split = 0;
                p.mods = m;
                p.life = 1.6f;
                p.wakes = true;
                p.turnVisual = true;
                p.alreadyHit.Clear();
                p.sparkColor = new Color(1f, 0.9f, 0.5f);
                var sr = p.GetComponentInChildren<SpriteRenderer>();
                if (sr) sr.color = new Color(1f, 0.9f, 0.45f);
                if (!Fx.Anim("reflect", p.transform.position, 0.5f))
                    Fx.Burst(p.transform.position, new Color(1f, 0.95f, 0.6f), 6, 3f, 0.25f, 1.2f);
                n++;
            }
            return n;
        }

        static Vector2 WallNormal(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 0.0001f) return Vector2.up;
            int n = Physics2D.Raycast(from - d / len * 0.05f, d / len, solidFilter, rayHits, len + 0.5f);
            for (int i = 0; i < n; i++)
            {
                var c = rayHits[i].collider;
                if (!c || c.isTrigger) continue;
                var rb = c.attachedRigidbody;
                if (rb == null || rb.bodyType == RigidbodyType2D.Static) return rayHits[i].normal;
            }
            // fallback: bounce along the dominant axis
            return Mathf.Abs(d.x) > Mathf.Abs(d.y) ? new Vector2(-Mathf.Sign(d.x), 0f) : new Vector2(0f, -Mathf.Sign(d.y));
        }

        void Impact(Vector2 p)
        {
            if (team != Team.Player || !Fx.Anim("spark", p, 0.25f, Random.Range(0f, 360f)))
                Fx.Burst(p, sparkColor, 5, 2.5f, 0.25f, 1f);
            if (team == Team.Player && RoomManager.I) RoomManager.I.OnWallHit(p);
            if (team == Team.Player && mods.blast > 0f && !Rules.ShotsHeal) Explode(p);
            if (team == Team.Player && wakes && !soul) Enemy.WakeNear(p, 3.5f);
            Destroy(gameObject);
        }

        void Explode(Vector2 p) => Explosion(p, mods.blast, Mathf.Max(0.5f, damage * 0.6f), mods.napalm);

        /// <summary>Hero explosion (Gunpowder relic, Napalm, the Sorceress' full charge).</summary>
        public static void Explosion(Vector2 p, float r, float dmg, bool napalm)
        {
            if (Rules.ShotsHeal) return;
            bool anim = napalm ? Fx.Anim("bigboom", p, r * 0.5f) : Fx.Anim("boom", p, r);
            if (!anim) Fx.Burst(p, new Color(1f, 0.6f, 0.2f), napalm ? 22 : 14, napalm ? 5f : 4f, 0.4f, 1.8f);
            Fx.Burst(p, new Color(1f, 0.95f, 0.6f), 6, 2f, 0.25f, 2f);
            if (RoomManager.I) RoomManager.I.OnWallHit(p, r);
            Sfx.Play("boom", 0.35f, 1.3f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.08f);
            foreach (var e in Enemy.All.ToArray())
            {
                if (!e || !e.Alive || Vector2.Distance(e.Position, p) > r) continue;
                e.Hit(dmg, e.Position - p);
                if (napalm) { var st = e.GetComponent<Status>(); if (st) st.Burn(3f); }
            }
            var b = Bosses.Current;
            if (b != null && Vector2.Distance(b.Position, p) < r + 0.6f)
            {
                b.Hit(dmg, b.Position - p);
                if (napalm) { var st = Status.Of(b); if (st) st.Burn(3f); }
            }
            Enemy.WakeNear(p, 4.5f);
        }
    }
}
