namespace RulesShift
{
    /// <summary>Run-wide difficulty knobs (Fury curse, Endless depth).</summary>
    public static class Difficulty
    {
        public static int EnemyHpBonus;
        public static float EnemyHpMul = 1f;      // grows with the floor and with the hero's relics
        public static int EnemyDamage = 1;        // halves of a heart per touch / orb
        public static float EnemySpeed = 1f;
        public static float CountMul = 1f;
        public static float BossHpMul = 1f;

        public static void Reset()
        {
            EnemyHpBonus = 0;
            EnemyHpMul = 1f;
            EnemyDamage = 1;
            EnemySpeed = 1f;
            CountMul = 1f;
            BossHpMul = 1f;
        }
    }
}
