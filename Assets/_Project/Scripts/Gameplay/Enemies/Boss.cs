using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Законодатель (big_demon). Every 15 s (10 s below half HP) brings back the law of one of the rooms
    /// you passed in this run, shoots fans and rings of orbs and summons runners in that law's skin.
    /// </summary>
    public class Boss : MonoBehaviour, IBoss
    {
        public static Boss I { get; private set; }
        public static bool Defeated { get => Bosses.Defeated; set => Bosses.Defeated = value; }

        public float maxHp = 90f;
        public float hp;
        Status status;
        public float lawInterval = 15f;

        public Law CurrentLaw { get; private set; } = Law.Boss;
        public float LawTimeLeft => lawTimer;
        public float Health01 => Mathf.Clamp01(hp / maxHp);
        public string BossName => Loc.T("hud.boss");
        public string StatusLine => CurrentLaw == Law.Boss
            ? Loc.T("hud.bossPrep")
            : Loc.F("hud.bossLaw", Rules.Info(CurrentLaw).name, Mathf.CeilToInt(lawTimer));
        public Color StatusColor => Rules.Info(CurrentLaw).color;

        Rigidbody2D rb;
        SpriteRenderer body, shadow;
        SpriteAnimator anim;
        Sprite[] idle, run;
        List<Law> laws;
        int lawIndex = -1;
        float lawTimer, fanTimer, ringTimer, summonTimer, flash, strafe = 1f, strafeTimer;
        bool dead;
        Color tint = Color.white;

        public bool Alive => !dead;
        public Team Team => Team.Enemy;
        public Vector2 Position => (Vector2)transform.position + new Vector2(0f, 1.1f);
        bool Enraged => hp < maxHp / 2f;

        /// <summary>The last fight of the run: he doesn't explode, he stays down for the ending scene.</summary>
        [HideInInspector] public bool final;
        public SpriteRenderer BodyRenderer => body;
        bool enragedTold;

        public static Boss Spawn(Vector2 feet, Transform parent, List<Law> passedLaws)
        {
            var go = new GameObject("Boss_Lawgiver");
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            var b = go.AddComponent<Boss>();
            b.Init(passedLaws);
            return b;
        }

        void Init(List<Law> passed)
        {
            I = this;
            Defeated = false;
            maxHp = Mathf.Round(maxHp * Difficulty.BossHpMul);
            hp = maxHp;
            laws = new List<Law>();
            foreach (var l in passed) if (!laws.Contains(l)) laws.Add(l);
            if (laws.Count == 0) laws.AddRange(new[] { Law.Noir, Law.Ice, Law.Mirror, Law.Upside });
            ListUtil.Shuffle(laws);

            lawTimer = 4f;        // the intro card first
            fanTimer = 3f;
            ringTimer = 7f;
            summonTimer = 9f;

            gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.mass = 50f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.radius = 0.6f;
            c.offset = new Vector2(0f, 0.6f);

            var hb = new GameObject("Hurtbox");
            hb.transform.SetParent(transform, false);
            var box = hb.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.6f, 2f);
            box.offset = new Vector2(0f, 1.05f);
            hb.AddComponent<Hurtbox>().owner = this;

            var a = GameAssets.I;
            idle = a ? a.Anim("big_demon_idle_anim") : new Sprite[0];
            run = a ? a.Anim("big_demon_run_anim") : new Sprite[0];

            shadow = Child("Shadow", Gfx.Shadow, -10, new Vector3(0f, 0.08f, 0f));
            shadow.transform.localScale = new Vector3(2.4f, 2f, 1f);
            body = Child("Body", idle.Length > 0 ? idle[0] : Gfx.White, 0, Vector3.zero);
            anim = body.gameObject.AddComponent<SpriteAnimator>();
            anim.target = body;
            anim.Play(idle, 7f);
            LightUtil.Point(transform, new Vector3(0f, 1.1f, 0f), new Color(1f, 0.3f, 0.3f), 3.5f, 0.8f);

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
            var player = PlayerController.I;
            flash -= dt;

            // law cycle
            lawTimer -= dt;
            if (lawTimer <= 0f) NextLaw();

            if (player == null || !player.Alive) { rb.linearVelocity = Vector2.zero; return; }

            Vector2 to = player.Feet - (Vector2)transform.position;
            float dist = to.magnitude;
            Vector2 dir = dist > 0.01f ? to / dist : Vector2.zero;

            strafeTimer -= dt;
            if (strafeTimer <= 0f) { strafeTimer = Random.Range(1.5f, 3f); strafe = -strafe; }
            float spd = (Enraged ? 1.7f : 1.3f) * Rules.EnemySpeedMul * status.SpeedMul;
            Vector2 v = dist > 4.5f ? dir * spd : dist < 2.5f ? -dir * spd : new Vector2(-dir.y, dir.x) * strafe * spd;
            rb.linearVelocity = v;
            anim.Play(v.sqrMagnitude > 0.05f ? run : idle, 8f);
            body.flipX = dir.x < 0f;

            if (dist < 1.3f && player.Hit(1, dir) && player.Has(RelicId.Thorns) && !Rules.ShotsHeal) Hit(Mathf.Max(1f, player.Damage), -dir);
            if (dead) return;

            // attacks
            fanTimer -= dt;
            ringTimer -= dt;
            summonTimer -= dt;
            if (fanTimer <= 0f)
            {
                fanTimer = Enraged ? 1.7f : 2.4f;
                Fan(player.Position - Position, Enraged ? 9 : 7, 70f);
            }
            if (ringTimer <= 0f)
            {
                ringTimer = Enraged ? 4.5f : 6.5f;
                Ring(Enraged ? 18 : 14);
            }
            if (summonTimer <= 0f)
            {
                summonTimer = Enraged ? 7f : 9f;
                Summon();
            }

            Color c = flash > 0f ? new Color(1f, 0.4f, 0.4f) : tint * status.Tint;
            body.color = Color.Lerp(body.color, c, dt * 12f);
        }

        void NextLaw()
        {
            lawTimer = Enraged ? 10f : lawInterval;
            lawIndex = (lawIndex + 1) % laws.Count;
            CurrentLaw = laws[lawIndex];
            tint = Color.Lerp(Color.white, Rules.Info(CurrentLaw).color, 0.45f);
            if (RoomManager.I) RoomManager.I.SetLaw(CurrentLaw, true);
            Fx.Burst(Position, Rules.Info(CurrentLaw).color, 20, 5f, 0.6f, 2f);
            Fx.Anim("lightning", Position, 2f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.3f);
            // once all memories are found, he starts to remember
            Say(RoomManager.AllMemories && Random.value < 0.5f
                ? Loc.T("boss.mem." + Random.Range(1, 4))
                : Loc.T("boss.line." + Random.Range(1, 7)));
        }

        /// <summary>The Lawgiver talks while he rewrites the rules.</summary>
        void Say(string line) => Fx.Text(Position + Vector2.up * 2.4f, line, new Color(1f, 0.75f, 0.7f), 0.75f, 2.4f);

        void Fan(Vector2 aim, int count, float spread)
        {
            for (int i = 0; i < count; i++)
            {
                float off = Mathf.Lerp(-spread / 2f, spread / 2f, count == 1 ? 0.5f : i / (float)(count - 1));
                Projectile.Orb(Position, Quaternion.Euler(0f, 0f, off) * aim, 5f, 1, new Color(1f, 0.35f, 0.3f), 1.2f);
            }
            Sfx.Play("orb", 1f, 0.7f);
        }

        void Ring(int count)
        {
            float start = Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                float a = (start + i * 360f / count) * Mathf.Deg2Rad;
                Projectile.Orb(Position, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 4f, 1, Rules.Info(CurrentLaw).color, 1.1f);
            }
            Sfx.Play("orb", 1f, 0.5f);
        }

        void Summon()
        {
            if (Enemy.All.Count >= 5 || RoomManager.I == null || RoomManager.I.CurrentRoom == null) return;
            var room = RoomManager.I.CurrentRoom;
            string skin = SkinFor(CurrentLaw);
            for (int i = 0; i < 2; i++)
            {
                Vector2 p = room.NearestFree((Vector2)transform.position + Random.insideUnitCircle.normalized * 2.5f);
                Enemy.Spawn(EnemyKind.Runner, skin, p, room.Content, false).noLoot = true;
            }
            Fx.Text(Position + Vector2.up * 1.4f, Loc.T("fx.rise"), new Color(1f, 0.4f, 0.4f), 1f, 1f);
        }

        public static string SkinFor(Law law)
        {
            switch (law)
            {
                case Law.Crypt: return "skelet";
                case Law.Noir: return "masked_orc";
                case Law.Ice: return "ice_zombie";
                case Law.Mirror: return "zombie";
                case Law.Pacifist: return "swampy";
                case Law.Lava: return "chort";
                case Law.Stealth: return "orc_warrior";
                case Law.Neon: return "wogol";
                default: return "goblin";
            }
        }

        public bool Hit(float dmg, Vector2 dir)
        {
            if (dead) return false;
            if (Rules.ShotsHeal)
            {
                hp = Mathf.Min(maxHp, hp + 1);
                Fx.Text(Position + Vector2.up * 1.3f, "+1", new Color(0.5f, 1f, 0.5f), 1f, 0.6f);
                Sfx.Play("heal", 0.5f);
                return true;
            }
            hp -= Rules.OneHit ? 10 : dmg;
            Juice.Number(Position, dmg);
            flash = 0.1f;
            Sfx.Play("hit", 0.8f, 0.7f);
            Fx.Burst(Position, new Color(1f, 0.5f, 0.4f), 4, 3f, 0.3f, 1.2f);
            if (Enraged && !enragedTold && hp > 0f)
            {
                enragedTold = true;
                Say(Loc.T("boss.enraged"));
                if (RoomManager.I) RoomManager.I.OnLawgiverEnraged();
            }
            if (hp <= 0f) Die();
            return true;
        }

        void Die()
        {
            dead = true;
            Defeated = true;
            Juice.Slow(0.25f, 1.2f);
            Juice.Flash(Color.white, 0.4f);
            rb.linearVelocity = Vector2.zero;
            if (final)
            {
                Fall();
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                Fx.Burst(Position + Random.insideUnitCircle, new Color(1f, Random.Range(0.3f, 0.9f), 0.2f), 25, 7f, 0.9f, 2.5f);
                Fx.Anim("bigboom", Position + Random.insideUnitCircle * 1.2f, 1f);
            }
            Sfx.Play("boom", 1f, 0.6f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.8f);
            Say(Loc.T("boss.death"));
            Enemy.VanishAll();
            Pickup.Coins(Position, 25);
            if (RoomManager.I) RoomManager.I.OnEnemyKilled(true);
            Destroy(gameObject, 0.05f);
        }

        /// <summary>Beaten, but still there: grey, slow, harmless — the ending scene takes over from here.</summary>
        void Fall()
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
            if (status) status.enabled = false;
            body.color = new Color(0.62f, 0.6f, 0.66f);
            if (anim) anim.Play(idle, 3f);
            Fx.Anim("lightning", Position, 2.2f);
            for (int i = 0; i < 2; i++)
                Fx.Burst(Position + Random.insideUnitCircle * 0.6f, new Color(1f, Random.Range(0.3f, 0.7f), 0.3f), 20, 5f, 0.7f, 2f);
            Sfx.Play("boom", 0.8f, 0.5f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.6f);
            Enemy.VanishAll();
            Pickup.Coins(Position, 25);
            if (RoomManager.I) RoomManager.I.OnEnemyKilled(true);
        }
    }
}
