using System;
using UnityEngine;

namespace Starter.Runtime.Startup
{
    [CreateAssetMenu(menuName = "Starter/Startup Settings")]
    public sealed class StartupSettings : ScriptableObject
    {
        [Tooltip("Only Updatable modes use this endpoint. Return the JSON documented in docs/procedures.md.")]
        public string VersionManifestUrl = "";
        [Min(1)] public int VersionRequestTimeoutSeconds = 15;
        [Range(1, 5)] public int VersionRequestAttempts = 3;
        [Range(0, 5)] public int ResourceUpdateRetryCount = 2;
        [Min(10)] public float OperationTimeoutSeconds = 120;
        public bool RequireDownloadConfirmation = true;
        [Tooltip("Empty updates all resources. Otherwise update these GF groups in order.")]
        public string[] RequiredResourceGroups = Array.Empty<string>();

        public void Validate(bool needsRemoteVersion)
        {
            if (VersionRequestTimeoutSeconds < 1 || VersionRequestAttempts < 1 || VersionRequestAttempts > 5 ||
                ResourceUpdateRetryCount < 0 || ResourceUpdateRetryCount > 5 ||
                float.IsNaN(OperationTimeoutSeconds) || float.IsInfinity(OperationTimeoutSeconds) || OperationTimeoutSeconds < 10)
                throw new ArgumentException("Startup timeout or retry settings are outside their supported ranges.");
            if (needsRemoteVersion && !VersionManifest.IsHttpUrl(VersionManifestUrl, out _))
                throw new ArgumentException("Updatable mode needs an absolute HTTP(S) VersionManifestUrl in StartupSettings.");
            if (RequiredResourceGroups != null)
                foreach (string group in RequiredResourceGroups)
                    if (string.IsNullOrWhiteSpace(group)) throw new ArgumentException("RequiredResourceGroups must not contain blank names. Use an empty array for all resources.");
        }
    }
}
