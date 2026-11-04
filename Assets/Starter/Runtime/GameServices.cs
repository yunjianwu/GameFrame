using System;
using UnityGameFramework.Runtime;
using GfEntry = UnityGameFramework.Runtime.GameEntry;

namespace Starter.Runtime
{
    /// <summary>项目入口处集中取得 GF 组件。业务变大后可通过构造函数注入所需接口。</summary>
    public static class GameServices
    {
        public static bool IsBound { get; private set; }
        public static BaseComponent Base { get; private set; }
        public static EventComponent Event { get; private set; }
        public static ResourceComponent Resource { get; private set; }
        public static SettingComponent Setting { get; private set; }
        public static UIComponent UI { get; private set; }
        public static SceneComponent Scene { get; private set; }
        public static SoundComponent Sound { get; private set; }
        public static EntityComponent Entity { get; private set; }
        public static GameBootstrap Host => GameBootstrap.Current;

        public static void Bind()
        {
            if (IsBound) return;
            Base = Require<BaseComponent>();
            Event = Require<EventComponent>();
            Resource = Require<ResourceComponent>();
            Setting = Require<SettingComponent>();
            UI = Require<UIComponent>();
            Scene = Require<SceneComponent>();
            Sound = Require<SoundComponent>();
            Entity = Require<EntityComponent>();
            if (Host == null) throw new InvalidOperationException("GameBootstrap is missing from Boot scene.");
            IsBound = true;
        }

        public static void Unbind()
        {
            IsBound = false;
            Base = null;
            Event = null;
            Resource = null;
            Setting = null;
            UI = null;
            Scene = null;
            Sound = null;
            Entity = null;
        }

        private static T Require<T>() where T : GameFrameworkComponent
        {
            T component = GfEntry.GetComponent<T>();
            if (component == null) throw new InvalidOperationException("Missing GF component: " + typeof(T).FullName);
            return component;
        }
    }
}
