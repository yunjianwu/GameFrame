using System;
using System.Linq;
using Starter.Tests;

internal static class Program
{
    private static int Main()
    {
        int passed = 0;
        int failed = 0;
        foreach (var test in CoreRegressionSuite.Cases().Concat(StartupResourceRegressionSuite.Cases()))
        {
            try { test.Value(); Console.WriteLine("PASS " + test.Key); passed++; }
            catch (Exception exception) { Console.WriteLine("FAIL " + test.Key + ": " + exception.Message); failed++; }
        }
        Console.WriteLine("RESULT: " + passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
