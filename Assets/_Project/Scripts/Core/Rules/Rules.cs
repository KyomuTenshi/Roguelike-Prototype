using UnityEngine;

namespace RulesShift
{
    /// <summary>The law of a room. Each one changes both the mechanics (Rules) and the look (StyleController).</summary>
    public enum Law
    {
        None,       // Прихожая
        Crypt,      // Склеп
        Noir,       // Нуар
        Ice,        // Ледяная пещера
        Mirror,     // Зеркальный зал
        Pacifist,   // Сад пацифиста
        Retro,      // Ретро
        Lava,       // Лавовый зал
        Stealth,    // Сокровищница
        Upside,     // Перевёрнутая
        Neon,       // Неон
        Console,    // Пульт
        Boss        // Законодатель
    }

    public struct LawInfo
    {
        public string name;     // short name for cards and the boss bar
        public string rule;     // one line rule
        public Color color;     // accent colour of the card
        public string music;    // music key (Assets/_Project/Audio/Music/<key>.*), procedural fallback otherwise
    }

    /// <summary>
    /// Global modifiers of the current law. Everything reads them every frame,
    /// so changing the law is just Apply(newLaw): nothing has to be rolled back.
    /// </summary>
    public static class Rules
    {
        public static Law Current { get; private set; }
        /// <summary>Second law of the "Двойной закон" curse (Law.None = no second law).</summary>
        public static Law Extra { get; private set; }

        public static float Accel;          // 0 = instant movement, >0 = slippery (units/s²)
        public static bool InvertMove;      // WASD reversed
        public static bool ShootOnlyStill;  // can't shoot while moving
        public static bool ShotsHeal;       // arrows heal enemies, enemies can't die
        public static bool OneHit;          // one hit kills anyone, the player too
        public static float Vision;         // >0: enemies are invisible outside this radius
        public static bool BeatMove;        // you can move only on the beat
        public static bool Stealth;         // enemies sleep, shots wake them up
        public static bool EnemyLights;     // enemies and orbs glow (Neon)
        public static float CameraAngle;    // 180 = upside down
        public static float EnemySpeedMul;  // global enemy speed factor

        static Rules() { Reset(); }

        public static void Reset()
        {
            Current = Law.None;
            Extra = Law.None;
            Accel = 0f;
            InvertMove = false;
            ShootOnlyStill = false;
            ShotsHeal = false;
            OneHit = false;
            Vision = 0f;
            BeatMove = false;
            Stealth = false;
            EnemyLights = false;
            CameraAngle = 0f;
            EnemySpeedMul = 1f;
        }

        public static void Apply(Law law)
        {
            Reset();
            Current = law;
            Mods(law);
        }

        /// <summary>Adds the mechanics of a second law on top of the current one.</summary>
        public static void AddExtra(Law law)
        {
            Extra = law;
            Mods(law);
        }

        /// <summary>Laws that can be stacked as a second law without breaking the room's goal.</summary>
        public static readonly Law[] Stackable = { Law.Crypt, Law.Noir, Law.Ice, Law.Mirror, Law.Upside, Law.Neon };

        /// <summary>"Нуар" or "Нуар + Лёд".</summary>
        public static string Label =>
            Extra != Law.None ? $"{Info(Current).name} + {Info(Extra).name}" : Info(Current).name;

        static void Mods(Law law)
        {
            switch (law)
            {
                case Law.Crypt: Vision = 4.2f; break;
                case Law.Noir: ShootOnlyStill = true; break;
                case Law.Ice: Accel = 5.5f; break;
                case Law.Mirror: InvertMove = true; break;
                case Law.Pacifist: ShotsHeal = true; EnemySpeedMul = 0.85f; break;
                case Law.Retro: OneHit = true; EnemySpeedMul = 0.9f; break;
                case Law.Stealth: Stealth = true; break;
                case Law.Upside: CameraAngle = 180f; break;
                case Law.Neon: BeatMove = true; EnemyLights = true; break;
            }
        }

        public static string Key(Law law)
        {
            switch (law)
            {
                case Law.Crypt: return "crypt";
                case Law.Noir: return "noir";
                case Law.Ice: return "ice";
                case Law.Mirror: return "mirror";
                case Law.Pacifist: return "pacifist";
                case Law.Retro: return "retro";
                case Law.Lava: return "lava";
                case Law.Stealth: return "stealth";
                case Law.Upside: return "upside";
                case Law.Neon: return "neon";
                case Law.Console: return "console";
                case Law.Boss: return "boss";
                default: return "none";
            }
        }

        public static LawInfo Info(Law law)
        {
            switch (law)
            {
                case Law.Crypt: return L(law, new Color(0.55f, 0.65f, 1f), "crypt");
                case Law.Noir: return L(law, new Color(0.9f, 0.9f, 0.9f), "noir");
                case Law.Ice: return L(law, new Color(0.55f, 0.85f, 1f), "ice");
                case Law.Mirror: return L(law, new Color(0.85f, 0.5f, 1f), "mirror");
                case Law.Pacifist: return L(law, new Color(0.6f, 1f, 0.6f), "pacifist");
                case Law.Retro: return L(law, new Color(0.75f, 1f, 0.7f), "retro");
                case Law.Lava: return L(law, new Color(1f, 0.5f, 0.2f), "lava");
                case Law.Stealth: return L(law, new Color(1f, 0.85f, 0.45f), "treasury");
                case Law.Upside: return L(law, new Color(1f, 0.7f, 0.4f), "upside");
                case Law.Neon: return L(law, new Color(1f, 0.3f, 0.9f), "neon");
                case Law.Console: return L(law, new Color(0.5f, 0.9f, 1f), "console");
                case Law.Boss: return L(law, new Color(1f, 0.25f, 0.25f), "boss");
                default: return L(law, new Color(0.95f, 0.85f, 0.7f), "entry");
            }
        }

        static LawInfo L(Law law, Color c, string m)
        {
            string k = Key(law);
            return new LawInfo { name = Loc.T("law." + k + ".name"), rule = Loc.T("law." + k + ".rule"), color = c, music = m };
        }
    }
}
