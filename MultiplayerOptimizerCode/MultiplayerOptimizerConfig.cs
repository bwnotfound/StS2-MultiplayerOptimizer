using BaseLib.Config;
using BaseLib.Utils;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
/// MultiplayerOptimizer 的 Mod 配置类。
/// 注册后会自动出现在游戏内的 Mod Configuration 菜单中。
/// </summary>
internal class MultiplayerOptimizerConfig : SimpleModConfig
{
    // 占位配置项 —— 仅用于让 Mod 在配置菜单中显示出条目
    // 实际开发时把它改成或新增联机优化相关的真实配置即可
    public static bool Enabled { get; set; } = true;

    // 后续可以按需扩展，例如：
    //
    // [ConfigSlider(0, 500, 10, Format = "{0} ms")]
    // public static int MaxLatencyBuffer { get; set; } = 100;
    //
    // public enum SyncMode { Conservative, Balanced, Aggressive }
    // public static SyncMode Mode { get; set; } = SyncMode.Balanced;
}