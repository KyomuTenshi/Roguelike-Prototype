using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>A coin on the floor. Pops out, then flies to the hero when close (or when the room is cleared).</summary>
    public class Pickup : MonoBehaviour
    {
        static readonly List<Pickup> all = new List<Pickup>();

        SpriteRenderer sr;
        Vector2 vel;
        float age, bob;
        bool magnet;

        public static void Coins(Vector2 pos, int count)
        {
            for (int i = 0; i < count; i++) Spawn(pos);
        }

        static void Spawn(Vector2 pos)
        {
            var go = new GameObject("Coin");
            if (Fx.Root) go.transform.SetParent(Fx.Root, false);
            go.transform.position = pos;
            var p = go.AddComponent<Pickup>();
            var child = new GameObject("Sprite");
            child.transform.SetParent(go.transform, false);
            p.sr = child.AddComponent<SpriteRenderer>();
            var frames = GameAssets.I ? GameAssets.I.Anim("coin_anim") : new Sprite[0];
            p.sr.sprite = frames.Length > 0 ? frames[0] : Gfx.Dot;
            p.sr.sortingOrder = 5;
            Gfx.Unlit(p.sr);
            if (frames.Length > 1)
            {
                var a = child.AddComponent<SpriteAnimator>();
                a.target = p.sr;
                a.Play(frames, Random.Range(7f, 10f));
            }
            p.vel = Random.insideUnitCircle.normalized * Random.Range(2f, 4.5f);
            p.bob = Random.value * 6f;
            all.Add(p);
        }

        /// <summary>The room is cleared: every coin flies to the hero.</summary>
        public static void MagnetAll()
        {
            foreach (var p in all) if (p) p.magnet = true;
        }

        void OnDestroy() => all.Remove(this);

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;
            var player = PlayerController.I;
            Vector2 pos = transform.position;
            if (player != null && player.Alive && age > 0.35f)
            {
                Vector2 to = player.Position - pos;
                float d = to.magnitude;
                if (d < 1.7f) magnet = true;
                if (magnet) vel = Vector2.MoveTowards(vel, to.normalized * 14f, 60f * dt);
                if (d < 0.45f)
                {
                    if (RoomManager.I) RoomManager.I.AddCoins(1);
                    Sfx.Play("coin", 0.45f, Random.Range(0.95f, 1.1f));
                    Fx.Burst(pos, new Color(1f, 0.9f, 0.4f), 3, 1.5f, 0.2f, 1f);
                    Destroy(gameObject);
                    return;
                }
            }
            if (!magnet) vel = Vector2.MoveTowards(vel, Vector2.zero, 9f * dt);
            pos += vel * dt;
            // stay inside the room
            var room = RoomManager.I ? RoomManager.I.CurrentRoom : null;
            if (room && !magnet)
            {
                pos.x = Mathf.Clamp(pos.x, 0.4f, room.W - 0.4f);
                pos.y = Mathf.Clamp(pos.y, 0.3f, room.H - 0.6f);
            }
            transform.position = new Vector3(pos.x, pos.y, 0f);
            sr.transform.localPosition = new Vector3(0f, Mathf.Round(Mathf.Sin(Time.time * 4f + bob) * 1.5f) / 16f, 0f);
        }
    }
}
