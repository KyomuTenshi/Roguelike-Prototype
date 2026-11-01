using System;

namespace RulesShift
{
    public enum GameMode { Standard, Daily, Chaos, Endless }

    public enum Curse { GlassHeart, Fury, DoubleLaw, Ascetic }

    /// <summary>Everything chosen before a run: mode, hero, curses and the seed.</summary>
    public class RunConfig
    {
        public GameMode mode;
        public HeroDef hero = Heroes.All[0];
        public int curses;      // bit mask of Curse
        public int seed;        // 0 = random

        public bool Has(Curse c) => (curses & (1 << (int)c)) != 0;

        public float ScoreMul
        {
            get
            {
                float m = 1f;
                for (int i = 0; i < Curses.Count; i++)
                    if (Has((Curse)i)) m += Curses.Info((Curse)i).bonus;
                if (mode == GameMode.Chaos) m += 0.5f;
                return m;
            }
        }

        public RunConfig Copy() => (RunConfig)MemberwiseClone();
    }

    public struct ModeInfo
    {
        public string name, desc, lockHint;
    }

    public static class Modes
    {
        public const int Count = 4;

        public static string Key(GameMode m) =>
            m == GameMode.Daily ? "daily" : m == GameMode.Chaos ? "chaos" : m == GameMode.Endless ? "endless" : "standard";

        public static ModeInfo Info(GameMode m)
        {
            string k = "mode." + Key(m);
            return new ModeInfo
            {
                name = Loc.T(k + ".name"),
                desc = Loc.T(k + ".desc"),
                lockHint = Loc.Has(k + ".lock") ? Loc.T(k + ".lock") : null
            };
        }
    }

    public struct CurseInfo
    {
        public string name, desc;
        public float bonus;
    }

    /// <summary>Optional handicaps for a higher score multiplier.</summary>
    public static class Curses
    {
        public const int Count = 4;

        public static CurseInfo Info(Curse c)
        {
            switch (c)
            {
                case Curse.GlassHeart: return C("glass", 0.5f);
                case Curse.Fury: return C("fury", 0.4f);
                case Curse.DoubleLaw: return C("double", 0.6f);
                default: return C("ascetic", 0.3f);
            }
        }

        static CurseInfo C(string k, float bonus) =>
            new CurseInfo { name = Loc.T("curse." + k + ".name"), desc = Loc.T("curse." + k + ".desc"), bonus = bonus };
    }

    public static class Daily
    {
        public static string Date => DateTime.Now.ToString("yyyy-MM-dd");
        public static int Seed => int.Parse(DateTime.Now.ToString("yyyyMMdd"));
        public static Curse TodayCurse => (Curse)(Seed % Curses.Count);
    }
}
