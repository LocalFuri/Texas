using System;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>
    /// Per-player tendency axes for future style-aware AI.
    /// Structural only — not consumed by decision logic yet.
    /// </summary>
    public readonly struct PlayerTendencyProfile : IEquatable<PlayerTendencyProfile>
    {
        /// <summary>−1 = very loose, 0 = neutral, +1 = very tight.</summary>
        public float Tightness { get; }

        /// <summary>−1 = very passive, 0 = neutral, +1 = very aggressive.</summary>
        public float Aggression { get; }

        public PlayerTendencyProfile(float tightness, float aggression)
        {
            Tightness  = Mathf.Clamp(tightness, -1f, 1f);
            Aggression = Mathf.Clamp(aggression, -1f, 1f);
        }

        public static PlayerTendencyProfile Neutral { get; } = new PlayerTendencyProfile(0f, 0f);

        public bool Equals(PlayerTendencyProfile other) =>
            Tightness.Equals(other.Tightness) && Aggression.Equals(other.Aggression);

        public override bool Equals(object obj) =>
            obj is PlayerTendencyProfile other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Tightness.GetHashCode() * 397) ^ Aggression.GetHashCode();
            }
        }

        public override string ToString() =>
            $"Tightness={Tightness:F2}, Aggression={Aggression:F2}";
    }

    /// <summary>
    /// Fixed tendency assignments for named table players.
    /// Lookup only — does not affect AI decisions until wired later.
    /// </summary>
    public static class PlayerTendencyProfiles
    {
        private static readonly PlayerTendencyProfile LadyLuck =
            new PlayerTendencyProfile(-0.65f, 0.65f);

        private static readonly PlayerTendencyProfile PrinceBeaumont =
            new PlayerTendencyProfile(0.65f, -0.65f);

        private static readonly PlayerTendencyProfile VictorShark =
            new PlayerTendencyProfile(0.65f, 0.65f);

        private static readonly PlayerTendencyProfile JasmineVale =
            new PlayerTendencyProfile(-0.65f, -0.65f);

        private static readonly PlayerTendencyProfile AlexHunter =
            PlayerTendencyProfile.Neutral;

        private static readonly PlayerTendencyProfile AceMaverick =
            PlayerTendencyProfile.Neutral;

        /// <summary>Returns the fixed tendency profile for a player name; unknown names → Neutral.</summary>
        public static PlayerTendencyProfile GetProfile(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return PlayerTendencyProfile.Neutral;

            switch (playerName)
            {
                case "Lady Luck":
                    return LadyLuck;
                case "Prince Beaumont":
                    return PrinceBeaumont;
                case "Victor Shark":
                    return VictorShark;
                case "Jasmine Vale":
                    return JasmineVale;
                case "Alex Hunter":
                    return AlexHunter;
                case "Ace Maverick":
                    return AceMaverick;
                default:
                    return PlayerTendencyProfile.Neutral;
            }
        }

        /// <summary>Returns the fixed tendency profile for a player; null → Neutral.</summary>
        public static PlayerTendencyProfile GetProfile(PlayerState player) =>
            GetProfile(player != null ? player.Name : null);
    }
}
