using UnityEngine;
using UnityEngine.InputSystem;

namespace RulesShift
{
    /// <summary>The active item slot: Q (gamepad: right bumper) uses it, every cleared room adds a charge.</summary>
    public partial class PlayerController
    {
        ActiveId activeItem;
        int activeCharge;
        float barrierTime, overclockTime;
        SpriteRenderer barrierBubble;

        public ActiveId ActiveItem => activeItem;
        public ActiveDef Active => activeItem == ActiveId.None ? null : Actives.Get(activeItem);
        public int ActiveCharge => activeCharge;
        public bool ActiveReady => Active != null && activeCharge >= Active.charge;

        /// <summary>Takes a new active item (the old one is gone), fully charged.</summary>
        public void SetActive(ActiveId id)
        {
            activeItem = id;
            var a = Active;
            activeCharge = a != null ? a.charge : 0;
            if (a != null) Hud.Hint("active");
        }

        /// <summary>A room was cleared: one more charge.</summary>
        public void ChargeActive(int n = 1)
        {
            var a = Active;
            if (a == null || activeCharge >= a.charge) return;
            activeCharge = Mathf.Min(a.charge, activeCharge + n);
            if (activeCharge >= a.charge)
            {
                Fx.Text(Position + Vector2.up * 1.3f, Loc.T("hud.activeReady"), a.color, 0.8f, 1f);
                Sfx.Play("pickup", 0.5f, 1.4f);
            }
        }

        void ActiveTimers(float dt)
        {
            overclockTime -= dt;
            if (barrierTime > 0f)
            {
                barrierTime -= dt;
                Projectile.ClearEnemyNear(Position, 1.5f);
                if (barrierBubble)
                {
                    float k = barrierTime < 0.8f ? Mathf.Repeat(barrierTime * 10f, 1f) : 1f;
                    barrierBubble.color = new Color(0.55f, 0.8f, 1f, 0.35f * k);
                }
                if (barrierTime <= 0f && barrierBubble) { Destroy(barrierBubble.gameObject); barrierBubble = null; }
            }
        }

        void ActiveInput()
        {
            if (!Binds.ActiveDown()) return;
            var a = Active;
            if (a == null) return;
            if (activeCharge < a.charge)
            {
                if (hintCooldown <= 0f)
                {
                    Hud.Toast(Loc.F("toast.activeCharge", activeCharge, a.charge), new Color(1f, 1f, 1f, 0.85f));
                    hintCooldown = 1.5f;
                }
                return;
            }
            if (UseActive(a)) activeCharge = 0;
        }

        bool UseActive(ActiveDef a)
        {
            switch (a.id)
            {
                case ActiveId.Bomb:
                    HeroBomb.Drop(Feet + aim * 1.2f, 1f, 2.4f, Mathf.Max(3f, Damage * 4f));
                    break;

                case ActiveId.Freeze:
                    foreach (var e in Enemy.All.ToArray()) if (e && e.Alive) e.Freeze(3f);
                    var bs = Bosses.Current != null ? Status.Of(Bosses.Current) : null;
                    if (bs) bs.Freeze(3f);
                    Fx.Burst(Position, new Color(0.7f, 0.95f, 1f), 30, 8f, 0.7f, 2f);
                    Sfx.Play("law", 0.6f, 1.7f);
                    break;

                case ActiveId.Teleport:
                    if (!TeleportToAim()) return false;
                    break;

                case ActiveId.Potion:
                    if (halves >= maxHalves) { Hud.Toast(Loc.T("toast.fullHealth"), new Color(1f, 0.6f, 0.6f)); return false; }
                    Heal(4);
                    Fx.Anim("heal", Position, 0.6f, 0f, 36, null, transform);
                    Sfx.Play("heal");
                    break;

                case ActiveId.Barrier:
                    barrierTime = 3f;
                    invuln = Mathf.Max(invuln, 3f);
                    if (!barrierBubble)
                    {
                        barrierBubble = new GameObject("Barrier").AddComponent<SpriteRenderer>();
                        barrierBubble.transform.SetParent(transform, false);
                        barrierBubble.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                        barrierBubble.transform.localScale = Vector3.one * 2.4f;
                        barrierBubble.sprite = Gfx.Orb;
                        barrierBubble.sortingOrder = 5;
                        Gfx.Unlit(barrierBubble);
                    }
                    foreach (var e in Enemy.All.ToArray())
                        if (e && e.Alive && Vector2.Distance(e.Position, Position) < 3f) e.Push((e.Position - Position).normalized * 10f);
                    Fx.Anim("lightburst", Position + Vector2.up * 0.5f, 0.45f, 0f, 36, null, transform);
                    Sfx.Play("button", 0.8f, 0.8f);
                    break;

                case ActiveId.Storm:
                    if (Enemy.All.Count == 0 && Bosses.Current == null) return false;
                    Fx.Anim("strike", Position + Vector2.up * 1.2f, 0.7f, 0f, 40);
                    for (int i = 0; i < 6; i++) Thunder();
                    if (CameraFollow.I) CameraFollow.I.Shake(0.25f);
                    break;

                case ActiveId.Overclock:
                    overclockTime = 6f;
                    if (!Fx.Anim("haste", Position + Vector2.up * 0.3f, 0.6f, 0f, 36, null, transform))
                        Fx.Anim("power", Position, 0.6f, 0f, 36, null, transform);
                    Sfx.Play("law", 0.6f, 1.4f);
                    break;
            }
            Fx.Text(Position + Vector2.up * 1.2f, a.Name, a.color, 0.8f, 0.9f);
            return true;
        }

        /// <summary>Teleport scroll: to the cursor (or 5 units along the aim), never into a wall.</summary>
        bool TeleportToAim()
        {
            var room = RoomManager.I ? RoomManager.I.CurrentRoom : null;
            if (room == null) return false;
            Vector2 target = Feet + aim * 5f;
            var mouse = TouchInput.Active ? null : Mouse.current;   // on a phone: along the aim stick
            if (mouse != null && cam)
            {
                Vector3 mp = mouse.position.ReadValue();
                mp.z = -cam.transform.position.z;
                Vector2 world = (Vector2)cam.ScreenToWorldPoint(mp) - new Vector2(0f, 0.55f);
                target = Feet + Vector2.ClampMagnitude(world - Feet, 7f);
            }
            target = room.NearestFree(target);
            if (!room.Contains(target, -0.3f) || (target - Feet).sqrMagnitude < 0.5f) return false;
            if (!Fx.Anim("warp", Position, 0.5f)) Fx.Burst(Position, new Color(0.8f, 0.6f, 1f), 14, 3f, 0.35f, 1.5f);
            Teleport(target);
            invuln = Mathf.Max(invuln, 0.5f);
            if (!Fx.Anim("warp", Position, 0.5f)) Fx.Burst(Position, new Color(0.8f, 0.6f, 1f), 14, 3f, 0.35f, 1.5f);
            Sfx.Play("blink", 0.8f);
            return true;
        }
    }

    /// <summary>The bomb of the active slot: blinks for a second, then explodes (it opens cracks in the walls too).</summary>
    public class HeroBomb : MonoBehaviour
    {
        float timer, radius, damage;
        SpriteRenderer sr;

        public static void Drop(Vector2 pos, float fuse, float radius, float damage)
        {
            var go = new GameObject("HeroBomb");
            if (Fx.Root) go.transform.SetParent(Fx.Root, false);
            go.transform.position = pos;
            var b = go.AddComponent<HeroBomb>();
            b.timer = fuse;
            b.radius = radius;
            b.damage = damage;
            b.sr = go.AddComponent<SpriteRenderer>();
            var a = GameAssets.I;
            var frames = a ? a.Anim("bomb") : new Sprite[0];
            b.sr.sprite = frames.Length > 0 ? frames[0] : a ? a.Any("r:778") : Gfx.Orb;
            b.sr.sortingOrder = 4;
            b.sr.spriteSortPoint = SpriteSortPoint.Pivot;
            Gfx.Lit(b.sr);
            if (frames.Length > 1)
            {
                var anim = go.AddComponent<SpriteAnimator>();
                anim.target = b.sr;
                anim.Play(frames, 6f);
            }
            Sfx.Play("fuse", 0.7f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            timer -= dt;
            sr.color = Mathf.Repeat(timer * (timer < 0.4f ? 16f : 6f), 1f) > 0.5f ? new Color(1f, 0.5f, 0.4f) : Color.white;
            if (timer > 0f) return;
            Projectile.Explosion(transform.position, radius, damage, PlayerController.I && PlayerController.I.Has(RelicId.Fire));
            Fx.Anim("epicboom", transform.position, radius * 0.5f, 0f, 38);
            if (CameraFollow.I) CameraFollow.I.Shake(0.3f);
            Destroy(gameObject);
        }
    }
}
