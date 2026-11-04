using System;
using Starter.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Game.UI
{
    public sealed class MenuForm : ScopedUIForm
    {
        [SerializeField] private Text _title;
        [SerializeField] private Text _balance;
        [SerializeField] private Button _play;
        private Action _onPlay;

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            _play.onClick.AddListener(() => _onPlay?.Invoke());
        }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            _onPlay = userData as Action;
            _title.text = GameSession.Config.title;
            _balance.text = "Saved coins: " + GameSession.Wallet.Balance;
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            _onPlay = null;
            base.OnClose(isShutdown, userData);
        }
    }
}
