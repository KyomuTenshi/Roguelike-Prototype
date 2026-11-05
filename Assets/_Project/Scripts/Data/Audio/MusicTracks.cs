using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Which track plays where, with loop points in seconds (loopLength 0 = loop the whole file).
    /// - "16-bit Fantasy & Adventure" (xDeviruchi): by track number, loop points from the pack's documentation.
    /// - "8-bit Fantasy & Adventure" (xDeviruchi): the Retro law; loop points = lengths of the pack's separated intro/loop files.
    /// - "28 High Quality 16-bit RPG Music": by file name; loop points = lengths of the pack's (intro)/(loop) files.
    /// - "Music Pack for Fast-Paced Games" (Tomasz Kucza): the Neon law, tempo and first beat measured from the files.
    /// A key can have variants (Variants): each room picks one of them, so the same law doesn't always sound the same.
    /// Loudness of every file was measured (LUFS) and is evened out to about -11 LUFS.
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
            { 15, -14.9f }, { 16, -11.1f }, { 17, -9.6f }, { 18, -14.4f }, { 19, -12.2f }, { 20, -10.0f }, { 21, -10.4f }, { 22, -10.7f },
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

        /// <summary>A track by file name with its loop (start, length; 0 = the whole file) and measured loudness.</summary>
        static Track N(string file, float lufs, float start = 0f, float length = 0f) =>
            new Track { file = file, loopStart = start, loopLength = length, gain = Gain(lufs) };

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
            { "gamble", T(7, 2.135f, 76.800f) },                // Port Town (the gacha room)
            { "jingle_night", T(15) },                          // Peaceful Night: a save file opens (one shot)
            { "jingle_comeback", T(18) },                       // Never Give Up: the 1-UP revives you (one shot)
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
            { "retro", N("xDeviruchi - Mysterious Dungeon", -11.9f) },
            { "retro1", N("xDeviruchi - Mysterious Dungeon", -11.9f) },    // the same tracks without variants:
            { "ice1", T(12, 0f, 73.410f) },                                 // rooms of the worlds pick one each
            { "lava1", T(11, 11.503f, 54.715f) },
            { "retro2", N("xDeviruchi - Exploring The Unknown", -12.5f, 8.734f, 112.033f) },
            { "retro3", N("xDeviruchi - And The Journey Begins", -12.2f, 8.001f, 109.356f) },
            { "retro4", N("xDeviruchi - Prepare for Battle!", -11.9f, 4.533f, 116.292f) },
            { "floorboss_slime", N("xDeviruchi - Decisive Battle", -12.4f, 0f, 116.022f) },
            { "retro6", N("xDeviruchi - The Final of The Fantasy", -12.1f) },

            // 28 High Quality 16-bit RPG Music: alternatives for rooms and laws
            { "floor1b", N("01. Battle Theme I (full)", -15.1f, 1.638f, 59.179f) },
            { "shop2", N("02. Lively City", -13.8f) },
            { "entry2", N("03. Royal Castle", -14.6f) },
            { "fountain2", N("04. Peaceful Village", -16.8f) },
            { "deep2", N("05. Long Journey", -16.4f) },
            { "ice2", N("06. Hidden Cavern", -17.2f) },
            { "pacifist2", N("07. Spirits Forest (full)", -18.8f, 27.344f, 54.638f) },
            { "pacifist3", N("08. Wood Forest Town", -17.7f) },
            { "floor2b", N("09. Battle Theme II (full)", -15f, 1.061f, 97.284f) },
            { "treasury3", N("10. Dwarven Mine", -16.3f) },
            { "crypt2", N("11. Dangerous Cave", -16.4f) },
            { "mirror", N("12. Goofy Monster", -15.9f) },
            { "shrine2", N("13. Magic Temple", -15.5f) },
            { "upside", N("14. Traveling the Sky", -14.4f) },
            { "lava2", N("15. Volcanic Crater", -13.2f) },
            { "floorboss_zombie", N("16. Battle Theme III (full)", -14.2f, 1.171f, 83.881f) },
            { "deep3", N("17. Unknown Island", -16.7f) },
            { "shrine3", N("18. The Old Magician", -14.4f) },
            { "shop3", N("19. East Town", -16.1f) },
            { "console2", N("20. Military Base", -15.4f) },
            { "noir", N("21. Malicious Scheme", -18.8f) },
            { "codex", N("22. Ancient Library", -16.2f) },
            { "treasury2", N("23. Pyramid", -17.2f) },
            { "floorboss_demon", N("24. Battle Theme IV (full)", -15.9f, 1.741f, 83.478f) },
            { "console", N("25. Dark Factory", -16.2f) },
            { "floorboss_frost", N("26. Demon King Castle (full)", -15.5f, 19.290f, 70.705f) },
            { "crypt", N("27. The Evil One", -15.7f) },
            { "fountain3", N("28. Holy Sanctuary", -18f) },

            // 8-bit Fantasy & Adventure outside the Retro law
            { "console3", N("xDeviruchi - Minigame", -12f, 5.501f, 68.013f) },
            { "ice3", N("xDeviruchi - The Icy Cave", -12.4f, 0f, 122.691f) },
            { "fountain4", N("xDeviruchi - Take some rest and eat some food!", -12.8f, 0.333f, 122.692f) },
            { "about", N("xDeviruchi - Title Theme", -12.6f, 0f, 120.624f) },
            { "victory", T(6, 8.501f, 32.0f) },                 // Victory!
            { "defeat", T(1) },                                 // Falling Apart (Prologue)
            { "prologue", T(1) },                               // the night the console turned on
        };

        public static Track Get(string key) => key != null && map.TryGetValue(key, out var t) ? t : null;

        /// <summary>Keys that can play instead of a key (the key itself comes first).</summary>
        static readonly Dictionary<string, string[]> variants = new Dictionary<string, string[]>
        {
            { "entry", new[] { "entry", "entry2" } },
            { "floor1", new[] { "floor1", "floor1b" } },
            { "floor2", new[] { "floor2", "floor2b" } },
            { "deep", new[] { "deep", "deep2", "deep3" } },
            { "shop", new[] { "shop", "shop2", "shop3" } },
            { "shrine", new[] { "shrine", "shrine2", "shrine3" } },
            { "fountain", new[] { "fountain", "fountain2", "fountain3", "fountain4" } },
            { "ice", new[] { "ice", "ice2", "ice3" } },
            { "pacifist", new[] { "pacifist", "pacifist2", "pacifist3" } },
            { "treasury", new[] { "treasury", "treasury2", "treasury3" } },
            { "lava", new[] { "lava", "lava2" } },
            { "crypt", new[] { "crypt", "crypt2" } },
            { "console", new[] { "console", "console2", "console3" } },
            { "retro", new[] { "retro", "retro2", "retro3", "retro4", "retro6" } },
        };

        static readonly string[] none = new string[0];

        public static string[] Variants(string key) => key != null && variants.TryGetValue(key, out var v) ? v : none;
    }
}
