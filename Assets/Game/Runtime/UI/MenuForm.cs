using System;
using AlgorithmDemos;
using Starter.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Game.UI
{
    public sealed class MenuNavigation
    {
        public Action OpenFramework { get; }
        public Action<DemoKind> OpenDemo { get; }
        public MenuNavigation(Action openFramework, Action<DemoKind> openDemo)
        {
            OpenFramework = openFramework;
            OpenDemo = openDemo;
        }
    }

    public sealed class MenuForm : ScopedUIForm
    {
        [SerializeField] private Text _title;
        [SerializeField] private Text _balance;
        [SerializeField] private Button _play;
        [SerializeField] private Button _boids;
        [SerializeField] private Button _flowField;
        [SerializeField] private Button _quadTree;
        [SerializeField] private Button _navMesh;
        private Action _onPlay;
        private Action<DemoKind> _onDemo;

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            _play.onClick.AddListener(() => _onPlay?.Invoke());
            _boids.onClick.AddListener(() => _onDemo?.Invoke(DemoKind.Boids));
            _flowField.onClick.AddListener(() => _onDemo?.Invoke(DemoKind.FlowField));
            _quadTree.onClick.AddListener(() => _onDemo?.Invoke(DemoKind.QuadTree));
            _navMesh.onClick.AddListener(() => _onDemo?.Invoke(DemoKind.NavMesh));
        }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            var navigation = userData as MenuNavigation;
            _onPlay = navigation?.OpenFramework ?? userData as Action;
            _onDemo = navigation?.OpenDemo;
            _title.text = GameSession.Config.title;
            _balance.text = "Saved coins: " + GameSession.Wallet.Balance;
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            _onPlay = null;
            _onDemo = null;
            base.OnClose(isShutdown, userData);
        }
    }
}
