using Starter.Core;
using Starter.Game.Events;
using UnityGameFramework.Runtime;

namespace Starter.Game.Services
{
    public sealed class WalletService
    {
        public const string SaveKey = "starter.demo.coins.v1";
        private readonly SettingComponent _settings;
        private readonly EventComponent _events;
        private readonly Wallet _wallet;
        public int Balance => _wallet.Balance;

        public WalletService(SettingComponent settings, EventComponent events, int startingCoins)
        {
            _settings = settings;
            _events = events;
            int saved = settings.GetInt(SaveKey, startingCoins);
            _wallet = new Wallet(saved < 0 ? startingCoins : saved);
        }

        public void Grant(int amount)
        {
            _wallet.Grant(amount);
            _settings.SetInt(SaveKey, Balance);
            // Fire 下一次 Update 派发并回收参数。订阅者只复制 Balance，不保留 args。
            _events.Fire(this, CoinsChangedEventArgs.Create(Balance));
        }

        public void Save() { _settings.Save(); }
    }
}
