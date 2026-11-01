using System;
using UnityEngine;

namespace RulesShift
{
    /// <summary>Small sprites generated in code: pixels, orbs, shadows, lava. No extra art needed.</summary>
    public static class Gfx
    {
        public const float PPU = 16f;
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

        static Sprite white, orb, shadow, ring, dot, lavaA, lavaB, warn, uiPanel;

        /// <summary>1x1 white pixel (1/16 world unit).</summary>
        public static Sprite White => white ? white : white = Make(1, 1, (x, y) => Color.white, Center);

        /// <summary>7x7 orb: white core, dark rim. Tint with SpriteRenderer.color.</summary>
        public static Sprite Orb => orb ? orb : orb = Make(7, 7, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(3, 3));
            if (d > 3.4f) return Color.clear;
            if (d > 2.5f) return new Color(0.15f, 0.05f, 0.2f, 1f);
            if (d < 1.1f) return Color.white;
            return new Color(0.85f, 0.85f, 0.85f, 1f);
        }, Center);

        /// <summary>Soft ellipse shadow under characters.</summary>
        public static Sprite Shadow => shadow ? shadow : shadow = Make(12, 5, (x, y) =>
        {
            float nx = (x - 5.5f) / 6f, ny = (y - 2f) / 2.5f;
            float d = nx * nx + ny * ny;
            return d > 1f ? Color.clear : new Color(0f, 0f, 0f, 0.35f);
        }, Center);

        /// <summary>32x32 ring for UI (beat indicator).</summary>
        public static Sprite Ring => ring ? ring : ring = Make(32, 32, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
            return d < 15.5f && d > 12.5f ? Color.white : Color.clear;
        }, Center);

        /// <summary>16x16 filled circle for UI.</summary>
        public static Sprite Dot => dot ? dot : dot = Make(16, 16, (x, y) =>
            Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f)) < 7.5f ? Color.white : Color.clear, Center);

        /// <summary>9-slice UI panel: dark fill with a light 1px border.</summary>
        public static Sprite Panel
        {
            get
            {
                if (uiPanel) return uiPanel;
                var tex = NewTex(9, 9);
                for (int y = 0; y < 9; y++)
                for (int x = 0; x < 9; x++)
                {
                    bool border = x == 0 || y == 0 || x == 8 || y == 8;
                    bool inner = x == 1 || y == 1 || x == 7 || y == 7;
                    tex.SetPixel(x, y, border ? new Color(0.05f, 0.04f, 0.07f, 1f)
                        : inner ? new Color(0.78f, 0.69f, 0.58f, 1f)
                        : new Color(0.13f, 0.11f, 0.15f, 0.94f));
                }
                tex.Apply();
                uiPanel = Sprite.Create(tex, new Rect(0, 0, 9, 9), Center, PPU, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
                uiPanel.name = "gfx_panel";
                return uiPanel;
            }
        }

        static Sprite vignette;

        /// <summary>64x64 radial vignette: transparent centre, opaque edges (stretched full-screen).</summary>
        public static Sprite Vignette
        {
            get
            {
                if (vignette) return vignette;
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color[64 * 64];
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * 64 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((d - 0.55f) / 0.6f));
                }
                tex.SetPixels(px);
                tex.Apply();
                vignette = Sprite.Create(tex, new Rect(0, 0, 64, 64), Center, 100f);
                vignette.name = "gfx_vignette";
                return vignette;
            }
        }

        public static Sprite LavaA => lavaA ? lavaA : lavaA = Lava(0);
        public static Sprite LavaB => lavaB ? lavaB : lavaB = Lava(1);

        /// <summary>Red warning tile drawn before a floor cell turns into lava.</summary>
        public static Sprite Warning => warn ? warn : warn = Make(16, 16, (x, y) =>
        {
            bool edge = x == 0 || y == 0 || x == 15 || y == 15;
            bool cross = Mathf.Abs(x - y) < 1 || Mathf.Abs(x - (15 - y)) < 1;
            if (edge) return new Color(1f, 0.35f, 0.1f, 0.9f);
            if (cross) return new Color(1f, 0.45f, 0.15f, 0.55f);
            return new Color(1f, 0.3f, 0.05f, 0.18f);
        }, BottomCenter);

        static Sprite Lava(int frame)
        {
            var rnd = new System.Random(1234 + frame * 77);
            float ox = frame * 3.7f;
            return Make(16, 16, (x, y) =>
            {
                float n = Mathf.PerlinNoise((x + ox) * 0.22f, (y + frame * 2.1f) * 0.22f);
                float n2 = Mathf.PerlinNoise((x + 40) * 0.5f, (y + 13 + frame) * 0.5f);
                float v = n * 0.75f + n2 * 0.25f;
                Color dark = new Color(0.55f, 0.08f, 0.05f, 1f);
                Color mid = new Color(0.95f, 0.32f, 0.07f, 1f);
                Color hot = new Color(1f, 0.83f, 0.3f, 1f);
                Color c = v < 0.45f ? dark : v < 0.62f ? mid : hot;
                if (rnd.NextDouble() < 0.02) c = hot;
                return c;
            }, BottomCenter);
        }

        /// <summary>Make sure a renderer uses the 2D-lit sprite material (needed for the dark rooms).</summary>
        public static void Lit(Renderer r)
        {
            var m = GameAssets.I ? GameAssets.I.litSprite : null;
            if (r && m) r.sharedMaterial = m;
        }

        public static void Unlit(Renderer r)
        {
            var m = GameAssets.I ? GameAssets.I.unlitSprite : null;
            if (r && m) r.sharedMaterial = m;
        }

        static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        public static Sprite Make(int w, int h, Func<int, int, Color> pixel, Vector2 pivot)
        {
            var tex = NewTex(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = pixel(x, y);
            tex.SetPixels(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, PPU, 0, SpriteMeshType.FullRect);
            s.name = "gfx";
            return s;
        }
    }
}
