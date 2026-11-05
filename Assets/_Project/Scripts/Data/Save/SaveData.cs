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
        public bool saveFound;                      // a broken save of Amelia was found at least once
        public int saveFiles;                       // bit per save found (0 before the game, 1 the first night, 2 the empty room)
        public int finales;                         // bit per ending seen
        public bool prologueSeen;                   // the night the console turned on
        public bool pixieMet;                       // Pixie introduced herself
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
        public bool damageNumbers;      // numbers over hit enemies
        public bool neonAssist;         // Neon: holding a direction hops on every beat by itself
        public float uiScale = 1f;      // interface size (0.8 - 1.3)
        public int dashKey, activeKey, useKey;   // rebound keys (UnityEngine.InputSystem.Key), 0 = default
        public string padDash = "", padActive = "", padUse = "";   // rebound gamepad buttons ("south", "rb"...), "" = default
        public bool touchAutoFire = true;   // touch screen: shoot the nearest enemy when the aim stick is not held
        public float touchSize = 1f;        // touch screen: size of the sticks and buttons (0.8 - 1.3)
        public int beatOffsetMs;            // Neon: the music is heard this much later (headphones, browsers), -200..300
        public List<string> seenHints = new List<string>();

        // Relics & combos seen (codex)
        public List<string> relics = new List<string>();
        public List<string> combos = new List<string>();

        // Meta progress
        public int relicPoints;                                        // unlock more relics (finished runs, achievements)
        public List<string> achievements = new List<string>();
        public List<RunRecord> history = new List<RunRecord>();      // the last runs, newest first
        public List<string> npcMeets = new List<string>();           // one entry per meeting with a stuck player
    }

    /// <summary>One finished run in the history (codex).</summary>
    [Serializable]
    public class RunRecord
    {
        public string date = "";
        public int mode;
        public string hero = "";
        public int floor;
        public int score;
        public bool win;
        public int relics;
        public int combos;
        public int kills;
        public int seconds;
    }
}
