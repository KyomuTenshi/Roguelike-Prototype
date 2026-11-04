using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// The newer enemy behaviours (Charger, Guard, Summoner, Blinker), attack telegraphs and the traits of elites.
    /// The base behaviours (Runner, Shooter, Kamikaze) live in Enemy.cs.
    /// </summary>
    public partial class Enemy
    {
        public Affix affix;
        bool telegraph;                 // flashes before an attack
        int state, shieldHits;
        int guardShield;          // the Guard's shield breaks after this many blocked hits
        float guardCycle;         // every few seconds the Guard lowers the shield to catch its breath
        float stateTimer, healTimer = 3f;
        Vector2 chargeDir, faceDir = Vector2.down;
        SpriteRenderer shieldSr, bubble;
        readonly List<Enemy> minions = new List<Enemy>();

        bool champion;

        /// <summary>A champion of the deeper floors: a big monster with a lot of health and a red glow.</summary>
        public void MakeChampion(float hpMul)
        {
            miniBoss = true;
            champion = true;
            maxHp *= hpMul;
            hp = maxHp;
            speed *= 0.9f;
            LightUtil.Point(transform, new Vector3(0f, 0.8f, 0f), new Color(1f, 0.3f, 0.25f), 2.6f, 1f);
            Fx.Text(Position + Vector2.up * 1.6f, Loc.T("fx.champion"), new Color(1f, 0.4f, 0.3f), 1f, 1.6f);
            Fx.Loop("crown", transform, new Vector2(0f, 1.75f), 0.45f, 40);   // the champion wears a crown
            Hud.Hint("champion");
        }

        /// <summary>A Charger in the middle of its rush: it hurts more.</summary>
        bool Charging => kind == EnemyKind.Charger && state == 2;

        /// <summary>Base behaviour of a kind (skins and spawn pools).</summary>
        public static EnemyKind BaseKind(EnemyKind k) =>
            k == EnemyKind.Charger || k == EnemyKind.Guard ? EnemyKind.Runner
            : k == EnemyKind.Summoner || k == EnemyKind.Blinker ? EnemyKind.Shooter : k;

        void InitExtra()
        {
            var a = GameAssets.I;
            switch (kind)
            {
                case EnemyKind.Guard:
                    shieldSr = Child("Shield", a ? a.Icon(664) : null, 1, new Vector3(0f, 0.45f, 0f));
                    if (shieldSr) shieldSr.transform.localScale = Vector3.one * 0.9f;
                    guardShield = 5 + 2 * (RoomManager.I ? RoomManager.I.Floor : 0);
                    guardCycle = Random.Range(0f, GuardCycle);
                    Hud.Hint("guard");
                    break;
                case EnemyKind.Charger: Hud.Hint("charger"); break;
                case EnemyKind.Summoner:
                    LightUtil.Point(transform, new Vector3(0f, 0.6f, 0f), new Color(0.7f, 0.4f, 1f), 1.6f, 0.8f);
                    Hud.Hint("summoner");
                    break;
                case EnemyKind.Blinker: Hud.Hint("blinker"); break;
            }
            if (!elite) return;

            affix = (Affix)Random.Range(1, 5);
            Color c = Color.white;
            switch (affix)
            {
                case Affix.Swift: speed *= 1.35f; c = new Color(0.5f, 1f, 1f); Fx.Anim("haste", Position, 0.35f, 0f, 36, null, transform); break;
                case Affix.Volatile: c = new Color(1f, 0.45f, 0.25f); break;
                case Affix.Healer: c = new Color(0.5f, 1f, 0.5f); break;
                case Affix.Shielded:
                    shieldHits = 2;
                    c = new Color(0.55f, 0.75f, 1f);
                    bubble = Child("Bubble", Gfx.Orb, 2, new Vector3(0f, 0.5f, 0f));
                    if (bubble)
                    {
                        bubble.color = new Color(0.55f, 0.75f, 1f, 0.3f);
                        bubble.transform.localScale = Vector3.one * 2.2f;
                        Gfx.Unlit(bubble);
                    }
                    break;
            }
            Fx.Text(Position + Vector2.up * 1.1f, Loc.T("affix." + affix.ToString().ToLowerInvariant()), c, 0.7f, 1.4f);
            Hud.Hint("affix");
        }

        void ExtraTick(float dt, Vector2 dir)
        {
            if (kind == EnemyKind.Guard && shieldSr)
            {
                // the shield turns slowly: walk around it
                if (!status.Frozen && dir.sqrMagnitude > 0.01f)
                    faceDir = Vector3.RotateTowards(faceDir, dir, 1.5f * dt, 0f);
                guardCycle = (guardCycle + dt) % GuardCycle;
                bool lowered = ShieldLowered;
                // raised: in front of the body; lowered: down at the side, see-through
                shieldSr.transform.localPosition = lowered
                    ? new Vector3(faceDir.x * 0.3f, 0.15f, 0f)
                    : new Vector3(faceDir.x * 0.42f, 0.45f + faceDir.y * 0.25f, 0f);
                shieldSr.sortingOrder = faceDir.y > 0.3f ? -1 : 1;
                shieldSr.flipX = faceDir.x < 0f;
                // the more it has taken, the redder it gets
                float wear = 1f - guardShield / (5f + 2f * (RoomManager.I ? RoomManager.I.Floor : 0));
                var sc = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.35f), wear);
                sc.a = body.color.a * (lowered ? 0.4f : 1f);
                shieldSr.color = sc;
            }
            if (bubble)
            {
                float k = 0.25f + 0.08f * Mathf.Sin(Time.time * 4f);
                bubble.color = new Color(0.55f, 0.75f, 1f, k * body.color.a);
            }
            if (affix == Affix.Healer && (healTimer -= dt) <= 0f)
            {
                healTimer = 3f;
                bool any = false;
                foreach (var e in All)
                {
                    if (!e || !e.Alive || e.hp >= e.maxHp || Vector2.Distance(e.Position, Position) > 3.2f) continue;
                    // a champion has several times the health: it gets a small share, or the pair can't be beaten
                    e.hp = Mathf.Min(e.maxHp, e.hp + e.maxHp * (e.champion ? 0.04f : 0.15f));
                    e.healFlash = 0.25f;
                    Fx.Burst(e.Position, new Color(0.5f, 1f, 0.5f), 5, 1.5f, 0.35f, 1.1f);
                    any = true;
                }
                if (any && !Fx.Anim("absorb", Position + Vector2.up * 0.4f, 0.4f, 0f, 36, null, transform))
                    Fx.Burst(Position, new Color(0.5f, 1f, 0.5f), 12, 3f, 0.5f, 1.4f);
            }
        }

        const float GuardCycle = 4f;

        /// <summary>For the last second of every four the shield is down: a window for a frontal attack.</summary>
        bool ShieldLowered => guardCycle > GuardCycle - 1.1f;

        /// <summary>
        /// The Guard's shield turns the shots that come from the front — but not while it is lowered, and it breaks
        /// after a few blocked hits, so even a hero who can't get around it (or auto-fire on a phone) gets through.
        /// An elite's bubble takes two hits.
        /// </summary>
        bool Blocks(Vector2 dir)
        {
            if (kind == EnemyKind.Guard && shieldSr && !status.Frozen && !ShieldLowered && dir.sqrMagnitude > 0.0001f &&
                Vector2.Dot(-dir.normalized, faceDir) > 0.5f)
            {
                knock = dir.normalized * 1.5f;
                if (--guardShield <= 0)
                {
                    // the shield shatters: the Guard is an ordinary brute from now on
                    Fx.Burst((Vector2)shieldSr.transform.position, new Color(0.85f, 0.85f, 0.8f), 16, 4f, 0.4f, 1.4f);
                    Sfx.Play("kill", 0.6f, 1.6f);
                    Fx.Text(Position + Vector2.up * 1.2f, Loc.T("fx.shieldBroken"), new Color(1f, 0.85f, 0.6f), 0.8f, 0.9f);
                    Destroy(shieldSr.gameObject);
                    shieldSr = null;
                    return true;
                }
                Fx.Burst((Vector2)shieldSr.transform.position, new Color(1f, 1f, 0.85f), 6, 3f, 0.2f, 1.1f);
                Sfx.Play("button", 0.5f, 1.8f);
                return true;
            }
            if (shieldHits > 0)
            {
                shieldHits--;
                Fx.Burst(Position, new Color(0.55f, 0.75f, 1f), 10, 3f, 0.3f, 1.2f);
                Sfx.Play("button", 0.5f, 1.4f);
                if (shieldHits == 0 && bubble) { Destroy(bubble.gameObject); bubble = null; }
                return true;
            }
            return false;
        }

        void OnDeathExtra()
        {
            if (affix == Affix.Volatile) DelayedBlast.Create(Position, 0.7f, 1.9f);
        }

        // ---------------- Charger ----------------

        Vector2 ChargerTick(float dt, Vector2 dir, float dist, float spd)
        {
            float mul = speed > 0.01f ? spd / speed : 1f;   // law, status and relic slow-downs
            if (!status.Frozen) stateTimer -= dt;
            switch (state)
            {
                case 0:
                    if (stateTimer <= 0f && dist < 7f && dist > 1.2f)
                    {
                        state = 1;
                        stateTimer = 0.6f;
                        chargeDir = dir;
                        telegraph = true;
                        Fx.Anim("alert", Position + Vector2.up * 1.4f, 0.35f, 0f, 40, null, transform);
                        Sfx.Play("fuse", 0.5f, 1.4f);
                    }
                    return dir * spd;
                case 1:
                    if (Random.value < dt * 10f) Fx.Burst((Vector2)transform.position, new Color(0.8f, 0.75f, 0.65f, 0.7f), 2, 1f, 0.3f, 1f);
                    if (stateTimer <= 0f)
                    {
                        state = 2;
                        stateTimer = 0.55f;
                        telegraph = false;
                        Sfx.Play("dash", 0.7f, 0.8f);
                    }
                    return -chargeDir * 0.4f * mul;
                case 2:
                    var room = RoomManager.I ? RoomManager.I.CurrentRoom : null;
                    bool wall = false;
                    if (room)
                    {
                        var c = room.Cell((Vector2)transform.position + chargeDir * 0.7f + Vector2.up * 0.2f);
                        wall = !room.IsFree(c.x, c.y);
                    }
                    if (wall || stateTimer <= 0f)
                    {
                        state = 3;
                        stateTimer = wall ? 1.1f : 0.5f;
                        if (wall)
                        {
                            Fx.Anim("dust", Position, 0.6f);
                            Fx.Burst(Position + Vector2.up * 0.6f, new Color(1f, 0.9f, 0.4f), 6, 1.5f, 0.6f, 1f);
                            Sfx.Play("hit", 0.7f, 0.6f);
                            if (CameraFollow.I) CameraFollow.I.Shake(0.08f);
                        }
                        return Vector2.zero;
                    }
                    if (Random.value < 0.5f) Fx.Burst((Vector2)transform.position, new Color(1f, 0.6f, 0.4f, 0.6f), 1, 0.5f, 0.3f, 1.2f);
                    return chargeDir * 10f * mul;
                default:
                    if (stateTimer <= 0f) { state = 0; stateTimer = Random.Range(1.4f, 2.4f); }
                    return Vector2.zero;
            }
        }

        // ---------------- Summoner ----------------

        Vector2 SummonerTick(float dt, Vector2 dir, float dist, float spd)
        {
            Vector2 v = dist < 4f ? -dir * spd : dist > 7f ? dir * spd : new Vector2(-dir.y, dir.x) * strafe * spd * 0.6f;
            if (!status.Frozen) shootTimer -= dt;
            minions.RemoveAll(m => !m || !m.Alive);
            telegraph = shootTimer < 0.6f && minions.Count < 3;
            if (telegraph && Random.value < dt * 12f)
                Fx.Burst(Position + Random.insideUnitCircle * 0.8f, new Color(0.7f, 0.4f, 1f, 0.8f), 1, 0.6f, 0.4f, 1.2f);
            if (shootTimer <= 0f)
            {
                shootTimer = Random.Range(4.5f, 6f);
                if (minions.Count < 3 && All.Count < 14) Summon();
            }
            return v;
        }

        void Summon()
        {
            var room = RoomManager.I ? RoomManager.I.CurrentRoom : null;
            bool retro = GameAssets.I && GameAssets.I.RetroOn;
            for (int i = 0; i < 2; i++)
            {
                Vector2 p = (Vector2)transform.position + Random.insideUnitCircle.normalized * 1.2f;
                if (room) p = room.NearestFree(p);
                var e = Spawn(EnemyKind.Runner, retro ? "sgq_slime" : "tiny_zombie", p, transform.parent, false);
                e.noLoot = true;
                e.maxHp = Mathf.Max(1f, Mathf.Round(e.maxHp * 0.4f));
                e.hp = e.maxHp;
                minions.Add(e);
            }
            Fx.Burst(Position, new Color(0.7f, 0.4f, 1f), 14, 3f, 0.5f, 1.4f);
            Sfx.Play("wake", 0.6f, 1.3f);
        }

        // ---------------- Blinker ----------------

        Vector2 BlinkerTick(float dt, Vector2 dir, float dist, float spd, PlayerController player)
        {
            if (!status.Frozen) shootTimer -= dt;
            switch (state)
            {
                case 0:
                    if (shootTimer <= 0f && dist < 14f)
                    {
                        state = 1;
                        stateTimer = 0.3f;
                        if (!Fx.Anim("warp", Position, 0.5f)) Fx.Burst(Position, new Color(0.8f, 0.5f, 1f), 10, 3f, 0.3f, 1.3f);
                        Sfx.Play("blink", 0.5f, 1.3f);
                    }
                    return new Vector2(-dir.y, dir.x) * strafe * spd * 0.5f;
                case 1:
                    if (!status.Frozen) stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        BlinkNear(player);
                        state = 2;
                        stateTimer = 0.45f;
                        telegraph = true;
                        Fx.Anim("charge", Position + Vector2.up * 0.5f, 0.4f, 0f, 36, null, transform);
                    }
                    return Vector2.zero;
                default:
                    if (!status.Frozen) stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        Vector2 aim = player.Position - Position;
                        for (int i = -1; i <= 1; i++)
                            Projectile.Orb(Position, Quaternion.Euler(0f, 0f, i * 16f) * aim, 5f, Difficulty.EnemyDamage, orbColor);
                        Sfx.Play("orb", 0.6f, 1.2f);
                        state = 0;
                        shootTimer = Random.Range(2.4f, 3.2f);
                        telegraph = false;
                    }
                    return Vector2.zero;
            }
        }

        void BlinkNear(PlayerController player)
        {
            var room = RoomManager.I ? RoomManager.I.CurrentRoom : null;
            Vector2 p = transform.position;
            for (int t = 0; t < 8; t++)
            {
                Vector2 c = player.Feet + Random.insideUnitCircle.normalized * Random.Range(3.2f, 4.5f);
                if (room) c = room.NearestFree(c);
                if (room && !room.Contains(c, -0.5f)) continue;
                if (Vector2.Distance(c, player.Feet) < 2.5f) continue;
                p = c;
                break;
            }
            transform.position = p;
            rb.position = p;
            if (!Fx.Anim("warp", Position, 0.5f)) Fx.Burst(Position, new Color(0.8f, 0.5f, 1f), 10, 3f, 0.3f, 1.3f);
        }
    }

    /// <summary>The Volatile elite's last gift: a flashing mark that explodes a moment later.</summary>
    public class DelayedBlast : MonoBehaviour
    {
        float timer, radius, blink;

        public static void Create(Vector2 pos, float delay, float radius)
        {
            var go = new GameObject("DelayedBlast");
            if (Fx.Root) go.transform.SetParent(Fx.Root, false);
            go.transform.position = pos;
            var b = go.AddComponent<DelayedBlast>();
            b.timer = delay;
            b.radius = radius;
            Fx.Anim("warning", pos + Vector2.up * 0.9f, 0.3f, 0f, 40);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            timer -= dt;
            blink -= dt;
            Vector2 p = transform.position;
            if (blink <= 0f)
            {
                blink = 0.1f;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    Fx.Burst(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, new Color(1f, 0.35f, 0.25f, 0.8f), 1, 0.2f, 0.15f, 1f);
                }
            }
            if (timer > 0f) return;
            if (!Fx.Anim("boom", p, radius * 0.8f)) Fx.Burst(p, new Color(1f, 0.55f, 0.2f), 20, 5f, 0.45f, 2f);
            Sfx.Play("boom", 0.7f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.2f);
            var player = PlayerController.I;
            if (player != null && player.Alive && Vector2.Distance(player.Position, p) < radius)
                player.Hit(Difficulty.EnemyDamage, player.Position - p);
            Destroy(gameObject);
        }
    }
}
