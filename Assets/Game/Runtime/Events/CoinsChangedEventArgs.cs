using GameFramework;
using GameFramework.Event;

namespace Starter.Game.Events
{
    public sealed class CoinsChangedEventArgs : GameEventArgs
    {
        // 稳定 ID 只用于进程内事件路由，不是网络协议号。
        public const int EventId = 10001;
        public override int Id => EventId;
        public int Balance { get; private set; }

        public static CoinsChangedEventArgs Create(int balance)
        {
            var args = ReferencePool.Acquire<CoinsChangedEventArgs>();
            args.Balance = balance;
            return args;
        }

        public override void Clear() { Balance = 0; }
    }
}
