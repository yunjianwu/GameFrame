using System;
using AlgorithmDemos;
using Starter.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Game.UI
{
    public sealed class DemoNavigation
    {
        public string Title { get; }
        public Action Back { get; }
        public Action Restart { get; }
        public DemoNavigation(string title, Action back, Action restart)
        {
            Title = title;
            Back = back;
            Restart = restart;
        }
    }

    public sealed class DemoNavigationForm : ScopedUIForm
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Text _title;
        [SerializeField] private Button _back;
        [SerializeField] private Button _restart;
        private DemoNavigation _navigation;

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            _back.onClick.AddListener(() => _navigation?.Back?.Invoke());
            _restart.onClick.AddListener(() => _navigation?.Restart?.Invoke());
        }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            _navigation = userData as DemoNavigation;
            _title.text = _navigation?.Title ?? "ALGORITHM DEMO";
            DemoInputGate.NavigationArea = _panel;
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            DemoInputGate.Release(_panel);
            _navigation = null;
            base.OnClose(isShutdown, userData);
        }
    }
}
