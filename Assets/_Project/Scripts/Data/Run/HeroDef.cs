using UnityEngine;

namespace RulesShift
{
    /// <summary>How a hero attacks.</summary>
    public enum WeaponKind
    {
        Bow,        // Elf: fast arrows
        Sword,      // Knight: melee arc that reflects enemy shots
        Staff,      // Sorceress: hold to charge an orb, full charge explodes; blink instead of dash
        Daggers     // Lizard: poisoned knives in pairs, built-in roll, heals after a clean room
    }

    /// <summary>A playable hero: sprites from the pack, a weapon and base stats.</summary>
    public class HeroDef
    {
        public string id;
        public WeaponKind weapon;
        public string skin;            // "elf_m" → elf_m_idle_anim / run / hit
        public string held;            // weapon sprite in the hand
        public bool heldPointsUp;      // 0x72 weapons are drawn pointing up (the bow is not)
        public string projectile;      // sprite of the projectile, null = glowing orb
        public bool spin;              // throwing axes spin
        public Color projColor = Color.white;
        public bool projGlow;

        public int halves = 6;         // 2 halves = 1 heart
        public float speed = 5.2f;
        public int damage = 1;
        public float fireRate = 3.2f;  // shots / swings per second
        public int shots = 1;
        public int pierce;
        public float projSpeed = 13f;

        public bool innateDash;        // can roll without the Swift Boots relic
        public int innatePoison;       // poison stacks on every hit
        public bool blink;             // Shift / RMB teleports instead of dashing
        public bool cleanRoomHeal;     // +½ heart after a room without damage

        public System.Func<SaveData, bool> unlocked = s => true;

        public string name => Loc.T("hero." + id + ".name");
        public string desc => Loc.T("hero." + id + ".desc");
        public string unlockHint => Loc.Has("hero." + id + ".unlock") ? Loc.T("hero." + id + ".unlock") : null;
        public bool Melee => weapon == WeaponKind.Sword;
    }

    public static class Heroes
    {
        public static readonly HeroDef[] All =
        {
            new HeroDef
            {
                id = "elf", weapon = WeaponKind.Bow,
                skin = "elf_m", held = "weapon_bow", heldPointsUp = false, projectile = "weapon_arrow",
                halves = 6, speed = 5.2f, damage = 1, fireRate = 3.2f, projSpeed = 13f
            },
            new HeroDef
            {
                id = "knight", weapon = WeaponKind.Sword,
                skin = "knight_m", held = "weapon_knight_sword", heldPointsUp = true,
                halves = 10, speed = 4.7f, damage = 2, fireRate = 2.3f,
                unlocked = s => s.wins >= 1
            },
            new HeroDef
            {
                id = "wizard", weapon = WeaponKind.Staff,
                skin = "wizzard_f", held = "weapon_red_magic_staff", heldPointsUp = true, projectile = null,
                projColor = new Color(1f, 0.45f, 0.95f), projGlow = true,
                halves = 6, speed = 4.9f, damage = 1, fireRate = 2.6f, projSpeed = 11f,
                blink = true,
                unlocked = s => s.combos != null && s.combos.Count >= 5
            },
            new HeroDef
            {
                id = "lizard", weapon = WeaponKind.Daggers,
                skin = "lizard_m", held = "weapon_knife", heldPointsUp = true, projectile = "weapon_knife",
                projColor = new Color(0.6f, 1f, 0.45f),
                halves = 6, speed = 6.2f, damage = 1, fireRate = 3f, shots = 2, projSpeed = 17f,
                innateDash = true, innatePoison = 1, cleanRoomHeal = true,
                unlocked = s => s.maxCursesWin >= 2
            },
        };

        public static HeroDef Get(string id)
        {
            foreach (var h in All) if (h.id == id) return h;
            return All[0];
        }
    }
}
