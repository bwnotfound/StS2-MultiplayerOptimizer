using BaseLib.Config;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
///     Partial: act4/5 的数值倍率和池权重字段。
///     字段定义保持跟重构前完全一致——不改任何字段名（红线 1）、初始值或类型，
///     否则旧 cfg 文件读不到 / 行为变更。
/// </summary>
internal partial class NotEnoughDifficultyConfig
{
    // ============================================================
    // 折叠分组总开关（需求3）：默认关闭。本开关作为「第4·5层详细配置」大组的 section 头，
    // 始终可见；本文件下方所有 act4/5 权重/倍率字段都带 [ConfigVisibleIf(nameof(ShowAct4Act5Details))]，
    // 关闭时它们（及各自子 section 标题）自动隐藏，等价「一个大折叠组」，避免一进页面信息过载。
    // 放在 .Acts.cs 顶部是为了让「展开点」紧贴它所控制的内容，而不是浮在配置页最上方。
    // ============================================================

    [ConfigSection("Act4Act5Scaling")] public static bool ShowAct4Act5Details { get; set; } = false;

    // ============================================================
    // 池子混合权重（保存时归一化到 sum=1；sum=0 时恢复默认）
    // ============================================================

    [ConfigSection("Act4_EncWeights")]
    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_EncWeight_Act1 { get; set; } = 0.25;

    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_EncWeight_Act2 { get; set; } = 0.35;

    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_EncWeight_Act3 { get; set; } = 0.40;

    [ConfigSection("Act4_EventWeights")]
    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_EventWeight_Act1 { get; set; } = 0.25;

    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_EventWeight_Act2 { get; set; } = 0.35;

    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_EventWeight_Act3 { get; set; } = 0.40;

    [ConfigSection("Act4_BossWeights")]
    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_BossWeight_Act1 { get; set; } = 0.25;

    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_BossWeight_Act2 { get; set; } = 0.35;

    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_BossWeight_Act3 { get; set; } = 0.40;

    [ConfigSection("Act5_EventWeights")]
    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_EventWeight_Act1 { get; set; } = 0.25;

    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_EventWeight_Act2 { get; set; } = 0.35;

    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_EventWeight_Act3 { get; set; } = 0.40;

    [ConfigSection("Act5_BossWeights")]
    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_BossWeight_Act1 { get; set; } = 0.25;

    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_BossWeight_Act2 { get; set; } = 0.35;

    [ConfigSlider(0, 1, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_BossWeight_Act3 { get; set; } = 0.40;

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
    // 范围给 0.1-100 足够覆盖"减弱到 10%"到"增强到 100 倍"的极端情况。

    [ConfigSection("Act4_OverallMultipliers")]
    [ConfigSlider(0.1, 100, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_OverallHpMult { get; set; } = 1.0;

    [ConfigSlider(0.1, 100, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_OverallDmgMult { get; set; } = 1.0;

    [ConfigSection("Act5_OverallMultipliers")]
    [ConfigSlider(0.1, 100, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_OverallHpMult { get; set; } = 1.0;

    [ConfigSlider(0.1, 100, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_OverallDmgMult { get; set; } = 1.0;

    // ============================================================
    // 全局倍率（普通敌人 = 起始 → 结束 按层内进度线性插值；boss = 单值）
    // ============================================================

    [ConfigSection("Act4_NormalEnemyMultipliers")]
    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_NormalEnemyHpMultStart { get; set; } = 2;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_NormalEnemyHpMultEnd { get; set; } = 2.5;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_NormalEnemyDmgMultStart { get; set; } = 1.4;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_NormalEnemyDmgMultEnd { get; set; } = 1.6;

    [ConfigSection("Act4_BossMultipliers")]
    [ConfigSlider(0.5, 10.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_BossHpMult { get; set; } = 5.0;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_BossDmgMult { get; set; } = 2;

    [ConfigSection("Act5_NormalEnemyMultipliers")]
    [ConfigSlider(0.5, 10.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_NormalEnemyHpMultStart { get; set; } = 6;

    [ConfigSlider(0.5, 10.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_NormalEnemyHpMultEnd { get; set; } = 8;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_NormalEnemyDmgMultStart { get; set; } = 1.9;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_NormalEnemyDmgMultEnd { get; set; } = 2.2;

    [ConfigSection("Act5_FinalBossMultipliers")]
    [ConfigSlider(0.5, 20.0, 0.1)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_FinalBossHpMult { get; set; } = 15;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_FinalBossDmgMult { get; set; } = 3;

    // ============================================================
    // 来源 act 倍率（叠加在全局倍率之上）
    // ============================================================

    [ConfigSection("Act4_NormalEnemySrcMultipliers")]
    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_NormalEnemySrcHpMult_Act1 { get; set; } = 3;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_NormalEnemySrcHpMult_Act2 { get; set; } = 2;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_NormalEnemySrcHpMult_Act3 { get; set; } = 1;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_NormalEnemySrcDmgMult_Act1 { get; set; } = 1.7;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_NormalEnemySrcDmgMult_Act2 { get; set; } = 1.5;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_NormalEnemySrcDmgMult_Act3 { get; set; } = 1.0;

    [ConfigSection("Act4_BossSrcMultipliers")]
    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_BossSrcHpMult_Act1 { get; set; } = 3;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_BossSrcHpMult_Act2 { get; set; } = 2;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_BossSrcHpMult_Act3 { get; set; } = 1.0;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_BossSrcDmgMult_Act1 { get; set; } = 1.7;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_BossSrcDmgMult_Act2 { get; set; } = 1.5;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act4_BossSrcDmgMult_Act3 { get; set; } = 1.0;

    [ConfigSection("Act5_NormalEnemySrcMultipliers")]
    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_NormalEnemySrcHpMult_Act1 { get; set; } = 3;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_NormalEnemySrcHpMult_Act2 { get; set; } = 2;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_NormalEnemySrcHpMult_Act3 { get; set; } = 1.0;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_NormalEnemySrcDmgMult_Act1 { get; set; } = 1.7;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_NormalEnemySrcDmgMult_Act2 { get; set; } = 1.5;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_NormalEnemySrcDmgMult_Act3 { get; set; } = 1.0;

    [ConfigSection("Act5_FinalBossSrcMultipliers")]
    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_FinalBossSrcHpMult_Act1 { get; set; } = 3;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_FinalBossSrcHpMult_Act2 { get; set; } = 2.0;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_FinalBossSrcHpMult_Act3 { get; set; } = 1.0;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_FinalBossSrcDmgMult_Act1 { get; set; } = 1.7;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_FinalBossSrcDmgMult_Act2 { get; set; } = 1.5;

    [ConfigSlider(0.5, 5.0, 0.05)]
    [ConfigVisibleIf(nameof(ShowAct4Act5Details))]
    public static double Act5_FinalBossSrcDmgMult_Act3 { get; set; } = 1.0;

    // ============================================================
}