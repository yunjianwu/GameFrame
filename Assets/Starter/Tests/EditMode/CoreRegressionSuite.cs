using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameFramework;
using GameFramework.Event;
using Starter.Core;
using UnityGameFramework.Runtime;

namespace Starter.Tests
{
    // Shared by the Unity tests and the dependency-free command-line runner.
    public static class CoreRegressionSuite
    {
        public static IEnumerable<KeyValuePair<string, Action>> Cases()
        {
            yield return Case("Queue waits for the main-thread pump", QueueWaitsForPump);
            yield return Case("Worker results execute on the owner thread", WorkerResultsRunOnOwner);
            yield return Case("Pump budget preserves FIFO order", PumpBudgetPreservesOrder);
            yield return Case("Worker cannot pump the queue", WorkerCannotPump);
            yield return Case("Full queue rejects additional work", FullQueueRejectsWork);
            yield return Case("Disposed queue discards pending work", DisposedQueueDiscardsWork);
            yield return Case("One failed callback does not block later work", CallbackErrorsAreIsolated);
            yield return Case("Reentrant posting still obeys the frame budget", ReentrantPostingObeysBudget);
            yield return Case("Event scope releases only owned subscriptions", ScopeReleasesOwnedSubscriptions);
            yield return Case("Duplicate event subscriptions are rejected", ScopeRejectsDuplicateSubscriptions);
            yield return Case("Disposed scope cannot subscribe", DisposedScopeCannotSubscribe);
            yield return Case("Wallet rejects negative balances and invalid grants", WalletRejectsInvalidAmounts);
            yield return Case("Wallet overflow leaves balance unchanged", WalletOverflowIsAtomic);
            yield return Case("Gzip round-trip respects the requested byte range", GzipRoundTripUsesRange);
            yield return Case("Gzip helper leaves caller streams open", GzipLeavesCallerStreamsOpen);
            yield return Case("Gzip helper rejects invalid offset and length", GzipRejectsInvalidRanges);
        }

        private static KeyValuePair<string, Action> Case(string name, Action test)
        {
            return new KeyValuePair<string, Action>(name, test);
        }

        private static void QueueWaitsForPump()
        {
            using (var queue = new MainThreadQueue())
            {
                int calls = 0;
                Check(queue.TryPost(() => calls++), "Post should be accepted.");
                Equal(0, calls);
                Equal(1, queue.Pump(4));
                Equal(1, calls);
            }
        }

        private static void WorkerResultsRunOnOwner()
        {
            using (var queue = new MainThreadQueue())
            {
                int owner = Thread.CurrentThread.ManagedThreadId;
                int executedOn = -1;
                bool accepted = Task.Run(() => queue.TryPost(() => executedOn = Thread.CurrentThread.ManagedThreadId)).GetAwaiter().GetResult();
                Check(accepted, "Worker post should be accepted.");
                Equal(-1, executedOn);
                queue.Pump(4);
                Equal(owner, executedOn);
            }
        }

        private static void PumpBudgetPreservesOrder()
        {
            using (var queue = new MainThreadQueue())
            {
                var results = new List<int>();
                queue.TryPost(() => results.Add(1));
                queue.TryPost(() => results.Add(2));
                queue.TryPost(() => results.Add(3));
                Equal(2, queue.Pump(2));
                Equal("1,2", string.Join(",", results));
                Equal(1, queue.Pump(2));
                Equal("1,2,3", string.Join(",", results));
            }
        }

        private static void WorkerCannotPump()
        {
            using (var queue = new MainThreadQueue())
            {
                Task.Run(() => Throws<InvalidOperationException>(() => queue.Pump(1))).GetAwaiter().GetResult();
            }
        }

        private static void FullQueueRejectsWork()
        {
            using (var queue = new MainThreadQueue(1))
            {
                Check(queue.TryPost(() => { }), "First post should fit.");
                Check(!queue.TryPost(() => { }), "Overflow must be rejected.");
                Equal(1, queue.PendingCount);
            }
        }

        private static void DisposedQueueDiscardsWork()
        {
            var queue = new MainThreadQueue();
            bool invoked = false;
            queue.TryPost(() => invoked = true);
            queue.Dispose();
            queue.Dispose();
            Check(!queue.TryPost(() => { }), "Disposed queue must reject posts.");
            Equal(0, queue.Pump(4));
            Check(!invoked, "Discarded work must never execute.");
        }

        private static void CallbackErrorsAreIsolated()
        {
            using (var queue = new MainThreadQueue())
            {
                int errors = 0;
                int calls = 0;
                queue.TryPost(() => throw new InvalidOperationException("Expected regression fixture."));
                queue.TryPost(() => calls++);
                Equal(2, queue.Pump(4, exception => errors++));
                Equal(1, errors);
                Equal(1, calls);
            }
        }

        private static void ReentrantPostingObeysBudget()
        {
            using (var queue = new MainThreadQueue())
            {
                queue.TryPost(() => queue.TryPost(() => { }));
                Equal(1, queue.Pump(1));
                Equal(1, queue.PendingCount);
            }
        }

        private static void ScopeReleasesOwnedSubscriptions()
        {
            IEventManager events = GameFrameworkEntry.GetModule<IEventManager>();
            int ownedCalls = 0;
            int externalCalls = 0;
            EventHandler<GameEventArgs> external = (sender, args) => externalCalls++;
            events.Subscribe(TestEvent.EventId, external);
            try
            {
                var scope = new EventScope(events);
                scope.Subscribe(TestEvent.EventId, (sender, args) => ownedCalls++);
                events.FireNow(null, ReferencePool.Acquire<TestEvent>());
                scope.Dispose();
                scope.Dispose();
                events.FireNow(null, ReferencePool.Acquire<TestEvent>());
                Equal(1, ownedCalls);
                Equal(2, externalCalls);
            }
            finally
            {
                events.Unsubscribe(TestEvent.EventId, external);
            }
        }

        private static void ScopeRejectsDuplicateSubscriptions()
        {
            using (var scope = new EventScope(GameFrameworkEntry.GetModule<IEventManager>()))
            {
                EventHandler<GameEventArgs> handler = (sender, args) => { };
                scope.Subscribe(TestEvent.EventId, handler);
                Throws<InvalidOperationException>(() => scope.Subscribe(TestEvent.EventId, handler));
            }
        }

        private static void DisposedScopeCannotSubscribe()
        {
            var scope = new EventScope(GameFrameworkEntry.GetModule<IEventManager>());
            scope.Dispose();
            Throws<ObjectDisposedException>(() => scope.Subscribe(TestEvent.EventId, (sender, args) => { }));
        }

        private static void WalletRejectsInvalidAmounts()
        {
            Throws<ArgumentOutOfRangeException>(() => new Wallet(-1));
            var wallet = new Wallet(10);
            Throws<ArgumentOutOfRangeException>(() => wallet.Grant(0));
            Throws<ArgumentOutOfRangeException>(() => wallet.Grant(-1));
            wallet.Grant(5);
            Equal(15, wallet.Balance);
        }

        private static void WalletOverflowIsAtomic()
        {
            var wallet = new Wallet(int.MaxValue);
            Throws<OverflowException>(() => wallet.Grant(1));
            Equal(int.MaxValue, wallet.Balance);
        }

        private static void GzipRoundTripUsesRange()
        {
            var helper = new DefaultCompressionHelper();
            byte[] source = Encoding.UTF8.GetBytes("XXhello worldYY");
            using (var compressed = new MemoryStream())
            using (var restored = new MemoryStream())
            {
                Check(helper.Compress(source, 2, 11, compressed), "Compression should succeed.");
                byte[] bytes = compressed.ToArray();
                Check(helper.Decompress(bytes, 0, bytes.Length, restored), "Decompression should succeed.");
                Equal("hello world", Encoding.UTF8.GetString(restored.ToArray()));
            }
        }

        private static void GzipLeavesCallerStreamsOpen()
        {
            var helper = new DefaultCompressionHelper();
            using (var source = new MemoryStream(Encoding.UTF8.GetBytes("payload")))
            using (var compressed = new MemoryStream())
            using (var restored = new MemoryStream())
            {
                Check(helper.Compress(source, compressed), "Compression should succeed.");
                Check(source.CanRead && compressed.CanWrite, "Caller owns both streams.");
                compressed.Position = 0;
                Check(helper.Decompress(compressed, restored), "Decompression should succeed.");
                Check(compressed.CanRead && restored.CanWrite, "Caller streams must stay open.");
                Equal("payload", Encoding.UTF8.GetString(restored.ToArray()));
            }
        }

        private static void GzipRejectsInvalidRanges()
        {
            var helper = new DefaultCompressionHelper();
            using (var output = new MemoryStream())
            {
                Check(!helper.Compress(new byte[2], int.MaxValue, 10, output), "Overflowing range must fail.");
                Check(!helper.Compress(new byte[2], -1, 1, output), "Negative offset must fail.");
                Check(!helper.Compress(null, 0, 0, output), "Null input must fail.");
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("Expected " + expected + ", got " + actual + ".");
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected exception: " + typeof(T).Name);
        }

        public sealed class TestEvent : GameEventArgs
        {
            public const int EventId = 170001;
            public override int Id => EventId;
            public override void Clear() { }
        }
    }
}
