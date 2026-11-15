using UnityEngine;

namespace P7
{
    // Bonus points for staying on the line: after 2 seconds of near-perfect tracking,
    // points are given every half second, and the multiplier rises every four ticks.
    // Any break (off the line, tracking lost, rest) starts the warm-up again.
    public class PerfectFlowBonus
    {
        public const float WarmupSeconds = 2;
        public const float TickSeconds = .5f;
        public const float MinimumQuality = .9f;
        public const int PointsPerTick = 100;

        public float Seconds { get; private set; }
        public int Ticks { get; private set; }
        public int StreakPoints { get; private set; }
        public int Multiplier => Mathf.Clamp(1 + (Ticks - 1) / 4, 1, 5);

        public void Reset()
        {
            Seconds = 0;
            Ticks = 0;
            StreakPoints = 0;
        }

        // Returns the points earned this frame.
        public int Advance(bool eligible, float quality, float deltaTime)
        {
            if (!eligible || float.IsNaN(quality) || quality < MinimumQuality)
            {
                Reset();
                return 0;
            }
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0)
                return 0;

            // Capped so a frame hitch never pays out several ticks at once.
            Seconds += Mathf.Min(deltaTime, .1f);
            float nextTick = WarmupSeconds + Ticks * TickSeconds;
            if (Seconds + .00001f < nextTick)
                return 0;

            Ticks++;
            int points = PointsPerTick * Multiplier;
            StreakPoints += points;
            return points;
        }
    }
}
