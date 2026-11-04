using System.Collections.Generic;

namespace RulesShift
{
    /// <summary>
    /// Seeded randomness for everything that defines a run (room choices, flasks, extra laws),
    /// so the Daily run is the same for everyone. Enemy AI keeps using UnityEngine.Random.
    /// </summary>
    public static class RunRandom
    {
        static System.Random rng = new System.Random();
        public static int Seed { get; private set; }

        public static void Init(int seed)
        {
            Seed = seed;
            rng = new System.Random(seed);
        }

        /// <summary>
        /// A fresh stream for one room of the run: the same room at the same step gives the same doors, chests and elites
        /// whatever happened before it (how many chests were opened, how long a fight took).
        /// </summary>
        public static void ForRoom(int step, string roomId)
        {
            int h = 17;
            if (roomId != null) foreach (char c in roomId) h = unchecked(h * 31 + c);   // stable on every platform
            rng = new System.Random(unchecked(Seed ^ (step * 92821) ^ (h * 486187739)));
        }

        public static int Range(int min, int maxExclusive) => rng.Next(min, maxExclusive);

        public static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        public static float Value => (float)rng.NextDouble();

        public static T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];

        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int j = rng.Next(i, list.Count);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
