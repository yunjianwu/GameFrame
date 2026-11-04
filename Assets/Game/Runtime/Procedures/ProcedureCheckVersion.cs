using System;
using System.Collections;
using GameFramework.Resource;
using Starter.Runtime;
using Starter.Runtime.Startup;
using UnityEngine;
using UnityEngine.Networking;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    public sealed class ProcedureCheckVersion : ProcedureBase
    {
        private Coroutine _routine;
        private UnityWebRequest _request;
        private bool _ready;
        private float _elapsed;

        protected override void OnEnter(ProcedureOwner owner)
        {
            base.OnEnter(owner);
            _ready = false;
            _elapsed = 0;
            GameServices.Host.SetStatus("Checking resource version...");
            if (GameServices.Base.EditorResourceMode || GameServices.Resource.ResourceMode == ResourceMode.Package)
                _ready = true;
            else _routine = GameServices.Host.StartCoroutine(FetchManifest(Generation));
        }

        private IEnumerator FetchManifest(int generation)
        {
            StartupSettings settings = GameServices.Host.StartupSettings;
            for (int attempt = 1; attempt <= settings.VersionRequestAttempts; attempt++)
            {
                _request = UnityWebRequest.Get(settings.VersionManifestUrl);
                _request.timeout = settings.VersionRequestTimeoutSeconds;
                yield return _request.SendWebRequest();
                if (!IsCurrent(generation)) yield break;
                string error = _request.result == UnityWebRequest.Result.Success ? null : _request.error;
                string json = error == null ? _request.downloadHandler.text : null;
                _request.Dispose();
                _request = null;
                if (error == null)
                {
                    ApplyManifest(json, generation);
                    yield break;
                }
                if (attempt == settings.VersionRequestAttempts)
                {
                    GameServices.Host.ReportFailure("Version request failed after " + attempt + " attempts: " + error);
                    yield break;
                }
                GameServices.Host.SetStatus("Retrying version request (" + (attempt + 1) + ")...");
                yield return new WaitForSecondsRealtime(attempt);
                if (!IsCurrent(generation)) yield break;
            }
        }

        private void ApplyManifest(string json, int generation)
        {
            try
            {
                VersionManifest manifest = JsonUtility.FromJson<VersionManifest>(json);
                if (manifest == null) throw new ArgumentException("Empty version manifest.");
                manifest.Validate();
                GameServices.Resource.UpdatePrefixUri = manifest.updatePrefixUri.TrimEnd('/');
                GameServices.Resource.UpdateRetryCount = GameServices.Host.StartupSettings.ResourceUpdateRetryCount;
                if (GameServices.Resource.CheckVersionList(manifest.internalResourceVersion) == CheckVersionListResult.Updated)
                { _ready = true; return; }
                GameServices.Host.SetStatus("Downloading GF version list...");
                GameServices.Resource.UpdateVersionList(manifest.versionListLength, manifest.versionListHashCode,
                    manifest.versionListCompressedLength, manifest.versionListCompressedHashCode,
                    new UpdateVersionListCallbacks(
                        (path, uri) => { if (IsCurrent(generation)) _ready = true; },
                        (uri, error) => { if (IsCurrent(generation)) GameServices.Host.ReportFailure("GF version list failed: " + error); }));
            }
            catch (Exception exception) { GameServices.Host.ReportFailure("Invalid version manifest or GF version list: " + exception.Message); }
        }

        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            base.OnUpdate(owner, elapsed, realElapsed);
            if (!IsCurrent(Generation)) return;
            _elapsed += realElapsed;
            if (_ready) ChangeState<ProcedureUpdateResource>(owner);
            else if (_elapsed > GameServices.Host.StartupSettings.OperationTimeoutSeconds)
            {
                ReleaseRequest();
                GameServices.Host.ReportFailure("Version check timed out.");
            }
        }

        private void ReleaseRequest()
        {
            if (_routine != null && GameServices.Host != null) GameServices.Host.StopCoroutine(_routine);
            _routine = null;
            if (_request == null) return;
            _request.Abort();
            _request.Dispose();
            _request = null;
        }

        protected override void OnLeave(ProcedureOwner owner, bool isShutdown)
        {
            base.OnLeave(owner, isShutdown);
            ReleaseRequest();
        }
    }
}
