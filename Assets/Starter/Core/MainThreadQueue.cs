using System;
using System.Collections.Generic;
using System.Threading;

namespace Starter.Core
{
    /// <summary>后台只提交普通数据的处理回调；执行回调的 Pump 必须由创建队列的主线程调用。</summary>
    public sealed class MainThreadQueue : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Queue<Action> _pending = new Queue<Action>();
        private readonly int _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
        private readonly int _capacity;
        private bool _disposed;

        public MainThreadQueue(int capacity = 1024)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public int PendingCount { get { lock (_gate) return _pending.Count; } }

        /// <summary>队列已销毁或达到容量上限时返回 false，调用方应丢弃结果或自行重试。</summary>
        public bool TryPost(Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            lock (_gate)
            {
                if (_disposed || _pending.Count >= _capacity) return false;
                _pending.Enqueue(callback);
                return true;
            }
        }

        public int Pump(int maxActions, Action<Exception> onError = null)
        {
            if (Thread.CurrentThread.ManagedThreadId != _ownerThreadId)
                throw new InvalidOperationException("Only the owner thread may pump this queue.");
            if (maxActions < 1) throw new ArgumentOutOfRangeException(nameof(maxActions));

            int executed = 0;
            while (executed < maxActions)
            {
                Action callback;
                lock (_gate)
                {
                    if (_disposed || _pending.Count == 0) break;
                    callback = _pending.Dequeue();
                }

                // 不持锁执行用户回调，允许回调继续提交工作；每帧预算防止无限自提交。
                try { callback(); }
                catch (Exception exception)
                {
                    if (onError == null) throw;
                    onError(exception);
                }
                executed++;
            }
            return executed;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _pending.Clear();
            }
        }
    }
}
