using UnityEngine;

namespace RulesShift
{
    /// <summary>TreasureChest that opens when the hero walks into it.</summary>
    public class TreasureChest : MonoBehaviour
    {
        public bool opened;
        public System.Action onOpen;
        SpriteRenderer sr;
        Sprite[] frames;
        float t;

        public static TreasureChest Spawn(Room room, Vector2 feet, bool golden, System.Action onOpen)
        {
            var a = GameAssets.I;
            Sprite[] f;
            if (golden && room.Theme == Theme.Classic && a && a.C("chest_golden_closed"))
                f = new[] { a.C("chest_golden_closed"), a.C("chest_golden_open_full") };
            else if (room.Theme == Theme.Retro && a && a.HasAnim("sgq_chest_anim"))
                f = a.Anim("sgq_chest_anim");
            else f = a ? a.Anim("chest_full_open_anim") : new Sprite[0];

            var go = new GameObject("Chest");
            go.transform.SetParent(room.Content, false);
            go.transform.position = feet;
            var c = go.AddComponent<TreasureChest>();
            c.frames = f;
            c.onOpen = onOpen;
            c.sr = go.AddComponent<SpriteRenderer>();
            c.sr.sprite = f.Length > 0 ? f[0] : Gfx.White;
            c.sr.sortingOrder = 0;
            c.sr.spriteSortPoint = SpriteSortPoint.Pivot;
            Gfx.Lit(c.sr);
            if (Rules.Current == Law.Crypt || Rules.Current == Law.Neon)
                LightUtil.Point(go.transform, new Vector3(0f, 0.5f, 0f), new Color(1f, 0.85f, 0.4f), 2.5f, 1.2f);
            Fx.Burst(feet + Vector2.up * 0.5f, new Color(1f, 0.9f, 0.5f), 12, 3f, 0.5f, 1.5f);
            return c;
        }

        void Update()
        {
            var p = PlayerController.I;
            if (!opened)
            {
                // little bounce so it reads as "take me"
                float s = 1f + Mathf.Sin(Time.time * 6f) * 0.04f;
                transform.localScale = new Vector3(1f, s, 1f);
                if (p != null && p.Alive && Vector2.Distance(p.Feet, transform.position) < 0.95f) Open();
                return;
            }
            if (frames.Length > 1)
            {
                t += Time.unscaledDeltaTime * 10f;
                sr.sprite = frames[Mathf.Min(frames.Length - 1, (int)t)];
            }
        }

        public void Open()
        {
            if (opened) return;
            opened = true;
            transform.localScale = Vector3.one;
            Sfx.Play("pickup");
            Fx.Burst((Vector2)transform.position + Vector2.up * 0.5f, new Color(1f, 0.85f, 0.3f), 16, 4f, 0.6f, 1.5f);
            Fx.Anim("sparkle", (Vector2)transform.position + Vector2.up * 0.6f, 1f);
            onOpen?.Invoke();
        }
    }
}
