using System;
using System.Collections;
using NUnit.Framework;

namespace Starter.Tests
{
    public sealed class CoreTests
    {
        public static IEnumerable Cases()
        {
            foreach (var test in CoreRegressionSuite.Cases())
                yield return new TestCaseData(test.Value).SetName(test.Key);
        }

        [TestCaseSource(nameof(Cases))]
        public void ReusableContracts(Action test) { test(); }
    }
}
