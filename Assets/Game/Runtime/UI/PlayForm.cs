using System;
using GameFramework.Event;
using Starter.Game.Events;
using Starter.Runtime;
using Starter.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Game.UI
{
    public sealed class PlayForm : ScopedUIForm
    {
        [SerializeField] private Text _balance;
        [SerializeField] private Text _feedback;
        [SerializeField] private Button _grant;
        [SerializeField] private Button _save;
        [SerializeField] private Button _back;
        private Action _onBack;

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            // OnInit 每个实例只执行一次。OnOpen 再加监听会在对象池复用后重复触发。
            _grant.onClick.AddListener(GrantCoins);
            _save.onClick.AddListener(Save);
            _back.onClick.AddListener(() => _onBack?.Invoke());
        }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            _onBack = userData as Action;
            _balance.text = "Coins: " + GameSession.Wallet.Balance;
            _feedback.text = "Grant updates the model, then GF delivers an event next frame.";
            Events.Subscribe(CoinsChangedEventArgs.EventId, OnCoinsChanged);
        }

        private void GrantCoins()
        {
            try { GameSession.Wallet.Grant(GameSession.Config.rewardPerClick); }
            catch (OverflowException) { _feedback.text = "Coin limit reached."; }
        }

        private void Save()
        {
            GameSession.Wallet.Save();
            _feedback.text = "Saved locally. Pause and quit also flush settings.";
        }

        private void OnCoinsChanged(object sender, GameEventArgs args)
        {
            int balance = ((CoinsChangedEventArgs)args).Balance;
            _balance.text = "Coins: " + balance;
            _feedback.text = "Event received on the main thread. Event args are returned to the pool.";
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            _onBack = null;
            base.OnClose(isShutdown, userData);
        }
    }
}
