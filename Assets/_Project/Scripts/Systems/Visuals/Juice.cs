using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Game feel: a short hit-stop on heavy hits, slow motion on the last kill of a room and on a boss's death,
    /// a flash on crits, optional damage numbers. Works only while the game is running (never fights pause menus).
    /// </summary>
    public class Juice : MonoBehaviour
    {
        static Juice inst;
        float until, scale = 1f;
        bool active;

        static Juice I
        {
            get
            {
                if (!inst)
                {
                    var go = new GameObject("Juice");
                    inst = go.AddComponent<Juice>();
                }
                return inst;
            }
        }

        static bool Playing => RoomManager.I && RoomManager.I.State == GameState.Playing;

        /// <summary>Freezes the game for a few hundredths of a second (big hits, crits, kills of elites).</summary>
        public static void HitStop(float seconds) => Slow(0.05f, seconds);

        /// <summary>Slow motion (the last enemy of a room, a boss falls).</summary>
        public static void Slow(float timeScale, float seconds)
        {
            if (!Playing) return;
            var j = I;
            // never override a pause or a reward screen (they set the time scale to 0)
            if (!j.active && Time.timeScale < 0.99f) return;
            if (j.active && timeScale > j.scale) return;   // a stronger effect is already running
            j.active = true;
            j.scale = timeScale;
            j.until = Mathf.Max(j.until, Time.unscaledTime + seconds);
            Time.timeScale = timeScale;
        }

        void Update()
        {
            if (!active) return;
            // something else (pause, upgrade cards) took over the time scale: let it be
            if (!Mathf.Approximately(Time.timeScale, scale)) { active = false; return; }
            if (Time.unscaledTime < until) return;
            Time.timeScale = 1f;
            active = false;
        }

        /// <summary>A white flash over the screen (crits, a boss dies).</summary>
        public static void Flash(Color c, float alpha)
        {
            if (Hud.I) Hud.I.Flash(c, alpha);
        }

        /// <summary>Damage numbers over a target, when turned on in the settings.</summary>
        public static void Number(Vector2 pos, float dmg, bool crit = false)
        {
            if (!SaveSystem.Data.damageNumbers || dmg <= 0f) return;
            string s = dmg >= 10f ? Mathf.RoundToInt(dmg).ToString() : dmg.ToString(dmg % 1f < 0.05f ? "0" : "0.0");
            Fx.Text(pos + new Vector2(Random.Range(-0.25f, 0.25f), 0.9f), s,
                crit ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.9f), crit ? 0.8f : 0.6f, 0.55f);
        }
    }
}
