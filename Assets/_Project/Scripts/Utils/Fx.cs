using UnityEngine;

namespace RulesShift
{
    /// <summary>Tiny effects without a particle system: pixel bursts and floating text.</summary>
    public static class Fx
    {
        public static Transform Root;   // the current room, so effects die with it

        public static void Burst(Vector2 pos, Color color, int count = 10, float speed = 4f, float life = 0.45f, float size = 1.5f)
        {
            var go = new GameObject("Burst");
            if (Root) go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var b = go.AddComponent<FxBurst>();
            b.Init(color, count, speed, life, size);
        }

        public static void Text(Vector2 pos, string text, Color color, float size = 1f, float life = 0.9f)
        {
            var go = new GameObject("Text");
            if (Root) go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var t = go.AddComponent<FxText>();
            t.Init(text, color, size, life);
        }

        /// <summary>A small icon that floats up and fades (a relic just worked).</summary>
        public static void Icon(Sprite s, Vector2 pos, float life = 0.7f, float scale = 0.8f)
        {
            if (!s) return;
            var go = new GameObject("Icon");
            if (Root) go.transform.SetParent(Root, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = 41;
            if (Unlit) sr.sharedMaterial = Unlit;
            go.AddComponent<FxFloat>().Init(sr, life);
        }

        public static Material Unlit => GameAssets.I ? GameAssets.I.unlitSprite : null;

        /// <summary>
        /// Plays a pixel effect from the Super Pixel Effects pack ("hit", "death", "boom"…) at 15 fps.
        /// scale 1 = the effect's own pixel size. Returns false when the effect is missing (use a Burst instead).
        /// </summary>
        public static bool Anim(string key, Vector2 pos, float scale = 0.5f, float angle = 0f, int order = 36, Color? tint = null, Transform follow = null)
        {
            var frames = GameAssets.I ? GameAssets.I.Effect(key) : null;
            if (frames == null || frames.Length == 0) return false;
            var go = new GameObject("Fx_" + key);
            if (follow) go.transform.SetParent(follow, false);
            else if (Root) go.transform.SetParent(Root, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = order;
            if (tint.HasValue) sr.color = tint.Value;
            if (Unlit) sr.sharedMaterial = Unlit;
            go.AddComponent<FxAnim>().Init(sr, frames, false);
            return true;
        }

        /// <summary>Plays any frame set once (a monster's death, a boss's swing) and removes itself. Lit like the room.</summary>
        public static GameObject Frames(Sprite[] frames, Vector2 pos, float fps, bool flipX = false, int order = 0, float scale = 1f)
        {
            if (frames == null || frames.Length == 0) return null;
            var go = new GameObject("Fx_frames");
            if (Root) go.transform.SetParent(Root, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.flipX = flipX;
            sr.sortingOrder = order;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            Gfx.Lit(sr);
            go.AddComponent<FxAnim>().Init(sr, frames, false, fps);
            return go;
        }

        /// <summary>A looping effect (fire on a burning enemy). Destroy the returned object to stop it.</summary>
        public static GameObject Loop(string key, Transform parent, Vector2 localPos, float scale = 0.35f, int order = 3)
        {
            var frames = GameAssets.I ? GameAssets.I.Effect(key) : null;
            if (frames == null || frames.Length == 0 || !parent) return null;
            var go = new GameObject("FxLoop_" + key);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = order;
            if (Unlit) sr.sharedMaterial = Unlit;
            go.AddComponent<FxAnim>().Init(sr, frames, true);
            return go;
        }
    }

    /// <summary>Frame animation for Fx.Anim / Fx.Loop (game time, 15 fps).</summary>
    public class FxAnim : MonoBehaviour
    {
        SpriteRenderer sr;
        Sprite[] frames;
        bool loop;
        float t;

        float fps = 15f;

        public void Init(SpriteRenderer r, Sprite[] f, bool looping, float framesPerSecond = 15f)
        {
            sr = r;
            frames = f;
            loop = looping;
            fps = framesPerSecond;
        }

        void Update()
        {
            t += Time.deltaTime * fps;
            int i = (int)t;
            if (i >= frames.Length)
            {
                if (!loop) { Destroy(gameObject); return; }
                t = 0f;
                i = 0;
            }
            sr.sprite = frames[i];
        }
    }

    /// <summary>
    /// A burst of square pixels. The pixels come from a shared pool (bursts are everywhere: hits, trails, relics),
    /// so a busy fight doesn't create and destroy hundreds of objects every second.
    /// </summary>
    public class FxBurst : MonoBehaviour
    {
        static readonly System.Collections.Generic.Stack<SpriteRenderer> pool = new System.Collections.Generic.Stack<SpriteRenderer>();
        static Transform poolRoot;

        SpriteRenderer[] parts;
        Vector2[] vel;
        float life, t;

        static SpriteRenderer Take(Transform parent)
        {
            while (pool.Count > 0)
            {
                var sr = pool.Pop();
                if (!sr) continue;   // destroyed together with an old room
                sr.transform.SetParent(parent, false);
                sr.transform.localPosition = Vector3.zero;
                sr.gameObject.SetActive(true);
                return sr;
            }
            var go = new GameObject("p");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = Gfx.White;
            r.sortingOrder = 30;
            return r;
        }

        void Give()
        {
            if (!poolRoot)
            {
                poolRoot = new GameObject("FxPool").transform;
                poolRoot.gameObject.SetActive(false);
            }
            foreach (var p in parts)
            {
                if (!p) continue;
                p.transform.SetParent(poolRoot, false);
                if (pool.Count < 600) pool.Push(p);
                else Destroy(p.gameObject);
            }
            parts = null;
        }

        public void Init(Color color, int count, float speed, float lifeTime, float size)
        {
            life = lifeTime;
            parts = new SpriteRenderer[count];
            vel = new Vector2[count];
            var mat = Fx.Unlit;
            for (int i = 0; i < count; i++)
            {
                var sr = Take(transform);
                sr.transform.localScale = Vector3.one * size * Random.Range(0.7f, 1.3f);
                sr.color = color;
                if (mat) sr.sharedMaterial = mat;
                parts[i] = sr;
                vel[i] = Random.insideUnitCircle.normalized * speed * Random.Range(0.4f, 1f) + Vector2.up * 1.5f;
            }
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = 1f - t / life;
            if (k <= 0f) { Give(); Destroy(gameObject); return; }
            for (int i = 0; i < parts.Length; i++)
            {
                if (!parts[i]) continue;
                vel[i] *= 1f - Time.deltaTime * 4f;
                vel[i].y -= Time.deltaTime * 6f;
                parts[i].transform.localPosition += (Vector3)(vel[i] * Time.deltaTime);
                var c = parts[i].color; c.a = k; parts[i].color = c;
            }
        }
    }

    /// <summary>Floats up and fades out (Fx.Icon).</summary>
    public class FxFloat : MonoBehaviour
    {
        SpriteRenderer sr;
        float life, t;

        public void Init(SpriteRenderer r, float lifeTime) { sr = r; life = lifeTime; }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t >= life) { Destroy(gameObject); return; }
            transform.position += Vector3.up * Time.unscaledDeltaTime * 1.4f;
            float k = t / life;
            transform.localScale = Vector3.one * (0.8f + 0.25f * Mathf.Sin(Mathf.Min(1f, k * 3f) * Mathf.PI));
            var c = sr.color; c.a = Mathf.Clamp01((1f - k) * 2.5f); sr.color = c;
        }
    }

    public class FxText : MonoBehaviour
    {
        TextMesh tm;
        float life, t;
        Color color;

        public void Init(string text, Color c, float size, float lifeTime)
        {
            life = lifeTime;
            color = c;
            tm = gameObject.AddComponent<TextMesh>();
            var font = GameAssets.I ? GameAssets.I.BodyFont : null;
            if (font)
            {
                tm.font = font;
                GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.12f * size;   // ≈ fontSize * characterSize / 10 world units tall
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = c;
            GetComponent<MeshRenderer>().sortingOrder = 40;
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t >= life) { Destroy(gameObject); return; }
            transform.position += Vector3.up * Time.deltaTime * 1.2f;
            // keep text readable when the camera is upside down
            if (CameraFollow.I) transform.rotation = CameraFollow.I.transform.rotation;
            var c = color; c.a = Mathf.Clamp01((life - t) * 3f); tm.color = c;
        }
    }
}
