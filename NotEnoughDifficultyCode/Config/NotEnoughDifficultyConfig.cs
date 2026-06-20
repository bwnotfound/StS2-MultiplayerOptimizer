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
}