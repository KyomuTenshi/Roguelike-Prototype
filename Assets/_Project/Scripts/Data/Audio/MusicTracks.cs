using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Which track of "16-bit Fantasy & Adventure" (xDeviruchi) plays where, with the loop points
    /// from the pack's documentation (seconds). loopLength 0 = loop the whole file.
    /// bpm/beatOffset are set where the game needs the exact beat (the Neon law).
    /// The Neon law plays the "Music Pack for Fast-Paced Games" (Tomasz Kucza): tracks by file name,
    /// tempo and first beat measured from the files.
    /// </summary>
    public static class MusicTracks
    {
        public class Track
        {
            public int number;
            public string file;             // clip name (packs without track numbers)
            public float loopStart, loopLength;
            public float bpm, beatOffset;   // 0 = no beat sync
            public float gain = 1f;         // evens out loudness between tracks
        }

        // measured loudness of the files (LUFS); every track is brought to about -11 LUFS
        static readonly Dictionary<int, float> Lufs = new Dictionary<int, float>
        {
            { 1, -8.9f }, { 2, -10.5f }, { 3, -10.5f }, { 4, -10.9f }, { 5, -9.8f }, { 6, -12.3f }, { 7, -10.1f },
            { 8, -10.5f }, { 9, -10.7f }, { 10, -11.9f }, { 11, -11.2f }, { 12, -10.8f }, { 13, -9.7f }, { 14, -9.8f },
            { 16, -11.1f }, { 17, -9.6f }, { 19, -12.2f }, { 20, -10.0f }, { 21, -10.4f }, { 22, -10.7f },
        };

        static float Gain(float lufs) => Mathf.Pow(10f, (-11f - lufs) / 20f);

        static Track T(int n, float start = 0f, float length = 0f, float bpm = 0f, float beatOffset = 0f)
        {
            float gain = Lufs.TryGetValue(n, out float l) ? Gain(l) : 1f;
            return new Track { number = n, loopStart = start, loopLength = length, bpm = bpm, beatOffset = beatOffset, gain = gain };
        }

        /// <summary>A whole-file loop from a pack without numbers, with a measured tempo.</summary>
        static Track F(string file, float bpm, float beatOffset, float lufs) =>
            new Track { file = file, bpm = bpm, beatOffset = beatOffset, gain = Gain(lufs) };

        /// <summary>Music keys of the Neon law; each Neon room picks one of them.</summary>
        public static readonly string[] NeonKeys = { "neon1", "neon2", "neon3", "neon4", "neon5" };

        static readonly Dictionary<string, Track> map = new Dictionary<string, Track>
        {
            { "menu", T(2) },                                   // Title Theme
            { "entry", T(16, 2.438f, 83.267f) },                // The Calm Before The Storm
            { "floor1", T(5, 10.760f, 62.608f) },               // Battle 1
            { "floor2", T(9, 0.63f, 63.278f) },                 // Battle 2
            { "floor3", T(22, 5.337f, 114.667f) },              // The Final of The Fantasy
            { "deep", T(20) },                                  // The Journey (Endless, floor 4+)
            { "floorboss", T(13, 0.167f, 107.502f) },           // Decisive Battle 1 - Don't Be Afraid
            { "lawgiver", T(17, 13.846f, 82.308f) },            // Decisive Battle 2 - The Calamity
            { "lawgiver2", T(21, 36.416f, 92.600f) },           // Final Battle - For Love
            { "shop", T(8, 0f, 53.615f) },                      // Shop
            { "shrine", T(10, 0f, 92.957f) },                   // Lost Shrine
            { "fountain", T(3, 23.106f, 74.483f) },             // Definitely Our Town
            { "secret", T(14, 2.675f, 71.287f) },               // Tales of Firelight Town
            { "ice", T(12, 0f, 73.410f) },                      // Frozen Abyss
            { "pacifist", T(19, 0.639f, 66.032f) },             // Where The Winds Roam
            { "treasury", T(4, 0f, 85.807f) },                  // Silent Forest
            { "lava", T(11, 11.503f, 54.715f) },                // The Mighty Kingdom
            { "neon1", F("Deadly Contracts-loop1", 112f, 0f, -13.1f) },
            { "neon2", F("Going Undercover-loop1", 123f, 0f, -14.0f) },
            { "neon3", F("Hostile Territory-loop1", 123f, 0.07f, -11.4f) },
            { "neon4", F("Shadow Operations-loop1", 117.06f, 0.015f, -16.4f) },   // 48 beats exactly
            { "neon5", F("The Price of Freedom-loop1", 111.93f, 0f, -13.7f) },    // 80 beats exactly; louder copy in normalized/
            { "retro", T(20) },                                 // The Journey
            { "victory", T(6, 8.501f, 32.0f) },                 // Victory!
            { "defeat", T(1) },                                 // Falling Apart (Prologue)
        };

        public static Track Get(string key) => key != null && map.TryGetValue(key, out var t) ? t : null;
    }
}
