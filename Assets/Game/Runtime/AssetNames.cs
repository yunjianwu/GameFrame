namespace Starter.Game
{
    public static class AssetNames
    {
        public const string MenuScene = "Assets/Game/Scenes/Menu.unity";
        public const string PlayScene = "Assets/Game/Scenes/Play.unity";
        // GF 使用完整的 Assets 路径；不要和 Resources.Load 的相对路径混用。
        public const string Config = "Assets/Game/Content/Config/GameConfig.json";
        public const string Menu = "Assets/Game/Content/UI/MenuForm.prefab";
        public const string Play = "Assets/Game/Content/UI/PlayForm.prefab";
        public const string DefaultUIGroup = "Default";
    }
}
