using System;
using UnityEngine;

namespace RulesShift
{
    public enum Lang { Ru, En, Zh, Es }

    /// <summary>
    /// Localization: every player-facing string lives in Strings.Table (key → ru, en, zh, es).
    /// Loc.T("menu.play") / Loc.F("hud.room", 2, 6). Missing translations fall back to English.
    /// </summary>
    public static class Loc
    {
        public const int Count = 4;
        public static Lang Current { get; private set; } = Lang.Ru;
        public static event Action Changed;
        static bool inited;

        /// <summary>Chinese needs its own font (the pixel fonts have no CJK glyphs).</summary>
        public static bool Cjk => Current == Lang.Zh;

        public static void Init()
        {
            if (inited) return;
            inited = true;
            int l = SaveSystem.Data.lang;
            Current = l >= 0 && l < Count ? (Lang)l : Detect();
        }

        public static Lang Detect()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian:
                case SystemLanguage.Ukrainian:
                case SystemLanguage.Belarusian:
                    return Lang.Ru;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional:
                    return Lang.Zh;
                case SystemLanguage.Spanish:
                    return Lang.Es;
                default:
                    return Lang.En;
            }
        }

        public static void Set(Lang l)
        {
            Current = l;
            inited = true;
            SaveSystem.Data.lang = (int)l;
            SaveSystem.Save();
            UiKit.Init();
            Changed?.Invoke();
        }

        public static string T(string key)
        {
            if (key != null && Strings.Table.TryGetValue(key, out var a))
            {
                string s = a[(int)Current];
                return string.IsNullOrEmpty(s) ? a[1] : s;
            }
            return key;
        }

        public static string F(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch (FormatException) { return T(key); }
        }

        public static bool Has(string key) => key != null && Strings.Table.ContainsKey(key);

        /// <summary>Name of a language in that language (for the settings selector).</summary>
        public static string Name(Lang l) => Strings.Table["lang.name"][(int)l];

        /// <summary>Separator between items in a line: the CJK font has no middle dot.</summary>
        public static string Dot => Cjk ? "　" : "  ·  ";

        /// <summary>Quote a name in the current language's style.</summary>
        public static string Q(string s)
        {
            switch (Current)
            {
                case Lang.Ru: return "«" + s + "»";
                case Lang.Zh: return "「" + s + "」";
                default: return s;
            }
        }
    }
}
