using System;

namespace Starter.Game
{
    [Serializable]
    public sealed class GameConfig
    {
        public string title;
        public int startingCoins;
        public int rewardPerClick;
        public int targetFrameRate;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("Config title is required.");
            if (startingCoins < 0) throw new InvalidOperationException("startingCoins must be non-negative.");
            if (rewardPerClick < 1) throw new InvalidOperationException("rewardPerClick must be positive.");
            if (targetFrameRate < 15 || targetFrameRate > 120)
                throw new InvalidOperationException("targetFrameRate must be between 15 and 120.");
        }
    }
}
