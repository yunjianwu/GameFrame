using System;
using System.Collections.Generic;
using System.Threading;
using GameFramework.Event;

namespace Starter.Core
{
    /// <summary>界面打开时创建，关闭时释放；只取消本作用域拥有的 GF 订阅。限主线程操作。</summary>
    public sealed class EventScope : IDisposable
    {
        private readonly IEventManager _events;
        private readonly int _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
        private readonly List<KeyValuePair<int, EventHandler<GameEventArgs>>> _owned =
            new List<KeyValuePair<int, EventHandler<GameEventArgs>>>();
        private bool _disposed;

        public EventScope(IEventManager events)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public void Subscribe(int eventId, EventHandler<GameEventArgs> handler)
        {
            RequireOwnerThread();
            if (_disposed) throw new ObjectDisposedException(nameof(EventScope));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_events.Check(eventId, handler))
                throw new InvalidOperationException("This handler is already subscribed to this event.");

            _events.Subscribe(eventId, handler);
            _owned.Add(new KeyValuePair<int, EventHandler<GameEventArgs>>(eventId, handler));
        }

        public void Dispose()
        {
            RequireOwnerThread();
            if (_disposed) return;
            _disposed = true;
            for (int index = _owned.Count - 1; index >= 0; index--)
            {
                var subscription = _owned[index];
                if (_events.Check(subscription.Key, subscription.Value))
                    _events.Unsubscribe(subscription.Key, subscription.Value);
            }
            _owned.Clear();
        }

        private void RequireOwnerThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _ownerThreadId)
                throw new InvalidOperationException("Event subscriptions belong to their owner thread.");
        }
    }
}
