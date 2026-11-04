using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RulesShift
{
    public enum EnemyKind
    {
        Runner, Shooter, Kamikaze,
        Charger,    // winds up, then rams in a straight line (stunned if it hits a wall)
        Guard,      // a shield in front: shots from the front bounce off, go around it
        Summoner,   // keeps away and calls small minions
        Blinker     // teleports next to the hero and fires a fan of orbs
    }

    /// <summary>An extra trait of an elite (golden) enemy.</summary>
    public enum Affix { None, Swift, Volatile, Healer, Shielded }

    /// <summary>
    /// Three behaviours for the whole game; the skin (sprite set) changes per room.
    /// Reads Rules every frame: invisible in the Crypt, healed in the Garden, asleep in the Treasury…
    /// </summary>
    public partial class Enemy : MonoBehaviour, IDamageable
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        public EnemyKind kind;
        public float hp, maxHp;
        public float speed;
        public string skin;
        public bool elite;      // golden, tougher, drops more coins
        public bool noLoot;     // summoned by a boss
        public int splits;      // slimes: split in two this many more times when killed (the Slime King)
        public bool miniBoss;   // an escapee from another world: big, tough, leaves a trail of its world's colour
        Color trailColor;
        float trailTimer;

        Rigidbody2D rb;
        SpriteRenderer body, shadow;
        SpriteAnimator anim;
        Sprite[] idle, run, death;
        Light2D glow;
        Color orbColor;
        bool asleep, dead;
        float fuse = -1f, shootTimer, flash, healFlash, grace, zTimer, strafe;
        Vector2 knock;
        float baseScale = 1f, growth, thornTimer;
        Status status;

        public bool Alive => !dead;

        /// <summary>
        /// Turns this monster into an escapee from another world: bigger, much tougher, glowing in that world's colour
        /// and leaving a faint trail of it — the only hint of where it came from.
        /// </summary>
        public void MakeMiniBoss(float hpMul, float scale, Color color)
        {
            miniBoss = true;
            maxHp *= hpMul;
            hp = maxHp;
            baseScale = scale;
            speed *= 0.85f;
            trailColor = color;
            LightUtil.Point(transform, new Vector3(0f, 0.6f, 0f), color, 2.4f, 1f);
        }

        /// <summary>A frost crystal broke nearby.</summary>
        public void Freeze(float time)
        {
            if (dead || status == null) return;
            status.Freeze(time);
            Fx.Burst(Position, new Color(0.75f, 0.95f, 1f), 6, 2f, 0.4f, 1.2f);
        }
        /// <summary>Knockback from outside (the Mallet relic).</summary>
        public void Push(Vector2 v)
        {
            if (!dead) knock = v;
        }

        public float HpFraction => maxHp > 0f ? hp / maxHp : 1f;

        public Team Team => Team.Enemy;
        public Vector2 Position => (Vector2)transform.position + new Vector2(0f, 0.45f);
        public bool Asleep => asleep;

        // ---------- spawning ----------

        public static Enemy Spawn(EnemyKind kind, string skin, Vector2 feet, Transform parent, bool asleep, bool elite = false)
        {
            var go = new GameObject($"{kind}_{skin}");
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            var e = go.AddComponent<Enemy>();
            e.elite = elite;
            e.Init(kind, skin, asleep);
            return e;
        }

        static Sprite[] Frames(string skin, string action)
        {
            var a = GameAssets.I;
            if (!a) return new Sprite[0];
            var f = a.Anim($"{skin}_{action}_anim");
            if (f.Length == 0) f = a.Anim($"{skin}_anim");
            if (f.Length == 0) f = a.Anim($"goblin_{action}_anim");
            return f;
        }

        void Init(EnemyKind k, string s, bool sleeping)
        {
            kind = k;
            skin = s;
            asleep = sleeping;
            switch (k)
            {
                case EnemyKind.Runner: maxHp = 4; speed = 2.3f; break;
                case EnemyKind.Shooter: maxHp = 4; speed = 1.7f; shootTimer = Random.Range(1.2f, 2.2f); break;
                case EnemyKind.Charger: maxHp = 6; speed = 1.7f; stateTimer = Random.Range(1f, 2f); break;
                case EnemyKind.Guard: maxHp = 7; speed = 1.5f; break;
                case EnemyKind.Summoner: maxHp = 5; speed = 1.4f; shootTimer = Random.Range(2f, 3f); break;
                case EnemyKind.Blinker: maxHp = 3; speed = 1.3f; shootTimer = Random.Range(1.5f, 2.5f); break;
                default: maxHp = 2; speed = 3.7f; break;
            }
            maxHp = Mathf.Round((maxHp + Difficulty.EnemyHpBonus) * Difficulty.EnemyHpMul);
            speed *= Difficulty.EnemySpeed;
            if (elite)
            {
                maxHp = maxHp * 2f + 1f;
                speed *= 1.1f;
                baseScale = 1.25f;
            }
            hp = maxHp;
            strafe = Random.value < 0.5f ? -1f : 1f;
            grace = 0.6f;
            orbColor = s.StartsWith("sgq_") ? new Color(0.85f, 1f, 0.7f)
                     : s.Contains("necromancer") ? new Color(0.75f, 0.4f, 1f)
                     : s.Contains("imp") || s.Contains("chort") ? new Color(1f, 0.55f, 0.2f)
                     : new Color(0.45f, 1f, 0.45f);

            gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            idle = Frames(s, "idle");
            run = Frames(s, "run");
            death = GameAssets.I ? GameAssets.I.Anim(s + "_death_anim") : new Sprite[0];
            float hgt = idle.Length > 0 ? idle[0].rect.height / 16f : 1f;
            float wid = idle.Length > 0 ? idle[0].rect.width / 16f : 1f;

            var c = gameObject.AddComponent<CircleCollider2D>();
            c.radius = Mathf.Max(0.3f, wid * 0.25f);
            c.offset = new Vector2(0f, c.radius);

            var hb = new GameObject("Hurtbox");
            hb.transform.SetParent(transform, false);
            var box = hb.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(Mathf.Max(0.75f, wid * 0.6f), Mathf.Max(0.8f, hgt * 0.85f));
            box.offset = new Vector2(0f, box.size.y / 2f);
            hb.AddComponent<Hurtbox>().owner = this;

            shadow = Child("Shadow", Gfx.Shadow, -10, new Vector3(0f, 0.05f, 0f));
            body = Child("Body", idle.Length > 0 ? idle[0] : Gfx.White, 0, Vector3.zero);
            anim = body.gameObject.AddComponent<SpriteAnimator>();
            anim.target = body;
            anim.Play(idle, 8f);

            status = gameObject.AddComponent<Status>();
            status.position = () => Position;
            status.dot = TakeDot;

            glow = LightUtil.Point(transform, new Vector3(0f, 0.5f, 0f), new Color(1f, 0.2f, 0.6f), 1.8f, 1.3f);
            glow.enabled = Rules.EnemyLights;
            if (elite)
                LightUtil.Point(transform, new Vector3(0f, 0.5f, 0f), new Color(1f, 0.8f, 0.3f), 1.6f, 0.9f);

            InitExtra();
            transform.localScale = Vector3.zero;
            All.Add(this);
            if (!Fx.Anim("spawn", Position, 0.5f, 0f, 36, new Color(1f, 1f, 1f, 0.8f)))
                Fx.Burst(Position, new Color(0.8f, 0.8f, 1f, 0.8f), 8, 2f, 0.4f, 1.2f);
        }

        SpriteRenderer Child(string name, Sprite sp, int order, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = order;
            Gfx.Lit(sr);
            return sr;
        }

        void OnDestroy()
        {
            All.Remove(this);
        }

        // ---------- behaviour ----------

        void Update()
        {
            float dt = Time.deltaTime * Rules.WorldTime;   // the Time law slows the world down
            if (dt <= 0f || dead) return;
            var player = PlayerController.I;

            grace -= dt;
            flash -= dt;
            thornTimer -= dt;
            healFlash -= dt;
            float s = (baseScale + growth) * Mathf.Clamp01(1f - grace / 0.6f + 0.25f);
            transform.localScale = new Vector3(s, s, 1f);

            Visibility(player);
            glow.enabled = Rules.EnemyLights;
            if (miniBoss && !champion)
            {
                trailTimer -= dt;
                if (trailTimer <= 0f)
                {
                    trailTimer = 0.12f;
                    Fx.Burst((Vector2)transform.position + Random.insideUnitCircle * 0.4f, new Color(trailColor.r, trailColor.g, trailColor.b, 0.6f), 1, 0.4f, 0.6f, 1.4f);
                }
            }

            if (player == null || !player.Alive || grace > 0.2f)
            {
                rb.linearVelocity = knock;
                knock = Vector2.MoveTowards(knock, Vector2.zero, dt * 20f);
                return;
            }

            Vector2 to = player.Feet - (Vector2)transform.position;
            float dist = to.magnitude;
            Vector2 dir = dist > 0.001f ? to / dist : Vector2.zero;

            if (asleep)
            {
                rb.linearVelocity = Vector2.zero;
                anim.Play(idle, 3f);
                zTimer -= dt;
                if (zTimer <= 0f) { zTimer = 1.6f; Fx.Text(Position + Vector2.up * 0.7f, "z", new Color(0.8f, 0.85f, 1f), 0.8f, 1.2f); }
                if (dist < 1.3f) Wake();
                body.color = new Color(0.7f, 0.7f, 0.8f, body.color.a);
                return;
            }

            float spd = speed * Rules.EnemySpeedMul * status.SpeedMul * PlayerController.EnemyPace * Rules.WorldTime;
            Vector2 v = Vector2.zero;
            switch (kind)
            {
                case EnemyKind.Runner:
                    v = dir * spd;
                    break;
                case EnemyKind.Shooter:
                    if (dist < 3.5f) v = -dir * spd;
                    else if (dist > 6.5f) v = dir * spd;
                    else v = new Vector2(-dir.y, dir.x) * strafe * spd * 0.6f;
                    if (!status.Frozen) shootTimer -= dt;
                    telegraph = shootTimer < 0.35f && dist < 12f;   // a flash before the shot
                    if (shootTimer <= 0f && dist < 12f)
                    {
                        shootTimer = Random.Range(1.7f, 2.4f);
                        Projectile.Orb(Position, player.Position - Position, 5.5f, Difficulty.EnemyDamage, orbColor);
                        Sfx.Play("orb", 0.6f);
                    }
                    break;
                case EnemyKind.Charger: v = ChargerTick(dt, dir, dist, spd); break;
                case EnemyKind.Guard: v = dir * spd; break;
                case EnemyKind.Summoner: v = SummonerTick(dt, dir, dist, spd); break;
                case EnemyKind.Blinker: v = BlinkerTick(dt, dir, dist, spd, player); break;
                case EnemyKind.Kamikaze:
                    if (fuse < 0f)
                    {
                        v = dir * spd;
                        if (dist < 1.4f) { fuse = 0.65f; Sfx.Play("fuse"); }
                    }
                    else
                    {
                        fuse -= dt;
                        float k = Mathf.Repeat(fuse * 12f, 1f) > 0.5f ? 1f : 0f;
                        body.color = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.2f), k);
                        transform.localScale *= 1f + (0.65f - fuse) * 0.4f;
                        if (fuse <= 0f) { Explode(); return; }
                    }
                    break;
            }

            rb.linearVelocity = v + knock * Rules.WorldTime + Rules.Pull(transform.position) * 0.6f;
            knock = Vector2.MoveTowards(knock, Vector2.zero, dt * 20f);
            ExtraTick(dt, dir);

            if (kind != EnemyKind.Kamikaze && dist < 0.5f + c0Radius && grace <= 0f && !status.Frozen)
            {
                player.Hit(Difficulty.EnemyDamage + (elite || miniBoss || Charging ? 1 : 0), dir);
                if (player.Has(RelicId.Thorns) && thornTimer <= 0f && !Rules.ShotsHeal)
                {
                    thornTimer = 0.5f;
                    Hit(Mathf.Max(1f, player.Damage) * player.ThornsMul, -dir);
                    if (dead) return;
                }
            }

            anim.Play(rb.linearVelocity.sqrMagnitude > 0.05f ? run : idle, 9f);
            if (Mathf.Abs(dir.x) > 0.05f) body.flipX = dir.x < 0f;

            if (fuse < 0f)
            {
                Color c = status.Tint;
                if (elite) c *= Color.Lerp(new Color(1f, 0.85f, 0.4f), Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 5f));
                if (telegraph) c = Mathf.Repeat(Time.time * 14f, 1f) > 0.5f ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 1f, 0.85f);
                if (flash > 0f) c = new Color(1f, 0.35f, 0.35f);
                else if (healFlash > 0f) c = new Color(0.5f, 1f, 0.5f);
                body.color = new Color(c.r, c.g, c.b, body.color.a);
            }
        }

        void Visibility(PlayerController player)
        {
            float alpha = 1f;
            if (Rules.Vision > 0f && player != null)
            {
                float d = Vector2.Distance(player.Feet, transform.position);
                float vision = Rules.Vision * player.VisionMul;
                alpha = Mathf.Clamp01((vision + 0.4f - d) / 1.2f);
            }
            var c = body.color; c.a = alpha; body.color = c;
            var sc = shadow.color; sc.a = alpha; shadow.color = sc;
        }

        /// <summary>Burn / poison tick: no knockback, no sound.</summary>
        void TakeDot(float dmg)
        {
            if (dead) return;
            hp -= dmg;
            if (hp <= 0f) Die();
        }

        public bool Hit(float dmg, Vector2 dir)
        {
            if (dead) return false;
            if (asleep) Wake();
            knock = dir.normalized * 4f;

            if (Rules.ShotsHeal)
            {
                hp = Mathf.Min(hp + 1, maxHp * 3);
                growth = Mathf.Min(0.6f, growth + 0.08f);
                healFlash = 0.25f;
                Fx.Text(Position + Vector2.up * 0.8f, "+1", new Color(0.5f, 1f, 0.5f), 0.9f, 0.7f);
                Sfx.Play("heal", 0.6f);
                return true;
            }

            if (Blocks(dir)) return true;   // the Guard's shield or an elite's bubble
            hp = Rules.OneHit ? 0f : hp - dmg;
            Juice.Number(Position, dmg);
            flash = 0.12f;
            Sfx.Play("hit", 0.7f);
            if (!Fx.Anim("hit", Position, 0.5f, Random.Range(0f, 360f)))
                Fx.Burst(Position, new Color(1f, 0.9f, 0.8f), 4, 2.5f, 0.25f, 1f);
            if (hp <= 0f) Die();
            return true;
        }

        float c0Radius => transform.localScale.x * (idle.Length > 0 ? Mathf.Max(0.25f, idle[0].rect.width / 16f * 0.25f) : 0.25f);

        void Die()
        {
            dead = true;
            All.Remove(this);
            if (elite || miniBoss) Juice.HitStop(0.06f);
            if (splits > 0) Split();
            OnDeathExtra();
            if (RoomManager.I) RoomManager.I.OnEnemyKilled(false);
            if (PlayerController.I) PlayerController.I.OnEnemyDied(Position, status);
            if (!noLoot)
            {
                float drop = Rules.Inflation ? 0.9f : kind == EnemyKind.Kamikaze ? 0.15f : 0.35f;   // inflation: money everywhere
                int coins = miniBoss ? 6 : elite ? (Rules.Inflation ? 4 : 2) : Random.value < drop ? 1 : 0;
                if (coins > 0) Pickup.Coins(Position, coins);
            }
            if (death.Length > 0)
                Fx.Frames(death, transform.position, 10f, body.flipX, 0, transform.localScale.x);
            else if (!Fx.Anim("death", Position + Vector2.up * 0.2f, 1f))
                Fx.Burst(Position, new Color(0.95f, 0.95f, 0.85f), 14, 4f, 0.5f, 1.5f);
            Fx.Burst(Position, orbColor, 6, 3f, 0.4f, 1.2f);
            Sfx.Play("kill", 0.8f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.08f);
            Destroy(gameObject);
        }

        /// <summary>The Slime King and his children: two smaller slimes jump out (they don't drop coins twice).</summary>
        void Split()
        {
            string child = splits >= 2 && skin != "sgq_medium_slime" ? "sgq_medium_slime" : "sgq_slime";
            var room = RoomManager.I ? RoomManager.I.CurrentRoom : null;
            for (int i = 0; i < 2; i++)
            {
                Vector2 p = (Vector2)transform.position + new Vector2(i == 0 ? -0.6f : 0.6f, 0f);
                if (room) p = room.NearestFree(p);
                var e = Spawn(EnemyKind.Runner, child, p, transform.parent, false);
                e.splits = splits - 1;
                e.grace = 0.35f;
                e.knock = new Vector2(i == 0 ? -5f : 5f, 1f);
            }
            Fx.Text(Position + Vector2.up * 0.8f, Loc.T("fx.split"), new Color(0.8f, 1f, 0.6f), 1f, 0.8f);
        }

        /// <summary>Disappear without counting as a kill (end of Survive, boss death).</summary>
        public void Vanish()
        {
            if (dead) return;
            dead = true;
            All.Remove(this);
            Fx.Burst(Position, new Color(0.8f, 0.8f, 1f), 10, 3f, 0.5f, 1.3f);
            Destroy(gameObject);
        }

        void Explode()
        {
            dead = true;
            All.Remove(this);
            Vector2 p = Position;
            if (!Fx.Anim("boom", p, 1.5f))
                Fx.Burst(p, new Color(1f, 0.6f, 0.2f), 22, 6f, 0.5f, 2f);
            Fx.Burst(p, new Color(1f, 0.95f, 0.6f), 10, 3f, 0.3f, 2.5f);
            Sfx.Play("boom");
            if (CameraFollow.I) CameraFollow.I.Shake(0.35f);
            var player = PlayerController.I;
            if (player != null && Vector2.Distance(player.Position, p) < 1.8f)
                player.Hit(1 + Difficulty.EnemyDamage, player.Position - p);
            foreach (var e in All.ToArray())
                if (e && e != this && Vector2.Distance(e.Position, p) < 1.8f) e.Hit(2, e.Position - p);
            Destroy(gameObject);
        }

        public void Wake()
        {
            if (!asleep) return;
            asleep = false;
            body.color = Color.white;
            Fx.Text(Position + Vector2.up * 0.9f, "!", new Color(1f, 0.85f, 0.2f), 1.2f, 0.8f);
            Sfx.Play("wake", 0.8f);
        }

        public static void WakeNear(Vector2 p, float radius)
        {
            if (!Rules.Stealth) return;
            foreach (var e in All.ToArray())
                if (e && e.asleep && Vector2.Distance(e.Position, p) < radius) e.Wake();
        }

        public static void VanishAll()
        {
            foreach (var e in All.ToArray()) if (e) e.Vanish();
            All.Clear();
        }
    }
}
