using System;
using System.Collections.Generic;
using UnityEngine;

namespace RulesShift
{
    public enum Objective
    {
        KillAll,    // убить всех
        Survive,    // продержаться N секунд
        ReachDoor,  // дверь открыта сразу, добежать
        FindKey,    // открыть сундук с ключом
        Buttons,    // нажать все кнопки
        Boss,       // босс (Законодатель или босс этажа)
        Shop,       // лавка: товары за монеты
        Shrine,     // алтарь: реликвия за сердце
        Fountain,   // фонтан: лечение
        Secret      // тайная комната: воспоминание о прошлом
    }

    public enum Theme
    {
        Dungeon,    // 0x72 DungeonTileset II
        Classic,    // 0x72 16x16 Dungeon Tileset v5 — "другой мир"
        Modern      // LimeZu Modern tiles — the secret room (the past)
    }

    /// <summary>One room of the pool. Edit them on the RoomManager in the Game scene.</summary>
    [Serializable]
    public class RoomDef
    {
        public string id = "room";
        public string title = "Комната";
        [TextArea] public string rule = "";
        public Law law;
        public Objective objective;
        public Theme theme;
        [Tooltip("Index in Layouts.All: 0 Зал, 1 Колонны, 2 Ящики, 3 Сокровищница, 4 Лавовый коридор, 5 Пульт, 6 Арена")]
        public int layout;
        [Tooltip("Seconds for Survive")]
        public float time = 20f;

        [Header("Enemies")]
        public int runners;
        public int shooters;
        public int kamikazes;
        public string runnerSkin = "goblin";
        public string shooterSkin = "orc_shaman";
        public string kamikazeSkin = "tiny_zombie";

        [Header("Run")]
        [Tooltip("Can be picked for a run")]
        public bool inPool = true;
        [Tooltip("The boss can bring this law back")]
        public bool usableInBoss = true;

        [HideInInspector] public bool floorBoss;            // Ogre / Rotting Giant instead of the Lawgiver
        [HideInInspector] public FloorBossKind bossKind;
        [HideInInspector] public int memory;                 // secret room: 0 the dream, 1 the work, 2 the silence

        public RoomDef Clone() => (RoomDef)MemberwiseClone();

        /// <summary>Localized name and rule shown in the game (title/rule above are editor notes).</summary>
        public string Title
        {
            get
            {
                switch (objective)
                {
                    case Objective.Boss: return floorBoss ? Loc.T(BossKey) : Loc.T("law.boss.name");
                    case Objective.Shop: return Loc.T("room.shop");
                    case Objective.Shrine: return Loc.T("room.shrine");
                    case Objective.Fountain: return Loc.T("room.fountain");
                    case Objective.Secret: return Loc.T("room.secret." + memory);
                    default: return Rules.Info(law).name;
                }
            }
        }

        public string Rule
        {
            get
            {
                switch (objective)
                {
                    case Objective.Boss: return floorBoss ? Loc.T(BossKey + ".rule") : Loc.T("law.boss.room");
                    case Objective.Shop: return Loc.T("room.shop.rule");
                    case Objective.Shrine: return Loc.T("room.shrine.rule");
                    case Objective.Fountain: return Loc.T("room.fountain.rule");
                    case Objective.Secret: return Loc.T("room.secret." + memory + ".rule");
                    default: return Rules.Info(law).rule;
                }
            }
        }

        public bool Special => objective == Objective.Shop || objective == Objective.Shrine || objective == Objective.Fountain || objective == Objective.Secret;
        string BossKey => bossKind == FloorBossKind.Ogre ? "boss.ogre" : "boss.zombie";

        /// <summary>Colour of the door sign and the room card.</summary>
        public Color SignColor
        {
            get
            {
                switch (objective)
                {
                    case Objective.Boss: return new Color(1f, 0.35f, 0.3f);
                    case Objective.Shop: return new Color(1f, 0.85f, 0.35f);
                    case Objective.Shrine: return new Color(1f, 0.45f, 0.5f);
                    case Objective.Fountain: return new Color(0.5f, 0.85f, 1f);
                    case Objective.Secret: return new Color(0.75f, 0.9f, 0.7f);
                    default: return Rules.Info(law).color;
                }
            }
        }
    }

    /// <summary>Default rooms (RoomManager keeps its own copies in the scene, editable in the Inspector).</summary>
    public static class RoomCatalog
    {
        public static List<RoomDef> Pool()
        {
            return new List<RoomDef>
            {
                R("entry", Law.None, Objective.KillAll, 0, "WASD — движение, мышь — прицел, ЛКМ — выстрел", runners: 3, inPool: false, boss: false),
                R("crypt", Law.Crypt, Objective.KillAll, 1, null, runners: 4, shooters: 2, rs: "skelet", ss: "necromancer"),
                R("noir", Law.Noir, Objective.KillAll, 2, null, runners: 3, shooters: 3, rs: "masked_orc", ss: "necromancer"),
                R("ice", Law.Ice, Objective.KillAll, 0, null, runners: 5, kamikazes: 3, rs: "ice_zombie", ks: "tiny_slug"),
                R("mirror", Law.Mirror, Objective.KillAll, 1, null, runners: 4, shooters: 2, rs: "zombie", ss: "orc_shaman", theme: Theme.Classic),
                R("pacifist", Law.Pacifist, Objective.Survive, 2, null, runners: 4, kamikazes: 2, rs: "swampy", ks: "muddy"),
                R("retro", Law.Retro, Objective.KillAll, 0, null, runners: 5, shooters: 2, rs: "skelet", ss: "imp", boss: false),
                R("lava", Law.Lava, Objective.ReachDoor, 4, null, runners: 4, shooters: 1, rs: "chort", ss: "imp", boss: false),
                R("treasury", Law.Stealth, Objective.FindKey, 3, null, runners: 6, shooters: 2, rs: "orc_warrior", ss: "orc_shaman", theme: Theme.Classic, boss: false),
                R("upside", Law.Upside, Objective.KillAll, 2, null, runners: 4, shooters: 2, kamikazes: 2),
                R("neon", Law.Neon, Objective.KillAll, 1, null, runners: 4, shooters: 1, kamikazes: 3, rs: "wogol", ss: "imp"),
                R("console", Law.Console, Objective.Buttons, 5, null, runners: 3, shooters: 2, boss: false),
            };
        }

        public static RoomDef Boss()
        {
            var b = R("boss", Law.Boss, Objective.Boss, 6, null, inPool: false, boss: false);
            b.title = "Законодатель";
            b.rule = "Каждые 15 секунд он возвращает закон одной из твоих комнат";
            return b;
        }

        static RoomDef R(string id, Law law, Objective obj, int layout, string rule,
            int runners = 0, int shooters = 0, int kamikazes = 0,
            string rs = "goblin", string ss = "orc_shaman", string ks = "tiny_zombie",
            Theme theme = Theme.Dungeon, bool inPool = true, bool boss = true)
        {
            var info = Rules.Info(law);
            return new RoomDef
            {
                id = id, title = info.name, rule = rule ?? info.rule, law = law, objective = obj, layout = layout, theme = theme,
                runners = runners, shooters = shooters, kamikazes = kamikazes,
                runnerSkin = rs, shooterSkin = ss, kamikazeSkin = ks,
                inPool = inPool, usableInBoss = boss, time = 20f
            };
        }
    }
}
