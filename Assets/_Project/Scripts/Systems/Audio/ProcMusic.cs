using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Tiny loop synthesizer: every law gets its own 4-bar loop (dark drone, noir jazz,
    /// chiptune, synthwave…). Used when there is no file in Assets/_Project/Audio/Music for that key.
    /// </summary>
    public static class ProcMusic
    {
        const int Rate = 22050;

        enum Wave { Sine, Triangle, Square, Saw }

        struct Chord
        {
            public int offset;
            public int[] tones;
            public Chord(int offset, string quality)
            {
                this.offset = offset;
                switch (quality)
                {
                    case "M": tones = new[] { 0, 4, 7 }; break;
                    case "m7": tones = new[] { 0, 3, 7, 10 }; break;
                    case "M7": tones = new[] { 0, 4, 7, 11 }; break;
                    case "7": tones = new[] { 0, 4, 7, 10 }; break;
                    case "dim": tones = new[] { 0, 3, 6 }; break;
                    case "sus": tones = new[] { 0, 5, 7 }; break;
                    default: tones = new[] { 0, 3, 7 }; break;
                }
            }
        }

        class Style
        {
            public float bpm = 90;
            public int root = 50;
            public Chord[] prog;
            public Wave padWave = Wave.Triangle; public float pad = 0.15f; public float detune; public bool swell;
            public Wave bassWave = Wave.Sine; public float bass = 0.3f; public int bassMode = 1; // 0 off, 1 quarters, 2 eighths, 3 walking
            public Wave arpWave = Wave.Square; public float arp; public int arpRate = 2; public float arpChance = 1f;
            public float hats, kick, snare, swing, bells, drone;
        }

        static readonly Dictionary<string, (AudioClip clip, float bpm)> cache = new Dictionary<string, (AudioClip, float)>();

        public static AudioClip Get(string key, out float bpm)
        {
            key = string.IsNullOrEmpty(key) ? "entry" : key.ToLowerInvariant();
            if (cache.TryGetValue(key, out var hit) && hit.clip) { bpm = hit.bpm; return hit.clip; }
            var style = Make(key);
            var clip = Render(key, style);
            cache[key] = (clip, style.bpm);
            bpm = style.bpm;
            return clip;
        }

        static Style Make(string key)
        {
            switch (key)
            {
                case "crypt":
                    return new Style { bpm = 56, root = 38, prog = P((0, "m"), (1, "M"), (0, "m"), (-1, "dim")),
                        padWave = Wave.Sine, pad = 0.22f, detune = 0.004f, bass = 0f, bassMode = 0, drone = 0.28f, bells = 0.22f };
                case "noir":
                    return new Style { bpm = 82, root = 50, prog = P((0, "m7"), (5, "7"), (-2, "M7"), (7, "7")),
                        padWave = Wave.Triangle, pad = 0.1f, bassWave = Wave.Triangle, bass = 0.42f, bassMode = 3,
                        hats = 0.07f, swing = 0.16f, snare = 0.05f };
                case "ice":
                    return new Style { bpm = 72, root = 64, prog = P((0, "M7"), (5, "M7"), (-3, "m7"), (2, "sus")),
                        padWave = Wave.Sine, pad = 0.14f, detune = 0.003f, bass = 0.18f, bassMode = 1,
                        arpWave = Wave.Sine, arp = 0.13f, arpRate = 2, bells = 0.12f };
                case "mirror":
                    return new Style { bpm = 96, root = 57, prog = P((0, "m"), (-2, "M"), (-4, "M"), (-5, "M")),
                        padWave = Wave.Saw, pad = 0.08f, detune = 0.008f, swell = true, bass = 0.28f, bassMode = 1,
                        arpWave = Wave.Triangle, arp = 0.08f, arpRate = 2, hats = 0.03f };
                case "pacifist":
                    return new Style { bpm = 84, root = 60, prog = P((0, "M"), (-3, "m"), (5, "M"), (7, "M")),
                        padWave = Wave.Sine, pad = 0.14f, bass = 0.22f, bassMode = 1,
                        arpWave = Wave.Triangle, arp = 0.12f, arpRate = 2, hats = 0.025f };
                case "retro":
                    return new Style { bpm = 140, root = 57, prog = P((0, "m"), (-4, "M"), (-2, "M"), (-5, "M")),
                        pad = 0f, bassWave = Wave.Square, bass = 0.16f, bassMode = 2,
                        arpWave = Wave.Square, arp = 0.12f, arpRate = 4, hats = 0.07f, kick = 0.35f };
                case "lava":
                    return new Style { bpm = 124, root = 40, prog = P((0, "m"), (1, "M"), (0, "m"), (3, "M")),
                        padWave = Wave.Saw, pad = 0.06f, bassWave = Wave.Saw, bass = 0.32f, bassMode = 2,
                        kick = 0.6f, snare = 0.25f, hats = 0.05f };
                case "treasury":
                    return new Style { bpm = 64, root = 57, prog = P((0, "m"), (5, "m"), (-2, "M"), (-5, "M")),
                        padWave = Wave.Sine, pad = 0.09f, bass = 0f, bassMode = 0,
                        arpWave = Wave.Triangle, arp = 0.14f, arpRate = 1, arpChance = 0.6f, bells = 0.08f };
                case "upside":
                    return new Style { bpm = 96, root = 54, prog = P((0, "m"), (6, "dim"), (1, "M"), (0, "m")),
                        padWave = Wave.Triangle, pad = 0.11f, detune = 0.012f, bass = 0.26f, bassMode = 1,
                        arpWave = Wave.Square, arp = 0.07f, arpRate = 3, hats = 0.04f };
                case "neon":
                    return new Style { bpm = 120, root = 45, prog = P((0, "m"), (-4, "M"), (3, "M"), (-2, "M")),
                        padWave = Wave.Saw, pad = 0.09f, detune = 0.006f, bassWave = Wave.Square, bass = 0.22f, bassMode = 2,
                        arpWave = Wave.Square, arp = 0.06f, arpRate = 4, kick = 0.7f, snare = 0.32f, hats = 0.06f };
                case "console":
                    return new Style { bpm = 110, root = 52, prog = P((0, "m"), (0, "m"), (3, "M"), (-2, "M")),
                        pad = 0f, bass = 0.2f, bassMode = 1, drone = 0.16f,
                        arpWave = Wave.Square, arp = 0.09f, arpRate = 4, arpChance = 0.5f, hats = 0.04f };
                case "boss":
                    return new Style { bpm = 140, root = 41, prog = P((0, "m"), (-4, "M"), (1, "M"), (-1, "7")),
                        padWave = Wave.Saw, pad = 0.08f, bassWave = Wave.Saw, bass = 0.32f, bassMode = 2,
                        arpWave = Wave.Square, arp = 0.05f, arpRate = 4, kick = 0.7f, snare = 0.38f, hats = 0.06f };
                case "menu":
                    return new Style { bpm = 70, root = 50, prog = P((0, "m"), (-4, "M"), (3, "M"), (-2, "M")),
                        padWave = Wave.Triangle, pad = 0.16f, detune = 0.004f, bass = 0.22f, bassMode = 1, bells = 0.08f };
                default: // entry
                    return new Style { bpm = 88, root = 50, prog = P((0, "m"), (-4, "M"), (3, "M"), (-2, "M")),
                        padWave = Wave.Triangle, pad = 0.15f, bass = 0.3f, bassMode = 1, hats = 0.04f,
                        arpWave = Wave.Triangle, arp = 0.05f, arpRate = 2 };
            }
        }

        static Chord[] P(params (int, string)[] c)
        {
            var r = new Chord[c.Length];
            for (int i = 0; i < c.Length; i++) r[i] = new Chord(c[i].Item1, c[i].Item2);
            return r;
        }

        static float Freq(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        static float Osc(Wave w, float cycles)
        {
            float p = cycles - Mathf.Floor(cycles);
            switch (w)
            {
                case Wave.Square: return p < 0.5f ? 0.7f : -0.7f;
                case Wave.Saw: return (2f * p - 1f) * 0.7f;
                case Wave.Triangle: return 1f - 4f * Mathf.Abs(p - 0.5f);
                default: return Mathf.Sin(p * Mathf.PI * 2f);
            }
        }

        static float Hash(int n)
        {
            unchecked
            {
                uint x = (uint)n * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return (x & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        static float NoiseAt(int i) => Hash(i) * 2f - 1f;

        static AudioClip Render(string key, Style s)
        {
            float beatLen = 60f / s.bpm;
            int beats = 16;
            int n = Mathf.RoundToInt(beats * beatLen * Rate);
            int tail = Rate; // 1 s tail folded back into the start → seamless loop
            var buf = new float[n + tail];

            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)Rate;
                float beat = t / beatLen;
                int beatIdx = Mathf.FloorToInt(beat);
                float inBeat = beat - beatIdx;
                int bar = beatIdx / 4;
                float inBar = beat - bar * 4f;
                var chord = s.prog[((bar % s.prog.Length) + s.prog.Length) % s.prog.Length];
                float root = s.root + chord.offset;
                float v = 0f;

                // pad
                if (s.pad > 0f)
                {
                    float amp = Mathf.Clamp01(inBar * 2f) * Mathf.Clamp01((4f - inBar) * 2f);
                    if (s.swell) amp *= Mathf.Pow(inBar / 4f, 1.5f) * 1.6f;
                    float sum = 0f;
                    foreach (var tone in chord.tones)
                    {
                        float f = Freq(root + 12 + tone);
                        sum += Osc(s.padWave, t * f);
                        if (s.detune > 0f) sum += Osc(s.padWave, t * f * (1f + s.detune));
                    }
                    v += sum * s.pad * amp / (s.detune > 0f ? 2f : 1f) * 0.6f;
                }

                // drone
                if (s.drone > 0f)
                    v += (Mathf.Sin(t * Freq(s.root - 12) * Mathf.PI * 2f) * 0.7f
                          + Mathf.Sin(t * Freq(s.root - 5) * Mathf.PI * 2f) * 0.3f)
                         * s.drone * (0.75f + 0.25f * Mathf.Sin(t * 0.7f));

                // bass
                if (s.bass > 0f && s.bassMode > 0)
                {
                    float step = s.bassMode == 2 ? 0.5f : 1f;
                    float pos = beat / step;
                    int noteIdx = Mathf.FloorToInt(pos);
                    float noteT = (pos - noteIdx) * step * beatLen;
                    int degree = 0;
                    if (s.bassMode == 3)
                    {
                        int[] walk = { 0, 2, 1, 3 };
                        int w = walk[noteIdx % 4];
                        degree = w < chord.tones.Length ? chord.tones[w] : 12;
                    }
                    else if (s.bassMode == 2 && noteIdx % 2 == 1) degree = 12;
                    float f = Freq(root - 12 + degree);
                    float env = Mathf.Exp(-noteT * (s.bassMode == 2 ? 7f : 3.5f)) * Mathf.Clamp01(noteT * 200f);
                    v += Osc(s.bassWave, noteT * f) * s.bass * env;
                }

                // arpeggio
                if (s.arp > 0f)
                {
                    float pos = beat * s.arpRate;
                    int k = Mathf.FloorToInt(pos);
                    float noteT = (pos - k) / s.arpRate * beatLen;
                    if (Hash(k * 31 + key.Length) <= s.arpChance)
                    {
                        int tone = chord.tones[k % chord.tones.Length] + 12 * ((k / chord.tones.Length) % 2 + 1);
                        float env = Mathf.Exp(-noteT * 9f) * Mathf.Clamp01(noteT * 300f);
                        v += Osc(s.arpWave, noteT * Freq(root + tone)) * s.arp * env;
                    }
                }

                // bells
                if (s.bells > 0f)
                {
                    float h = Hash(beatIdx * 13 + 5);
                    if (h < 0.35f)
                    {
                        float noteT = inBeat * beatLen;
                        int tone = chord.tones[(int)(h * 100) % chord.tones.Length] + 24;
                        v += Mathf.Sin(noteT * Freq(root + tone) * Mathf.PI * 2f) * s.bells * Mathf.Exp(-noteT * 2.2f) * 0.6f;
                    }
                }

                // drums
                if (s.kick > 0f)
                {
                    float kt = inBeat * beatLen;
                    float kf = Mathf.Lerp(130f, 42f, Mathf.Clamp01(kt / 0.12f));
                    v += Mathf.Sin(kt * kf * Mathf.PI * 2f) * s.kick * Mathf.Exp(-kt * 14f);
                }
                if (s.snare > 0f && beatIdx % 2 == 1)
                {
                    float st = inBeat * beatLen;
                    v += NoiseAt(i) * s.snare * Mathf.Exp(-st * 18f);
                }
                if (s.hats > 0f)
                {
                    float half = beat * 2f;
                    int hk = Mathf.FloorToInt(half);
                    float off = (hk % 2 == 1) ? s.swing : 0f;
                    float ht = (half - hk - off) * beatLen * 0.5f;
                    if (ht >= 0f) v += NoiseAt(i * 3 + 1) * s.hats * Mathf.Exp(-ht * 70f);
                }

                buf[i] = v;
            }

            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = buf[i];
            for (int i = 0; i < tail; i++) data[i] += buf[n + i];

            float peak = 0.001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = 0.8f / peak;
            for (int i = 0; i < n; i++) data[i] *= gain;

            var clip = AudioClip.Create("music_" + key, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
