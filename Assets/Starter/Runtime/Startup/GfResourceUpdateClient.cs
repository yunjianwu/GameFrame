using System;
using GameFramework.Resource;
using UnityGameFramework.Runtime;

namespace Starter.Runtime.Startup
{
    /// <summary>Uses only official GF APIs; the coordinator owns ordering and the Procedure owns its lifetime.</summary>
    public sealed class GfResourceUpdateClient : IResourceUpdateClient
    {
        private readonly ResourceComponent _resource;
        private IResourceGroupCollection _selection;

        public GfResourceUpdateClient(ResourceComponent resource) { _resource = resource ?? throw new ArgumentNullException(nameof(resource)); }

        public void Check(Action completed)
        {
            _resource.CheckResources((moved, removed, count, length, compressed) => completed());
        }

        public bool IsReady(string group) { return RequireGroup(group).Ready; }

        public ResourceUpdateProgress GetProgress(string[] groups)
        {
            if (groups.Length == 1 && groups[0] == string.Empty)
            {
                IResourceGroup all = RequireGroup(string.Empty);
                return new ResourceUpdateProgress(all.TotalCompressedLength, all.ReadyCompressedLength, all.Ready);
            }
            // ResourceGroupCollection deduplicates bundles shared by multiple groups. Cache it for this run.
            if (_selection == null) _selection = _resource.GetResourceGroupCollection(groups);
            return new ResourceUpdateProgress(_selection.TotalCompressedLength, _selection.ReadyCompressedLength, _selection.Ready);
        }

        public void Update(string group, Action<bool> completed)
        {
            if (string.IsNullOrEmpty(group)) _resource.UpdateResources((selection, success) => completed(success));
            else _resource.UpdateResources(group, (selection, success) => completed(success));
        }

        public void Stop()
        {
            // GF throws when StopUpdateResources is called with no active group.
            if (_resource.UpdatingResourceGroup != null) _resource.StopUpdateResources();
        }

        private IResourceGroup RequireGroup(string name)
        {
            IResourceGroup group = string.IsNullOrEmpty(name) ? _resource.GetResourceGroup() : _resource.GetResourceGroup(name);
            if (group == null) throw new InvalidOperationException("Unknown GF resource group: " + name);
            return group;
        }
    }
}
