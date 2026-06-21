using BaseLib.Config;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
///     NotEnoughDifficulty 的 Mod 配置类。
///     ## 总开关
///     <see cref="Enabled" /> 是 mod 行为总开关。关闭后所有 patch 在入口都早返，
///     等价于"mod 进入睡眠"——但 patch 仍然绑定在 base game 方法上，所以你能在主菜单切换
///     而不必重启。注意：自定义 act 已经被 BaseLib 注册到了 ModelDb，关闭 Enabled 不会让
///     act4/5 从 act 列表里消失（会影响 multiplayer mod 校验），如果想完全卸载 mod 请在
///     mod 列表禁用本 mod。
///     ## 数值倍率分两层
///     - 全局倍率（NormalEnemy{Hp|Dmg}MultStart/End 或 Boss{Hp|Dmg}Mult）：对该 act 所有怪物生效
///     - 来源倍率（NormalEnemySrc{Hp|Dmg}Mult_Act{1,2,3} 或 BossSrc{...}）：根据怪物原属 act 各自加倍
///     - 总倍率（Overall{Hp|Dmg}Mult）：叠加在最末尾，用于快速调整整体难度
///     - 最终倍率 = 全局 × 来源 × 总倍率
///     池权重在保存时会被自动归一化（参见 WeightNormalizationPatch）。手动改成 sum=0 时恢复默认。
///     ## Partial 拆分
///     - .cs（本文件）：类头 + Enabled
///     - .Acts.cs: 数值倍率/池权重字段
///     - .Behaviors.cs: 行为开关
///     - .Speed.cs: 速度倍率（[ConfigSyncIgnore]）
///     ⚠️ 红线 1：所有字段名严格不变，旧 cfg 文件向后兼容。
/// </summary>
internal partial class NotEnoughDifficultyConfig : SimpleModConfig
{
    /// <summary>
    ///     Mod 行为总开关。false 时所有自定义 act 相关 patch（数值倍率、池子混合、UI 修正等）
    ///     都跳过；patch 仍然存在但不工作。
    /// </summary>
    [ConfigSection("General")]
    public static bool Enabled { get; set; } = true;

    // ============================================================
    // 难度预设（需求4）：一键设置第 4·5 层「整体倍率」(Overall{Hp|Dmg}Mult)。
    // Overall 倍率叠加在全局×来源倍率之后，是调整整体难度最直接的旋钮。
    // 数值口径可按平衡意图调整（这里给一组合理的简单/困难/极限三档）。
    // 预设只改 Overall 四个字段，不动其它细项，行为可预测。
    // ============================================================

    [ConfigSection("Presets")]
    [ConfigButton("PresetEasy")]
    public static void ApplyPresetEasy(ModConfig cfg) => ApplyPreset(cfg, hp: 0.8, dmg: 0.9);

    [ConfigButton("PresetHard")]
    public static void ApplyPresetHard(ModConfig cfg) => ApplyPreset(cfg, hp: 2, dmg: 1.4);

    [ConfigButton("PresetExtreme")]
    public static void ApplyPresetExtreme(ModConfig cfg) => ApplyPreset(cfg, hp: 5, dmg: 2);

    [ConfigButton("PresetImpossible")]
    public static void ApplyPresetImpossible(ModConfig cfg) => ApplyPreset(cfg, hp: 12, dmg: 3);

    private static void ApplyPreset(ModConfig cfg, double hp, double dmg)
    {
        Act4_OverallHpMult = hp;
        Act4_OverallDmgMult = dmg;
        Act5_OverallHpMult = hp;
        Act5_OverallDmgMult = dmg;

        // 落盘 + 通知 UI 重读（滑块等控件订阅了 OnConfigReloaded）
        cfg.Save();
        cfg.ConfigReloaded();
    }
}