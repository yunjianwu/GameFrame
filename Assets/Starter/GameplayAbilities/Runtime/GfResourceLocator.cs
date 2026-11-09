using System;
using System.Threading;
using CycloneGames.GameplayAbilities.Runtime;
using Cysharp.Threading.Tasks;
using GameFramework.Resource;
using UnityEngine;
using UnityGameFramework.Runtime;
using Object = UnityEngine.Object;

namespace Starter.GameplayAbilities
{
    /// <summary>把技能表现资源的加载和释放交给 GF。</summary>
    public sealed class GfResourceLocator : IResourceLocator
    {
        private readonly Action<string, Type, LoadAssetCallbacks> _load;
        private readonly Action<object> _unload;
        private readonly SynchronizationContext _context;
        private readonly int _ownerThread;

        public GfResourceLocator(ResourceComponent resource)
            : this(GetLoader(resource), GetUnloader(resource))
        {
        }

        public GfResourceLocator(Action<string, Type, LoadAssetCallbacks> load, Action<object> unload)
        {
            _load = load ?? throw new ArgumentNullException(nameof(load));
            _unload = unload ?? throw new ArgumentNullException(nameof(unload));
            _context = SynchronizationContext.Current
                ?? throw new InvalidOperationException("请在 Unity 主线程创建 GF 资源适配器。");
            _ownerThread = Thread.CurrentThread.ManagedThreadId;
        }

        public UniTask<IResourceHandle<T>> LoadAssetAsync<T>(
            string key,
            string bucket = null,
            string cacheTag = null,
            string cacheOwner = null,
            CancellationToken cancellationToken = default) where T : Object
        {
            EnsureOwnerThread();
            if (string.IsNullOrWhiteSpace(key) ||
                !key.StartsWith("Assets/", StringComparison.Ordinal) || key.Length == "Assets/".Length)
            {
                throw new ArgumentException("GF 资源路径必须以 Assets/ 开头并包含资源名称。", nameof(key));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return UniTask.FromCanceled<IResourceHandle<T>>(cancellationToken);
            }

            var request = new Request<T>(this, key, cancellationToken);
            return request.Start();
        }

        private static Action<string, Type, LoadAssetCallbacks> GetLoader(ResourceComponent resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            return resource.LoadAsset;
        }

        private static Action<object> GetUnloader(ResourceComponent resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            return resource.UnloadAsset;
        }

        private void EnsureOwnerThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _ownerThread)
            {
                throw new InvalidOperationException("GF 资源必须在创建适配器的线程上操作。");
            }
        }

        private sealed class Handle<T> : IResourceHandle<T> where T : Object
        {
            private readonly GfResourceLocator _owner;
            private T _asset;

            public Handle(GfResourceLocator owner, T asset)
            {
                _owner = owner;
                _asset = asset;
            }

            public T Asset => _asset;

            public void Dispose()
            {
                _owner.EnsureOwnerThread();
                T asset = _asset;
                _asset = null;
                if (!ReferenceEquals(asset, null)) _owner._unload(asset);
            }
        }

        private sealed class Request<T> where T : Object
        {
            private readonly GfResourceLocator _owner;
            private readonly string _key;
            private readonly CancellationToken _token;
            private readonly UniTaskCompletionSource<IResourceHandle<T>> _completion =
                new UniTaskCompletionSource<IResourceHandle<T>>();
            private CancellationTokenRegistration _registration;

            public Request(GfResourceLocator owner, string key, CancellationToken token)
            {
                _owner = owner;
                _key = key;
                _token = token;
            }

            public UniTask<IResourceHandle<T>> Start()
            {
                if (_token.CanBeCanceled)
                {
                    _registration = _token.Register(OnCancellationRequested);
                }

                if (_completion.Task.Status != UniTaskStatus.Pending)
                {
                    _registration.Dispose();
                    return _completion.Task;
                }

                try
                {
                    _owner._load(_key, typeof(T), new LoadAssetCallbacks(OnLoaded, OnFailed));
                }
                catch (Exception exception)
                {
                    _registration.Dispose();
                    _completion.TrySetException(exception);
                }

                return _completion.Task;
            }

            private void OnCancellationRequested()
            {
                if (Thread.CurrentThread.ManagedThreadId == _owner._ownerThread)
                {
                    CancelOnOwnerThread();
                }
                else
                {
                    _owner._context.Post(_ => CancelOnOwnerThread(), null);
                }
            }

            private void CancelOnOwnerThread()
            {
                _owner.EnsureOwnerThread();
                _completion.TrySetCanceled(_token);
                _registration.Dispose();
            }

            private void OnLoaded(string assetName, object asset, float duration, object userData)
            {
                _owner.EnsureOwnerThread();
                _registration.Dispose();

                // GF 不提供通用加载取消接口，取消后到达的资源仍需交还 GF。
                if (_token.IsCancellationRequested || _completion.Task.Status != UniTaskStatus.Pending)
                {
                    _completion.TrySetCanceled(_token);
                    if (!ReferenceEquals(asset, null)) _owner._unload(asset);
                    return;
                }

                if (!(asset is T typedAsset) || typedAsset == null)
                {
                    if (!ReferenceEquals(asset, null)) _owner._unload(asset);
                    _completion.TrySetException(new InvalidOperationException($"GF 资源类型无效：{assetName}"));
                    return;
                }

                var handle = new Handle<T>(_owner, typedAsset);
                if (!_completion.TrySetResult(handle)) handle.Dispose();
            }

            private void OnFailed(string assetName, LoadResourceStatus status, string errorMessage, object userData)
            {
                _owner.EnsureOwnerThread();
                _registration.Dispose();
                if (_token.IsCancellationRequested)
                {
                    _completion.TrySetCanceled(_token);
                    return;
                }

                _completion.TrySetException(
                    new InvalidOperationException($"GF 资源加载失败：{assetName}，状态：{status}，原因：{errorMessage}"));
            }
        }
    }
}
