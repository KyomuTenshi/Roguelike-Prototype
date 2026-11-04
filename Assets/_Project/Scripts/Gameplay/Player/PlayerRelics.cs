using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Effects of the relics and combos added in the big relic update (Whetstone … Ember, Sharpshooter … Neon Dream).
    /// The original sixteen still live in PlayerController; everything here is called from small hooks there.
    /// </summary>
    public partial class PlayerController
    {
        /// <summary>Enemy speed factor from relics (Stopwatch, Cursed Eye). Read by Enemy and FloorBoss.</summary>
        public static float EnemyPace = 1f;

        /// <summary>Six hearts: as many as the HUD shows.</summary>
        public const int MaxHalvesCap = 12;

        /// <summary>The hero fires projectiles (not the Knight's sword): shot relics and the Multishot flask.</summary>
        public bool CanShootExtras => Hero != null && !Hero.Melee;
        /// <summary>The hero can dash or blink (or will, with the Dash relic).</summary>
        public bool CanDash => Hero != null && (Hero.blink || Hero.innateDash || Has(RelicId.Dash));

        struct PendingShot
        {
            public float time;
            public Vector2 dir;      // zero = use the current aim
            public float damage, size;
            public ShotMods mods;
        }

        readonly List<PendingShot> pending = new List<PendingShot>();
        bool movingNow, mantleReady, oneUpUsed, wasInCombat;
        int shotCount, fangKills, roomsForMantle, coinsPicked;
        float novaTimer = 4f, pixieTimer = 1.4f, thunderTimer = 5f, regenTimer, timeStopTimer = 15f, rageTime, adrenTime;
        SpriteRenderer pixieBody;

        readonly Dictionary<RelicId, float> procTime = new Dictionary<RelicId, float>();

        /// <summary>A relic just worked: its icon pops above the hero (at most every 0.4 s per relic).</summary>
        void Proc(RelicId id)
        {
            if (procTime.TryGetValue(id, out float t) && Time.unscaledTime - t < 0.4f) return;
            procTime[id] = Time.unscaledTime;
            var def = Relics.Get(id);
            var s = GameAssets.I ? GameAssets.I.Any(def.sprite) : null;
            Fx.Icon(s, Position + new Vector2(Random.Range(-0.3f, 0.3f), 1.3f));
            Sfx.Play("pickup", 0.18f, 1.6f + Random.Range(-0.1f, 0.1f));
        }

        /// <summary>The Inflation law: an attack costs a coin and hits twice as hard (free and normal when broke).</summary>
        float InflationPay()
        {
            if (!Rules.Inflation || !RoomManager.I || RoomManager.I.Coins <= 0) return 1f;
            RoomManager.I.AddCoins(-1);
            Fx.Burst(Position + aim * 0.6f, new Color(1f, 0.85f, 0.3f), 3, 1.5f, 0.25f, 0.9f);
            return 2f;
        }

        // ---------------- stats ----------------

        int TotalShots => shots + (Has(RelicId.Quiver) ? 1 : 0);
        int TotalPierce => pierce + (Has(RelicId.Piercer) ? 1 : 0) + (HasCombo(ComboId.Sharpshooter) ? 1 : 0)
                         + (HasCombo(ComboId.GhostShip) ? 2 : 0);
        float ShotSpeed => arrowSpeed * (Has(RelicId.Longbow) ? 1.35f : 1f);
        float ShotSize => Has(RelicId.Giant) ? 1.5f : 1f;
        /// <summary>Damage factor of each projectile of a volley (Quiver spreads the damage).</summary>
        float ShotMul() => Has(RelicId.Quiver) ? 0.85f : 1f;

        public float EffectiveSpeed
        {
            get
            {
                float s = moveSpeed;
                if (Has(RelicId.Feather)) s *= 1.2f;
                if (Has(RelicId.HeavyHeart)) s *= 0.9f;
                if (Has(RelicId.Skates)) s *= 1.1f;
                if (HasCombo(ComboId.SpeedDemon)) s *= 1.15f;
                if (adrenTime > 0f) s *= 1.4f;
                return s;
            }
        }

        public float EffectiveRate
        {
            get
            {
                float r = fireRate;
                if (Has(RelicId.Metronome)) r *= 1.25f;
                if (Has(RelicId.Giant)) r *= 0.85f;
                if (Has(RelicId.Burst)) r *= 0.75f;
                if (Has(RelicId.Hourglass)) r *= 1.15f;
                if (Has(RelicId.LastStand) && halves <= 2) r *= 1.5f;
                if (Has(RelicId.NeonHeart) && Rules.Current != Law.Neon) r *= 1.1f;
                if (HasCombo(ComboId.NeonDream)) r *= 1.2f;
                if (overclockTime > 0f) r *= 2f;   // the Overclock active item
                return r;
            }
        }

        public float ThornsMul => HasCombo(ComboId.SpikedFortress) ? 2f : 1f;
        public float VisionMul => Has(RelicId.NightVision) ? 1.6f : 1f;
        public float MagnetRadius => Has(RelicId.Magnet) ? 5f : 1.7f;
        float SurviveMul => Has(RelicId.Hourglass) ? 0.7f : 1f;
        public float SurviveTime(float t) => t * SurviveMul;

        static bool Hindering(Law l) =>
            l == Law.Crypt || l == Law.Noir || l == Law.Ice || l == Law.Mirror || l == Law.Upside || l == Law.Neon || l == Law.Lava;

        /// <summary>Multiplier of all relic damage bonuses (used by the Damage property).</summary>
        float RelicDamageMul()
        {
            float add = 0f;
            if (Has(RelicId.Whetstone)) add += 0.2f;
            if (Has(RelicId.Longbow)) add += 0.1f;
            if (Has(RelicId.Giant)) add += 0.25f;
            if (Has(RelicId.Glass)) add += 0.6f;
            if (Has(RelicId.Mallet)) add += 0.1f;
            if (Has(RelicId.CursedEye)) add += 0.5f;
            if (Has(RelicId.Momentum) && movingNow) add += 0.3f;
            if (Has(RelicId.Focus) && !movingNow) add += 0.4f;
            if (Has(RelicId.FullHeart) && halves >= maxHalves) add += 0.35f;
            if (Has(RelicId.NightVision) && Rules.Vision > 0f) add += 0.15f;
            if (Has(RelicId.Cartridge) && GameAssets.I && GameAssets.I.RetroOn) add += 0.5f;
            if (Has(RelicId.Lawbreaker) && (Hindering(Rules.Current) || (HasCombo(ComboId.Lawyer) && Rules.Current != Law.None)))
                add += 0.4f;
            if (Has(RelicId.Greed) && RoomManager.I) add += Mathf.Min(0.4f, 0.01f * (RoomManager.I.Coins / 2));
            if (rageTime > 0f) add += HasCombo(ComboId.BerserkerRage) ? 1f : 0.6f;
            return 1f + add;
        }

        float DashCooldown(float baseCd)
        {
            // Phantom: the dash is back very fast, but never before its invulnerability ends (no endless i-frames)
            if (HasCombo(ComboId.PhantomDash)) return Mathf.Max(0.5f, baseCd * 0.4f);
            return Has(RelicId.BlinkBoots) ? baseCd * 0.6f : baseCd;
        }

        /// <summary>Invulnerability of a dash or blink: with Phantom it is shorter than the cooldown, so spamming has gaps.</summary>
        float DashInvuln(float baseTime) => HasCombo(ComboId.PhantomDash) ? Mathf.Min(baseTime, 0.32f) : baseTime;

        // ---------------- picking up ----------------

        void ChangeHearts(int halvesDelta)
        {
            maxHalves = Mathf.Clamp(maxHalves + halvesDelta, 2, MaxHalvesCap);
            halves = Mathf.Clamp(halves + Mathf.Max(0, halvesDelta), 1, maxHalves);
        }

        void RelicPickedUp(RelicId id)
        {
            switch (id)
            {
                case RelicId.HeavyHeart: ChangeHearts(2); break;
                case RelicId.Container: ChangeHearts(2); break;
                case RelicId.Glass: ChangeHearts(-2); break;
                case RelicId.BloodOath: ChangeHearts(-2); break;
                case RelicId.BattlePixie: EnsurePixie(); break;
            }
            UpdatePace();
        }

        void ComboFormed(ComboId id)
        {
            if (id == ComboId.Juggernaut) ChangeHearts(2);
            UpdatePace();
        }

        void UpdatePace()
        {
            float p = 1f;
            if (Has(RelicId.Stopwatch)) p *= 0.85f;
            if (Has(RelicId.CursedEye)) p *= 1.15f;
            EnemyPace = p;
        }

        void RelicRoomReset()
        {
            pending.Clear();
            rageTime = adrenTime = 0f;
            novaTimer = 4f;
            thunderTimer = 5f;
            timeStopTimer = 15f;
            roomsForMantle++;
            var d = RoomManager.I ? RoomManager.I.Current : null;
            bool bossRoom = d != null && (d.objective == Objective.Boss || d.worldBoss);
            mantleReady = Has(RelicId.Mantle) && (bossRoom || roomsForMantle % 3 == 0);
            if (Has(RelicId.BattlePixie)) EnsurePixie();
        }

        /// <summary>The room is cleared (Bandage heals, the Investor pays interest).</summary>
        public void OnRoomClear(int coins)
        {
            if (dead) return;
            if (Has(RelicId.Bandage) && halves < maxHalves)
            {
                Proc(RelicId.Bandage);
                Heal(1);
                Fx.Text(Position + Vector2.up * 0.9f, "+1", new Color(1f, 0.95f, 0.85f), 0.9f, 0.8f);
            }
            if (Has(RelicId.Investor) && RoomManager.I)
            {
                int interest = Mathf.Min(HasCombo(ComboId.CashFlow) ? 10 : 5, RoomManager.I.Coins / 10);
                if (interest > 0)
                {
                    RoomManager.I.AddCoins(interest);
                    Fx.Text(Position + Vector2.up * 1.2f, "+" + interest, new Color(1f, 0.85f, 0.3f), 0.9f, 1f);
                }
            }
        }

        /// <summary>A coin was picked up (the Piggy Bank adds one for every five).</summary>
        public void OnCoinPicked()
        {
            if (!Has(RelicId.Piggy) || !RoomManager.I) return;
            if (++coinsPicked % 5 != 0) return;
            RoomManager.I.AddCoins(1);
            Fx.Text(Position + Vector2.up * 1f, "+1", new Color(1f, 0.75f, 0.6f), 0.7f, 0.7f);
        }

        // ---------------- every frame ----------------

        void RelicTimers(float dt)
        {
            rageTime -= dt;
            adrenTime -= dt;
        }

        static bool InCombat => Enemy.All.Count > 0 || Bosses.Fighting;

        void RelicTick(float dt)
        {
            bool combat = InCombat && !Rules.ShotsHeal;
            FirePending();
            if (pixieBody) PixieFollow(dt);
            if (!combat) { wasInCombat = false; return; }
            if (!wasInCombat) { wasInCombat = true; regenTimer = HasCombo(ComboId.Healer) ? 15f : 25f; }

            if (Has(RelicId.Nova) && (novaTimer -= dt) <= 0f)
            {
                novaTimer = 4f;
                Ring(HasCombo(ComboId.Starfall) ? 16 : 8, 0.7f, new Color(1f, 0.75f, 0.35f));
                Proc(RelicId.Nova);
                Sfx.Play("shoot", 0.5f, 0.8f);
            }
            if (Has(RelicId.BattlePixie) && (pixieTimer -= dt) <= 0f)
            {
                pixieTimer = HasCombo(ComboId.FairyTale) ? 0.7f : 1.4f;
                // the story's Pixie fights herself; the extra sprite is only a fallback when she is missing
                Vector2 from = Pixie.I ? (Vector2)Pixie.I.transform.position
                             : pixieBody ? (Vector2)pixieBody.transform.position : Position + Vector2.up;
                Projectile.Soul(from, Damage * 0.5f, false);
                Fx.Burst(from, Pixie.Color, 4, 1.5f, 0.25f, 1f);
            }
            if (Has(RelicId.Thunder) && (thunderTimer -= dt) <= 0f)
            {
                thunderTimer = 5f;
                Thunder();
            }
            if (Has(RelicId.Regen) && (regenTimer -= dt) <= 0f)
            {
                regenTimer = HasCombo(ComboId.Healer) ? 15f : 25f;
                Proc(RelicId.Regen);
                if (halves < maxHalves)
                {
                    Heal(1);
                    Fx.Anim("heal", Position, 0.5f, 0f, 36, null, transform);
                    Sfx.Play("heal", 0.5f);
                }
            }
            if (HasCombo(ComboId.TimeStop) && (timeStopTimer -= dt) <= 0f)
            {
                timeStopTimer = 15f;
                Proc(RelicId.Stopwatch);
                foreach (var e in Enemy.All.ToArray()) if (e && e.Alive) e.Freeze(1.5f);
                Fx.Burst(Position, new Color(0.6f, 1f, 0.7f), 24, 6f, 0.6f, 2f);
                Sfx.Play("law", 0.5f, 1.8f);
            }
        }

        /// <summary>A ring of hero shots around the hero (Nova, Neon Heart, Shockwave, Judge).</summary>
        void Ring(int count, float dmgMul, Color color)
        {
            var mods = GetShotMods();
            mods.spectral = false;
            float start = Random.Range(0f, 360f / count);
            for (int i = 0; i < count; i++)
            {
                float a = (start + i * 360f / count) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Projectile.Spike(Position + dir * 0.4f, dir, 9f, Damage * dmgMul, color, mods);
            }
        }

        void Thunder()
        {
            var targets = new List<IDamageable>();
            foreach (var e in Enemy.All) if (e && e.Alive) targets.Add(e);
            var boss = Bosses.Current;
            if (boss != null) targets.Add(boss);
            if (targets.Count == 0) return;
            var first = targets[Random.Range(0, targets.Count)];
            Strike(first, Damage * 3f);
            Proc(RelicId.Thunder);
            if (!HasCombo(ComboId.Storm)) return;
            Vector2 from = first.Position;
            int left = 3;
            foreach (var t in targets)
            {
                if (left <= 0) break;
                if (t == first || !t.Alive || Vector2.Distance(t.Position, from) > 5f) continue;
                Arc(from, t.Position, new Color(1f, 0.95f, 0.5f));
                Strike(t, Damage * 1.5f);
                left--;
            }
        }

        void Strike(IDamageable t, float dmg)
        {
            if (!Fx.Anim("strike", t.Position + Vector2.up * 1.4f, 0.5f) && !Fx.Anim("lightning", t.Position + Vector2.up * 0.6f, 0.6f))
                Fx.Burst(t.Position, new Color(1f, 0.95f, 0.5f), 12, 4f, 0.35f, 1.6f);
            Sfx.Play("boom", 0.3f, 1.8f);
            t.Hit(dmg, Vector2.down);
        }

        static void Arc(Vector2 a, Vector2 b, Color c)
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.5f));
            for (int i = 0; i <= n; i++)
                Fx.Burst(Vector2.Lerp(a, b, i / (float)n) + Random.insideUnitCircle * 0.12f, c, 1, 0.3f, 0.18f, 1.1f);
        }

        // ---------------- the Battle Pixie ----------------

        void EnsurePixie()
        {
            if (pixieBody || Pixie.I) return;
            var go = new GameObject("BattlePixie");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(-0.6f, 1.3f, 0f);
            pixieBody = go.AddComponent<SpriteRenderer>();
            pixieBody.sprite = Gfx.Orb;
            pixieBody.color = new Color(1f, 0.6f, 0.95f);
            pixieBody.sortingOrder = 6;
            go.transform.localScale = Vector3.one * 0.7f;
            Gfx.Unlit(pixieBody);
            LightUtil.Point(go.transform, Vector3.zero, new Color(1f, 0.6f, 0.95f), 1.4f, 0.8f);
        }

        void PixieFollow(float dt)
        {
            float t = Time.time;
            pixieBody.transform.localPosition = new Vector3(-0.6f + Mathf.Sin(t * 1.7f) * 0.25f, 1.3f + Mathf.Sin(t * 3.1f) * 0.15f, 0f);
            if (Random.value < dt * 6f)
                Fx.Burst(pixieBody.transform.position, new Color(1f, 0.75f, 1f, 0.7f), 1, 0.4f, 0.4f, 1f);
        }

        // ---------------- shooting ----------------

        /// <summary>The Fifth Shot and the Bomb Bag count shots; returns true when this one is empowered.</summary>
        bool NextShotEmpowered(ref ShotMods mods, ref float dmg, ref float size, out int extraPierce)
        {
            extraPierce = 0;
            shotCount++;
            bool any = false;
            int fifthEvery = HasCombo(ComboId.FifthElement) ? 3 : 5;
            if (Has(RelicId.Fifth) && shotCount % fifthEvery == 0)
            {
                dmg *= 3f;
                Proc(RelicId.Fifth);
                size *= 1.4f;
                extraPierce = 3;
                any = true;
            }
            int bombEvery = HasCombo(ComboId.Demolition) ? 4 : 8;
            if (Has(RelicId.BombBag) && shotCount % bombEvery == 0)
            {
                mods.blast = Mathf.Max(mods.blast, 1.6f);
                Proc(RelicId.BombBag);
                any = true;
            }
            return any;
        }

        /// <summary>Extra shots of a volley: backwards, sideways, echoes and bursts.</summary>
        void ShotExtras(Vector2 origin, float dmg, ShotMods mods, float size)
        {
            if (Has(RelicId.Rear))
            {
                Projectile.Hero(origin, -aim, Hero, ShotSpeed, dmg * 0.6f, TotalPierce, mods, size);
                if (HasCombo(ComboId.BulletHell))
                {
                    Projectile.Hero(origin, Quaternion.Euler(0f, 0f, 35f) * -aim, Hero, ShotSpeed, dmg * 0.6f, TotalPierce, mods, size);
                    Projectile.Hero(origin, Quaternion.Euler(0f, 0f, -35f) * -aim, Hero, ShotSpeed, dmg * 0.6f, TotalPierce, mods, size);
                }
            }
            if (Has(RelicId.Wings))
            {
                Projectile.Hero(origin, new Vector2(-aim.y, aim.x), Hero, ShotSpeed, dmg * 0.4f, TotalPierce, mods, size * 0.85f);
                Projectile.Hero(origin, new Vector2(aim.y, -aim.x), Hero, ShotSpeed, dmg * 0.4f, TotalPierce, mods, size * 0.85f);
            }
            if (Has(RelicId.Burst))
            {
                int extra = HasCombo(ComboId.Overcharge) ? 3 : 2;
                for (int i = 1; i <= extra; i++)
                    pending.Add(new PendingShot { time = Time.time + 0.08f * i, dir = Vector2.zero, damage = dmg * 0.6f, size = size, mods = mods });
            }
            float echo = HasCombo(ComboId.EchoChamber) ? 0.4f : Has(RelicId.Echo) ? 0.2f : 0f;
            if (echo > 0f && Random.value < echo)
            {
                pending.Add(new PendingShot { time = Time.time + 0.15f, dir = aim, damage = dmg * ShotMul(), size = size, mods = mods });
                Proc(RelicId.Echo);
            }
        }

        void FirePending()
        {
            if (pending.Count == 0) return;
            if (dead || frozen) { pending.Clear(); return; }
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var s = pending[i];
                if (Time.time < s.time) continue;
                pending.RemoveAt(i);
                Vector2 origin = (Vector2)bowPivot.position + aim * (0.55f + weaponHalf);
                Vector2 dir = s.dir.sqrMagnitude > 0.01f ? s.dir : aim;
                Projectile.Hero(origin, dir, Hero, ShotSpeed, s.damage, TotalPierce, s.mods, s.size);
                Sfx.Play("shoot", 0.4f, 1.2f);
            }
        }

        // ---------------- hitting ----------------

        /// <summary>Damage of a hero hit after the conditional relics (Scope, Brawler, Mark, Ember).</summary>
        public float PreHit(IDamageable target, float dmg, Vector2 origin)
        {
            if (target == null || target is Breakable) return dmg;
            float d = Vector2.Distance(origin, target.Position);
            float hd = Vector2.Distance(Position, target.Position);
            if (Has(RelicId.Scope) && d > 6f) dmg *= HasCombo(ComboId.Sharpshooter) ? 1.75f : 1.5f;
            if (Has(RelicId.Brawler) && hd < 2.5f) dmg *= 1.5f;
            var st = Status.Of(target);
            if (st)
            {
                if (st.Marked) dmg *= HasCombo(ComboId.NightHunter) ? 1.5f : 1.25f;
                if (Has(RelicId.Ember) && st.Burning) dmg *= HasCombo(ComboId.Inferno) ? 2f : 1.5f;
            }
            return dmg;
        }

        /// <summary>On-hit relics: Execute, Glitch, Chain, Stun, Mark, Midas, Mallet.</summary>
        public void PostHit(IDamageable target, float dmg, Vector2 dir)
        {
            if (target == null || target is Breakable) return;
            var e = target as Enemy;
            var st = Status.Of(target);
            if (e && e.Alive && !e.miniBoss)
            {
                float cut = HasCombo(ComboId.Edge) ? 0.3f : 0.15f;
                if (Has(RelicId.Execute) && e.HpFraction < cut)
                {
                    if (!Fx.Anim("deathspell", e.Position + Vector2.up * 0.5f, 0.7f))
                        Fx.Burst(e.Position, new Color(1f, 0.3f, 0.3f), 10, 3f, 0.3f, 1.4f);
                    Proc(RelicId.Execute);
                    e.Hit(e.hp + 1f, dir);
                    return;
                }
                bool pp = HasCombo(ComboId.PixelPerfect);
                if (Has(RelicId.Glitch) && (!e.elite || pp) && Random.value < (pp ? 0.2f : 0.1f))
                {
                    Fx.Burst(e.Position, new Color(0.3f, 1f, 0.4f), 14, 3f, 0.35f, 1.2f);
                    Fx.Burst(e.Position, new Color(1f, 0.3f, 0.9f), 8, 2f, 0.35f, 1.2f);
                    Sfx.Play("blink", 0.5f, 1.6f);
                    Proc(RelicId.Glitch);
                    e.Hit(e.hp + 1f, dir);
                    return;
                }
            }
            if (Has(RelicId.Mark) && st) st.Mark(3f);
            if (Has(RelicId.Stun) && st && Random.value < 0.12f) { st.Freeze(0.6f); Proc(RelicId.Stun); }
            if (Has(RelicId.Mallet) && e && e.Alive && !e.miniBoss) e.Push(dir.normalized * 9f);
            if (Has(RelicId.Midas) && e && Random.value < (HasCombo(ComboId.GoldenTouch) ? 0.15f : 0.06f))
            {
                Pickup.Coins(e.Position, 1);
                Proc(RelicId.Midas);
            }
            if (Has(RelicId.Chain) && Random.value < 0.2f)
            {
                int left = 2;
                Vector2 from = target.Position;
                foreach (var o in Enemy.All.ToArray())
                {
                    if (left <= 0) break;
                    if (!o || !o.Alive || ReferenceEquals(o, target) || Vector2.Distance(o.Position, from) > 4f) continue;
                    Arc(from, o.Position, new Color(0.7f, 0.9f, 1f));
                    Proc(RelicId.Chain);
                    o.Hit(dmg * 0.5f, o.Position - from);
                    left--;
                }
            }
        }

        // ---------------- getting hit ----------------

        /// <summary>Iron Skin, the Mantle and the Mirror Shield can stop a hit before it lands.</summary>
        bool AvoidHit(Vector2 dir)
        {
            float dodge = HasCombo(ComboId.Fortress) ? 0.35f : Has(RelicId.IronSkin) ? 0.2f : 0f;
            if (dodge > 0f && Random.value < dodge)
            {
                invuln = 0.4f;
                Fx.Text(Position + Vector2.up * 0.9f, Loc.T("fx.dodge"), new Color(0.8f, 0.85f, 0.9f), 0.8f, 0.7f);
                Proc(RelicId.IronSkin);
                Sfx.Play("button", 0.5f, 1.4f);
                return true;
            }
            if (mantleReady)
            {
                mantleReady = false;
                Proc(RelicId.Mantle);
                invuln = 0.8f;
                if (!Fx.Anim("shield", Position, 0.5f)) Fx.Burst(Position, new Color(0.9f, 0.95f, 1f), 12, 3f, 0.4f, 1.5f);
                Sfx.Play("button");
                return true;
            }
            if (Has(RelicId.MirrorShield) && Random.value < 0.15f)
            {
                invuln = 0.6f;
                Fx.Anim("reflect", Position, 0.5f);
                Proc(RelicId.MirrorShield);
                Enemy best = null;
                float bd = 8f;
                foreach (var e in Enemy.All)
                {
                    if (!e || !e.Alive) continue;
                    float d = Vector2.Distance(e.Position, Position);
                    if (d < bd) { bd = d; best = e; }
                }
                if (best) { Arc(Position, best.Position, new Color(0.55f, 0.8f, 1f)); best.Hit(Damage * 2f, best.Position - Position); }
                else if (Bosses.Current != null) Bosses.Current.Hit(Damage * 2f, Bosses.Current.Position - Position);
                return true;
            }
            return false;
        }

        /// <summary>The hero was hurt and is still alive.</summary>
        void AfterHurt()
        {
            if (Has(RelicId.Rage)) rageTime = HasCombo(ComboId.BerserkerRage) ? 8f : 4f;
            if (Has(RelicId.Adrenaline)) adrenTime = HasCombo(ComboId.SpeedDemon) ? 6f : 3f;
            if (Has(RelicId.Rage)) Proc(RelicId.Rage);
            if (Has(RelicId.Shell))
            {
                Proc(RelicId.Shell);
                invuln += 0.6f;
                Ring(HasCombo(ComboId.SpikedFortress) ? 16 : 8, 0.6f, new Color(1f, 0.7f, 0.4f));
            }
            if (Has(RelicId.Smoke))
            {
                Proc(RelicId.Smoke);
                if (!Fx.Anim("smoke", Position, 1f)) Fx.Burst(Position, new Color(0.7f, 0.7f, 0.75f), 18, 3f, 0.8f, 2f);
                foreach (var e in Enemy.All.ToArray())
                {
                    if (!e || !e.Alive || Vector2.Distance(e.Position, Position) > 3f) continue;
                    var st = e.GetComponent<Status>();
                    if (!st) continue;
                    st.Slow(3f);
                    if (HasCombo(ComboId.VenomCloud)) st.Poison(3);
                }
            }
            if (Has(RelicId.CrystalHeart))
            {
                Proc(RelicId.CrystalHeart);
                Fx.Burst(Position, new Color(0.6f, 0.9f, 1f), 16, 4f, 0.5f, 1.6f);
                foreach (var e in Enemy.All.ToArray())
                    if (e && e.Alive && Vector2.Distance(e.Position, Position) < 2.5f) e.Freeze(1.2f);
            }
        }

        /// <summary>The 1-Up mushroom: one revive per run (full health with the Glass Phoenix).</summary>
        bool TryOneUp()
        {
            // under the Retro law death only restarts the room: the mushroom is kept for a real death
            if (!Has(RelicId.OneUp) || oneUpUsed || Rules.OneHit) return false;
            oneUpUsed = true;
            Proc(RelicId.OneUp);
            halves = HasCombo(ComboId.GlassPhoenix) ? maxHalves : Mathf.Min(4, maxHalves);
            invuln = 2f;
            Fx.Burst(Position, new Color(0.6f, 1f, 0.6f), 30, 6f, 0.8f, 2f);
            Fx.Anim("heal", Position, 0.8f, 0f, 36, null, transform);
            Fx.Text(Position + Vector2.up * 1.1f, Loc.T("fx.oneup"), new Color(0.6f, 1f, 0.6f), 1.1f, 1.2f);
            Sfx.Play("law", 0.8f, 1.4f);
            if (MusicPlayer.I) MusicPlayer.I.Jingle("jingle_comeback");   // Never Give Up
            return true;
        }

        // ---------------- kills ----------------

        void RelicKill(Vector2 pos, Status st)
        {
            if (dead) return;
            if (Has(RelicId.Fang) && ++fangKills >= 15)
            {
                fangKills = 0;
                Proc(RelicId.Fang);
                if (halves < maxHalves) { Heal(1); Fx.Text(Position + Vector2.up * 0.9f, "+1", new Color(1f, 0.4f, 0.45f), 0.9f, 0.8f); }
            }
            float oath = HasCombo(ComboId.Bloodlust) ? 0.15f : Has(RelicId.BloodOath) ? 0.08f : 0f;
            if (oath > 0f && halves < maxHalves && Random.value < oath)
            {
                Heal(1);
                Fx.Text(Position + Vector2.up * 0.9f, "+1", new Color(1f, 0.3f, 0.35f), 0.9f, 0.8f);
                Proc(RelicId.BloodOath);
            }
            if (HasCombo(ComboId.Shatter) && st != null && st.Chilled && !Rules.ShotsHeal)
            {
                var mods = new ShotMods { slow = 2f };
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Projectile.Spike(pos + dir * 0.3f, dir, 8f, Mathf.Max(0.5f, Damage * 0.5f), new Color(0.6f, 0.9f, 1f), mods, 0.7f);
                }
            }
        }

        // ---------------- dash, blink, Neon ----------------

        void DashTick()
        {
            if (Has(RelicId.Phase)) Projectile.ClearEnemyNear(Position, 1.2f);
        }

        void DashEnded(Vector2 at)
        {
            if (!Has(RelicId.Shockwave) || dead) return;
            Fx.Anim("dust", at + Vector2.up * 0.3f, 0.8f);
            Ring(6, 0.8f, new Color(1f, 0.9f, 0.45f));
            if (!HasCombo(ComboId.Earthquake)) return;
            if (CameraFollow.I) CameraFollow.I.Shake(0.15f);
            foreach (var e in Enemy.All.ToArray())
                if (e && e.Alive && Vector2.Distance(e.Position, at) < 3f) e.Freeze(0.8f);
        }

        void OnNeonHop()
        {
            if (!Has(RelicId.NeonHeart) || !InCombat) return;
            Ring(HasCombo(ComboId.NeonDream) ? 8 : 4, 0.5f, new Color(1f, 0.35f, 0.9f));
        }
    }
}
