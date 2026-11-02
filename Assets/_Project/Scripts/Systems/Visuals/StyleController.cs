using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace RulesShift
{
    /// <summary>
    /// The look of each law: URP post-processing (built in code), global light, the hero's light,
    /// background colour and the full-screen colour inversion of the Mirror hall.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class StyleController : MonoBehaviour
    {
        public static StyleController I { get; private set; }

        public Camera cam;
        public Light2D globalLight;
        public Volume volume;

        struct Look
        {
            public float light; public Color lightColor; public Color background;
            public bool heroLight; public float heroRadius; public float heroIntensity; public Color heroColor;
            public float invert; public float shake;
        }

        readonly Dictionary<Law, VolumeProfile> profiles = new Dictionary<Law, VolumeProfile>();
        readonly Dictionary<Law, Vector2> bases = new Dictionary<Law, Vector2>(); // bloom, chroma as built
        Look look;
        Law current = (Law)(-1);
        RawImage invertImage;
        float invertAlpha;

        // components of the active profile that are animated every frame
        Bloom bloom; ChromaticAberration chroma;
        float bloomBase, chromaBase;

        void Awake()
        {
            I = this;
            if (!cam) cam = Camera.main;
            BuildInvertOverlay();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            foreach (var p in profiles.Values) if (p) Destroy(p);
        }

        void BuildInvertOverlay()
        {
            var mat = GameAssets.I ? GameAssets.I.invertMaterial : null;
            if (!mat) return;
            var go = new GameObject("InvertOverlay", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -50;
            var imgGo = new GameObject("Invert", typeof(RectTransform));
            imgGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)imgGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            invertImage = imgGo.AddComponent<RawImage>();
            invertImage.material = mat;
            invertImage.raycastTarget = false;
            invertImage.color = new Color(1f, 1f, 1f, 0f);
            invertImage.enabled = false;
        }

        public void Apply(Law law, bool instant = false)
        {
            current = law;
            look = LookOf(law);
            var profile = GetProfile(law);
            if (volume)
            {
                volume.profile = profile;
                volume.weight = 1f;
            }
            profile.TryGet(out bloom);
            profile.TryGet(out chroma);
            var b = bases[law];
            bloomBase = b.x;
            chromaBase = b.y;

            if (cam) cam.backgroundColor = look.background;
            if (instant)
            {
                if (globalLight) { globalLight.intensity = look.light; globalLight.color = look.lightColor; }
                invertAlpha = look.invert;
            }
            if (CameraFollow.I) CameraFollow.I.ambientShake = look.shake;
            ApplyHeroLight();
        }

        public void ApplyHeroLight()
        {
            var p = PlayerController.I;
            if (p == null || p.heroLight == null) return;
            p.heroLight.enabled = look.heroLight;
            p.heroLight.pointLightOuterRadius = look.heroRadius;
            p.heroLight.pointLightInnerRadius = look.heroRadius * 0.35f;
            p.heroLight.intensity = look.heroIntensity;
            p.heroLight.color = look.heroColor;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float pulse = 0f;
            if (current == Law.Neon)
            {
                float beat = MusicPlayer.Beat;
                pulse = Mathf.Exp(-(beat - Mathf.Floor(beat)) * 6f);
            }

            if (globalLight)
            {
                float target = look.light * (1f + 0.7f * pulse);
                globalLight.intensity = Mathf.MoveTowards(globalLight.intensity, target, dt * 3f);
                globalLight.color = Color.Lerp(globalLight.color, look.lightColor, dt * 4f);
            }

            if (bloom) bloom.intensity.value = bloomBase * (1f + 0.9f * pulse);
            if (chroma)
            {
                float flicker = current == Law.Console && Random.value < 0.05f ? Random.Range(0.4f, 1f) : 0f;
                chroma.intensity.value = Mathf.Clamp01(chromaBase + flicker);
            }

            invertAlpha = Mathf.MoveTowards(invertAlpha, look.invert, dt * 2.5f);
            if (invertImage)
            {
                invertImage.enabled = invertAlpha > 0.001f;
                invertImage.color = new Color(1f, 1f, 1f, invertAlpha);
            }
        }

        static Look LookOf(Law law)
        {
            var l = new Look
            {
                light = 1f, lightColor = new Color(1f, 0.95f, 0.9f), background = new Color(0.075f, 0.065f, 0.09f),
                heroLight = false, heroRadius = 5f, heroIntensity = 0.6f, heroColor = new Color(1f, 0.9f, 0.75f)
            };
            switch (law)
            {
                case Law.Crypt:
                    l.light = 0.03f; l.lightColor = new Color(0.55f, 0.65f, 1f); l.background = new Color(0.01f, 0.01f, 0.02f);
                    l.heroLight = true; l.heroRadius = 4.8f; l.heroIntensity = 1.35f; l.heroColor = new Color(1f, 0.82f, 0.6f);
                    break;
                case Law.Noir: l.light = 0.95f; l.lightColor = Color.white; l.background = Color.black; break;
                case Law.Ice: l.light = 0.95f; l.lightColor = new Color(0.8f, 0.9f, 1.1f); l.background = new Color(0.04f, 0.06f, 0.1f); break;
                case Law.Mirror: l.invert = 1f; l.background = new Color(0.08f, 0.05f, 0.09f); break;
                case Law.Pacifist: l.light = 1.15f; l.lightColor = new Color(1f, 0.93f, 0.95f); l.background = new Color(0.14f, 0.11f, 0.14f); break;
                case Law.Retro: l.light = 1f; l.lightColor = Color.white; l.background = new Color(0.06f, 0.09f, 0.06f); break;
                case Law.Lava:
                    l.light = 0.85f; l.lightColor = new Color(1f, 0.72f, 0.55f); l.background = new Color(0.1f, 0.02f, 0.01f);
                    l.shake = 0.025f;
                    break;
                case Law.Stealth:
                    l.light = 0.7f; l.lightColor = new Color(1f, 0.88f, 0.68f); l.background = new Color(0.06f, 0.04f, 0.02f);
                    l.heroLight = true; l.heroRadius = 6f; l.heroIntensity = 0.5f; l.heroColor = new Color(1f, 0.8f, 0.5f);
                    break;
                case Law.Upside: l.light = 1f; l.background = new Color(0.08f, 0.05f, 0.05f); break;
                case Law.Neon:
                    l.light = 0.12f; l.lightColor = new Color(0.65f, 0.4f, 1f); l.background = new Color(0.02f, 0f, 0.05f);
                    l.heroLight = true; l.heroRadius = 3.2f; l.heroIntensity = 1.6f; l.heroColor = new Color(0.3f, 1f, 1f);
                    break;
                case Law.Console: l.light = 0.6f; l.lightColor = new Color(0.7f, 0.85f, 1f); l.background = new Color(0.02f, 0.04f, 0.07f); break;
                case Law.Boss: l.light = 0.8f; l.lightColor = new Color(1f, 0.78f, 0.78f); l.background = new Color(0.08f, 0.01f, 0.02f); break;
            }
            return l;
        }

        VolumeProfile GetProfile(Law law)
        {
            if (profiles.TryGetValue(law, out var p) && p) return p;
            p = BuildProfile(law);
            profiles[law] = p;
            p.TryGet(out Bloom bl);
            p.TryGet(out ChromaticAberration cr);
            bases[law] = new Vector2(bl ? bl.intensity.value : 0f, cr ? cr.intensity.value : 0f);
            return p;
        }

        VolumeProfile BuildProfile(Law law)
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.name = "Style_" + law;
            var ca = p.Add<ColorAdjustments>(true);
            var bl = p.Add<Bloom>(true);
            var vg = p.Add<Vignette>(true);
            var fg = p.Add<FilmGrain>(true);
            var cr = p.Add<ChromaticAberration>(true);
            var wb = p.Add<WhiteBalance>(true);

            ca.postExposure.value = 0f; ca.contrast.value = 0f; ca.saturation.value = 0f; ca.hueShift.value = 0f;
            ca.colorFilter.value = Color.white;
            bl.threshold.value = 0.85f; bl.intensity.value = 0.4f; bl.scatter.value = 0.6f; bl.tint.value = Color.white;
            vg.intensity.value = 0.25f; vg.smoothness.value = 0.4f; vg.color.value = Color.black;
            fg.type.value = FilmGrainLookup.Thin1; fg.intensity.value = 0f; fg.response.value = 0.8f;
            cr.intensity.value = 0f;
            wb.temperature.value = 0f; wb.tint.value = 0f;

            switch (law)
            {
                case Law.None:
                    wb.temperature.value = 8f;
                    break;
                case Law.Crypt:
                    ca.postExposure.value = 0.2f; ca.saturation.value = -25f; ca.colorFilter.value = new Color(0.82f, 0.88f, 1f);
                    vg.intensity.value = 0.45f; fg.intensity.value = 0.3f;
                    break;
                case Law.Noir:
                    ca.saturation.value = -100f; ca.contrast.value = 35f;
                    fg.type.value = FilmGrainLookup.Medium4; fg.intensity.value = 0.9f; fg.response.value = 0.6f;
                    vg.intensity.value = 0.5f; vg.smoothness.value = 0.45f; bl.intensity.value = 0.2f;
                    break;
                case Law.Ice:
                    ca.colorFilter.value = new Color(0.8f, 0.93f, 1.15f); ca.contrast.value = 10f; wb.temperature.value = -35f;
                    bl.threshold.value = 0.75f; bl.intensity.value = 1.4f; bl.tint.value = new Color(0.7f, 0.9f, 1f);
                    vg.intensity.value = 0.2f;
                    break;
                case Law.Mirror:
                    cr.intensity.value = 0.35f; vg.intensity.value = 0.3f;
                    if (!GameAssets.I || !GameAssets.I.invertMaterial) { ca.hueShift.value = 180f; ca.contrast.value = 20f; }
                    break;
                case Law.Pacifist:
                    ca.contrast.value = -25f; ca.postExposure.value = 0.35f; ca.saturation.value = -10f;
                    ca.colorFilter.value = new Color(1f, 0.94f, 0.97f);
                    bl.threshold.value = 0.6f; bl.intensity.value = 2.2f; bl.scatter.value = 0.8f; bl.tint.value = new Color(1f, 0.9f, 0.95f);
                    vg.intensity.value = 0.15f;
                    break;
                case Law.Retro:
                    ca.saturation.value = -100f; ca.contrast.value = 100f; ca.postExposure.value = 0.6f;
                    ca.colorFilter.value = new Color(0.72f, 1f, 0.66f);
                    // bloom stays on but invisible: switching it off and on breaks URP's render graph in the editor
                    vg.intensity.value = 0.2f; bl.intensity.value = 0.01f; bl.threshold.value = 4f;
                    break;
                case Law.Lava:
                    ca.colorFilter.value = new Color(1.15f, 0.78f, 0.6f); ca.contrast.value = 20f; ca.saturation.value = 10f;
                    bl.threshold.value = 0.7f; bl.intensity.value = 1.6f; bl.tint.value = new Color(1f, 0.5f, 0.2f);
                    vg.intensity.value = 0.45f; vg.color.value = new Color(0.35f, 0f, 0f);
                    break;
                case Law.Stealth:
                    ca.saturation.value = -100f; ca.colorFilter.value = new Color(1.12f, 0.92f, 0.62f); ca.contrast.value = 15f;
                    fg.type.value = FilmGrainLookup.Thin2; fg.intensity.value = 0.35f;
                    vg.intensity.value = 0.45f; vg.color.value = new Color(0.15f, 0.08f, 0f);
                    break;
                case Law.Upside:
                    cr.intensity.value = 0.25f; ca.contrast.value = 10f; vg.intensity.value = 0.3f;
                    break;
                case Law.Neon:
                    bl.threshold.value = 0.45f; bl.intensity.value = 2.4f; bl.scatter.value = 0.75f;
                    ca.saturation.value = 30f; ca.contrast.value = 20f; cr.intensity.value = 0.2f;
                    vg.intensity.value = 0.35f; vg.color.value = new Color(0.15f, 0f, 0.2f);
                    break;
                case Law.Console:
                    ca.colorFilter.value = new Color(0.85f, 0.95f, 1.1f); cr.intensity.value = 0.2f;
                    fg.intensity.value = 0.25f; vg.intensity.value = 0.3f;
                    break;
                case Law.Boss:
                    ca.contrast.value = 15f; ca.saturation.value = 10f; cr.intensity.value = 0.15f; bl.intensity.value = 0.8f;
                    vg.intensity.value = 0.4f; vg.color.value = new Color(0.25f, 0f, 0f);
                    break;
            }
            return p;
        }
    }
}
