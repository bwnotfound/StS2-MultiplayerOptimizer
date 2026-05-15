using BaseLib.Config;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
/// MultiplayerOptimizer 的 Mod 配置类。
///
/// ## 总开关
/// <see cref="Enabled"/> 是 mod 行为总开关。关闭后所有 patch 在入口都早返，
/// 等价于"mod 进入睡眠"——但 patch 仍然绑定在 base game 方法上，所以你能在主菜单切换
/// 而不必重启。注意：自定义 act 已经被 BaseLib 注册到了 ModelDb，关闭 Enabled 不会让
/// act4/5 从 act 列表里消失（会影响 multiplayer mod 校验），如果想完全卸载 mod 请在
/// mod 列表禁用本 mod。
///
/// ## 数值倍率分两层
///   - 全局倍率（NormalEnemy{Hp|Dmg}MultStart/End 或 Boss{Hp|Dmg}Mult）：对该 act 所有怪物生效
///   - 来源倍率（NormalEnemySrc{Hp|Dmg}Mult_Act{1,2,3} 或 BossSrc{...}）：根据怪物原属 act 各自加倍
///   - 最终倍率 = 全局 × 来源
/// 例：Act4 全局 HP 倍率 1.4，act1 来源倍率 1.8 → act4 中遇到的 act1 来源敌人 HP × 1.4 × 1.8 = 2.52
///
/// 池权重在保存时会被自动归一化（参见 WeightNormalizationPatch）。手动改成 sum=0 时恢复默认。
/// </summary>
internal class MultiplayerOptimizerConfig : SimpleModConfig
{
    // ============================================================
    // 总开关
    // ============================================================

    /// <summary>
    /// Mod 行为总开关。false 时所有自定义 act 相关 patch（数值倍率、池子混合、UI 修正等）
    /// 都跳过；patch 仍然存在但不工作。
    /// </summary>
    [ConfigSection("General")]
    public static bool Enabled { get; set; } = true;

    // ============================================================
    // 池子混合权重（保存时归一化到 sum=1；sum=0 时恢复默认）
    // ============================================================

    [ConfigSection("Act4_EncWeights")]
    [ConfigSlider(0, 1, 0.05)]
    public static double Act4_EncWeight_Act1 { get; set; } = 0.25;

    [ConfigSlider(0, 1, 0.05)] public static double Act4_EncWeight_Act2 { get; set; } = 0.35;

    [ConfigSlider(0, 1, 0.05)] public static double Act4_EncWeight_Act3 { get; set; } = 0.40;

    [ConfigSection("Act4_EventWeights")]
    [ConfigSlider(0, 1, 0.05)]
    public static double Act4_EventWeight_Act1 { get; set; } = 0.25;

    [ConfigSlider(0, 1, 0.05)] public static double Act4_EventWeight_Act2 { get; set; } = 0.35;

    [ConfigSlider(0, 1, 0.05)] public static double Act4_EventWeight_Act3 { get; set; } = 0.40;

    [ConfigSection("Act4_BossWeights")]
    [ConfigSlider(0, 1, 0.05)]
    public static double Act4_BossWeight_Act1 { get; set; } = 0.25;

    [ConfigSlider(0, 1, 0.05)] public static double Act4_BossWeight_Act2 { get; set; } = 0.35;

    [ConfigSlider(0, 1, 0.05)] public static double Act4_BossWeight_Act3 { get; set; } = 0.40;

    [ConfigSection("Act5_EventWeights")]
    [ConfigSlider(0, 1, 0.05)]
    public static double Act5_EventWeight_Act1 { get; set; } = 0.25;

    [ConfigSlider(0, 1, 0.05)] public static double Act5_EventWeight_Act2 { get; set; } = 0.35;

    [ConfigSlider(0, 1, 0.05)] public static double Act5_EventWeight_Act3 { get; set; } = 0.40;

    [ConfigSection("Act5_BossWeights")]
    [ConfigSlider(0, 1, 0.05)]
    public static double Act5_BossWeight_Act1 { get; set; } = 0.25;

    [ConfigSlider(0, 1, 0.05)] public static double Act5_BossWeight_Act2 { get; set; } = 0.35;

    [ConfigSlider(0, 1, 0.05)] public static double Act5_BossWeight_Act3 { get; set; } = 0.40;

    // ============================================================
    // 全局总倍率（叠加在所有其他倍率最末尾，用于快速调整后两层整体难度）
    // ============================================================
    //
    // 设计意图：当来源 act 倍率 + 普通/boss 倍率都已经平衡好，但想整体上调或下调后两层
    // 难度时，避免逐个调所有数值。直接改这两个就能对 act4/5 全局调难。
    //
    // 放在所有池权重之后、所有具体倍率之前——既符合"先池子后倍率"的阅读顺序，又让用户
    // 调整整体难度时不用翻到最底下。
    //
    // 默认 1.0 = 不影响。所有 HP / Dmg 计算最末尾乘上这两个值。
    // 范围给 0.1-5.0 足够覆盖"减弱到 10%"到"增强到 5 倍"的极端情况。

    [ConfigSection("Act4_OverallMultipliers")]
    [ConfigSlider(0.1, 5.0, 0.05)]
    public static double Act4_OverallHpMult { get; set; } = 1.0;

    [ConfigSlider(0.1, 5.0, 0.05)] public static double Act4_OverallDmgMult { get; set; } = 1.0;

    [ConfigSection("Act5_OverallMultipliers")]
    [ConfigSlider(0.1, 5.0, 0.05)]
    public static double Act5_OverallHpMult { get; set; } = 1.0;

    [ConfigSlider(0.1, 5.0, 0.05)] public static double Act5_OverallDmgMult { get; set; } = 1.0;

    // ============================================================
    // 全局倍率（普通敌人 = 起始 → 结束 按层内进度线性插值；boss = 单值）
    // ============================================================

    [ConfigSection("Act4_NormalEnemyMultipliers")]
    [ConfigSlider(0.5, 5.0, 0.05)]
    public static double Act4_NormalEnemyHpMultStart { get; set; } = 2;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_NormalEnemyHpMultEnd { get; set; } = 2.5;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_NormalEnemyDmgMultStart { get; set; } = 1.4;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_NormalEnemyDmgMultEnd { get; set; } = 1.6;

    [ConfigSection("Act4_BossMultipliers")]
    [ConfigSlider(0.5, 10.0, 0.05)]
    public static double Act4_BossHpMult { get; set; } = 5.0;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_BossDmgMult { get; set; } = 2;

    [ConfigSection("Act5_NormalEnemyMultipliers")]
    [ConfigSlider(0.5, 10.0, 0.05)]
    public static double Act5_NormalEnemyHpMultStart { get; set; } = 6;

    [ConfigSlider(0.5, 10.0, 0.05)] public static double Act5_NormalEnemyHpMultEnd { get; set; } = 8;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_NormalEnemyDmgMultStart { get; set; } = 1.9;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_NormalEnemyDmgMultEnd { get; set; } = 2.2;

    [ConfigSection("Act5_FinalBossMultipliers")]
    [ConfigSlider(0.5, 20.0, 0.1)]
    public static double Act5_FinalBossHpMult { get; set; } = 15;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_FinalBossDmgMult { get; set; } = 3;

    // ============================================================
    // 来源 act 倍率（叠加在全局倍率之上）
    // ============================================================

    [ConfigSection("Act4_NormalEnemySrcMultipliers")]
    [ConfigSlider(0.5, 5.0, 0.05)]
    public static double Act4_NormalEnemySrcHpMult_Act1 { get; set; } = 3;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_NormalEnemySrcHpMult_Act2 { get; set; } = 2;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_NormalEnemySrcHpMult_Act3 { get; set; } = 1;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_NormalEnemySrcDmgMult_Act1 { get; set; } = 1.7;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_NormalEnemySrcDmgMult_Act2 { get; set; } = 1.5;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_NormalEnemySrcDmgMult_Act3 { get; set; } = 1.0;

    [ConfigSection("Act4_BossSrcMultipliers")]
    [ConfigSlider(0.5, 5.0, 0.05)]
    public static double Act4_BossSrcHpMult_Act1 { get; set; } = 3;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_BossSrcHpMult_Act2 { get; set; } = 2;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_BossSrcHpMult_Act3 { get; set; } = 1.0;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_BossSrcDmgMult_Act1 { get; set; } = 1.7;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_BossSrcDmgMult_Act2 { get; set; } = 1.5;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act4_BossSrcDmgMult_Act3 { get; set; } = 1.0;

    [ConfigSection("Act5_NormalEnemySrcMultipliers")]
    [ConfigSlider(0.5, 5.0, 0.05)]
    public static double Act5_NormalEnemySrcHpMult_Act1 { get; set; } = 3;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_NormalEnemySrcHpMult_Act2 { get; set; } = 2;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_NormalEnemySrcHpMult_Act3 { get; set; } = 1.0;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_NormalEnemySrcDmgMult_Act1 { get; set; } = 1.7;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_NormalEnemySrcDmgMult_Act2 { get; set; } = 1.5;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_NormalEnemySrcDmgMult_Act3 { get; set; } = 1.0;

    [ConfigSection("Act5_FinalBossSrcMultipliers")]
    [ConfigSlider(0.5, 5.0, 0.05)]
    public static double Act5_FinalBossSrcHpMult_Act1 { get; set; } = 3;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_FinalBossSrcHpMult_Act2 { get; set; } = 2.0;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_FinalBossSrcHpMult_Act3 { get; set; } = 1.0;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_FinalBossSrcDmgMult_Act1 { get; set; } = 1.7;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_FinalBossSrcDmgMult_Act2 { get; set; } = 1.5;

    [ConfigSlider(0.5, 5.0, 0.05)] public static double Act5_FinalBossSrcDmgMult_Act3 { get; set; } = 1.0;

    // ============================================================
    // 行为开关
    // ============================================================

    [ConfigSection("BehaviorToggles")] public static bool Act5_ShowDisguisedBossWarning { get; set; } = true;

    public static bool Act5_AvoidFinalBossEqualPenultimate { get; set; } = true;

    /// <summary>
    /// 是否从第 4、5 层 boss 池中去除门扉缔造者（Doormaker）。
    ///
    /// 开启后影响三处：
    ///   1. Act4 顶部 boss 抽样（Act4Model.GenerateAllEncounters 构造的 boss 池）
    ///   2. Act5 中部所有战斗（CustomActEncounterReplacementPatch 用的 act1/2/3 boss 混合池）
    ///   3. Act5 顶部最终 boss（Act5Model.GenerateAllEncounters 复用的 Glory.AllEncounters）
    /// </summary>
    public static bool ExcludeDoormakerFromBossPool { get; set; } = false;
}