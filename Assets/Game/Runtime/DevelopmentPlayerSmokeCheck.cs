#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using Starter.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Game
{
    /// <summary>仅开发包中显式传 -starterSmokeTest 才运行，用真实资源包检查启动和界面交互。</summary>
    public sealed class DevelopmentPlayerSmokeCheck : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-starterSmokeTest") < 0) return;
            var probe = new GameObject("Development Smoke Check");
            DontDestroyOnLoad(probe);
            probe.AddComponent<DevelopmentPlayerSmokeCheck>();
        }

        private IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 40;
            while (!GameServices.IsBound || !GameServices.UI.HasUIForm(AssetNames.Menu))
            {
                if (Time.realtimeSinceStartup > deadline ||
                    (GameBootstrap.Current != null && !string.IsNullOrEmpty(GameBootstrap.Current.Failure)))
                {
                    Fail("Package startup failed or timed out.");
                    yield break;
                }
                yield return null;
            }
            if (!GameServices.Scene.SceneIsLoaded(AssetNames.MenuScene)) { Fail("Menu scene was not loaded through GF."); yield break; }
            GameServices.UI.GetUIForm(AssetNames.Menu).transform.Find("Card/PlayButton").GetComponent<Button>().onClick.Invoke();
            while (!GameServices.UI.HasUIForm(AssetNames.Play))
            {
                if (Time.realtimeSinceStartup > deadline || !string.IsNullOrEmpty(GameServices.Host.Failure))
                { Fail("Play scene/UI failed or timed out."); yield break; }
                yield return null;
            }
            if (!GameServices.Scene.SceneIsLoaded(AssetNames.PlayScene) || GameServices.Scene.SceneIsLoaded(AssetNames.MenuScene))
            { Fail("GF business scene transition failed."); yield break; }
            int before = GameSession.Wallet.Balance;
            GameServices.UI.GetUIForm(AssetNames.Play).transform.Find("Card/GrantButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            bool success = GameSession.Wallet.Balance == before + GameSession.Config.rewardPerClick &&
                GameServices.UI.GetUIForm(AssetNames.Play).transform.Find("Card/CoinsValue").GetComponent<Text>().text == "Coins: " + GameSession.Wallet.Balance;
            GameServices.Setting.SetInt(Services.WalletService.SaveKey, before);
            GameServices.Setting.Save();
            if (!success) { Fail("Wallet/UI event flow failed."); yield break; }
            Debug.Log("STARTER_PLAYER_SMOKE_PASSED: Package resources, full Procedure startup, GF scene transition, UI and event flow.");
            Application.Quit(0);
        }

        private static void Fail(string error)
        {
            Debug.LogError("STARTER_PLAYER_SMOKE_FAILED: " + error);
            Application.Quit(2);
        }
    }
}
#endif
