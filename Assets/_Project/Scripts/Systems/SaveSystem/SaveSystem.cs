using UnityEngine;

namespace RulesShift
{
    /// <summary>Loads/saves SaveData and answers "is it unlocked / discovered".</summary>
    public static class SaveSystem
    {
        const string Key = "rulesshift.save.v1";
        static SaveData data;

        public static SaveData Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        public static void Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
            }
            catch
            {
                data = null;
            }
            if (data == null) data = new SaveData();
            if (data.laws == null) data.laws = new System.Collections.Generic.List<int>();
            if (data.seenHints == null) data.seenHints = new System.Collections.Generic.List<string>();
            if (data.relics == null) data.relics = new System.Collections.Generic.List<string>();
            if (data.combos == null) data.combos = new System.Collections.Generic.List<string>();
        }

        public static void Save()
        {
            if (data == null) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        /// <summary>Wipes progress but keeps the settings.</summary>
        public static void ResetProgress()
        {
            var d = Data;
            data = new SaveData { music = d.music, sfx = d.sfx, shake = d.shake, lang = d.lang, hints = d.hints };
            Save();
        }

        /// <summary>Marks a law as seen in the codex. Returns true the first time.</summary>
        public static bool Discover(Law law)
        {
            if (law == Law.None) return false;
            int v = (int)law;
            if (Data.laws.Contains(v)) return false;
            Data.laws.Add(v);
            return true;
        }

        static bool Mark(System.Collections.Generic.List<string> list, string id)
        {
            if (list.Contains(id)) return false;
            list.Add(id);
            return true;
        }

        public static bool DiscoverRelic(string id) => Mark(Data.relics, id);
        public static bool DiscoverCombo(string id) => Mark(Data.combos, id);
        public static bool KnowsRelic(string id) => Data.relics.Contains(id);
        public static bool KnowsCombo(string id) => Data.combos.Contains(id);

        /// <summary>True the first time a hint id is asked about (and remembers it).</summary>
        public static bool FirstTime(string hint) => Mark(Data.seenHints, hint);

        public static bool Knows(Law law) => Data.laws.Contains((int)law);

        public static bool HeroUnlocked(HeroDef h) => h.unlocked == null || h.unlocked(Data);

        /// <summary>Chaos: one win. Endless: three wins. Run and Daily are always open.</summary>
        public static bool ModeUnlocked(GameMode m)
        {
            switch (m)
            {
                case GameMode.Chaos: return Data.wins >= 1;
                case GameMode.Endless: return Data.wins >= 3;
                default: return true;
            }
        }

        public static int Best(GameMode m)
        {
            switch (m)
            {
                case GameMode.Daily: return Data.dailyDate == Daily.Date ? Data.dailyBest : 0;
                case GameMode.Chaos: return Data.bestChaos;
                case GameMode.Endless: return Data.bestEndless;
                default: return Data.bestStandard;
            }
        }
    }
}
