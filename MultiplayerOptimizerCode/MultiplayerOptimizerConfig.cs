using BaseLib.Config;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
/// MultiplayerOptimizer 的 Mod 配置类。
///
/// 数值倍率分两层：
///   - 全局倍率（NormalEnemy{Hp|Dmg}MultStart/End 或 Boss{Hp|Dmg}Mult）：对该 act 所有怪物生效
///   - 来源倍率（NormalEnemySrc{Hp|Dmg}Mult_Act{1,2,3} 或 BossSrc{...}）：根据怪物原属 act 各自加倍
///   - 最终倍率 = 全局 × 来源
/// 例：Act4 全局 HP 倍率 1.4，act1 来源倍率 1.8 → act4 中遇到的 act1 来源敌人 HP × 1.4 × 1.8 = 2.52
///
/// 池权重在保存时会被自动归一化（参见 WeightNormalizationPatch）。手动改成 sum=0 时恢复默认。
/// </summary>
internal class MultiplayerOptimizerConfig : SimpleModConfig
{
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
}