using System;
using System.Collections.Generic;
using GameFramework;
using GameFramework.Event;
using Starter.Core;
using Starter.Runtime;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    public enum SceneDestination { Menu, Play, AlgorithmDemo }

    public sealed class ProcedureChangeScene : ProcedureBase
    {
        private const string SceneKey = "Starter.NextScene";
        private const string DestinationKey = "Starter.NextProcedure";
        private const string ReloadKey = "Starter.ReloadScene";
        private readonly HashSet<string> _pendingUnloads = new HashSet<string>(StringComparer.Ordinal);
        private EventScope _events;
        private object _request;
        private string _target;
        private SceneDestination _destination;
        private bool _loadRequested;
        private bool _ready;
        private float _elapsed;

        public static void Request(ProcedureOwner owner, string sceneAsset, SceneDestination destination, bool reload = false)
        {
            if (string.IsNullOrEmpty(sceneAsset) || !sceneAsset.StartsWith("Assets/", StringComparison.Ordinal) ||
                !sceneAsset.EndsWith(".unity", StringComparison.Ordinal)) throw new ArgumentException("Use a full Unity scene asset path.", nameof(sceneAsset));
            if (!Enum.IsDefined(typeof(SceneDestination), destination)) throw new ArgumentOutOfRangeException(nameof(destination));
            owner.SetData<VarString>(SceneKey, sceneAsset);
            owner.SetData<VarInt32>(DestinationKey, (int)destination);
            owner.SetData<VarInt32>(ReloadKey, reload ? 1 : 0);
        }

        protected override void OnEnter(ProcedureOwner owner)
        {
            base.OnEnter(owner);
            _request = new object();
            _pendingUnloads.Clear();
            _loadRequested = false;
            _ready = false;
            _elapsed = 0;
            try
            {
                var scene = owner.GetData<VarString>(SceneKey);
                var destination = owner.GetData<VarInt32>(DestinationKey);
                var reload = owner.GetData<VarInt32>(ReloadKey);
                bool reloadScene = reload != null && reload.Value != 0;
                if (scene == null || destination == null) throw new InvalidOperationException("Request a destination before entering ProcedureChangeScene.");
                _target = scene.Value;
                _destination = (SceneDestination)destination.Value;
                owner.RemoveData(SceneKey);
                owner.RemoveData(DestinationKey);
                owner.RemoveData(ReloadKey);
                if (!Enum.IsDefined(typeof(SceneDestination), _destination)) throw new InvalidOperationException("Unknown destination procedure.");
                if (!GameServices.Scene.HasScene(_target)) throw new InvalidOperationException("Scene is not available in the ready resource set: " + _target);
                if (GameServices.Scene.GetLoadingSceneAssetNames().Length != 0 || GameServices.Scene.GetUnloadingSceneAssetNames().Length != 0)
                    throw new InvalidOperationException("Another GF scene operation is in progress. Keep scene transitions owned by this procedure.");
                _events = new EventScope(GameFrameworkEntry.GetModule<IEventManager>());
                _events.Subscribe(LoadSceneSuccessEventArgs.EventId, OnLoaded);
                _events.Subscribe(LoadSceneFailureEventArgs.EventId, OnLoadFailed);
                _events.Subscribe(UnloadSceneSuccessEventArgs.EventId, OnUnloaded);
                _events.Subscribe(UnloadSceneFailureEventArgs.EventId, OnUnloadFailed);
                GameServices.Host.SetStatus("Changing scene: " + _target);
                ClearTransientObjects();
                // Boot is loaded by Unity, not GF Scene, so it is never in this owned business-scene list.
                foreach (string loaded in GameServices.Scene.GetLoadedSceneAssetNames())
                    if (reloadScene || loaded != _target) _pendingUnloads.Add(loaded);
                foreach (string loaded in _pendingUnloads) GameServices.Scene.UnloadScene(loaded, _request);
            }
            catch (Exception exception) { Fail(exception.Message); }
        }

        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            base.OnUpdate(owner, elapsed, realElapsed);
            if (!IsCurrent(Generation)) return;
            _elapsed += realElapsed;
            if (_elapsed > GameServices.Host.StartupSettings.OperationTimeoutSeconds) { Fail("Scene transition timed out: " + _target); return; }
            if (_pendingUnloads.Count != 0) return;
            if (!_loadRequested)
            {
                _loadRequested = true;
                try
                {
                    if (GameServices.Scene.SceneIsLoaded(_target)) _ready = true;
                    else GameServices.Scene.LoadScene(_target, _request);
                }
                catch (Exception exception) { Fail(exception.Message); return; }
            }
            if (!_ready) return;
            if (_destination == SceneDestination.Menu) ChangeState<ProcedureMenu>(owner);
            else if (_destination == SceneDestination.AlgorithmDemo) ChangeState<ProcedureAlgorithmDemo>(owner);
            else ChangeState<ProcedurePlay>(owner);
        }

        private void OnLoaded(object sender, GameEventArgs args)
        {
            var data = (LoadSceneSuccessEventArgs)args;
            if (IsCurrent(Generation) && ReferenceEquals(data.UserData, _request) && data.SceneAssetName == _target) _ready = true;
        }

        private void OnLoadFailed(object sender, GameEventArgs args)
        {
            var data = (LoadSceneFailureEventArgs)args;
            if (IsCurrent(Generation) && ReferenceEquals(data.UserData, _request)) Fail(data.SceneAssetName + ": " + data.ErrorMessage);
        }

        private void OnUnloaded(object sender, GameEventArgs args)
        {
            var data = (UnloadSceneSuccessEventArgs)args;
            if (IsCurrent(Generation) && ReferenceEquals(data.UserData, _request)) _pendingUnloads.Remove(data.SceneAssetName);
        }

        private void OnUnloadFailed(object sender, GameEventArgs args)
        {
            var data = (UnloadSceneFailureEventArgs)args;
            if (IsCurrent(Generation) && ReferenceEquals(data.UserData, _request)) Fail("Cannot unload " + data.SceneAssetName);
        }

        private void Fail(string error)
        {
            GameServices.Host.ReportFailure("Scene transition: " + error);
            _events?.Dispose();
            _events = null;
        }

        protected override void OnLeave(ProcedureOwner owner, bool isShutdown)
        {
            base.OnLeave(owner, isShutdown);
            _events?.Dispose();
            _events = null;
            _request = null;
            _pendingUnloads.Clear();
        }
    }
}
