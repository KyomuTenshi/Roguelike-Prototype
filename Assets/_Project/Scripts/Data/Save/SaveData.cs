using System;
using System.Collections.Generic;

namespace RulesShift
{
    /// <summary>Progress and settings, stored as JSON in PlayerPrefs (works in WebGL too).</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;

        // Progress
        public int runs;
        public int wins;
        public int totalKills;
        public int bossReached;
        public int bestDepth;
        public int bestFloor;
        public int maxCursesWin;                    // most curses on a winning run (Lizard unlock)
        public bool memoryFound;                    // the secret room was found at least once
        public int memories;                        // bit per memory seen (0 the dream, 1 the work, 2 the silence)
        public int endings;                         // bit per ending seen
        public List<int> laws = new List<int>();   // discovered Law values (codex)

        // Records
        public int bestStandard;
        public int bestChaos;
        public int bestEndless;
        public int bestEndlessDepth;
        public string dailyDate = "";
        public int dailyBest;

        // Last choice
        public string hero = "elf";
        public int mode;
        public int curses;

        // Settings
        public float music = 0.4f;
        public float sfx = 0.55f;
        public bool shake = true;
        public int lang = -1;           // -1 = follow the system language
        public bool hints = true;
        public List<string> seenHints = new List<string>();

        // Relics & combos seen (codex)
        public List<string> relics = new List<string>();
        public List<string> combos = new List<string>();
    }
}
