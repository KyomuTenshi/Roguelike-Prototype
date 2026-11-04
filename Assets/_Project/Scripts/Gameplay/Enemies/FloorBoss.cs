using UnityEngine;

namespace RulesShift
{
    public enum FloorBossKind { Ogre, Zombie, DemonSlime, FrostGuardian, SlimeKing, Archdemon, Chieftain }

    /// <summary>
    /// Bosses at the end of floors 1 and 2.
    /// Ogre: chases, telegraphs a charge, rushes and slams the ground (ring of orbs), calls goblins.
    /// Rotting Giant: slow, spits fans of rot, raises tiny exploding zombies, rings when enraged.
    /// Demon Slime (chierit): a huge cleaver swing in front of him, spits fire from afar, calls imps when enraged.
    /// Frost Guardian (chierit): slams the ground — a ring of ice and four lines of shards, wakes frozen zombies.
    /// The two chierit bosses are drawn big, face left in their art and play their swing, stagger and death.
    /// At half health both add a rain: ice pillars (Guardian) or meteors (Demon) on marked cells around the hero.
    /// Slime King (SGQ, Version 1.0): hops at the hero, every landing sends a ring; big jumps bring slimes;
    /// at half health a Slime Prince splits off and he hops faster.
    /// </summary>
    public class FloorBoss : MonoBehaviour, IBoss
    {
        public static FloorBoss I { get; private set; }

        public FloorBossKind kind;
        public float maxHp, hp;

        Rigidbody2D rb;
        SpriteRenderer body, shadow;
        SpriteAnimator anim;
        Sprite[] idle, run, attackFrames, hitFrames, deathFrames;
        Status status;
        bool dead, big, attacking, struck, staggered, slime, princeOut;
        float hopTimer, hopTime, hopLength, rainTimer = 6f;
        int hops;
        Vector2 hopFrom, hopTo;
        readonly System.Collections.Generic.List<Strike> strikes = new System.Collections.Generic.List<Strike>();

        /// <summary>A marked cell that bursts after a delay (ice pillar or meteor).</summary>
        class Strike { public Vector2Int cell; public float t; }
        float flash, attackTimer, summonTimer, ringTimer, chargeTime, windup, attackTime, stagger, artScale = 1f;
        Vector2 chargeDir;

        public bool Alive => !dead;
        public Team Team => Team.Enemy;
        public Vector2 Position => (Vector2)transform.position + new Vector2(0f, slime ? 1.1f : big ? 1.7f : 0.9f);
        public float Health01 => Mathf.Clamp01(hp / maxHp);
        public string BossName => Loc.T(Key(kind));

        /// <summary>Localization key of a boss name ("boss.ogre"…); + ".rule" for its card.</summary>
        public static string Key(FloorBossKind k)
        {
            switch (k)
            {
                case FloorBossKind.Zombie: return "boss.zombie";
                case FloorBossKind.DemonSlime: return "boss.demon";
                case FloorBossKind.FrostGuardian: return "boss.frost";
                case FloorBossKind.SlimeKing: return "boss.slime";
                case FloorBossKind.Archdemon: return "boss.archdemon";
                case FloorBossKind.Chieftain: return "boss.chief";
                default: return "boss.ogre";
            }
        }

        static string Skin(FloorBossKind k)
        {
            switch (k)
            {
                case FloorBossKind.Zombie: return "big_zombie";
                case FloorBossKind.DemonSlime: return "demon_slime";
                case FloorBossKind.FrostGuardian: return "frost_guardian";
                case FloorBossKind.SlimeKing: return "sgq_slime_king";
                case FloorBossKind.Archdemon: return "big_demon";
                case FloorBossKind.Chieftain: return "masked_orc";
                default: return "ogre";
            }
        }

        /// <summary>The boss's sprites are in the project (the chierit packs are optional).</summary>
        public static bool Available(FloorBossKind k) => GameAssets.I && GameAssets.I.HasAnim(Skin(k) + "_idle_anim");
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
            float baseHp = k == FloorBossKind.Ogre ? 55f : k == FloorBossKind.Zombie ? 70f : k == FloorBossKind.DemonSlime ? 80f
                         : k == FloorBossKind.SlimeKing ? 60f : k == FloorBossKind.Archdemon ? 72f : k == FloorBossKind.Chieftain ? 58f : 75f;
            maxHp = baseHp * (1f + 0.3f * floor) * Difficulty.BossHpMul;
            hp = maxHp;
            attackTimer = 2.5f;
            summonTimer = k == FloorBossKind.Ogre ? 9f : k == FloorBossKind.Zombie ? 6f : 10f;
            ringTimer = 5f;
            slime = k == FloorBossKind.SlimeKing;
            big = k == FloorBossKind.DemonSlime || k == FloorBossKind.FrostGuardian || slime;
            artScale = k == FloorBossKind.DemonSlime ? 0.8f : k == FloorBossKind.FrostGuardian ? 0.85f : slime ? 2f
                     : k == FloorBossKind.Chieftain ? 1.9f : 1f;
            hopTimer = 1.4f;

            gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.mass = 40f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.radius = big ? 0.95f : 0.55f;
            c.offset = new Vector2(0f, c.radius);

            var hb = new GameObject("Hurtbox");
            hb.transform.SetParent(transform, false);
            var box = hb.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = slime ? new Vector2(3f, 2.2f) : big ? new Vector2(2.5f, 3.2f) : new Vector2(1.5f, 1.7f);
            box.offset = new Vector2(0f, slime ? 1.1f : big ? 1.7f : 0.9f);
            hb.AddComponent<Hurtbox>().owner = this;

            string skin = Skin(k);
            var a = GameAssets.I;
            idle = a ? a.Anim(skin + "_idle_anim") : new Sprite[0];
            run = a ? a.Anim(skin + "_run_anim") : new Sprite[0];
            attackFrames = a ? a.Anim(skin + "_attack_anim") : new Sprite[0];
            hitFrames = a ? a.Anim(skin + "_hit_anim") : new Sprite[0];
            deathFrames = a ? a.Anim(skin + "_death_anim") : new Sprite[0];
            if (run.Length == 0) run = idle;

            shadow = Child("Shadow", Gfx.Shadow, -10, new Vector3(0f, 0.08f, 0f));
            shadow.transform.localScale = big ? new Vector3(4.2f, 2.6f, 1f) : new Vector3(2.2f, 1.8f, 1f);
            body = Child("Body", idle.Length > 0 ? idle[0] : Gfx.White, 0, Vector3.zero);
            body.transform.localScale = new Vector3(artScale, artScale, 1f);
            anim = body.gameObject.AddComponent<SpriteAnimator>();
            anim.target = body;
            anim.Play(idle, 7f);
            Color light = k == FloorBossKind.Archdemon ? new Color(1f, 0.3f, 0.5f) : k == FloorBossKind.Chieftain ? new Color(1f, 0.75f, 0.35f)
                        : k == FloorBossKind.Ogre ? new Color(1f, 0.6f, 0.3f) : k == FloorBossKind.Zombie || slime ? new Color(0.5f, 1f, 0.4f)
                        : k == FloorBossKind.DemonSlime ? new Color(1f, 0.45f, 0.15f) : new Color(0.55f, 0.8f, 1f);
            LightUtil.Point(transform, new Vector3(0f, big ? 2f : 1f, 0f), light, big ? 5f : 3f, big ? 1f : 0.8f);

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
            float mul = Rules.EnemySpeedMul * status.SpeedMul * PlayerController.EnemyPace;

            TickStrikes(dt, player);
            if (slime) SlimeTick(dt, dir, dist, mul, player);
            else if (big && BigTick(dt, dir, dist, mul, player)) { }
            else if (kind == FloorBossKind.Ogre) OgreTick(dt, dir, dist, mul, player);
            else if (kind == FloorBossKind.Zombie) ZombieTick(dt, dir, dist, mul);
            else if (kind == FloorBossKind.Archdemon) ArchTick(dt, dir, dist, mul, player);
            else if (kind == FloorBossKind.Chieftain) ChiefTick(dt, dir, dist, mul);

            // touching hurts (more during a charge)
            if (dist < (big ? 1.5f : 1.1f) && !(slime && hopTime > 0f) && player.Hit(chargeTime > 0f ? 2 : 1, dir) && player.Has(RelicId.Thorns) && !Rules.ShotsHeal)
                Hit(Mathf.Max(1f, player.Damage), -dir);
            if (dead) return;

            if (slime)
            {
                if (hopTime <= 0f) anim.Play(idle, 4f);
            }
            else if (big)
            {
                // the chierit art faces left
                if (!attacking && stagger <= 0f && Mathf.Abs(rb.linearVelocity.x) > 0.05f) body.flipX = rb.linearVelocity.x > 0f;
                if (!attacking && stagger <= 0f) anim.Play(rb.linearVelocity.sqrMagnitude > 0.05f ? run : idle, 10f);
            }
            else
            {
                if (Mathf.Abs(rb.linearVelocity.x) > 0.05f) body.flipX = rb.linearVelocity.x < 0f;
                anim.Play(rb.linearVelocity.sqrMagnitude > 0.05f ? run : idle, chargeTime > 0f ? 14f : 8f);
            }
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

        // ---------------- Archdemon (0x72 big demon) ----------------

        float spiralTime, spiralAngle, spiralStep;
        int archPattern;

        /// <summary>
        /// Keeps a middle distance and cycles three attacks: a spinning spiral of fire, a rain of meteors on marked
        /// cells around the hero, and (enraged) a pair of imps. The spiral turns faster when he is enraged.
        /// </summary>
        void ArchTick(float dt, Vector2 dir, float dist, float mul, PlayerController player)
        {
            if (spiralTime > 0f)
            {
                rb.linearVelocity = Vector2.zero;
                spiralTime -= dt;
                spiralStep -= dt;
                if (spiralStep <= 0f)
                {
                    spiralStep = Enraged ? 0.07f : 0.09f;
                    spiralAngle += Enraged ? 19f : 15f;
                    int arms = Enraged ? 3 : 2;
                    for (int i = 0; i < arms; i++)
                    {
                        float a = (spiralAngle + i * 360f / arms) * Mathf.Deg2Rad;
                        Projectile.Orb(Position, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 4.2f, 1, new Color(1f, 0.35f, 0.45f), 1f);
                    }
                    if (Random.value < 0.3f) Sfx.Play("orb", 0.35f, 1.3f);
                }
                return;
            }
            rb.linearVelocity = (dist > 5f ? dir : dist < 3f ? -dir : new Vector2(-dir.y, dir.x) * 0.5f) * (Enraged ? 2f : 1.6f) * mul;
            if (status.Frozen) return;
            attackTimer -= dt;
            if (windup > 0f)
            {
                windup -= dt;
                if (windup <= 0f) { spiralTime = Enraged ? 2.4f : 1.8f; spiralStep = 0f; }
                return;
            }
            if (attackTimer > 0f) return;
            attackTimer = Enraged ? 2.6f : 3.4f;
            switch (archPattern++ % (Enraged ? 3 : 2))
            {
                case 0:
                    windup = 0.6f;
                    Fx.Text(Position + Vector2.up * 1.2f, "!", new Color(1f, 0.35f, 0.45f), 1.4f, 0.6f);
                    break;
                case 1:
                    Rain(player.Feet, Enraged ? 7 : 5);
                    break;
                default:
                    Summon(EnemyKind.Blinker, "imp", 2);
                    break;
            }
        }

        // ---------------- Chieftain (masked orc) ----------------

        int chargesLeft;

        /// <summary>Like the Ogre, but three short rushes in a row (a ring of orbs after the last one) and he calls
        /// a Guard and a Charger.</summary>
        void ChiefTick(float dt, Vector2 dir, float dist, float mul)
        {
            if (windup > 0f)
            {
                windup -= dt;
                rb.linearVelocity = Vector2.zero;
                if (windup <= 0f) { chargeTime = 0.38f; Sfx.Play("dash", 0.8f, 0.8f); }
                return;
            }
            if (chargeTime > 0f)
            {
                chargeTime -= dt;
                rb.linearVelocity = chargeDir * (Enraged ? 12f : 10f) * mul;
                if (Random.value < 0.5f) Fx.Burst((Vector2)transform.position, new Color(0.9f, 0.75f, 0.5f, 0.7f), 2, 1f, 0.3f, 1.5f);
                if (chargeTime <= 0f)
                {
                    if (--chargesLeft > 0)
                    {
                        // turn to the hero again and rush once more
                        var p = PlayerController.I;
                        chargeDir = p ? (p.Feet - (Vector2)transform.position).normalized : chargeDir;
                        windup = 0.35f;
                    }
                    else Slam();
                }
                return;
            }
            rb.linearVelocity = dir * (Enraged ? 2.5f : 2f) * mul;
            if (status.Frozen) return;
            attackTimer -= dt;
            summonTimer -= dt;
            if (attackTimer <= 0f)
            {
                attackTimer = Enraged ? 3.6f : 4.8f;
                chargesLeft = Enraged ? 4 : 3;
                windup = 0.6f;
                chargeDir = dir;
                Fx.Text(Position + Vector2.up * 1.2f, "!!!", new Color(1f, 0.6f, 0.3f), 1.2f, 0.7f);
            }
            if (summonTimer <= 0f)
            {
                summonTimer = 12f;
                Summon(EnemyKind.Guard, "orc_warrior", 1);
                Summon(EnemyKind.Charger, "masked_orc", 1);
            }
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

        // ---------------- Demon Slime and Frost Guardian ----------------

        const float AttackFps = 12f;

        /// <summary>Shared walk / swing / stagger loop of the two big bosses. Always handles the frame (returns true).</summary>
        bool BigTick(float dt, Vector2 dir, float dist, float mul, PlayerController player)
        {
            bool demon = kind == FloorBossKind.DemonSlime;

            // half health: a stagger, then he fights harder
            if (Enraged && !staggered)
            {
                staggered = true;
                attacking = false;
                stagger = hitFrames.Length > 0 ? hitFrames.Length / 10f : 0.5f;
                if (hitFrames.Length > 0) anim.Play(hitFrames, 10f, true, false);
                Fx.Text(Position + Vector2.up * 2f, "!!", demon ? new Color(1f, 0.5f, 0.2f) : new Color(0.6f, 0.9f, 1f), 1.6f, 1f);
                Sfx.Play("boom", 0.6f, 0.5f);
                if (CameraFollow.I) CameraFollow.I.Shake(0.4f);
            }
            if (stagger > 0f)
            {
                stagger -= dt;
                rb.linearVelocity = Vector2.zero;
                return true;
            }

            if (attacking)
            {
                rb.linearVelocity = Vector2.zero;
                attackTime += dt;
                float strikeAt = (demon ? 8f : 8f) / AttackFps;   // the frame the blade / the fists land
                windup = attackTime < strikeAt ? strikeAt - attackTime : 0f;
                if (!struck && attackTime >= strikeAt)
                {
                    struck = true;
                    if (demon) Cleave(player); else FrostSlam(player);
                }
                if (attackTime >= Mathf.Max(attackFrames.Length, 1) / AttackFps)
                {
                    attacking = false;
                    windup = 0f;
                }
                return true;
            }

            float speed = demon ? (Enraged ? 2.1f : 1.7f) : (Enraged ? 1.7f : 1.3f);
            rb.linearVelocity = (dist > 2.2f ? dir : Vector2.zero) * speed * mul;
            if (status.Frozen) return true;

            attackTimer -= dt;
            summonTimer -= dt;
            ringTimer -= dt;

            // the main attack: close enough for the swing (the Guardian slams from further away)
            if (attackTimer <= 0f && dist < (demon ? 4f : 5.5f))
            {
                attackTimer = demon ? (Enraged ? 2.1f : 3f) : (Enraged ? 2.6f : 3.4f);
                StartAttack(dir);
                return true;
            }

            // from afar the Demon spits fire, the Guardian throws an ice shard fan
            if (ringTimer <= 0f && dist >= 4f && player != null)
            {
                ringTimer = demon ? (Enraged ? 2.4f : 3.4f) : (Enraged ? 3f : 4f);
                Vector2 aim = player.Position - Position;
                int n = demon ? (Enraged ? 5 : 3) : (Enraged ? 7 : 5);
                float spread = demon ? 24f : 40f;
                for (int i = 0; i < n; i++)
                {
                    float off = n > 1 ? Mathf.Lerp(-spread, spread, i / (float)(n - 1)) : 0f;
                    Projectile.Orb(Position + Vector2.up * 0.4f, Quaternion.Euler(0f, 0f, off) * aim, demon ? 6f : 5f, 1,
                        demon ? new Color(1f, 0.5f, 0.15f) : new Color(0.6f, 0.9f, 1f), demon ? 1.3f : 1.1f);
                }
                Sfx.Play("orb", 0.9f, demon ? 0.5f : 1.3f);
            }

            if (summonTimer <= 0f && (Enraged || !demon))
            {
                summonTimer = demon ? 10f : 12f;
                Summon(demon ? EnemyKind.Shooter : EnemyKind.Runner, demon ? "imp" : "ice_zombie", 2);
            }

            // the second half: ice pillars / meteors on the cells around the hero
            if (Enraged && player != null)
            {
                rainTimer -= dt;
                if (rainTimer <= 0f)
                {
                    rainTimer = demon ? 4.5f : 5.5f;
                    Rain(player.Feet, demon ? 5 : 4);
                }
            }
            return true;
        }

        void Rain(Vector2 around, int count)
        {
            var room = RoomManager.I ? RoomManager.I.CurrentRoom : null;
            if (!room) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = i == 0 ? around : around + Random.insideUnitCircle * 3.2f;
                var c = room.Cell(p);
                if (!room.IsFree(c.x, c.y)) continue;
                room.Warn(c);
                strikes.Add(new Strike { cell = c, t = 1f + i * 0.12f });
            }
            Sfx.Play("fuse", 0.6f, kind == FloorBossKind.DemonSlime ? 0.7f : 1.3f);
        }

        void TickStrikes(float dt, PlayerController player)
        {
            if (strikes.Count == 0) return;
            var room = RoomManager.I ? RoomManager.I.CurrentRoom : null;
            for (int i = strikes.Count - 1; i >= 0; i--)
            {
                var s = strikes[i];
                s.t -= dt;
                if (s.t > 0f) continue;
                strikes.RemoveAt(i);
                if (room) room.Unwarn(s.cell);
                Vector2 p = new Vector2(s.cell.x + 0.5f, s.cell.y + 0.5f);
                bool demon = kind == FloorBossKind.DemonSlime || kind == FloorBossKind.Archdemon;
                if (!Fx.Anim(demon ? "boom" : "smoke", p, demon ? 1f : 1.2f, 0f, 36, demon ? (Color?)null : new Color(0.75f, 0.92f, 1f)))
                    Fx.Burst(p, demon ? new Color(1f, 0.5f, 0.2f) : new Color(0.8f, 0.95f, 1f), 14, 4f, 0.4f, 1.6f);
                if (player != null && player.Alive && Vector2.Distance(player.Feet, p) < 0.95f) player.Hit(1, player.Feet - p);
                Sfx.Play(demon ? "boom" : "hit", 0.4f, demon ? 0.9f : 1.4f);
            }
        }

        void ClearStrikes()
        {
            var room = RoomManager.I ? RoomManager.I.CurrentRoom : null;
            if (room) foreach (var s in strikes) room.Unwarn(s.cell);
            strikes.Clear();
        }

        // ---------------- Slime King ----------------

        /// <summary>Hops at the hero. A landing sends a ring; every third hop is a big one that brings slimes.</summary>
        void SlimeTick(float dt, Vector2 dir, float dist, float mul, PlayerController player)
        {
            rb.linearVelocity = Vector2.zero;
            if (hopTime > 0f)
            {
                hopTime -= dt * Mathf.Max(0.3f, mul);
                float k = 1f - Mathf.Clamp01(hopTime / hopLength);
                rb.MovePosition(Vector2.Lerp(hopFrom, hopTo, k));
                float height = (hops % 3 == 0 ? 2.6f : 1.3f) * Mathf.Sin(k * Mathf.PI);
                body.transform.localPosition = new Vector3(0f, height, 0f);
                if (hopTime <= 0f) Land(player);
                return;
            }
            body.transform.localPosition = Vector3.zero;
            if (status.Frozen) return;

            // half health: the Slime Prince splits off
            if (Enraged && !princeOut)
            {
                princeOut = true;
                var room = RoomManager.I ? RoomManager.I.CurrentRoom : null;
                if (room)
                {
                    var prince = Enemy.Spawn(EnemyKind.Runner, "sgq_medium_slime", room.NearestFree((Vector2)transform.position + Vector2.right * 2f), room.Content, false);
                    prince.splits = 1;
                    prince.noLoot = true;
                }
                Fx.Text(Position + Vector2.up * 1.6f, Loc.T("fx.split"), new Color(0.8f, 1f, 0.6f), 1.4f, 1f);
                Sfx.Play("boom", 0.6f, 1.3f);
            }

            hopTimer -= dt;
            if (hopTimer > 0f || player == null) return;
            hops++;
            bool bigHop = hops % 3 == 0;
            hopLength = bigHop ? 1f : 0.55f;
            hopTime = hopLength;
            hopTimer = Enraged ? 0.55f : 0.9f;
            hopFrom = transform.position;
            Vector2 to = player.Feet - hopFrom;
            float reach = bigHop ? 6f : 3.2f;
            hopTo = hopFrom + Vector2.ClampMagnitude(to, reach);
            var r = RoomManager.I ? RoomManager.I.CurrentRoom : null;
            if (r) hopTo = new Vector2(Mathf.Clamp(hopTo.x, 1.5f, r.W - 1.5f), Mathf.Clamp(hopTo.y, 0.8f, r.H - 2f));
            if (run.Length > 0) anim.Play(run, run.Length / hopLength, true, false);
            Sfx.Play("dash", 0.5f, bigHop ? 0.6f : 1.1f);
        }

        void Land(PlayerController player)
        {
            body.transform.localPosition = Vector3.zero;
            bool bigHop = hops % 3 == 0;
            Vector2 at = transform.position;
            if (player != null && player.Alive && Vector2.Distance(player.Feet, at) < (bigHop ? 2.2f : 1.6f)) player.Hit(bigHop ? 2 : 1, player.Feet - at);
            int n = bigHop ? (Enraged ? 18 : 14) : (Enraged ? 10 : 8);
            float start = Random.value * 360f;
            for (int i = 0; i < n; i++)
            {
                float a = (start + i * 360f / n) * Mathf.Deg2Rad;
                Projectile.Orb(at + Vector2.up * 0.4f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), bigHop ? 4.4f : 3.6f, 1, new Color(0.7f, 1f, 0.5f), 1f);
            }
            if (!Fx.Anim("smoke", at, bigHop ? 2.2f : 1.4f, 0f, 36, new Color(0.75f, 1f, 0.65f))) Fx.Burst(at, new Color(0.7f, 1f, 0.55f), 16, 5f, 0.4f, 1.8f);
            Sfx.Play("boom", bigHop ? 0.8f : 0.5f, 1.2f);
            if (CameraFollow.I) CameraFollow.I.Shake(bigHop ? 0.45f : 0.2f);
            if (bigHop) Summon(EnemyKind.Kamikaze, "sgq_slime", Enraged ? 3 : 2);
        }

        void StartAttack(Vector2 dir)
        {
            attacking = true;
            struck = false;
            attackTime = 0f;
            if (Mathf.Abs(dir.x) > 0.05f) body.flipX = dir.x > 0f;
            if (attackFrames.Length > 0) anim.Play(attackFrames, AttackFps, true, false);
            Fx.Text(Position + Vector2.up * 2.1f, "!", new Color(1f, 0.4f, 0.3f), 1.4f, 0.7f);
            Sfx.Play("dash", 0.7f, kind == FloorBossKind.DemonSlime ? 0.5f : 0.8f);
        }

        /// <summary>The Demon's cleaver: a wide arc in front of him, then a spray of embers along it.</summary>
        void Cleave(PlayerController player)
        {
            Vector2 facing = body.flipX ? Vector2.right : Vector2.left;
            Vector2 center = Position + facing * 1.2f - Vector2.up * 0.6f;
            if (player != null && player.Alive)
            {
                Vector2 to = player.Position - Position;
                if (to.magnitude < 4.3f && Vector2.Angle(facing, to) < 75f) player.Hit(2, to.normalized);
            }
            float baseAng = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            if (!Fx.Anim("slash", center, 2.2f, baseAng)) Fx.Burst(center, new Color(1f, 0.55f, 0.2f), 18, 6f, 0.4f, 2f);
            Fx.Anim("fire", center, 1.4f);
            int n = Enraged ? 7 : 5;
            for (int i = 0; i < n; i++)
            {
                float a = (baseAng + Mathf.Lerp(-60f, 60f, i / (float)(n - 1))) * Mathf.Deg2Rad;
                Projectile.Orb(center, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 4.2f, 1, new Color(1f, 0.55f, 0.2f), 1f);
            }
            Sfx.Play("boom", 0.8f, 0.6f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.35f);
        }

        /// <summary>The Guardian's slam: whoever is close is hit, a ring of ice and four lines of shards fly out.</summary>
        void FrostSlam(PlayerController player)
        {
            Vector2 facing = body.flipX ? Vector2.right : Vector2.left;
            Vector2 ground = (Vector2)transform.position + facing * 1.4f + Vector2.up * 0.3f;
            if (player != null && player.Alive && Vector2.Distance(player.Feet, ground) < 2.4f) player.Hit(2, (player.Position - ground).normalized);
            int n = Enraged ? 18 : 14;
            float start = Random.value * 360f;
            for (int i = 0; i < n; i++)
            {
                float a = (start + i * 360f / n) * Mathf.Deg2Rad;
                Projectile.Orb(ground, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 4f, 1, new Color(0.65f, 0.9f, 1f), 1f);
            }
            float rot = Random.value < 0.5f ? 0f : 45f;
            for (int line = 0; line < 4; line++)
            {
                float a = (rot + line * 90f) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                for (int k = 0; k < 3; k++)
                    Projectile.Orb(ground, d, 5f + k * 1.3f, 1, new Color(0.85f, 0.97f, 1f), 0.9f);
            }
            if (!Fx.Anim("smoke", ground, 2f, 0f, 36, new Color(0.75f, 0.9f, 1f))) Fx.Burst(ground, new Color(0.8f, 0.95f, 1f), 22, 6f, 0.5f, 2f);
            Sfx.Play("boom", 0.8f, 0.9f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.45f);
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
            Juice.Number(Position, dmg);
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
            ClearStrikes();
            if (kind == FloorBossKind.SlimeKing) Achievements.Unlock("slime");
            if (kind == FloorBossKind.FrostGuardian) Achievements.Unlock("frost");
            if (kind == FloorBossKind.DemonSlime) Achievements.Unlock("demon");
            if (PlayerController.I && !PlayerController.I.HurtThisRoom) Achievements.Unlock("flawless");
            Juice.Slow(0.25f, 1.1f);
            Juice.Flash(Color.white, 0.35f);
            Fx.Anim("success", Position + Vector2.up * 3f, 0.6f, 0f, 60);
            if (slime) body.transform.localPosition = Vector3.zero;
            Bosses.Defeated = true;
            rb.linearVelocity = Vector2.zero;
            for (int i = 0; i < 3; i++)
            {
                Fx.Burst(Position + Random.insideUnitCircle, new Color(1f, Random.Range(0.4f, 0.9f), 0.3f), 22, 6f, 0.8f, 2.2f);
                Fx.Anim("bigboom", Position + Random.insideUnitCircle, 1f);
            }
            Sfx.Play("boom", 1f, 0.7f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.6f);
            if (big && deathFrames.Length > 0)
            {
                body.enabled = false;
                shadow.enabled = false;
                Fx.Frames(deathFrames, transform.position, 11f, body.flipX, 0, artScale);
            }
            Enemy.VanishAll();
            Pickup.Coins(Position, 15);
            if (RoomManager.I) RoomManager.I.OnEnemyKilled(true);
            Destroy(gameObject, 0.05f);
        }
    }
}
