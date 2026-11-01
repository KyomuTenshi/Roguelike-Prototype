namespace RulesShift
{
    /// <summary>Run-wide difficulty knobs (Fury curse, Endless depth).</summary>
    public static class Difficulty
    {
        public static int EnemyHpBonus;
        public static float EnemySpeed = 1f;
        public static float CountMul = 1f;
        public static float BossHpMul = 1f;

        public static void Reset()
        {
            EnemyHpBonus = 0;
            EnemySpeed = 1f;
            CountMul = 1f;
            BossHpMul = 1f;
        }
    }
}
