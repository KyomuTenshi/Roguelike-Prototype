using System;
using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Sound effects synthesized at startup, so the prototype has sound without any audio files.
    /// Put Kenney sounds later and swap Play(id) calls if you like.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class Sfx : MonoBehaviour
    {
        public static Sfx I { get; private set; }
        const int Rate = 22050;

        [Range(0f, 1f)] public float volume = 0.55f;

        AudioSource[] pool;
        int next;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        System.Random rnd = new System.Random(7);

        void Awake()
        {
            I = this;
            pool = new AudioSource[14];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
            Build();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public static void Play(string id, float vol = 1f, float pitch = 1f)
        {
            if (I == null || !I.clips.TryGetValue(id, out var clip)) return;
            // the same sound at most every 40 ms, so 20 arrows don't clip the mix
            float now = Time.unscaledTime;
            if (I.lastPlayed.TryGetValue(id, out float t) && now - t < 0.04f) return;
            I.lastPlayed[id] = now;

            var src = I.pool[I.next];
            I.next = (I.next + 1) % I.pool.Length;
            src.clip = clip;
            src.volume = I.volume * vol;
            src.pitch = pitch * UnityEngine.Random.Range(0.94f, 1.06f);
            src.Play();
        }

        // ---------- synthesis ----------

        void Build()
        {
            Add("shoot", 0.13f, (t, n) => Square(Sweep(t, 0.13f, 950, 320)) * 0.35f * Env(t, 0.13f, 18) + Noise() * 0.15f * Env(t, 0.13f, 40));
            Add("orb", 0.2f, (t, n) => Sine(Sweep(t, 0.2f, 420, 760)) * 0.4f * Env(t, 0.2f, 10));
            Add("hit", 0.09f, (t, n) => (Noise() * 0.5f + Square(t * 180) * 0.3f) * Env(t, 0.09f, 30));
            Add("kill", 0.28f, (t, n) => Square(Sweep(t, 0.28f, 620, 70)) * 0.4f * Env(t, 0.28f, 7) + Noise() * 0.2f * Env(t, 0.28f, 25));
            Add("hurt", 0.32f, (t, n) => Saw(Sweep(t, 0.32f, 320, 110) + Mathf.Sin(t * 60f) * 3f) * 0.45f * Env(t, 0.32f, 6));
            Add("boom", 0.7f, (t, n) => (LowNoise(0.08f) * 0.9f + Sine(t * 55f) * 0.6f) * Env(t, 0.7f, 5));
            Add("door", 0.45f, (t, n) => (Sine(t * 70f) * 0.7f + Saw(Sweep(t, 0.45f, 130, 85)) * 0.25f) * Env(t, 0.45f, 6));
            Add("pickup", 0.4f, (t, n) => Square(Arp(t, 0.08f, 523, 659, 784, 1046)) * 0.3f * Env(t, 0.4f, 4));
            Add("heal", 0.3f, (t, n) => Sine(Sweep(t, 0.3f, 420, 840)) * 0.35f * Env(t, 0.3f, 6));
            Add("law", 0.9f, (t, n) => (Sine(Sweep(t, 0.9f, 180, 1300)) * (0.5f + 0.5f * Mathf.Sin(t * 50f)) * 0.35f
                                        + LowNoise(0.3f) * 0.2f) * Rise(t, 0.9f));
            Add("card", 0.35f, (t, n) => LowNoise(Mathf.Lerp(0.05f, 0.6f, t / 0.35f)) * 0.4f * Rise(t, 0.35f));
            Add("beat", 0.06f, (t, n) => (Noise() * 0.4f + Sine(t * 1100f) * 0.5f) * Env(t, 0.06f, 50));
            Add("button", 0.12f, (t, n) => Square(t * 1250f) * 0.25f * Env(t, 0.12f, 30) + Square(t * 620f) * 0.2f * Env(t, 0.12f, 20));
            Add("wake", 0.22f, (t, n) => Square(t < 0.09f ? t * 820f : t * 1240f) * 0.25f * Env(t, 0.22f, 8));
            Add("fuse", 0.1f, (t, n) => Square(t * 1500f) * 0.18f * Env(t, 0.1f, 25));
            Add("select", 0.08f, (t, n) => Square(t * 900f) * 0.2f * Env(t, 0.08f, 30));
            Add("lava", 0.5f, (t, n) => LowNoise(0.15f) * 0.5f * Env(t, 0.5f, 4) + Sine(t * 90f) * 0.2f * Env(t, 0.5f, 6));
            Add("win", 1.2f, (t, n) => Square(Arp(t, 0.15f, 392, 523, 659, 784, 1046, 1318)) * 0.25f * Env(t, 1.2f, 1.5f));
            Add("lose", 1.2f, (t, n) => Saw(Arp(t, 0.25f, 330, 262, 220, 165)) * 0.3f * Env(t, 1.2f, 2f));
            Add("swing", 0.16f, (t, n) => LowNoise(Mathf.Lerp(0.6f, 0.15f, t / 0.16f)) * 0.5f * Rise(t, 0.16f) * Env(t, 0.16f, 6) + Saw(Sweep(t, 0.16f, 300, 120)) * 0.12f * Env(t, 0.16f, 12));
            Add("dash", 0.22f, (t, n) => LowNoise(Mathf.Lerp(0.2f, 0.7f, t / 0.22f)) * 0.45f * Env(t, 0.22f, 7));
            Add("coin", 0.18f, (t, n) => Square(t < 0.06f ? t * 988f : t * 1319f) * 0.22f * Env(t, 0.18f, 9));
            Add("blink", 0.3f, (t, n) => Sine(Sweep(t, 0.3f, 300, 1800)) * 0.35f * Env(t, 0.3f, 7) + Noise() * 0.06f * Env(t, 0.3f, 20));
            Add("buy", 0.5f, (t, n) => Square(Arp(t, 0.07f, 784, 988, 1175, 1568)) * 0.25f * Env(t, 0.5f, 4));
        }

        float lp; // one-pole low-pass state for LowNoise

        void Add(string id, float seconds, Func<float, int, float> f)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            lp = 0f;
            for (int i = 0; i < count; i++)
                data[i] = Mathf.Clamp(f(i / (float)Rate, i), -1f, 1f);
            // tiny fade in/out against clicks
            int fade = Mathf.Min(64, count / 4);
            for (int i = 0; i < fade; i++)
            {
                data[i] *= i / (float)fade;
                data[count - 1 - i] *= i / (float)fade;
            }
            var clip = AudioClip.Create("sfx_" + id, count, 1, Rate, false);
            clip.SetData(data, 0);
            clips[id] = clip;
        }

        // phase helpers: argument is "cycles" (t * freq)
        static float Sine(float cycles) => Mathf.Sin(cycles * Mathf.PI * 2f);
        static float Square(float cycles) => (cycles - Mathf.Floor(cycles)) < 0.5f ? 1f : -1f;
        static float Saw(float cycles) => 2f * (cycles - Mathf.Floor(cycles)) - 1f;

        float Noise() => (float)(rnd.NextDouble() * 2.0 - 1.0);

        float LowNoise(float k)
        {
            lp += (Noise() - lp) * Mathf.Clamp01(k);
            return lp * 2f;
        }

        /// <summary>Integrated exponential frequency sweep → phase in cycles.</summary>
        static float Sweep(float t, float dur, float f0, float f1)
        {
            float k = Mathf.Log(f1 / f0) / dur;
            if (Mathf.Abs(k) < 1e-4f) return f0 * t;
            return f0 * (Mathf.Exp(k * t) - 1f) / k;
        }

        static float Arp(float t, float step, params float[] notes)
        {
            int i = Mathf.Min(notes.Length - 1, (int)(t / step));
            return t * notes[i];
        }

        static float Env(float t, float dur, float decay) => Mathf.Exp(-t * decay) * Mathf.Clamp01((dur - t) * 40f);
        static float Rise(float t, float dur) => Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
    }
}
