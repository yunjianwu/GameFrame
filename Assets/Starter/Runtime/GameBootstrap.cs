using System;
using System.Threading;
using Starter.Core;
using Starter.Runtime.Startup;
using UnityEngine;

namespace Starter.Runtime
{
    /// <summary>只放在 Boot 场景的 GameRoot；GF 的流程组件负责启动流程，不在这里手动再启动一次。</summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private StartupSettings _startupSettings;
        public static GameBootstrap Current { get; private set; }
        public StartupSettings StartupSettings => _startupSettings;
        public MainThreadQueue MainThread { get; private set; }
        public CancellationToken Lifetime => _lifetime.Token;
        public string Status { get; private set; } = "Starting framework...";
        public string Failure { get; private set; }
        public long DownloadedBytes { get; private set; }
        public long TotalDownloadBytes { get; private set; }
        public bool DownloadConfirmationPending => _confirmDownload != null;
        private CancellationTokenSource _lifetime;
        private Action _confirmDownload;
        private bool _startupComplete;

        private void Awake()
        {
            if (Current != null && Current != this)
                throw new InvalidOperationException("Boot scene already has a GameBootstrap.");
            Current = this;
            _lifetime = new CancellationTokenSource();
            MainThread = new MainThreadQueue(1024);
            DontDestroyOnLoad(gameObject);
        }

        private void Update() { MainThread.Pump(64, Debug.LogException); }
        public void SetStatus(string status) { Status = status; }
        public void SetDownloadProgress(long downloaded, long total) { DownloadedBytes = downloaded; TotalDownloadBytes = total; }
        public void SetDownloadConfirmation(Action confirm) { _confirmDownload = confirm; }
        public void CompleteStartup() { _startupComplete = true; }

        public void ConfirmResourceDownload()
        {
            Action confirm = _confirmDownload;
            _confirmDownload = null;
            confirm?.Invoke();
        }

        public void ReportFailure(string message)
        {
            Failure = message;
            _confirmDownload = null;
            Status = "Startup failed";
            Debug.LogError(message);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && GameServices.IsBound) GameServices.Setting.Save();
        }

        private void OnApplicationQuit()
        {
            if (GameServices.IsBound) GameServices.Setting.Save();
        }

        private void OnDestroy()
        {
            if (Current != this) return;
            _confirmDownload = null;
            _lifetime.Cancel();
            MainThread.Dispose();
            _lifetime.Dispose();
            GameServices.Unbind();
            Current = null;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(Failure) && _startupComplete) return;
            // Startup and failures remain visible even before asset-backed UI can load.
            float width = Mathf.Max(280, Screen.width - 40);
            GUI.Box(new Rect(20, 20, width, 210), "GF Starter");
            GUI.Label(new Rect(35, 55, width - 30, 95), string.IsNullOrEmpty(Failure) ? Status : Failure);
            if (!string.IsNullOrEmpty(Failure))
                GUI.Label(new Rect(35, 155, width - 30, 50), "Check the startup settings/network and restart the application.");
            else if (DownloadConfirmationPending)
            {
                if (GUI.Button(new Rect(35, 160, 240, 40), "Download " + (TotalDownloadBytes / 1048576f).ToString("F2") + " MiB"))
                    ConfirmResourceDownload();
            }
            else if (TotalDownloadBytes > 0)
                GUI.Label(new Rect(35, 160, width - 30, 40), DownloadedBytes + " / " + TotalDownloadBytes + " bytes");
        }
    }
}
