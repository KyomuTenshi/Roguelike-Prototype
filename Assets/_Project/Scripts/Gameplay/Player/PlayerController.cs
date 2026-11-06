using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace RulesShift
{
    /// <summary>
    /// The hero (see HeroDef). Permanent stats (upgrades) live here; the temporary law modifiers live in Rules,
    /// so leaving a room only needs Rules.Apply(nextLaw) — nothing on the player has to be undone.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public partial class PlayerController : MonoBehaviour, IDamageable
    {
        public static PlayerController I { get; private set; }

        [Header("Stats (upgrades change these)")]
        public int maxHalves = 6;      // 2 halves = 1 heart
        public int halves = 6;
        public float moveSpeed = 5.2f;
        public float damage = 1f;
        public float fireRate = 3.2f;  // shots per second
        public int shots = 1;
        public int pierce;
        public float arrowSpeed = 13f;

        [Header("Flask perks")]
        public int vampireEvery;       // heal half a heart every N kills (0 = off)
        public bool hasShield;         // first hit in each room is blocked
        public bool luck;              // chests offer 3 flasks
        public bool bombOnHurt;        // getting hit blasts nearby enemies

        public HeroDef Hero { get; private set; }
        public bool ShieldReady => shieldReady;

        // relics & combos of this run
        readonly HashSet<RelicId> relicSet = new HashSet<RelicId>();
        readonly List<RelicId> relicOrder = new List<RelicId>();
        readonly HashSet<ComboId> comboSet = new HashSet<ComboId>();
        readonly List<ComboId> comboOrder = new List<ComboId>();
        public ICollection<RelicId> RelicSet => relicSet;
        public IReadOnlyList<RelicId> RelicList => relicOrder;
        public IReadOnlyList<ComboId> ComboList => comboOrder;
        public bool Has(RelicId id) => relicSet.Contains(id);
        public bool HasCombo(ComboId id) => comboSet.Contains(id);
        float lawBuff, lawHealCd, dashTime, dashCd;
        // weapons
        const float SwingTime = 0.16f, SwingRange = 1.9f, SwingHalfArc = 75f;
        float swingTime, swingSign = 1f, charge;
        bool charging, hurtThisRoom;
        SpriteRenderer chargeOrb;
        public bool HurtThisRoom => hurtThisRoom;
        Vector2 dashDir;
        readonly HashSet<Enemy> rammed = new HashSet<Enemy>();

        /// <summary>Damage of one shot with every relic bonus.</summary>
        public float Damage
        {
            get
            {
                float d = damage + (lawBuff > 0f ? 1f : 0f);
                float m = 1f;
                if (Has(RelicId.Codex) && RoomManager.I) m += Mathf.Min(1f, 0.1f * RoomManager.I.RoomsPassed);   // up to +100%
                if (Has(RelicId.Berserk) && halves <= 2) m += 0.5f;
                if (HasCombo(ComboId.Frenzy) && halves * 2 <= maxHalves) m += 1f;
                return d * m * RelicDamageMul();
            }
        }
        string skin = "elf_m";
        bool shieldReady;
        int vampireCount;

        [HideInInspector] public Light2D heroLight;
        [HideInInspector] public bool frozen;
        [HideInInspector] public bool godMode;          // the debug menu (F1)
        [HideInInspector] public Vector2 scriptedMove;   // cutscenes walk the hero while frozen

        Rigidbody2D rb;
        SpriteRenderer body, shadow, bow;
        SpriteAnimator anim;
        Transform bowPivot;
        Sprite[] idle, run, hurtFrames;
        Vector2 vel, aim = Vector2.right;
        float cooldown, invuln, hurtTime, hintCooldown, recoil;
        bool dead;
        Camera cam;
        Vector3 weaponBase;
        float weaponHalf;

        public bool Alive => !dead;
        public Team Team => Team.Player;
        public Vector2 Position => (Vector2)transform.position + new Vector2(0f, 0.55f);
        public Vector2 Feet => transform.position;
        public Vector2 Velocity => vel;

        // ---------- creation ----------

        public static PlayerController Create(Transform parent, HeroDef hero)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            go.tag = "Player";
            var p = go.AddComponent<PlayerController>();
            p.Hero = hero ?? Heroes.All[0];
            p.skin = p.Hero.skin;
            p.maxHalves = p.halves = p.Hero.halves;
            p.moveSpeed = p.Hero.speed;
            p.damage = p.Hero.damage;
            p.fireRate = p.Hero.fireRate;
            p.shots = p.Hero.shots;
            p.pierce = p.Hero.pierce;
            p.arrowSpeed = p.Hero.projSpeed;
            p.Build();
            return p;
        }

        void Build()
        {
            I = this;
            EnemyPace = 1f;
            cam = Camera.main;
            gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var feet = gameObject.AddComponent<CircleCollider2D>();
            feet.radius = 0.28f;
            feet.offset = new Vector2(0f, 0.28f);

            var hbGo = new GameObject("Hurtbox");
            hbGo.transform.SetParent(transform, false);
            var box = hbGo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.55f, 1.0f);
            box.offset = new Vector2(0f, 0.55f);
            hbGo.AddComponent<Hurtbox>().owner = this;

            shadow = Child("Shadow", Gfx.Shadow, -10, new Vector3(0f, 0.05f, 0f));

            var a = GameAssets.I;
            idle = a ? a.Anim(skin + "_idle_anim") : new Sprite[0];
            run = a ? a.Anim(skin + "_run_anim") : new Sprite[0];
            hurtFrames = a ? a.Anim(skin + "_hit_anim") : new Sprite[0];

            body = Child("Body", idle.Length > 0 ? idle[0] : Gfx.White, 0, Vector3.zero);
            anim = body.gameObject.AddComponent<SpriteAnimator>();
            anim.target = body;
            anim.Play(idle, 8f);

            bowPivot = new GameObject("BowPivot").transform;
            bowPivot.SetParent(transform, false);
            bowPivot.localPosition = new Vector3(0f, 0.55f, 0f);
            var bowSprite = a ? a.D(Hero.held) : null;
            bow = new GameObject("Weapon").AddComponent<SpriteRenderer>();
            bow.transform.SetParent(bowPivot, false);
            bow.sprite = bowSprite;
            Gfx.Lit(bow);
            float h = bowSprite ? bowSprite.rect.height / bowSprite.pixelsPerUnit : 1.6f;
            if (Hero.heldPointsUp)
            {
                // weapon drawn pointing up: lay it along the aim, handle at the hero
                bow.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                weaponBase = new Vector3(0.25f, 0f, 0f);
                weaponHalf = h / 2f;
            }
            else
            {
                weaponBase = new Vector3(0.5f, -h / 2f, 0f);
                weaponHalf = 0f;
            }
            bow.transform.localPosition = weaponBase;

            if (Hero.weapon == WeaponKind.Staff)
            {
                chargeOrb = new GameObject("Charge").AddComponent<SpriteRenderer>();
                chargeOrb.transform.SetParent(bowPivot, false);
                chargeOrb.transform.localPosition = new Vector3(0.55f + weaponHalf, 0f, 0f);
                chargeOrb.sprite = Gfx.Orb;
                chargeOrb.color = Hero.projColor;
                chargeOrb.sortingOrder = 3;
                Gfx.Unlit(chargeOrb);
                chargeOrb.enabled = false;
            }

            heroLight = LightUtil.Point(transform, new Vector3(0f, 0.6f, 0f), Color.white, 5f, 1f);
            heroLight.enabled = false;
        }

        SpriteRenderer Child(string name, Sprite s, int order, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            Gfx.Lit(sr);
            return sr;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        // ---------- room lifecycle ----------

        public void Teleport(Vector2 feet)
        {
            transform.position = feet;
            if (rb) { rb.position = feet; rb.linearVelocity = Vector2.zero; }
            vel = Vector2.zero;
        }

        public void ResetForRoom()
        {
            dead = false;
            invuln = 0.6f;
            vel = Vector2.zero;
            body.enabled = true;
            body.color = Color.white;
            bow.enabled = true;
            shieldReady = hasShield;
            hurtThisRoom = false;
            charging = false;
            charge = 0f;
            dashTime = 0f;
            RelicRoomReset();
            RefreshSkin();
        }

        /// <summary>In Retro rooms the Elf becomes the 8-bit elf of the SGQ pack (and back outside them).</summary>
        public void RefreshSkin()
        {
            var a = GameAssets.I;
            if (!a || Hero == null || !anim) return;
            string want = Hero.skin == "elf_m" && a.RetroOn && a.HasAnim("sgq_elf_idle_anim") ? "sgq_elf" : Hero.skin;
            if (want == skin) return;
            skin = want;
            idle = a.Anim(skin + "_idle_anim");
            run = a.Anim(skin + "_run_anim");
            hurtFrames = a.Anim(skin + "_hit_anim");
            anim.Play(idle, 8f, true);
        }

        // ---------- relics ----------

        /// <summary>Adds a relic, applies its instant effect and forms combos.</summary>
        public void AddRelic(RelicId id)
        {
            if (!relicSet.Add(id)) return;
            relicOrder.Add(id);
            var def = Relics.Get(id);
            SaveSystem.DiscoverRelic(def.key);
            switch (id)
            {
                case RelicId.Blood:
                    maxHalves = Mathf.Max(2, maxHalves - 2);
                    halves = Mathf.Min(halves, maxHalves);
                    damage += 2;
                    break;
                case RelicId.Orbit:
                    OrbitBlades.Ensure(this).SetCount(1);
                    break;
                default:
                    RelicPickedUp(id);
                    break;
            }
            foreach (var c in Relics.Combos)
            {
                if (comboSet.Contains(c.id) || !relicSet.Contains(c.a) || !relicSet.Contains(c.b)) continue;
                comboSet.Add(c.id);
                comboOrder.Add(c.id);
                SaveSystem.DiscoverCombo(c.key);
                if (c.id == ComboId.Blades) OrbitBlades.Ensure(this).SetCount(3);
                ComboFormed(c.id);
                Sfx.Play("law", 0.8f, 1.3f);
                Fx.Burst(Position, new Color(1f, 0.85f, 0.3f), 24, 5f, 0.6f, 2f);
                Fx.Anim("firework", Position, 1f);
                if (Hud.I) Hud.I.ShowCombo(c);
            }
            Achievements.CheckHero(this);
            SaveSystem.Save();
        }

        /// <summary>The law changed (new room, boss, console, Chaos): Lawkeeper and the Supreme Judge react.</summary>
        public void OnLawChanged()
        {
            if (dead) return;
            RefreshSkin();
            if (Has(RelicId.Lawkeeper))
            {
                lawBuff = 6f;
                Fx.Anim("power", Position, 0.5f, 0f, 36, null, transform);
                if (lawHealCd <= 0f && halves < maxHalves)
                {
                    lawHealCd = 8f;
                    Heal(1);
                    Fx.Text(Position + Vector2.up * 0.9f, "+1", new Color(1f, 0.85f, 0.5f), 0.9f, 0.8f);
                }
            }
            if (HasCombo(ComboId.Judge))
            {
                for (int i = 0; i < 12; i++)
                {
                    float a = i * 30f * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Projectile.Hero(Position + dir * 0.4f, dir, Hero, arrowSpeed * 0.8f, Damage, 0, GetShotMods());
                }
                Sfx.Play("law", 0.5f, 1.6f);
            }
        }

        /// <summary>Relic effects a shot carries.</summary>
        public ShotMods GetShotMods()
        {
            var m = new ShotMods();
            if (Has(RelicId.Fire)) m.burn = 3f;
            if (Has(RelicId.Frost)) m.slow = 2f;
            if (Has(RelicId.Venom)) m.poison = 1;
            m.poison += Hero.innatePoison;
            if (Has(RelicId.Ricochet)) m.bounces = 1;
            if (Has(RelicId.Homing)) m.homing = 220f;
            if (HasCombo(ComboId.Smart)) { m.bounces += 2; m.homing = 600f; }
            if (Has(RelicId.Split)) m.split = HasCombo(ComboId.Hail) ? 3 : 2;
            if (HasCombo(ComboId.Hail)) m.freeze = 1f;
            if (Has(RelicId.Blast)) m.blast = HasCombo(ComboId.Napalm) ? 1.9f : 1.3f;
            if (HasCombo(ComboId.Napalm)) m.napalm = true;
            if (Has(RelicId.Crit)) m.crit = 0.15f;
            if (Has(RelicId.LuckyCoin)) m.crit += 0.1f;
            if (Has(RelicId.Ghost)) m.spectral = true;
            return m;
        }

        /// <summary>An enemy died (souls, plague).</summary>
        public void OnEnemyDied(Vector2 pos, Status st)
        {
            RelicKill(pos, st);
            if (Has(RelicId.Soul) && !Rules.ShotsHeal)
                Projectile.Soul(pos, Damage, HasCombo(ComboId.Souls));
            if (HasCombo(ComboId.Plague) && st != null && (st.Burning || st.Poisoned))
            {
                Fx.Burst(pos, new Color(0.6f, 1f, 0.3f), 14, 3f, 0.5f, 1.5f);
                foreach (var e in Enemy.All.ToArray())
                {
                    if (!e || !e.Alive || Vector2.Distance(e.Position, pos) > 2.6f) continue;
                    var s2 = e.GetComponent<Status>();
                    if (!s2) continue;
                    s2.Burn(3f);
                    s2.Poison(2);
                }
            }
        }

        /// <summary>Called for every kill (vampirism flask).</summary>
        public void OnKill()
        {
            if (vampireEvery <= 0 || dead) return;
            if (++vampireCount < vampireEvery) return;
            vampireCount = 0;
            if (halves < maxHalves)
            {
                Heal(1);
                Fx.Text(Position + Vector2.up * 0.9f, "+1", new Color(1f, 0.4f, 0.45f), 0.9f, 0.8f);
                Fx.Anim("heal", Position, 0.5f, 0f, 36, null, transform);
                Sfx.Play("heal", 0.6f);
            }
        }

        public void Revive(int hp)
        {
            halves = Mathf.Clamp(hp, 1, maxHalves);
            ResetForRoom();
        }

        public void Heal(int amount)
        {
            halves = Mathf.Min(maxHalves, halves + amount);
        }

        // ---------- update ----------

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (!cam) cam = Camera.main;

            invuln -= dt;
            hurtTime -= dt;
            lawBuff -= dt;
            lawHealCd -= dt;
            dashCd -= dt;
            cooldown -= dt;
            RelicTimers(dt);
            ActiveTimers(dt);
            hintCooldown -= dt;
            LowHealthBeat(dt);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 4f);

            if (frozen && !dead && scriptedMove.sqrMagnitude > 0.0001f)
            {
                vel = scriptedMove;
                rb.linearVelocity = vel;
                anim.Play(run, 10f);
                body.flipX = vel.x < -0.01f || (vel.x <= 0.01f && body.flipX);
                return;
            }
            if (dead || frozen)
            {
                vel = Vector2.zero;
                rb.linearVelocity = Vector2.zero;
                anim.Play(idle);
                return;
            }

            vel = rb.linearVelocity; // walls stop sliding on ice
            ReadInput(out Vector2 move, out bool fire, out Vector2 aimDir, out bool dash);

            // --- movement with the law's modifiers ---
            if (Rules.InvertMove && !Has(RelicId.Compass)) move = -move;
            movingNow = move.sqrMagnitude > 0.04f;
            if (dash && dashCd <= 0f)
            {
                if (Hero.blink) Blink(move);
                else if (Has(RelicId.Dash) || Hero.innateDash) StartDash(move);
            }
            if (dashTime > 0f)
            {
                dashTime -= dt;
                DashTick();
                if (dashTime <= 0f) DashEnded(Feet);
                vel = dashDir * 15f;
                rb.linearVelocity = vel;
                if (Random.value < 0.6f) Fx.Burst(Position, new Color(0.6f, 1f, 0.9f, 0.7f), 2, 0.5f, 0.25f, 1.5f);
                if (HasCombo(ComboId.Ram)) Ram();
            }
            else
            {
                Vector2 target = Rules.BeatMove ? BeatHop(move, dt) : move * EffectiveSpeed;
                float accel = Has(RelicId.Skates) ? 0f : Rules.Accel;
                vel = accel > 0f ? Vector2.MoveTowards(vel, target, accel * dt) : target;
                rb.linearVelocity = vel + Rules.Pull(Feet);
            }
            // the Time law: the world moves while you move (shooting nudges it a bit)
            if (Rules.TimeFlow)
            {
                bool acting = move.sqrMagnitude > 0.04f || dashTime > 0f;
                float want = acting ? 1f : fire ? 0.45f : 0.12f;
                Rules.WorldTime = Mathf.MoveTowards(Rules.WorldTime, want, dt * (want > Rules.WorldTime ? 8f : 3f));
            }
            else Rules.WorldTime = 1f;

            // --- aim & shoot ---
            if (aimDir.sqrMagnitude > 0.0001f) aim = aimDir.normalized;
            float ang = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            float swing = 0f;
            if (Hero.Melee)
            {
                // the sword rests a bit raised and sweeps through the arc when swinging
                swingTime -= dt;
                swing = swingTime > 0f
                    ? Mathf.Lerp(SwingHalfArc + 10f, -SwingHalfArc - 10f, 1f - swingTime / SwingTime) * swingSign
                    : 35f * swingSign;
            }
            bowPivot.localRotation = Quaternion.Euler(0f, 0f, ang + swing);
            bow.transform.localPosition = weaponBase + new Vector3(-recoil * 0.25f, 0f, 0f);
            bow.sortingOrder = aim.y > 0.35f ? -1 : 1;
            body.flipX = aim.x < 0f;

            bool still = move.sqrMagnitude < 0.01f && vel.magnitude < 0.8f;
            RelicTick(dt);
            ActiveInput();
            if (Hero.weapon == WeaponKind.Staff) StaffTick(fire, still, dt);
            else if (fire && cooldown <= 0f)
            {
                if (Rules.ShootOnlyStill && !still) StillHint();
                else if (Hero.Melee) Swing();
                else Shoot();
            }

            // --- animation ---
            if (hurtTime > 0f && hurtFrames.Length > 0) anim.Play(hurtFrames);
            else anim.Play(vel.sqrMagnitude > 0.1f ? run : idle, 10f);
            body.enabled = invuln <= 0f || Mathf.Repeat(invuln * 14f, 1f) > 0.35f;

            // keep the shadow & bow visible in the dark rooms only near the hero
            shadow.enabled = true;
        }

        void StartDash(Vector2 move)
        {
            dashDir = move.sqrMagnitude > 0.01f ? move.normalized : aim;
            dashTime = 0.16f;
            dashCd = DashCooldown(0.8f);
            invuln = Mathf.Max(invuln, DashInvuln(Has(RelicId.Phase) ? 0.55f : 0.3f));
            rammed.Clear();
            Fx.Anim("dust", Feet + Vector2.up * 0.3f, 0.5f, Mathf.Atan2(-dashDir.y, -dashDir.x) * Mathf.Rad2Deg);
            Sfx.Play("dash", 0.7f);
        }

        void Ram()
        {
            foreach (var e in Enemy.All.ToArray())
            {
                if (!e || !e.Alive || rammed.Contains(e) || Vector2.Distance(e.Position, Position) > 0.95f) continue;
                rammed.Add(e);
                e.Hit(Damage * 2f, dashDir);
            }
        }

        public bool DashReady => dashCd <= 0f && dashTime <= 0f;

        void ReadInput(out Vector2 move, out bool fire, out Vector2 aimDir, out bool dash)
        {
            move = Vector2.zero;
            fire = false;
            dash = Binds.DashDown();
            aimDir = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.spaceKey.isPressed) fire = true;
            }
            // a phone browser also turns taps into mouse clicks: with the touch controls on, the mouse is ignored
            var mouse = TouchInput.Active ? null : Mouse.current;
            if (mouse != null && cam)
            {
                Vector3 mp = mouse.position.ReadValue();
                mp.z = -cam.transform.position.z;
                Vector2 world = cam.ScreenToWorldPoint(mp);
                aimDir = world - Position;
                if (mouse.leftButton.isPressed) fire = true;
                if (mouse.rightButton.wasPressedThisFrame) dash = true;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 ls = pad.leftStick.ReadValue();
                if (ls.sqrMagnitude > 0.04f) move = ls;
                Vector2 rs = pad.rightStick.ReadValue();
                if (rs.sqrMagnitude > 0.09f) { aimDir = rs; fire = true; }
                if (pad.rightTrigger.isPressed && Binds.PadDash != "rt" && Binds.PadActive != "rt" && Binds.PadUse != "rt") fire = true;
            }
            if (TouchInput.Active) TouchAim(ref move, ref fire, ref aimDir);
            if (move.sqrMagnitude > 1f) move.Normalize();
        }

        /// <summary>
        /// Phone: the left stick moves, the right stick aims and shoots. With auto-fire on (settings), letting go of the
        /// aim stick shoots at the nearest enemy in reach — but never under laws where a shot hurts you (healing shots,
        /// paid shots, sleeping enemies).
        /// </summary>
        bool touchAimHeld;

        void TouchAim(ref Vector2 move, ref bool fire, ref Vector2 aimDir)
        {
            if (TouchInput.Move.sqrMagnitude > 0.0001f) move = TouchInput.Move;
            bool wasHeld = touchAimHeld;
            touchAimHeld = TouchInput.AimHeld;
            if (TouchInput.AimHeld) { aimDir = TouchInput.Aim; fire = true; return; }
            aimDir = Vector2.zero;
            if (wasHeld) return;   // the thumb just left the stick: this frame lets the staff's orb go where it aimed
            if (!SaveSystem.Data.touchAutoFire || Rules.ShotsHeal || Rules.Inflation || Rules.Stealth) return;
            float reach = Hero.Melee ? 2.6f : 9f;
            if (Rules.Vision > 0f) reach = Mathf.Min(reach, Rules.Vision * VisionMul);   // the Crypt: only what you can see
            float best = reach * reach;
            Vector2 target = Vector2.zero;
            bool found = false;
            foreach (var e in Enemy.All)
            {
                if (!e || !e.Alive) continue;
                float d = (e.Position - Position).sqrMagnitude;
                if (d < best) { best = d; target = e.Position; found = true; }
            }
            var b = Bosses.Current;
            if (b != null && (b.Position - Position).sqrMagnitude < (reach + 1.5f) * (reach + 1.5f) && !found)
            {
                target = b.Position;
                found = true;
            }
            if (!found) return;
            aimDir = target - Position;
            // the staff charges while firing: auto-fire lets a full orb go and starts the next one
            fire = !(Hero.weapon == WeaponKind.Staff && charging && charge >= 1f);
        }

        // ---------- Neon: the rhythm ----------

        const float HopTime = 0.18f;
        float hopTime;
        Vector2 hopDir, prevMove;
        int lastHopBeat = int.MinValue, grooveBeat = int.MinValue, groove;

        /// <summary>Hits in a row on the beat (0-4): the hero gets faster with each one.</summary>
        public int Groove => Rules.BeatMove ? groove : 0;

        /// <summary>
        /// Neon law: the hero always walks (a bit slower), but a direction pressed on the beat makes a long hop
        /// and builds the groove — every hit in a row makes walking and hopping faster. A press off the beat
        /// breaks the groove; two beats without a hit let it fade. A slightly early or late press still counts.
        /// </summary>
        Vector2 BeatHop(Vector2 move, float dt)
        {
            int beat = Mathf.FloorToInt(MusicPlayer.Beat + 0.16f);
            bool moving = move.sqrMagnitude > 0.04f;
            bool fresh = moving && (prevMove.sqrMagnitude <= 0.04f || Vector2.Angle(prevMove, move) > 40f);
            // rhythm assist (settings): holding a direction hops on every beat by itself
            if (SaveSystem.Data.neonAssist && moving && OnBeat() && beat != lastHopBeat) fresh = true;
            prevMove = move;
            if (groove > 0 && beat - grooveBeat > 2) groove = 0;   // the groove fades without hits

            if (fresh && hopTime <= 0f)
            {
                if (OnBeat() && beat != lastHopBeat)
                {
                    lastHopBeat = beat;
                    groove = beat == grooveBeat ? groove : Mathf.Min(4, groove + 1);
                    if (groove >= 4) Achievements.Unlock("groove");
                    grooveBeat = beat;
                    hopTime = HopTime;
                    hopDir = move.normalized;
                    OnNeonHop();
                    Fx.Burst(Feet, new Color(1f, 0.35f, 0.9f, 0.9f), 6 + groove * 2, 1.8f + groove * 0.3f, 0.3f, 1.3f);
                    if (groove >= 2) Fx.Anim("music", Position + Vector2.up * 0.4f, 0.25f + groove * 0.05f, Random.Range(-20f, 20f), 36);
                    Fx.Text(Position + Vector2.up * 1f, Loc.F("fx.groove", groove), Color.Lerp(new Color(1f, 0.45f, 0.9f), new Color(0.4f, 1f, 1f), groove / 4f), 0.7f, 0.5f);
                }
                else if (!OnBeat() && groove > 0)
                {
                    groove = 0;
                    Fx.Text(Position + Vector2.up * 1f, Loc.T("fx.offbeat"), new Color(0.7f, 0.6f, 0.8f), 0.6f, 0.5f);
                }
            }
            float boost = 1f + 0.1f * groove;
            if (hopTime > 0f)
            {
                hopTime -= dt;
                return hopDir * EffectiveSpeed * 2.6f * boost;
            }
            return move * EffectiveSpeed * 0.7f * boost;   // between the beats: always walking, faster with the groove
        }

        public static bool OnBeat()
        {
            float b = MusicPlayer.Beat;
            float f = b - Mathf.Floor(b);
#if UNITY_WEBGL && !UNITY_EDITOR
            return f < 0.34f || f > 0.78f;   // browsers deliver input and frames less evenly: a wider window
#else
            return f < 0.3f || f > 0.84f;
#endif
        }

        void StillHint()
        {
            if (hintCooldown > 0f) return;
            Hud.Toast(Loc.T("toast.still"), new Color(1f, 1f, 1f, 0.9f));
            hintCooldown = 2f;
        }

        // ---------- Knight: sword ----------

        void Swing()
        {
            cooldown = 1f / EffectiveRate;
            swingTime = SwingTime;
            swingSign = -swingSign;
            Vector2 c = Position;
            var mods = GetShotMods();
            float dmg = Damage * InflationPay();
            foreach (var e in Enemy.All.ToArray())
                if (e && e.Alive && InArc(e.Position, 0f)) MeleeHit(e, dmg, mods);
            var b = Bosses.Current;
            if (b != null && InArc(b.Position, 0.8f)) MeleeHit(b, dmg, mods);
            foreach (var br in Breakable.All.ToArray())
                if (br && br.Alive && InArc(br.Position, 0f)) MeleeHit(br, dmg, mods);

            if (Projectile.Reflect(c, aim, SwingRange + 0.4f, SwingHalfArc + 10f, dmg, mods) > 0)
                Sfx.Play("button", 0.6f, 1.5f);

            for (int i = 0; i < 7; i++)
            {
                Vector2 d = Quaternion.Euler(0f, 0f, Mathf.Lerp(-SwingHalfArc, SwingHalfArc, i / 6f)) * aim;
                Fx.Burst(c + d * SwingRange * 0.85f, new Color(1f, 1f, 0.9f, 0.8f), 1, 0.6f, 0.15f, 1.3f);
            }
            Sfx.Play("swing", 0.8f, Random.Range(0.9f, 1.1f));
            if (RoomManager.I) RoomManager.I.OnWallHit(c + aim * SwingRange * 0.85f, 0.6f);
            Enemy.WakeNear(c, 2.5f);   // a sword is quieter than arrows
        }

        bool InArc(Vector2 p, float extra)
        {
            Vector2 to = p - Position;
            float d = to.magnitude;
            return d < 0.8f + extra || (d <= SwingRange + extra && Vector2.Angle(aim, to) <= SwingHalfArc);
        }

        void MeleeHit(IDamageable t, float dmg, ShotMods mods)
        {
            if (mods.crit > 0f && Random.value < mods.crit)
            {
                dmg *= 3f;
                Fx.Text(t.Position + Vector2.up * 0.8f, Loc.T("hud.crit"), new Color(1f, 0.85f, 0.3f), 0.7f, 0.6f);
            }
            Vector2 dir = (t.Position - Position).normalized;
            Fx.Anim("slash", t.Position, 0.5f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            dmg = PreHit(t, dmg, Position);
            t.Hit(dmg, dir);
            PostHit(t, dmg, dir);
            Projectile.ApplyStatus(t, mods);
            Projectile.SplitShards(t, dir, 10f, dmg, mods, new Color(1f, 0.95f, 0.8f));
            if (mods.blast > 0f) Projectile.Explosion(t.Position, mods.blast, Mathf.Max(0.5f, dmg * 0.6f), mods.napalm);
        }

        // ---------- Sorceress: charged orb + blink ----------

        void StaffTick(bool fire, bool still, float dt)
        {
            if (fire && (charging || cooldown <= 0f))
            {
                charging = true;
                charge = Mathf.Min(1f, charge + dt / 0.9f);
            }
            else if (charging)
            {
                charging = false;
                if (Rules.ShootOnlyStill && !still) StillHint();
                else FireOrb(charge);
                charge = 0f;
            }
            if (chargeOrb)
            {
                chargeOrb.enabled = charging && charge > 0.08f;
                float pulse = charge >= 1f ? 1f + Mathf.Sin(Time.time * 30f) * 0.12f : 1f;
                chargeOrb.transform.localScale = Vector3.one * (0.4f + 1.3f * charge) * pulse;
                chargeOrb.color = charge >= 1f ? Color.white : Hero.projColor;
            }
        }

        void FireOrb(float c)
        {
            cooldown = 1f / EffectiveRate;
            recoil = 1f;
            var mods = GetShotMods();
            if (c >= 0.98f) mods.blast = Mathf.Max(mods.blast, 1.7f);
            float dmg = Damage * (1f + 2.5f * c) * InflationPay();
            float size = (1f + 1.2f * c) * ShotSize;
            int pr = TotalPierce + (c > 0.5f ? 1 : 0);
            Vector2 origin = (Vector2)bowPivot.position + aim * (0.55f + weaponHalf);
            int n = TotalShots;
            for (int i = 0; i < n; i++)
            {
                float off = (i - (n - 1) / 2f) * 12f;
                Vector2 dir = Quaternion.Euler(0f, 0f, off) * aim;
                Projectile.Hero(origin, dir, Hero, ShotSpeed * (1f - 0.3f * c), dmg * ShotMul(), pr, mods, size);
            }
            ShotExtras(origin, dmg, mods, size);
            Sfx.Play(c > 0.9f ? "blink" : "shoot", 0.7f, 1.3f - 0.4f * c);
        }

        void Blink(Vector2 move)
        {
            var room = RoomManager.I ? RoomManager.I.CurrentRoom : null;
            if (room == null) return;
            Vector2 dir = move.sqrMagnitude > 0.01f ? move.normalized : aim;
            Vector2 start = Feet, end = start;
            for (float d = 0.25f; d <= 3.6f; d += 0.25f)
            {
                Vector2 p = start + dir * d;
                var cell = room.Cell(p);
                if (!room.IsFree(cell.x, cell.y)) break;
                end = p;
            }
            if ((end - start).sqrMagnitude < 0.3f) return;
            dashCd = DashCooldown(Has(RelicId.Dash) ? 1.2f : 2f);
            if (!Fx.Anim("warp", Position, 0.5f)) Fx.Burst(Position, Hero.projColor, 14, 3f, 0.35f, 1.5f);
            Teleport(end);
            invuln = Mathf.Max(invuln, DashInvuln(0.35f));
            if (!Fx.Anim("warp", Position, 0.5f)) Fx.Burst(Position, Hero.projColor, 14, 3f, 0.35f, 1.5f);
            Sfx.Play("blink", 0.8f);
            if (HasCombo(ComboId.Ram)) { dashDir = dir; rammed.Clear(); Ram(); }
            if (Has(RelicId.Phase)) Projectile.ClearEnemyNear(Position, 2.2f);
            DashEnded(end);
        }

        // ---------- Elf / Lizard: projectiles ----------

        void Shoot()
        {
            cooldown = 1f / EffectiveRate;
            recoil = 1f;
            Vector2 origin = (Vector2)bowPivot.position + aim * (0.55f + weaponHalf);
            float spread = 9f;
            var mods = GetShotMods();
            float dmg = Damage * InflationPay();
            float size = ShotSize;
            bool empowered = NextShotEmpowered(ref mods, ref dmg, ref size, out int extraPierce);
            int n = TotalShots;
            float mul = ShotMul();
            for (int i = 0; i < n; i++)
            {
                float off = (i - (n - 1) / 2f) * spread;
                Vector2 dir = Quaternion.Euler(0f, 0f, off) * aim;
                Projectile.Hero(origin, dir, Hero, ShotSpeed, dmg * mul, TotalPierce + extraPierce, mods, size);
            }
            ShotExtras(origin, Damage, GetShotMods(), ShotSize);
            Sfx.Play("shoot", empowered ? 0.9f : 0.7f, empowered ? 0.8f : 1f);
        }

        // ---------- damage ----------

        public bool Hit(float damageIn, Vector2 dir)
        {
            if (dead || frozen || invuln > 0f || godMode) return false;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(damageIn));
            if (!Rules.OneHit && AvoidHit(dir)) return false;
            if (Has(RelicId.Armor) && !Rules.OneHit) dmg = 1;
            if (shieldReady && !Rules.OneHit)
            {
                shieldReady = false;
                invuln = 0.8f;
                Fx.Text(Position + Vector2.up * 0.9f, Loc.T("fx.shield"), new Color(0.6f, 0.85f, 1f), 0.9f, 0.8f);
                if (!Fx.Anim("shield", Position, 0.5f)) Fx.Burst(Position, new Color(0.6f, 0.85f, 1f), 12, 3f, 0.4f, 1.5f);
                Sfx.Play("button");
                return true;
            }
            if (bombOnHurt)
            {
                if (!Fx.Anim("boom", Position, 2f)) Fx.Burst(Position, new Color(1f, 0.6f, 0.2f), 18, 5f, 0.45f, 2f);
                Sfx.Play("boom", 0.7f);
                foreach (var e in Enemy.All.ToArray())
                    if (e && Vector2.Distance(e.Position, Position) < 2.3f) e.Hit(3, e.Position - Position);
            }
            hurtThisRoom = true;
            Juice.HitStop(0.06f);
            if (Rules.OneHit) halves = 0;
            else halves -= dmg;
            invuln = 1.0f;
            hurtTime = 0.25f;
            rb.linearVelocity = HasCombo(ComboId.Juggernaut) ? Vector2.zero : dir.normalized * 6f;
            if (halves > 0) AfterHurt();
            Sfx.Play("hurt");
            if (CameraFollow.I) CameraFollow.I.Shake(0.25f);
            if (!Fx.Anim("blood", Position, 0.5f, Random.Range(0f, 360f))) Fx.Burst(Position, new Color(1f, 0.25f, 0.25f), 8, 3.5f, 0.4f, 1.5f);
            if (halves <= 0 && !TryOneUp()) Die();
            return true;
        }

        float heartbeatTimer;

        /// <summary>Half a heart left: a heartbeat line pulses over the hero every second and a half.</summary>
        void LowHealthBeat(float dt)
        {
            if (dead || halves > 1 || maxHalves <= 2 || frozen) { heartbeatTimer = 0.5f; return; }
            if ((heartbeatTimer -= dt) > 0f) return;
            heartbeatTimer = 1.5f;
            Fx.Anim("heartbeat", Position + Vector2.up * 1.5f, 0.45f, 0f, 40, new Color(1f, 0.45f, 0.45f), transform);
        }

        void Die()
        {
            halves = 0;
            dead = true;
            body.color = new Color(1f, 0.4f, 0.4f, 1f);
            bow.enabled = false;
            Fx.Burst(Position, new Color(1f, 0.3f, 0.3f), 24, 5f, 0.8f, 2f);
            Fx.Anim("death", Position, 1f);
            Fx.Anim("gameover", Position + Vector2.up * 2.4f, 0.3f, 0f, 60);
            Sfx.Play("kill", 1f, 0.7f);
            if (RoomManager.I) RoomManager.I.OnPlayerDied();
        }
    }
}
