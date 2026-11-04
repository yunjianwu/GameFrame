using System;
using System.Collections.Generic;

namespace Starter.Runtime.Startup
{
    public enum ResourceUpdateState { Idle, Checking, AwaitingConfirmation, Updating, Completed, Failed, Cancelled }

    public readonly struct ResourceUpdateProgress
    {
        public readonly long TotalCompressedBytes;
        public readonly long ReadyCompressedBytes;
        public readonly bool Ready;
        public ResourceUpdateProgress(long total, long ready, bool complete)
        { TotalCompressedBytes = total; ReadyCompressedBytes = ready; Ready = complete; }
    }

    public interface IResourceUpdateClient
    {
        void Check(Action completed);
        ResourceUpdateProgress GetProgress(string[] groups);
        bool IsReady(string group);
        void Update(string group, Action<bool> completed);
        void Stop();
    }

    public sealed class ResourceUpdateCoordinator
    {
        private readonly IResourceUpdateClient _client;
        private readonly string[] _groups;
        private readonly bool _requireConfirmation;
        private readonly Dictionary<string, long> _partial = new Dictionary<string, long>(StringComparer.Ordinal);
        private int _index;
        private bool _waiting;
        private long _initialReadyBytes;
        private string _terminalFailure;

        public ResourceUpdateState State { get; private set; }
        public string Error { get; private set; }
        public string CurrentGroup { get; private set; }
        public long TotalDownloadBytes { get; private set; }
        public long DownloadedBytes { get; private set; }

        public ResourceUpdateCoordinator(IResourceUpdateClient client, string[] groups, bool confirm)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _requireConfirmation = confirm;
            var names = new List<string>();
            if (groups != null)
                foreach (string group in groups)
                {
                    if (string.IsNullOrWhiteSpace(group)) throw new ArgumentException("Resource group names must not be blank.", nameof(groups));
                    if (!names.Contains(group)) names.Add(group);
                }
            _groups = names.Count == 0 ? new[] { string.Empty } : names.ToArray();
        }

        public void Begin()
        {
            if (State != ResourceUpdateState.Idle) throw new InvalidOperationException("A resource update is one-shot.");
            State = ResourceUpdateState.Checking;
            try
            {
                _client.Check(() =>
                {
                    if (State != ResourceUpdateState.Checking) return;
                    try
                    {
                        foreach (string group in _groups) _client.IsReady(group);
                        ResourceUpdateProgress progress = _client.GetProgress(_groups);
                        _initialReadyBytes = progress.ReadyCompressedBytes;
                        TotalDownloadBytes = Math.Max(0, progress.TotalCompressedBytes - _initialReadyBytes);
                        State = progress.Ready ? ResourceUpdateState.Completed :
                            (_requireConfirmation ? ResourceUpdateState.AwaitingConfirmation : ResourceUpdateState.Updating);
                    }
                    catch (Exception exception) { Abort(exception.Message); }
                });
            }
            catch (Exception exception) { Abort(exception.Message); }
        }

        public void Confirm()
        {
            if (State == ResourceUpdateState.AwaitingConfirmation) State = ResourceUpdateState.Updating;
        }

        // Only the Procedure's OnUpdate calls Tick: GF callbacks never start another group reentrantly.
        public void Tick()
        {
            if (State != ResourceUpdateState.Updating) return;
            if (_terminalFailure != null) { Abort(_terminalFailure); return; }
            try
            {
                RefreshProgress();
                if (_waiting) return;
                while (_index < _groups.Length && _client.IsReady(_groups[_index])) _index++;
                if (_index == _groups.Length)
                {
                    if (!_client.GetProgress(_groups).Ready) { Abort("Selected resource groups are not ready."); return; }
                    DownloadedBytes = TotalDownloadBytes;
                    State = ResourceUpdateState.Completed;
                    return;
                }

                int requestIndex = _index;
                CurrentGroup = _groups[_index];
                _waiting = true;
                _client.Update(CurrentGroup, success =>
                {
                    if (State != ResourceUpdateState.Updating || !_waiting || _index != requestIndex) return;
                    _waiting = false;
                    try
                    {
                        if (!success || !_client.IsReady(_groups[requestIndex]))
                        { Abort("Resource group '" + DisplayGroup(_groups[requestIndex]) + "' failed or is not ready."); return; }
                        _partial.Clear();
                        _index++;
                    }
                    catch (Exception exception) { Abort(exception.Message); }
                });
            }
            catch (Exception exception) { Abort(exception.Message); }
        }

        public void ReportPartial(string resource, long bytes)
        {
            if (State == ResourceUpdateState.Updating && _waiting && !string.IsNullOrEmpty(resource))
                _partial[resource] = Math.Max(0, bytes);
        }

        public void ForgetPartial(string resource)
        {
            if (!string.IsNullOrEmpty(resource)) _partial.Remove(resource);
        }

        public void ReportFailure(string resource, int retryCount, int totalRetryCount, string error)
        {
            if (State != ResourceUpdateState.Updating && State != ResourceUpdateState.Failed) return;
            ForgetPartial(resource);
            if (retryCount < totalRetryCount) return;
            if (_terminalFailure == null)
                _terminalFailure = "Resource '" + resource + "' failed after " + (retryCount + 1) + " attempts: " + error;
            // Queued GF events can arrive after its synchronous group callback; keep the concrete error.
            if (State == ResourceUpdateState.Failed) Error = _terminalFailure;
        }

        public void Abort(string error)
        {
            if (State == ResourceUpdateState.Cancelled || State == ResourceUpdateState.Completed || State == ResourceUpdateState.Failed) return;
            Error = _terminalFailure ?? error;
            State = ResourceUpdateState.Failed;
            StopPending();
        }

        public void Cancel()
        {
            if (State == ResourceUpdateState.Completed || State == ResourceUpdateState.Failed || State == ResourceUpdateState.Cancelled) return;
            State = ResourceUpdateState.Cancelled;
            StopPending();
        }

        private void StopPending()
        {
            if (!_waiting) return;
            _waiting = false;
            try { _client.Stop(); }
            catch (Exception exception) { Error = (Error ?? "") + " Stop update: " + exception.Message; }
        }

        private void RefreshProgress()
        {
            ResourceUpdateProgress progress = _client.GetProgress(_groups);
            long bytes = Math.Min(TotalDownloadBytes, Math.Max(0, progress.ReadyCompressedBytes - _initialReadyBytes));
            foreach (long partial in _partial.Values) bytes += Math.Min(partial, TotalDownloadBytes - bytes);
            DownloadedBytes = bytes;
        }

        private static string DisplayGroup(string group) => string.IsNullOrEmpty(group) ? "all" : group;
    }
}
