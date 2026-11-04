using System;

namespace Starter.Core
{
    public sealed class Wallet
    {
        public Wallet(int initialBalance)
        {
            if (initialBalance < 0) throw new ArgumentOutOfRangeException(nameof(initialBalance));
            Balance = initialBalance;
        }

        public int Balance { get; private set; }

        public void Grant(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Balance = checked(Balance + amount);
        }
    }
}
