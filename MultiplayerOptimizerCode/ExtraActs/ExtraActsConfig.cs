using System;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 池子混合权重 (act1, act2, act3)。归一化后总和应当 = 1。
/// </summary>
internal readonly record struct PoolWeights(double Act1, double Act2, double Act3)
{
    public double Sum => Act1 + Act2 + Act3;
}

/// <summary>
/// 数值倍率（起始值 / 结束值），按 act 内进度做线性插值。
/// </summary>
internal readonly record struct ScalingRange(double Start, double End)
{
    public double Lerp(double progress)
    {
        progress = Math.Clamp(progress, 0.0, 1.0);
        return Start + (End - Start) * progress;
    }
}

/// <summary>
/// 把 <see cref="MultiplayerOptimizerConfig"/> 的 BaseLib slider 字段封装为语义化 API。
///
/// 池权重 getter 返回值是**已归一化**的（即使磁盘上的原值不归一，运行时使用值总是 sum=1）。
/// 这是防御性做法——配合 WeightNormalizationPatch 在保存时也归一化，但即使绕过保存（手改 ini），
/// 业务逻辑仍能拿到正常的归一化权重。
/// </summary>
internal static class ExtraActsConfig
{
    // ---------- 池子混合权重 ----------

    /// <summary>
    /// 默认权重值，用于（a）显示；（b）当前权重 sum=0 时的 fallback。
    /// 跟 MultiplayerOptimizerConfig 字段的初始化值保持一致。
    /// </summary>
    public static readonly PoolWeights DefaultWeights = new(0.25, 0.35, 0.40);

    public static PoolWeights GetEncounterWeights(int actIdx)
    {
        var raw = actIdx switch
        {
            4 => new PoolWeights(
                MultiplayerOptimizerConfig.Act4_EncWeight_Act1,
                MultiplayerOptimizerConfig.Act4_EncWeight_Act2,
                MultiplayerOptimizerConfig.Act4_EncWeight_Act3),
            _ => throw new ArgumentOutOfRangeException(nameof(actIdx))
        };
        return Normalize(raw);
    }

    public static PoolWeights GetEventWeights(int actIdx)
    {
        var raw = actIdx switch
        {
            4 => new PoolWeights(
                MultiplayerOptimizerConfig.Act4_EventWeight_Act1,
                MultiplayerOptimizerConfig.Act4_EventWeight_Act2,
                MultiplayerOptimizerConfig.Act4_EventWeight_Act3),
            5 => new PoolWeights(
                MultiplayerOptimizerConfig.Act5_EventWeight_Act1,
                MultiplayerOptimizerConfig.Act5_EventWeight_Act2,
                MultiplayerOptimizerConfig.Act5_EventWeight_Act3),
            _ => throw new ArgumentOutOfRangeException(nameof(actIdx))
        };
        return Normalize(raw);
    }

    public static PoolWeights GetBossWeights(int actIdx)
    {
        var raw = actIdx switch
        {
            4 => new PoolWeights(
                MultiplayerOptimizerConfig.Act4_BossWeight_Act1,
                MultiplayerOptimizerConfig.Act4_BossWeight_Act2,
                MultiplayerOptimizerConfig.Act4_BossWeight_Act3),
            5 => new PoolWeights(
                MultiplayerOptimizerConfig.Act5_BossWeight_Act1,
                MultiplayerOptimizerConfig.Act5_BossWeight_Act2,
                MultiplayerOptimizerConfig.Act5_BossWeight_Act3),
            _ => throw new ArgumentOutOfRangeException(nameof(actIdx))
        };
        return Normalize(raw);
    }

    /// <summary>
    /// 归一化到 sum=1。sum 接近 0 时（用户手动改成全 0）回落到默认权重，避免除 0。
    /// </summary>
    private static PoolWeights Normalize(PoolWeights raw)
    {
        var sum = raw.Sum;
        if (sum <= 1e-9) return DefaultWeights;
        if (Math.Abs(sum - 1.0) < 1e-6) return raw;
        return new PoolWeights(raw.Act1 / sum, raw.Act2 / sum, raw.Act3 / sum);
    }

    // ---------- 全局数值倍率 ----------

    public static ScalingRange GetNormalEnemyHpMult(int actIdx)
    {
        return actIdx switch
        {
            4 => new ScalingRange(
                MultiplayerOptimizerConfig.Act4_NormalEnemyHpMultStart,
                MultiplayerOptimizerConfig.Act4_NormalEnemyHpMultEnd),
            5 => new ScalingRange(
                MultiplayerOptimizerConfig.Act5_NormalEnemyHpMultStart,
                MultiplayerOptimizerConfig.Act5_NormalEnemyHpMultEnd),
            _ => new ScalingRange(1.0, 1.0)
        };
    }

    public static ScalingRange GetNormalEnemyDmgMult(int actIdx)
    {
        return actIdx switch
        {
            4 => new ScalingRange(
                MultiplayerOptimizerConfig.Act4_NormalEnemyDmgMultStart,
                MultiplayerOptimizerConfig.Act4_NormalEnemyDmgMultEnd),
            5 => new ScalingRange(
                MultiplayerOptimizerConfig.Act5_NormalEnemyDmgMultStart,
                MultiplayerOptimizerConfig.Act5_NormalEnemyDmgMultEnd),
            _ => new ScalingRange(1.0, 1.0)
        };
    }

    public static double GetBossHpMult(int actIdx)
    {
        return actIdx switch
        {
            4 => MultiplayerOptimizerConfig.Act4_BossHpMult,
            5 => MultiplayerOptimizerConfig.Act5_FinalBossHpMult,
            _ => 1.0
        };
    }

    public static double GetBossDmgMult(int actIdx)
    {
        return actIdx switch
        {
            4 => MultiplayerOptimizerConfig.Act4_BossDmgMult,
            5 => MultiplayerOptimizerConfig.Act5_FinalBossDmgMult,
            _ => 1.0
        };
    }

    // ---------- 来源 act 倍率（普通敌人） ----------

    public static double GetSourceNormalEnemyHpMult(int actIdx, int sourceActIdx)
    {
        return (actIdx, sourceActIdx) switch
        {
            (4, 1) => MultiplayerOptimizerConfig.Act4_NormalEnemySrcHpMult_Act1,
            (4, 2) => MultiplayerOptimizerConfig.Act4_NormalEnemySrcHpMult_Act2,
            (4, 3) => MultiplayerOptimizerConfig.Act4_NormalEnemySrcHpMult_Act3,
            (5, 1) => MultiplayerOptimizerConfig.Act5_NormalEnemySrcHpMult_Act1,
            (5, 2) => MultiplayerOptimizerConfig.Act5_NormalEnemySrcHpMult_Act2,
            (5, 3) => MultiplayerOptimizerConfig.Act5_NormalEnemySrcHpMult_Act3,
            _ => 1.0
        };
    }

    public static double GetSourceNormalEnemyDmgMult(int actIdx, int sourceActIdx)
    {
        return (actIdx, sourceActIdx) switch
        {
            (4, 1) => MultiplayerOptimizerConfig.Act4_NormalEnemySrcDmgMult_Act1,
            (4, 2) => MultiplayerOptimizerConfig.Act4_NormalEnemySrcDmgMult_Act2,
            (4, 3) => MultiplayerOptimizerConfig.Act4_NormalEnemySrcDmgMult_Act3,
            (5, 1) => MultiplayerOptimizerConfig.Act5_NormalEnemySrcDmgMult_Act1,
            (5, 2) => MultiplayerOptimizerConfig.Act5_NormalEnemySrcDmgMult_Act2,
            (5, 3) => MultiplayerOptimizerConfig.Act5_NormalEnemySrcDmgMult_Act3,
            _ => 1.0
        };
    }

    // ---------- 来源 act 倍率（boss） ----------

    public static double GetSourceBossHpMult(int actIdx, int sourceActIdx)
    {
        return (actIdx, sourceActIdx) switch
        {
            (4, 1) => MultiplayerOptimizerConfig.Act4_BossSrcHpMult_Act1,
            (4, 2) => MultiplayerOptimizerConfig.Act4_BossSrcHpMult_Act2,
            (4, 3) => MultiplayerOptimizerConfig.Act4_BossSrcHpMult_Act3,
            (5, 1) => MultiplayerOptimizerConfig.Act5_FinalBossSrcHpMult_Act1,
            (5, 2) => MultiplayerOptimizerConfig.Act5_FinalBossSrcHpMult_Act2,
            (5, 3) => MultiplayerOptimizerConfig.Act5_FinalBossSrcHpMult_Act3,
            _ => 1.0
        };
    }

    public static double GetSourceBossDmgMult(int actIdx, int sourceActIdx)
    {
        return (actIdx, sourceActIdx) switch
        {
            (4, 1) => MultiplayerOptimizerConfig.Act4_BossSrcDmgMult_Act1,
            (4, 2) => MultiplayerOptimizerConfig.Act4_BossSrcDmgMult_Act2,
            (4, 3) => MultiplayerOptimizerConfig.Act4_BossSrcDmgMult_Act3,
            (5, 1) => MultiplayerOptimizerConfig.Act5_FinalBossSrcDmgMult_Act1,
            (5, 2) => MultiplayerOptimizerConfig.Act5_FinalBossSrcDmgMult_Act2,
            (5, 3) => MultiplayerOptimizerConfig.Act5_FinalBossSrcDmgMult_Act3,
            _ => 1.0
        };
    }

    // ---------- 行为开关 ----------

    public static bool ShouldShowAct5DisguisedBossWarning =>
        MultiplayerOptimizerConfig.Act5_ShowDisguisedBossWarning;

    public static bool ShouldAvoidAct5FinalBossEqualPenultimate =>
        MultiplayerOptimizerConfig.Act5_AvoidFinalBossEqualPenultimate;
}