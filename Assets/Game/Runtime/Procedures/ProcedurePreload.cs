using System;
using GameFramework.Resource;
using Starter.Runtime;
using UnityEngine;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    public sealed class ProcedurePreload : ProcedureBase
    {
        private bool _ready;
        private float _elapsed;

        protected override void OnEnter(ProcedureOwner owner)
        {
            base.OnEnter(owner);
            _ready = false;
            _elapsed = 0;
            GameServices.Host.SetStatus("Loading local configuration...");
            LoadConfig(Generation);
        }

        private void LoadConfig(int generation)
        {
            var resource = GameServices.Resource;
            resource.LoadAsset(AssetNames.Config, typeof(TextAsset), new LoadAssetCallbacks(
                (name, asset, duration, data) =>
                {
                    // GF 没有通用的加载取消句柄；迟到结果仍必须释放。
                    try
                    {
                        if (!IsCurrent(generation)) return;
                        var config = JsonUtility.FromJson<GameConfig>(((TextAsset)asset).text);
                        if (config == null) throw new InvalidOperationException("GameConfig is empty.");
                        GameSession.Initialize(config);
                        _ready = true;
                    }
                    catch (Exception exception)
                    {
                        GameServices.Host.ReportFailure("Invalid configuration: " + exception.Message);
                    }
                    finally { resource.UnloadAsset(asset); }
                },
                (name, status, error, data) =>
                {
                    if (IsCurrent(generation)) GameServices.Host.ReportFailure("Cannot load " + name + ": " + error);
                }));
        }

        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            base.OnUpdate(owner, elapsed, realElapsed);
            if (!IsCurrent(Generation)) return;
            _elapsed += realElapsed;
            if (_ready) ChangeState<ProcedureEnterGame>(owner);
            else if (_elapsed > GameServices.Host.StartupSettings.OperationTimeoutSeconds)
                GameServices.Host.ReportFailure("Configuration preload timed out. Check the resource collection and startup resource groups.");
        }
    }
}
