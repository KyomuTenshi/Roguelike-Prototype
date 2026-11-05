using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Achievements (codex → records). Each one also gives relic points: new relics join the pool over time.
    /// The checks are called from where things happen (a boss falls, a combo forms, a run ends…).
    /// </summary>
    public static class Achievements
    {
        public static readonly string[] All =
        {
            "win", "floor2", "floor3", "combo", "combo5", "relics20", "slime", "frost", "demon", "save",
            "endings", "rich", "groove", "flawless", "jackpot", "challenge", "mimic", "rift", "kills1000", "heroes"
        };

        /// <summary>How many relics are in the pool: 40 at first, more with finished runs and achievements.</summary>
        public static int UnlockedRelics => Mathf.Clamp(40 + SaveSystem.Data.relicPoints, 40, Relics.All.Length);

        public static bool Has(string id) => SaveSystem.Data.achievements.Contains(id);

        public static void Unlock(string id)
        {
            var d = SaveSystem.Data;
            if (d.achievements.Contains(id)) return;
            d.achievements.Add(id);
            d.relicPoints += 2;
            SaveSystem.Save();
            Hud.Toast(Loc.F("ach.toast", Loc.T("ach." + id + ".name")), UiKit.Gold);
            var p = PlayerController.I;
            if (p != null && p.Alive) Fx.Anim("levelup", p.Position + Vector2.up * 2f, 0.25f, 0f, 60, null, p.transform);
            Sfx.Play("win", 0.35f, 1.3f);
        }

        /// <summary>Checks that only need the save (called at the end of a run).</summary>
        public static void CheckSave()
        {
            var d = SaveSystem.Data;
            if (d.totalKills >= 1000) Unlock("kills1000");
            if ((d.finales & 7) == 7) Unlock("endings");
            bool all = true;
            foreach (var h in Heroes.All) if (!SaveSystem.HeroUnlocked(h)) all = false;
            if (all) Unlock("heroes");
        }

        /// <summary>Checks on the hero during a run (relics, combos).</summary>
        public static void CheckHero(PlayerController p)
        {
            if (p == null) return;
            if (p.ComboList.Count >= 1) Unlock("combo");
            if (p.ComboList.Count >= 5) Unlock("combo5");
            if (p.RelicList.Count >= 20) Unlock("relics20");
        }
    }
}
