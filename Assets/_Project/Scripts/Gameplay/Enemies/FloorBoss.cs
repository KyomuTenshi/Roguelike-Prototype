using UnityEngine;

namespace RulesShift
{
    public enum FloorBossKind { Ogre, Zombie }

    /// <summary>
    /// Bosses at the end of floors 1 and 2.
    /// Ogre: chases, telegraphs a charge, rushes and slams the ground (ring of orbs), calls goblins.
    /// Rotting Giant: slow, spits fans of rot, raises tiny exploding zombies, rings when enraged.
    /// </summary>
    public class FloorBoss : MonoBehaviour, IBoss
    {
        public static FloorBoss I { get; private set; }

        public FloorBossKind kind;
        public float maxHp, hp;

        Rigidbody2D rb;
        SpriteRenderer body, shadow;
        SpriteAnimator anim;
        Sprite[] idle, run;
        Status status;
        bool dead;
        float flash, attackTimer, summonTimer, ringTimer, chargeTime, windup;
        Vector2 chargeDir;

        public bool Alive => !dead;
        public Team Team => Team.Enemy;
        public Vector2 Position => (Vector2)transform.position + new Vector2(0f, 0.9f);
        public float Health01 => Mathf.Clamp01(hp / maxHp);
        public string BossName => Loc.T(kind == FloorBossKind.Ogre ? "boss.ogre" : "boss.zombie");
        public string StatusLine => Rules.Current != Law.None ? Loc.F("hud.law", Rules.Label) : "";
        public Color StatusColor => Rules.Info(Rules.Current).color;
        bool Enraged => hp < maxHp * 0.5f;

        public static FloorBoss Spawn(FloorBossKind kind, Vector2 feet, Transform parent, int floor)
        {
            var go = new GameObject("FloorBoss_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            var b = go.AddComponent<FloorBoss>();
            b.Init(kind, floor);
            return b;
        }

        void Init(FloorBossKind k, int floor)
        {
            I = this;
            kind = k;
            Bosses.Defeated = false;
            maxHp = (k == FloorBossKind.Ogre ? 55f : 70f) * (1f + 0.3f * floor) * Difficulty.BossHpMul;
            hp = maxHp;
            attackTimer = 2.5f;
            summonTimer = k == FloorBossKind.Ogre ? 9f : 6f;
            ringTimer = 5f;

            gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.mass = 40f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.radius = 0.55f;
            c.offset = new Vector2(0f, 0.55f);

            var hb = new GameObject("Hurtbox");
            hb.transform.SetParent(transform, false);
            var box = hb.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.5f, 1.7f);
            box.offset = new Vector2(0f, 0.9f);
            hb.AddComponent<Hurtbox>().owner = this;

            string skin = k == FloorBossKind.Ogre ? "ogre" : "big_zombie";
            var a = GameAssets.I;
            idle = a ? a.Anim(skin + "_idle_anim") : new Sprite[0];
            run = a ? a.Anim(skin + "_run_anim") : new Sprite[0];

            shadow = Child("Shadow", Gfx.Shadow, -10, new Vector3(0f, 0.08f, 0f));
            shadow.transform.localScale = new Vector3(2.2f, 1.8f, 1f);
            body = Child("Body", idle.Length > 0 ? idle[0] : Gfx.White, 0, Vector3.zero);
            anim = body.gameObject.AddComponent<SpriteAnimator>();
            anim.target = body;
            anim.Play(idle, 7f);
            LightUtil.Point(transform, new Vector3(0f, 1f, 0f),
                k == FloorBossKind.Ogre ? new Color(1f, 0.6f, 0.3f) : new Color(0.5f, 1f, 0.4f), 3f, 0.8f);

            status = gameObject.AddComponent<Status>();
            status.resistFreeze = true;
            status.position = () => Position;
            status.dot = d => { if (dead) return; hp -= d; if (hp <= 0f) Die(); };
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
            if (I == this) I = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || dead) return;
            flash -= dt;
            var player = PlayerController.I;
            if (player == null || !player.Alive) { rb.linearVelocity = Vector2.zero; return; }

            Vector2 to = player.Feet - (Vector2)transform.position;
            float dist = to.magnitude;
            Vector2 dir = dist > 0.01f ? to / dist : Vector2.zero;
            float mul = Rules.EnemySpeedMul * status.SpeedMul;

            if (kind == FloorBossKind.Ogre) OgreTick(dt, dir, dist, mul, player);
            else ZombieTick(dt, dir, dist, mul);

            // touching hurts (more during a charge)
            if (dist < 1.1f && player.Hit(chargeTime > 0f ? 2 : 1, dir) && player.Has(RelicId.Thorns) && !Rules.ShotsHeal)
                Hit(Mathf.Max(1f, player.Damage), -dir);
            if (dead) return;

            if (Mathf.Abs(rb.linearVelocity.x) > 0.05f) body.flipX = rb.linearVelocity.x < 0f;
            anim.Play(rb.linearVelocity.sqrMagnitude > 0.05f ? run : idle, chargeTime > 0f ? 14f : 8f);
            Color tint = windup > 0f && Mathf.Repeat(windup * 10f, 1f) > 0.5f ? new Color(1f, 0.4f, 0.3f) : status.Tint;
            body.color = flash > 0f ? new Color(1f, 0.45f, 0.45f) : tint;
        }

        // ---------------- Ogre ----------------

        void OgreTick(float dt, Vector2 dir, float dist, float mul, PlayerController player)
        {
            if (windup > 0f)
            {
                windup -= dt;
                rb.linearVelocity = Vector2.zero;
                if (windup <= 0f)
                {
                    chargeTime = 0.55f;
                    Sfx.Play("dash", 0.8f, 0.6f);
                }
                return;
            }
            if (chargeTime > 0f)
            {
                chargeTime -= dt;
                rb.linearVelocity = chargeDir * (Enraged ? 11f : 9f) * mul;
                if (Random.value < 0.5f) Fx.Burst((Vector2)transform.position, new Color(0.8f, 0.7f, 0.6f, 0.7f), 2, 1f, 0.3f, 1.5f);
                if (chargeTime <= 0f) Slam();
                return;
            }

            rb.linearVelocity = dir * (Enraged ? 2.6f : 2.1f) * mul;
            attackTimer -= dt;
            summonTimer -= dt;
            if (attackTimer <= 0f && !status.Frozen)
            {
                attackTimer = Enraged ? 3.2f : 4.4f;
                windup = 0.7f;
                chargeDir = dir;
                Fx.Text(Position + Vector2.up * 1.2f, "!", new Color(1f, 0.4f, 0.3f), 1.4f, 0.7f);
            }
            if (summonTimer <= 0f)
            {
                summonTimer = 11f;
                Summon(EnemyKind.Runner, "goblin", 2);
            }
        }

        void Slam()
        {
            int n = Enraged ? 16 : 12;
            float start = Random.value * 360f;
            for (int i = 0; i < n; i++)
            {
                float a = (start + i * 360f / n) * Mathf.Deg2Rad;
                Projectile.Orb(Position, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 4.5f, 1, new Color(1f, 0.6f, 0.3f), 1.1f);
            }
            if (!Fx.Anim("smoke", transform.position, 2f)) Fx.Burst(transform.position, new Color(0.85f, 0.75f, 0.6f), 22, 6f, 0.5f, 2f);
            Sfx.Play("boom", 0.7f, 0.7f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.35f);
        }

        // ---------------- Rotting Giant ----------------

        void ZombieTick(float dt, Vector2 dir, float dist, float mul)
        {
            rb.linearVelocity = (dist > 3.5f ? dir : dist < 2f ? -dir : Vector2.zero) * (Enraged ? 1.6f : 1.2f) * mul;
            if (status.Frozen) return;
            attackTimer -= dt;
            summonTimer -= dt;
            ringTimer -= dt;
            var player = PlayerController.I;
            if (attackTimer <= 0f && player != null)
            {
                attackTimer = Enraged ? 1.6f : 2.2f;
                Vector2 aim = player.Position - Position;
                int n = Enraged ? 7 : 5;
                for (int i = 0; i < n; i++)
                {
                    float off = Mathf.Lerp(-35f, 35f, i / (float)(n - 1));
                    Projectile.Orb(Position, Quaternion.Euler(0f, 0f, off) * aim, 4.8f, 1, new Color(0.5f, 1f, 0.35f), 1.1f);
                }
                Sfx.Play("orb", 0.9f, 0.6f);
            }
            if (summonTimer <= 0f)
            {
                summonTimer = 8f;
                Summon(EnemyKind.Kamikaze, "tiny_zombie", 3);
            }
            if (Enraged && ringTimer <= 0f)
            {
                ringTimer = 5f;
                for (int i = 0; i < 14; i++)
                {
                    float a = i * 360f / 14f * Mathf.Deg2Rad;
                    Projectile.Orb(Position, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 3.8f, 1, new Color(0.6f, 0.9f, 0.3f), 1f);
                }
            }
        }

        void Summon(EnemyKind k, string skin, int count)
        {
            if (Enemy.All.Count >= 5 || RoomManager.I == null || RoomManager.I.CurrentRoom == null) return;
            var room = RoomManager.I.CurrentRoom;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = room.NearestFree((Vector2)transform.position + Random.insideUnitCircle.normalized * 2.5f);
                Enemy.Spawn(k, skin, p, room.Content, false).noLoot = true;
            }
            Fx.Text(Position + Vector2.up * 1.3f, Loc.T("fx.rise"), new Color(1f, 0.5f, 0.4f), 1f, 1f);
        }

        // ---------------- damage ----------------

        public bool Hit(float dmg, Vector2 dir)
        {
            if (dead) return false;
            if (Rules.ShotsHeal)
            {
                hp = Mathf.Min(maxHp, hp + 1f);
                Fx.Text(Position + Vector2.up * 1.1f, "+1", new Color(0.5f, 1f, 0.5f), 1f, 0.6f);
                Sfx.Play("heal", 0.5f);
                return true;
            }
            hp -= Rules.OneHit ? 10f : dmg;
            flash = 0.1f;
            Sfx.Play("hit", 0.8f, 0.75f);
            Fx.Burst(Position, new Color(1f, 0.6f, 0.45f), 4, 3f, 0.3f, 1.2f);
            if (hp <= 0f) Die();
            return true;
        }

        void Die()
        {
            if (dead) return;
            dead = true;
            Bosses.Defeated = true;
            rb.linearVelocity = Vector2.zero;
            for (int i = 0; i < 3; i++)
            {
                Fx.Burst(Position + Random.insideUnitCircle, new Color(1f, Random.Range(0.4f, 0.9f), 0.3f), 22, 6f, 0.8f, 2.2f);
                Fx.Anim("bigboom", Position + Random.insideUnitCircle, 1f);
            }
            Sfx.Play("boom", 1f, 0.7f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.6f);
            Enemy.VanishAll();
            Pickup.Coins(Position, 15);
            if (RoomManager.I) RoomManager.I.OnEnemyKilled(true);
            Destroy(gameObject, 0.05f);
        }
    }
}
