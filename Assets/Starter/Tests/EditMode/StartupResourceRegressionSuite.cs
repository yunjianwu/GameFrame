using System;
using System.Collections.Generic;
using Starter.Runtime.Startup;

namespace Starter.Tests
{
    public static class StartupResourceRegressionSuite
    {
        public static IEnumerable<KeyValuePair<string, Action>> Cases()
        {
            yield return Case("Resource download waits for confirmation", WaitsForConfirmation);
            yield return Case("Resource groups advance on the next tick", GroupsAdvanceOnTick);
            yield return Case("Failed resource group blocks startup", FailedGroupBlocksStartup);
            yield return Case("Late check callback cannot revive cancelled startup", LateCheckIsIgnored);
            yield return Case("Late update callback cannot advance cancelled startup", LateUpdateIsIgnored);
            yield return Case("Unready resources cannot report startup success", UnreadySuccessIsRejected);
            yield return Case("Empty group list updates all resources", EmptyGroupsUpdateAll);
            yield return Case("Ready resources skip download confirmation", ReadyResourcesSkipDownload);
            yield return Case("Retry progress replaces partial bytes", PartialProgressDoesNotAccumulate);
            yield return Case("Duplicate group names update only once", DuplicateGroupsAreNormalized);
            yield return Case("Manifest rejects invalid version and lengths", ManifestRejectsInvalidNumbers);
            yield return Case("Manifest rejects unusable CDN prefixes", ManifestRejectsUnusablePrefixes);
            yield return Case("Manifest accepts signed GF CRC values", ManifestAcceptsSignedHashes);
            yield return Case("Final resource failure ends startup without a group callback", FinalFailureDoesNotWaitForGroupCallback);
        }

        private static KeyValuePair<string, Action> Case(string name, Action test) => new KeyValuePair<string, Action>(name, test);

        private static void WaitsForConfirmation()
        {
            var client = new ControlledClient();
            var update = new ResourceUpdateCoordinator(client, new[] { "core", "game" }, true);
            update.Begin();
            client.CompleteCheck();
            update.Tick();
            Equal(ResourceUpdateState.AwaitingConfirmation, update.State);
            Equal(80L, update.TotalDownloadBytes);
            Equal(0, client.Requests.Count);
            update.Confirm();
            update.Tick();
            Equal("core", update.CurrentGroup);
            Equal("core", string.Join(",", client.Requests));
        }

        private static void GroupsAdvanceOnTick()
        {
            var client = new ControlledClient();
            var update = new ResourceUpdateCoordinator(client, new[] { "core", "game" }, false);
            update.Begin();
            client.CompleteCheck();
            update.Tick();
            client.CompleteUpdate(true);
            Equal("core", string.Join(",", client.Requests));
            update.Tick();
            Equal("core,game", string.Join(",", client.Requests));
            client.CompleteUpdate(true);
            update.Tick();
            Equal(ResourceUpdateState.Completed, update.State);
            Equal(80L, update.DownloadedBytes);
        }

        private static void FailedGroupBlocksStartup()
        {
            var client = new ControlledClient();
            var update = new ResourceUpdateCoordinator(client, new[] { "core", "game" }, false);
            update.Begin();
            client.CompleteCheck();
            update.Tick();
            client.CompleteUpdate(false);
            update.Tick();
            Equal(ResourceUpdateState.Failed, update.State);
            Check(update.Error.Contains("core"), "Failure must identify the group.");
            Equal("core", string.Join(",", client.Requests));
        }

        private static void LateCheckIsIgnored()
        {
            var client = new ControlledClient();
            var update = new ResourceUpdateCoordinator(client, new[] { "core" }, false);
            update.Begin();
            update.Cancel();
            client.CompleteCheck();
            update.Tick();
            Equal(ResourceUpdateState.Cancelled, update.State);
            Equal(0, client.Requests.Count);
        }

        private static void LateUpdateIsIgnored()
        {
            var client = new ControlledClient();
            var update = new ResourceUpdateCoordinator(client, new[] { "core", "game" }, false);
            update.Begin();
            client.CompleteCheck();
            update.Tick();
            update.Cancel();
            client.CompleteUpdate(true);
            update.Tick();
            Equal(ResourceUpdateState.Cancelled, update.State);
            Equal(1, client.Stops);
            Equal("core", string.Join(",", client.Requests));
        }

        private static void UnreadySuccessIsRejected()
        {
            var client = new ControlledClient { MarkReadyOnSuccess = false };
            var update = new ResourceUpdateCoordinator(client, new[] { "core" }, false);
            update.Begin();
            client.CompleteCheck();
            update.Tick();
            client.CompleteUpdate(true);
            update.Tick();
            Equal(ResourceUpdateState.Failed, update.State);
        }

        private static void EmptyGroupsUpdateAll()
        {
            var client = new ControlledClient();
            var update = new ResourceUpdateCoordinator(client, Array.Empty<string>(), false);
            update.Begin();
            client.CompleteCheck();
            update.Tick();
            Equal(1, client.Requests.Count);
            Equal(string.Empty, client.Requests[0]);
            client.CompleteUpdate(true);
            update.Tick();
            Equal(ResourceUpdateState.Completed, update.State);
        }

        private static void ReadyResourcesSkipDownload()
        {
            var client = new ControlledClient { CoreReady = true, GameReady = true };
            var update = new ResourceUpdateCoordinator(client, new[] { "core", "game" }, true);
            update.Begin();
            client.CompleteCheck();
            update.Tick();
            Equal(ResourceUpdateState.Completed, update.State);
            Equal(0, client.Requests.Count);
        }

        private static void PartialProgressDoesNotAccumulate()
        {
            var client = new ControlledClient();
            var update = new ResourceUpdateCoordinator(client, new[] { "core", "game" }, false);
            update.Begin();
            client.CompleteCheck();
            update.Tick();
            update.ReportPartial("bundle", 10);
            update.Tick();
            Equal(10L, update.DownloadedBytes);
            update.ReportPartial("bundle", 4);
            update.Tick();
            Equal(4L, update.DownloadedBytes);
            update.ForgetPartial("bundle");
            client.CompleteUpdate(true);
            update.Tick();
            Equal(30L, update.DownloadedBytes);
        }

        private static void DuplicateGroupsAreNormalized()
        {
            var client = new ControlledClient();
            var update = new ResourceUpdateCoordinator(client, new[] { "core", "core" }, false);
            update.Begin();
            client.CompleteCheck();
            update.Tick();
            client.CompleteUpdate(true);
            update.Tick();
            Equal(ResourceUpdateState.Completed, update.State);
            Equal("core", string.Join(",", client.Requests));
        }

        private static VersionManifest ValidManifest() => new VersionManifest
        {
            internalResourceVersion = 7,
            updatePrefixUri = "https://cdn.example.com/game/Windows64/",
            versionListLength = 128,
            versionListHashCode = -123,
            versionListCompressedLength = 64,
            versionListCompressedHashCode = -456
        };

        private static void ManifestRejectsInvalidNumbers()
        {
            var version = ValidManifest();
            version.internalResourceVersion = 0;
            Rejects(version.Validate);
            version = ValidManifest();
            version.versionListLength = 0;
            Rejects(version.Validate);
            version = ValidManifest();
            version.versionListCompressedLength = -1;
            Rejects(version.Validate);
        }

        private static void ManifestRejectsUnusablePrefixes()
        {
            foreach (string prefix in new[] { "", "relative/path", "file:///D:/test", "https://cdn.example.com/path?version=7", "https://cdn.example.com/#fragment" })
            {
                var version = ValidManifest();
                version.updatePrefixUri = prefix;
                Rejects(version.Validate);
            }
        }

        private static void ManifestAcceptsSignedHashes() { ValidManifest().Validate(); }

        private static void FinalFailureDoesNotWaitForGroupCallback()
        {
            var client = new ControlledClient();
            var update = new ResourceUpdateCoordinator(client, new[] { "core", "game" }, false);
            update.Begin();
            client.CompleteCheck();
            update.Tick();
            update.ReportFailure("missing-bundle", 0, 2, "HTTP 404");
            update.Tick();
            Equal(ResourceUpdateState.Updating, update.State);
            // GF v2021.05.31 may never complete the group when its last download fails.
            update.ReportFailure("missing-bundle", 2, 2, "HTTP 404");
            Equal(ResourceUpdateState.Updating, update.State);
            update.Tick();
            Equal(ResourceUpdateState.Failed, update.State);
            Check(update.Error.Contains("missing-bundle") && update.Error.Contains("HTTP 404"), "Preserve the actual terminal error.");
            Equal(1, client.Stops);
            client.CompleteUpdate(true);
            update.Tick();
            Equal(ResourceUpdateState.Failed, update.State);
            Equal("core", string.Join(",", client.Requests));
        }

        private static void Rejects(Action action)
        {
            try { action(); }
            catch (ArgumentException) { return; }
            throw new InvalidOperationException("Invalid manifest must raise ArgumentException.");
        }

        private static void Check(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("Expected " + expected + ", got " + actual);
        }

        // The external IO boundary is controlled; coordinator decisions and progress stay real.
        private sealed class ControlledClient : IResourceUpdateClient
        {
            public bool CoreReady;
            public bool GameReady;
            public bool MarkReadyOnSuccess = true;
            public int Stops;
            public readonly List<string> Requests = new List<string>();
            private Action _checked;
            private Action<bool> _updated;
            private string _current;

            public void Check(Action completed) { _checked = completed; }
            public void CompleteCheck() { _checked(); }
            public bool IsReady(string group)
            {
                if (group == string.Empty) return CoreReady && GameReady;
                if (group == "core") return CoreReady;
                if (group == "game") return GameReady;
                throw new InvalidOperationException("Unknown group: " + group);
            }

            public ResourceUpdateProgress GetProgress(string[] groups)
            {
                bool core = Array.IndexOf(groups, "core") >= 0 || Array.IndexOf(groups, string.Empty) >= 0;
                bool game = Array.IndexOf(groups, "game") >= 0 || Array.IndexOf(groups, string.Empty) >= 0;
                long total = (core ? 50 : 0) + (game ? 50 : 0);
                long ready = (core ? (CoreReady ? 50 : 20) : 0) + (game && GameReady ? 50 : 0);
                return new ResourceUpdateProgress(total, ready, (!core || CoreReady) && (!game || GameReady));
            }

            public void Update(string group, Action<bool> completed)
            {
                Requests.Add(group);
                _current = group;
                _updated = completed;
            }

            public void CompleteUpdate(bool success)
            {
                if (success && MarkReadyOnSuccess)
                {
                    if (_current == string.Empty || _current == "core") CoreReady = true;
                    if (_current == string.Empty || _current == "game") GameReady = true;
                }
                _updated(success);
            }

            public void Stop() { Stops++; }
        }
    }
}
