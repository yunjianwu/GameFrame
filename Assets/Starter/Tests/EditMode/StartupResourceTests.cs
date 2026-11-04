using System;
using System.Collections;
using NUnit.Framework;

namespace Starter.Tests
{
    public sealed class StartupResourceTests
    {
        public static IEnumerable Cases()
        {
            foreach (var test in StartupResourceRegressionSuite.Cases())
                yield return new TestCaseData(test.Value).SetName(test.Key);
        }

        [TestCaseSource(nameof(Cases))]
        public void ResourceStartupContracts(Action test) { test(); }
    }
}
