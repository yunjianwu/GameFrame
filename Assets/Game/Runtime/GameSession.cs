using Starter.Game.Services;
using Starter.Runtime;

namespace Starter.Game
{
    /// <summary>示例项目的数据入口；新项目可在此组合自己的业务服务。</summary>
    public static class GameSession
    {
        public static GameConfig Config { get; private set; }
        public static WalletService Wallet { get; private set; }

        public static void Initialize(GameConfig config)
        {
            config.Validate();
            Config = config;
            Wallet = new WalletService(GameServices.Setting, GameServices.Event, config.startingCoins);
            GameServices.Base.FrameRate = config.targetFrameRate;
        }
    }
}
