using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace RulesShift
{
    public enum BreakableKind { Crate, Barrel, Pot, FrostCrystal, MagmaRock }

    /// <summary>
    /// A crate, barrel or pot that breaks under shots and the sword: coins and sometimes a heart fall out.
    /// Cave crystals freeze the monsters around them, magma rocks explode (careful up close).
    /// The cell under it is solid until it breaks.
    /// </summary>
    public class Breakable : MonoBehaviour, IDamageable
    {
        public static readonly List<Breakable> All = new List<Breakable>();

        public BreakableKind kind;
        float hp, flash;
        bool broken;
        Vector2Int cell;
        Room room;
        SpriteRenderer sr;

        public Team Team => Team.Enemy;   // the hero's shots hit it
        public bool Alive => !broken;
        public Vector2 Position => (Vector2)transform.position + new Vector2(0f, 0.45f);

        public static Breakable Create(Room room, Sprite sprite, Vector2Int cell, BreakableKind kind)
        {
            if (!room || !sprite) return null;
            var sr = room.PropSprite(sprite, new Vector2(cell.x + 0.5f, cell.y), 0, "Breakable_" + kind);
            if (!sr) return null;
            var b = sr.gameObject.AddComponent<Breakable>();
            b.room = room;
            b.sr = sr;
            b.cell = cell;
            b.kind = kind;
            b.hp = kind == BreakableKind.Pot || kind == BreakableKind.FrostCrystal ? 1f : 2f;
            room.BlockCell(cell.x, cell.y);

            // the hit box is a bit larger than the solid cell on every side, so shots from any direction reach it first
            var box = sr.gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            float h = Mathf.Max(1.3f, sprite.rect.height / sprite.pixelsPerUnit + 0.2f);
            box.size = new Vector2(1.3f, h);
            box.offset = new Vector2(0f, h / 2f - 0.15f);
            sr.gameObject.AddComponent<Hurtbox>().owner = b;

            if (kind == BreakableKind.FrostCrystal || kind == BreakableKind.MagmaRock)
                LightUtil.Point(sr.transform, new Vector3(0f, 0.5f, 0f),
                    kind == BreakableKind.FrostCrystal ? new Color(0.5f, 0.85f, 1f) : new Color(1f, 0.45f, 0.15f), 1.8f, 0.9f);
            All.Add(b);
            return b;
        }

        void OnDestroy() => All.Remove(this);

        void Update()
        {
            if (flash <= 0f) return;
            flash -= Time.deltaTime;
            sr.color = flash > 0f ? new Color(1f, 0.75f, 0.6f) : Color.white;
        }

        public bool Hit(float damage, Vector2 dir)
        {
            if (broken) return false;
            hp -= Mathf.Max(1f, damage * 0.5f);
            flash = 0.1f;
            transform.position += (Vector3)(dir.normalized * 0.04f);
            Sfx.Play("hit", 0.5f, kind == BreakableKind.FrostCrystal ? 1.6f : 0.8f);
            if (hp <= 0f) Break();
            return true;
        }

        void Break()
        {
            broken = true;
            All.Remove(this);
            if (room) room.UnblockCell(cell.x, cell.y);
            Vector2 p = Position;
            switch (kind)
            {
                case BreakableKind.FrostCrystal:
                    // a burst of cold: every monster around freezes
                    Fx.Burst(p, new Color(0.75f, 0.95f, 1f), 22, 5f, 0.6f, 1.6f);
                    Fx.Anim("shield", p, 1.2f, 0f, 36, new Color(0.7f, 0.9f, 1f));
                    foreach (var e in Enemy.All.ToArray())
                        if (e && e.Alive && Vector2.Distance(e.Position, p) < 2.8f) e.Freeze(2.5f);
                    Sfx.Play("hit", 0.8f, 1.8f);
                    break;
                case BreakableKind.MagmaRock:
                    // it explodes: monsters around take a lot, the hero too if he stands close
                    if (!Fx.Anim("boom", p, 1.4f)) Fx.Burst(p, new Color(1f, 0.55f, 0.2f), 24, 6f, 0.5f, 2f);
                    foreach (var e in Enemy.All.ToArray())
                        if (e && e.Alive && Vector2.Distance(e.Position, p) < 2.2f) e.Hit(3f, e.Position - p);
                    var player = PlayerController.I;
                    if (player != null && player.Alive && Vector2.Distance(player.Position, p) < 1.5f) player.Hit(1, player.Position - p);
                    Sfx.Play("boom", 0.8f);
                    if (CameraFollow.I) CameraFollow.I.Shake(0.3f);
                    break;
                default:
                    Fx.Burst(p, kind == BreakableKind.Pot ? new Color(0.8f, 0.7f, 0.6f) : new Color(0.65f, 0.45f, 0.3f), 14, 4f, 0.45f, 1.4f);
                    Fx.Anim("dust", p, 0.6f);
                    Sfx.Play("kill", 0.6f, 1.3f);
                    break;
            }
            // loot
            float r = Random.value;
            if (r < 0.07f) Pickup.Heart(p);
            else if (r < 0.45f) Pickup.Coins(p, Random.Range(1, 3));
            Destroy(gameObject);
        }
    }

    /// <summary>Floor spikes: down, a short rattle, then up for a moment. They hurt the hero and the monsters alike.</summary>
    public class Spikes : MonoBehaviour
    {
        public static readonly List<Spikes> All = new List<Spikes>();

        SpriteRenderer sr;
        Sprite[] frames;
        float t, offset;
        bool off;
        readonly HashSet<Enemy> stung = new HashSet<Enemy>();

        const float Down = 2.2f, Rattle = 0.45f, Up = 0.8f;

        public static Spikes Create(Room room, Vector2Int cell, float offset)
        {
            var frames = GameAssets.I ? GameAssets.I.Anim("floor_spikes_anim") : new Sprite[0];
            if (frames.Length == 0) return null;
            var sr = room.PropSprite(frames[0], new Vector2(cell.x + 0.5f, cell.y), -90, "Spikes");
            if (!sr) return null;
            var s = sr.gameObject.AddComponent<Spikes>();
            s.sr = sr;
            s.frames = frames;
            s.offset = offset;
            All.Add(s);
            return s;
        }

        void OnDestroy() => All.Remove(this);

        /// <summary>A lever switched the trap off.</summary>
        public void Disable()
        {
            off = true;
            sr.sprite = frames[0];
            sr.color = new Color(0.6f, 0.6f, 0.65f);
        }

        bool Raised => !off && Phase > Down + Rattle;
        float Phase => Mathf.Repeat(t + offset, Down + Rattle + Up);

        void Update()
        {
            if (off) return;
            t += Time.deltaTime;
            float ph = Phase;
            int last = frames.Length - 1;
            if (ph < Down) { sr.sprite = frames[0]; stung.Clear(); }
            else if (ph < Down + Rattle) sr.sprite = frames[Mathf.Min(1, last)];
            else sr.sprite = frames[last];
            if (!Raised) return;

            Vector2 c = (Vector2)transform.position + new Vector2(0f, 0.4f);
            var player = PlayerController.I;
            if (player != null && player.Alive && Inside(player.Feet, c)) player.Hit(1, Vector2.up);
            foreach (var e in Enemy.All.ToArray())
                if (e && e.Alive && !stung.Contains(e) && Inside(e.transform.position, c)) { stung.Add(e); e.Hit(1f, Vector2.up); }
        }

        static bool Inside(Vector2 feet, Vector2 c) => Mathf.Abs(feet.x - c.x) < 0.5f && Mathf.Abs(feet.y - c.y) < 0.5f;
    }

    /// <summary>
    /// Something the hero can use: walk up and press E (or Y on a gamepad).
    /// A small "E" floats above it while it can be used.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        static Interactable focused;

        /// <summary>Something can be used right now (the touch "talk" button lights up).</summary>
        public static bool AnyFocused => focused != null;

        public float radius = 1.4f;
        protected bool used;
        TextMesh prompt;
        SpriteRenderer padPrompt;   // the gamepad button (gdb sheet) while a gamepad is used
        Vector3 promptBase;

        protected void MakePrompt(float height)
        {
            promptBase = new Vector3(0f, height, 0f);
            prompt = WorldLabel.Create(transform, (Vector2)transform.position + (Vector2)promptBase, Binds.Name(Binds.Use), UiKit.Gold, 47, 1.1f);
            if (prompt) prompt.gameObject.SetActive(false);
        }

        /// <summary>Can be used right now (e.g. the room is clear).</summary>
        protected virtual bool Ready => !used;

        protected abstract void Use();

        protected virtual void Update()
        {
            var p = PlayerController.I;
            bool near = p != null && p.Alive && !Cutscene.Playing && Ready &&
                        Vector2.Distance(p.Feet, transform.position) < radius &&
                        RoomManager.I && RoomManager.I.State == GameState.Playing;
            if (near) focused = this;
            else if (focused == this) focused = null;
            bool show = near && focused == this;
            Vector3 bob = transform.position + promptBase + new Vector3(0f, Mathf.Sin(Time.time * 5f) * 0.08f, 0f);
            bool padIcon = Binds.UsingPad && PadPromptSprite() != null;
            if (prompt)
            {
                prompt.gameObject.SetActive(show && !padIcon);
                prompt.transform.position = bob;
                if (show && !padIcon)
                {
                    string key = Binds.Name(Binds.Use);   // the key may have been rebound
                    if (prompt.text != key) prompt.text = key;
                }
            }
            if (show && padIcon)
            {
                if (!padPrompt)
                {
                    padPrompt = new GameObject("PadPrompt").AddComponent<SpriteRenderer>();
                    padPrompt.transform.SetParent(transform, false);
                    padPrompt.sortingOrder = 47;
                    padPrompt.transform.localScale = Vector3.one * 0.75f;
                    if (Fx.Unlit) padPrompt.sharedMaterial = Fx.Unlit;
                }
                padPrompt.sprite = PadPromptSprite();
                padPrompt.transform.position = bob + new Vector3(0f, 0.25f, 0f);
            }
            if (padPrompt) padPrompt.gameObject.SetActive(show && padIcon);
            if (!near || focused != this) return;
            if (Binds.UseDown()) Use();
        }

        static Sprite PadPromptSprite() => GameAssets.I ? GameAssets.I.Key(Binds.Icon("use")) : null;

        void OnDestroy()
        {
            if (focused == this) focused = null;
        }
    }

    /// <summary>A lever on the floor: pull it (E) or shoot it. It switches off the room's spikes or opens a hidden cache.</summary>
    public class Lever : Interactable, IDamageable
    {
        SpriteRenderer sr;
        Sprite onSprite;
        Sprite[] retroFrames;
        System.Action onPull;

        public Team Team => Team.Enemy;
        public bool Alive => !used;
        public Vector2 Position => (Vector2)transform.position + new Vector2(0f, 0.4f);

        public static Lever Create(Room room, Vector2Int cell, System.Action onPull)
        {
            var a = GameAssets.I;
            if (!a) return null;
            Sprite off, on;
            var retro = room.Theme == Theme.Retro ? a.Anim("sgq_lever_anim") : null;
            if (retro != null && retro.Length >= 3) { off = retro[0]; on = retro[2]; }
            else { off = a.D("lever_left"); on = a.D("lever_right"); }
            var sr = room.PropSprite(off, new Vector2(cell.x + 0.5f, cell.y), 0, "Lever");
            if (!sr) return null;
            room.BlockCell(cell.x, cell.y);
            var l = sr.gameObject.AddComponent<Lever>();
            l.sr = sr;
            l.onSprite = on;
            l.retroFrames = retro;
            l.onPull = onPull;
            l.radius = 1.5f;
            l.MakePrompt(1.4f);
            var box = sr.gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.3f, 1.3f);
            box.offset = new Vector2(0f, 0.5f);
            sr.gameObject.AddComponent<Hurtbox>().owner = l;
            LightUtil.Point(sr.transform, new Vector3(0f, 0.6f, 0f), new Color(1f, 0.85f, 0.4f), 1.4f, 0.7f);
            return l;
        }

        public bool Hit(float damage, Vector2 dir)
        {
            if (used) return false;
            Use();
            return true;
        }

        protected override void Use()
        {
            if (used) return;
            used = true;
            if (onSprite) sr.sprite = onSprite;
            Sfx.Play("button", 0.9f, 0.8f);
            Fx.Burst(Position, new Color(1f, 0.9f, 0.5f), 8, 2.5f, 0.3f, 1.2f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.12f);
            onPull?.Invoke();
        }
    }

    /// <summary>The mine cart of Version 1.0: hit it and it rolls along its rails, running over the monsters in the way.</summary>
    public class PushCart : MonoBehaviour, IDamageable
    {
        float minX, maxX, speed;
        readonly HashSet<Enemy> runOver = new HashSet<Enemy>();

        public Team Team => Team.Enemy;
        public bool Alive => true;
        public Vector2 Position => (Vector2)transform.position + new Vector2(0f, 0.4f);

        public static PushCart Attach(SpriteRenderer sr, float minX, float maxX)
        {
            if (!sr) return null;
            var c = sr.gameObject.AddComponent<PushCart>();
            c.minX = minX;
            c.maxX = maxX;
            var box = sr.gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.9f, 0.8f);
            box.offset = new Vector2(0f, 0.4f);
            sr.gameObject.AddComponent<Hurtbox>().owner = c;
            return c;
        }

        public bool Hit(float damage, Vector2 dir)
        {
            if (Mathf.Abs(speed) > 0.1f) return false;
            float x = transform.position.x;
            float sign = Mathf.Abs(dir.x) > 0.1f ? Mathf.Sign(dir.x) : (x - minX < maxX - x ? 1f : -1f);
            if ((sign > 0f && x >= maxX - 0.05f) || (sign < 0f && x <= minX + 0.05f)) sign = -sign;
            speed = 8f * sign;
            runOver.Clear();
            Sfx.Play("dash", 0.6f, 0.7f);
            return true;
        }

        void Update()
        {
            if (Mathf.Abs(speed) < 0.1f) return;
            var p = transform.position;
            p.x = Mathf.Clamp(p.x + speed * Time.deltaTime, minX, maxX);
            transform.position = p;
            if (Random.value < 0.4f) Fx.Burst((Vector2)p + Vector2.up * 0.1f, new Color(0.9f, 1f, 0.7f, 0.7f), 1, 1f, 0.2f, 1f);
            foreach (var e in Enemy.All.ToArray())
                if (e && e.Alive && !runOver.Contains(e) && Vector2.Distance(e.transform.position, p) < 0.8f)
                {
                    runOver.Add(e);
                    e.Hit(3f, new Vector2(Mathf.Sign(speed), 0.3f));
                }
            if (p.x <= minX || p.x >= maxX)
            {
                speed = 0f;
                Sfx.Play("hit", 0.6f, 0.6f);
                if (CameraFollow.I) CameraFollow.I.Shake(0.1f);
            }
        }
    }

    /// <summary>
    /// Someone to talk to (E): other players stuck in the game. A few lines in the dialogue box and, the first time, a gift.
    /// In a fight they wait until the room is clear.
    /// </summary>
    public class Npc : Interactable
    {
        public string id;
        public Color color = UiKit.Cream;
        System.Action gift;
        int talks;

        public static Npc Create(Room room, string id, string skin, Vector2 feet, Color color, System.Action gift, bool faceLeft = false)
        {
            var a = GameAssets.I;
            var frames = a ? a.Anim(skin + "_idle_anim") : new Sprite[0];
            if (frames.Length == 0 && a) frames = a.Anim(skin + "_anim");
            if (frames.Length == 0) return null;
            var sr = room.PropSprite(frames[0], feet, 0, "Npc_" + id);
            if (!sr) return null;
            sr.flipX = faceLeft;
            var anim = sr.gameObject.AddComponent<SpriteAnimator>();
            anim.target = sr;
            anim.Play(frames, 6f);
            room.BlockCell(Mathf.FloorToInt(feet.x), Mathf.FloorToInt(feet.y));
            var n = sr.gameObject.AddComponent<Npc>();
            n.id = id;
            n.color = color;
            n.gift = gift;
            n.radius = 1.7f;
            n.MakePrompt(frames[0].rect.height / frames[0].pixelsPerUnit + 0.6f);
            WorldLabel.Create(sr.transform, feet + new Vector2(0f, -0.35f), Loc.T("npc." + id + ".name"), color, 44, 0.7f);
            return n;
        }

        /// <summary>Makes an existing figure talkable (the merchant of the shop).</summary>
        public static Npc Attach(SpriteRenderer sr, string id, Color color, float promptHeight)
        {
            if (!sr) return null;
            var n = sr.gameObject.AddComponent<Npc>();
            n.id = id;
            n.color = color;
            n.radius = 2.4f;
            n.MakePrompt(promptHeight);
            return n;
        }

        string Name => Loc.T("npc." + id + ".name");

        protected override bool Ready => Enemy.All.Count == 0 && !Bosses.Fighting;

        protected override void Use()
        {
            if (RoomManager.I) RoomManager.I.StartCoroutine(Talk());
        }

        IEnumerator Talk()
        {
            Cutscene.Begin();
            Cutscene.Look((Vector2)transform.position + Vector2.up * 0.6f);
            if (talks == 0)
            {
                // they remember you: every new meeting goes a step further in their story
                int met = SaveSystem.MeetNpc(id);
                string story = $"npc.{id}.story{met}";
                if (met > 0 && Loc.Has(story))
                    yield return Cutscene.Say(Name, Loc.T(story), color);
                else
                    for (int i = 1; i <= 4 && Loc.Has($"npc.{id}.{i}"); i++)
                        yield return Cutscene.Say(Name, Loc.T($"npc.{id}.{i}"), color);
                if (gift != null)
                {
                    if (Loc.Has($"npc.{id}.gift")) yield return Cutscene.Say(Name, Loc.T($"npc.{id}.gift"), color);
                    gift();
                    gift = null;
                }
            }
            else
            {
                string key = Loc.Has($"npc.{id}.again{talks % 2}") ? $"npc.{id}.again{talks % 2}" : $"npc.{id}.again0";
                yield return Cutscene.Say(Name, Loc.T(key), color);
            }
            talks++;
            Cutscene.End();
        }
    }

    /// <summary>
    /// Pixie, the System's fairy navigator: a little light that follows the hero and speaks the hints.
    /// </summary>
    public class Pixie : MonoBehaviour
    {
        public static Pixie I { get; private set; }

        public static readonly Color Color = new Color(1f, 0.6f, 0.95f);
        public static string Name => Loc.T("name.pixie");

        Transform target;
        SpriteRenderer core;
        Light2D glow;
        Vector3 vel;
        float ping, phase;

        public static Pixie Spawn(Transform parent, Transform follow)
        {
            if (I) Destroy(I.gameObject);
            var go = new GameObject("Pixie");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<Pixie>();
            p.target = follow;
            go.transform.position = follow ? follow.position + new Vector3(-0.8f, 1.4f, 0f) : Vector3.zero;
            p.core = go.AddComponent<SpriteRenderer>();
            p.core.sprite = Gfx.Orb;
            p.core.color = Color.white;
            p.core.sortingOrder = 40;
            go.transform.localScale = Vector3.one * 0.9f;
            if (Fx.Unlit) p.core.sharedMaterial = Fx.Unlit;
            Fx.Loop("sparkle", go.transform, Vector2.zero, 0.25f, 39);
            p.glow = LightUtil.Point(go.transform, Vector3.zero, Color, 1.8f, 1.1f);
            I = p;
            return p;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        /// <summary>She jumps when she has something to say.</summary>
        public void Ping() => ping = 0.6f;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (!target) return;
            phase += dt;
            ping = Mathf.Max(0f, ping - dt);
            var player = PlayerController.I;
            float side = player != null && player.Velocity.x > 0.1f ? -1f : 1f;
            Vector3 want = target.position + new Vector3(0.9f * side, 1.5f + Mathf.Sin(phase * 3f) * 0.15f + ping * 0.8f, 0f);
            transform.position = Vector3.SmoothDamp(transform.position, want, ref vel, 0.25f, 30f, dt);
            float pulse = 0.85f + 0.15f * Mathf.Sin(phase * 6f);
            core.color = Color.Lerp(Color.white, Color, 0.4f + 0.2f * Mathf.Sin(phase * 2f));
            transform.localScale = Vector3.one * (0.8f + 0.15f * pulse + ping * 0.3f);
            if (glow) glow.intensity = 0.9f + 0.3f * pulse + ping;
        }
    }

    /// <summary>The rift before it opens: a thin line in the wall that flickers now and then. Easy to miss.</summary>
    public class RiftFlicker : MonoBehaviour
    {
        SpriteRenderer sr;
        Color color;
        float t, next;

        public void Init(SpriteRenderer r, Color c)
        {
            sr = r;
            color = c;
            next = Random.Range(1f, 3f);
        }

        void Update()
        {
            if (!sr) return;
            t += Time.deltaTime;
            float a = 0.12f + 0.08f * Mathf.Sin(t * 3f);
            if (t > next)
            {
                // a short glitch: brighter for a few frames, a pixel or two fly off
                a = 0.6f;
                if (t > next + 0.12f)
                {
                    next = t + Random.Range(2f, 5f);
                    Fx.Burst((Vector2)transform.position + Random.insideUnitCircle * 0.3f, color, 2, 0.8f, 0.3f, 1f);
                }
            }
            sr.color = new Color(color.r, color.g, color.b, a);
        }
    }

    /// <summary>The System's gacha terminal: E pays and pulls (see RoomManager.TryGamble). An old computer in the dungeon.</summary>
    public class GachaTerminal : Interactable
    {
        SpriteRenderer sr, screen;
        System.Func<Vector2, bool> onPull;
        float flash;

        public static GachaTerminal Create(Room room, Vector2 feet, System.Func<Vector2, bool> onPull)
        {
            var a = GameAssets.I;
            if (!a) return null;
            Sprite s = a.HasModern ? a.Modern("computer") : null;
            if (!s) s = a.D("column_wall") ?? a.D("column");
            var sr = room.PropSprite(s ? s : Gfx.White, feet, 0, "GachaTerminal");
            if (!sr) return null;
            sr.transform.localScale = Vector3.one * 1.4f;
            room.BlockCell(Mathf.FloorToInt(feet.x), Mathf.FloorToInt(feet.y));
            var t = sr.gameObject.AddComponent<GachaTerminal>();
            t.sr = sr;
            t.onPull = onPull;
            t.radius = 1.8f;
            t.MakePrompt(2.4f);
            // a glowing screen so it reads as "the System" in any tileset
            var glow = new GameObject("Screen").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(sr.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            glow.sprite = Gfx.Orb;
            glow.color = new Color(1f, 0.55f, 0.9f, 0.35f);
            glow.sortingOrder = 4;
            glow.transform.localScale = Vector3.one * 1.3f;
            Gfx.Unlit(glow);
            t.screen = glow;
            LightUtil.Point(sr.transform, new Vector3(0f, 0.9f, 0f), new Color(1f, 0.55f, 0.9f), 2.6f, 1f);
            WorldLabel.Create(sr.transform, feet + new Vector2(0f, -0.4f), Loc.T("room.gamble"), new Color(1f, 0.55f, 0.9f), 44, 0.7f);
            return t;
        }

        protected override bool Ready => Enemy.All.Count == 0 && !Bosses.Fighting;

        protected override void Use()
        {
            if (onPull == null) return;
            if (onPull((Vector2)transform.position)) flash = 0.6f;
        }

        protected override void Update()
        {
            base.Update();
            flash -= Time.deltaTime;
            if (!screen) return;
            float k = flash > 0f ? 0.6f + 0.4f * Mathf.Sin(Time.time * 40f) : 0.3f + 0.08f * Mathf.Sin(Time.time * 3f);
            var c = screen.color;
            c.a = k;
            screen.color = c;
        }
    }
}
