using System;
using System.Collections;
using GameFramework;
using GameFramework.Event;
using NUnit.Framework;
using Starter.Game;
using Starter.Game.Events;
using Starter.Game.Services;
using Starter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace Starter.Tests
{
    public sealed class BootFlowTests
    {
        [UnityTest]
        public IEnumerator BootUiEventsAndPooledReopenWorkTogether()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return WaitFor(() => GameServices.IsBound && GameServices.UI.HasUIForm(AssetNames.Menu));
            Assert.IsTrue(string.IsNullOrEmpty(GameServices.Host.Failure));
            Assert.IsTrue(SceneManager.GetSceneByPath("Assets/Game/Scenes/Menu.unity").isLoaded,
                "The startup chain must load the menu business scene through GF before showing its UI.");
            int originalBalance = GameSession.Wallet.Balance;
            var events = GameFrameworkEntry.GetModule<IEventManager>();
            try
            {
                Click(AssetNames.Menu, "PlayButton");
                yield return WaitFor(() => GameServices.UI.HasUIForm(AssetNames.Play));
                Assert.IsTrue(SceneManager.GetSceneByPath("Assets/Game/Scenes/Play.unity").isLoaded);
                Assert.IsFalse(SceneManager.GetSceneByPath("Assets/Game/Scenes/Menu.unity").isLoaded,
                    "Changing business scenes must release the previous scene.");
                Assert.IsTrue(SceneManager.GetSceneByName("Boot").isLoaded, "Boot must survive scene transitions.");
                Assert.AreEqual(1, events.Count(CoinsChangedEventArgs.EventId));
                Click(AssetNames.Play, "GrantButton");
                yield return null;
                yield return null;
                Assert.AreEqual(originalBalance + GameSession.Config.rewardPerClick, GameSession.Wallet.Balance);
                Assert.AreEqual("Coins: " + GameSession.Wallet.Balance,
                    GameServices.UI.GetUIForm(AssetNames.Play).transform.Find("Card/CoinsValue").GetComponent<Text>().text);
                Click(AssetNames.Play, "BackButton");
                yield return WaitFor(() => GameServices.UI.HasUIForm(AssetNames.Menu));
                Assert.IsFalse(SceneManager.GetSceneByPath("Assets/Game/Scenes/Play.unity").isLoaded);
                Assert.AreEqual(0, events.Count(CoinsChangedEventArgs.EventId), "Closed pooled forms must unsubscribe.");
                Click(AssetNames.Menu, "PlayButton");
                yield return WaitFor(() => GameServices.UI.HasUIForm(AssetNames.Play));
                Assert.AreEqual(1, events.Count(CoinsChangedEventArgs.EventId), "Reopening must not accumulate subscriptions.");
                Click(AssetNames.Play, "GrantButton");
                yield return null;
                yield return null;
                Assert.AreEqual(originalBalance + 2 * GameSession.Config.rewardPerClick, GameSession.Wallet.Balance,
                    "Reopening must not duplicate button listeners.");
                int callbackThread = -1;
                Assert.IsTrue(GameServices.Host.MainThread.TryPost(() => callbackThread = System.Threading.Thread.CurrentThread.ManagedThreadId));
                yield return null;
                yield return null;
                Assert.AreEqual(System.Threading.Thread.CurrentThread.ManagedThreadId, callbackThread);
            }
            finally
            {
                GameServices.Setting.SetInt(WalletService.SaveKey, originalBalance);
                GameServices.Setting.Save();
            }
        }

        [UnityTearDown]
        public IEnumerator Shutdown()
        {
            GameBootstrap host = GameBootstrap.Current;
            UnityGameFramework.Runtime.GameEntry.Shutdown(ShutdownType.None);
            if (host != null) UnityEngine.Object.Destroy(host.gameObject);
            yield return null;
        }

        private static void Click(string asset, string button)
        {
            GameServices.UI.GetUIForm(asset).transform.Find("Card/" + button).GetComponent<Button>().onClick.Invoke();
        }

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (!condition())
            {
                if (GameBootstrap.Current != null && !string.IsNullOrEmpty(GameBootstrap.Current.Failure))
                    Assert.Fail(GameBootstrap.Current.Failure);
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail("Startup/UI timed out. Host status: " +
                        (GameBootstrap.Current == null ? "missing" : GameBootstrap.Current.Status) +
                        "; services bound: " + GameServices.IsBound);
                yield return null;
            }
        }
    }
}
