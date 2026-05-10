using BaseLib.Config;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
/// MultiplayerOptimizer 的 Mod 配置类。
/// 注册后会自动出现在游戏内的 Mod Configuration 菜单中。
///
/// 注意：BaseLib config 的 UI 标签需要 localization 文件
/// （路径如 MultiplayerOptimizer/localization/en/settings_ui.json）。
/// 没有 localization 文件时 UI 显示 raw key（功能不影响），后续可以补。
/// </summary>
internal class MultiplayerOptimizerConfig : SimpleModConfig
{
    public static bool Enabled { get; set; } = true;

    // ============ 第 4 层 - 敌人池权重（用于 elite 战内容混合） ============
    [ConfigSection("Act4_EncWeights")]
    [ConfigSlider(0, 10, 0.1)]
    public static double Act4_EncWeight_Act1 { get; set; } = 0.2;

    [ConfigSlider(0, 10, 0.1)] public static double Act4_EncWeight_Act2 { get; set; } = 0.3;

    [ConfigSlider(0, 10, 0.1)] public static double Act4_EncWeight_Act3 { get; set; } = 0.5;

    // ============ 第 4 层 - 事件池权重 ============
    [ConfigSection("Act4_EventWeights")]
    [ConfigSlider(0, 10, 0.1)]
    public static double Act4_EventWeight_Act1 { get; set; } = 0.2;

    [ConfigSlider(0, 10, 0.1)] public static double Act4_EventWeight_Act2 { get; set; } = 0.3;

    [ConfigSlider(0, 10, 0.1)] public static double Act4_EventWeight_Act3 { get; set; } = 0.5;

    // ============ 第 4 层 - 顶端 boss 池权重 ============
    [ConfigSection("Act4_BossWeights")]
    [ConfigSlider(0, 10, 0.1)]
    public static double Act4_BossWeight_Act1 { get; set; } = 0.2;

    [ConfigSlider(0, 10, 0.1)] public static double Act4_BossWeight_Act2 { get; set; } = 0.3;

    [ConfigSlider(0, 10, 0.1)] public static double Act4_BossWeight_Act3 { get; set; } = 0.5;

    // ============ 第 5 层 - 事件池权重 ============
    [ConfigSection("Act5_EventWeights")]
    [ConfigSlider(0, 10, 0.1)]
    public static double Act5_EventWeight_Act1 { get; set; } = 0.2;

    [ConfigSlider(0, 10, 0.1)] public static double Act5_EventWeight_Act2 { get; set; } = 0.3;

    [ConfigSlider(0, 10, 0.1)] public static double Act5_EventWeight_Act3 { get; set; } = 0.5;

    // ============ 第 5 层 - 中部 boss 战内容池权重 ============
    // 注意：第 5 层最终 boss 不参与混合，必须从第 3 层 boss 池抽取（需求 5.3）
    [ConfigSection("Act5_BossWeights")]
    [ConfigSlider(0, 10, 0.1)]
    public static double Act5_BossWeight_Act1 { get; set; } = 0.1;

    [ConfigSlider(0, 10, 0.1)] public static double Act5_BossWeight_Act2 { get; set; } = 0.3;

    [ConfigSlider(0, 10, 0.1)] public static double Act5_BossWeight_Act3 { get; set; } = 0.6;
}